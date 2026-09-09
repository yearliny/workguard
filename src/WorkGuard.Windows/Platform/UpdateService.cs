using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WorkGuard.Windows.Platform;

internal sealed record PreparedUpdate(string Version, string InstallerPath);

internal sealed class UpdateService : IDisposable
{
    private static readonly Uri FeedUri = new("https://yearliny.github.io/workguard/update/win-x64.json");
    private static readonly TimeSpan AutomaticCheckInterval = TimeSpan.FromHours(12);
    private const long MaximumInstallerBytes = 250L * 1024 * 1024;

    private readonly string _updatesDirectory;
    private readonly string _checkMarker;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public UpdateService(string dataDirectory)
    {
        _updatesDirectory = Path.Combine(dataDirectory, "updates");
        _checkMarker = Path.Combine(_updatesDirectory, "last-check.txt");
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("WorkGuard-Updater/1.0");
    }

    public async Task<PreparedUpdate?> CheckAndPrepareAsync(bool force)
    {
        Directory.CreateDirectory(_updatesDirectory);
        if (!force && File.Exists(_checkMarker) && DateTime.UtcNow - File.GetLastWriteTimeUtc(_checkMarker) < AutomaticCheckInterval)
            return FindPreparedUpdate();

        using var response = await _http.GetAsync(FeedUri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var feed = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(feed, JsonOptions).ConfigureAwait(false)
            ?? throw new InvalidDataException("更新清单为空。");
        ValidateManifest(manifest);
        File.WriteAllText(_checkMarker, DateTime.UtcNow.ToString("O"), Encoding.ASCII);

        if (CompareSemanticVersions(manifest.Version, CurrentVersion()) <= 0) return null;

        var target = Path.Combine(_updatesDirectory, $"WorkGuard-{SafeVersion(manifest.Version)}-setup.exe");
        if (File.Exists(target) && await VerifyAsync(target, manifest).ConfigureAwait(false))
            return new PreparedUpdate(manifest.Version, target);

        var temporary = target + ".download";
        if (File.Exists(temporary)) File.Delete(temporary);
        try
        {
            using var download = await _http.GetAsync(manifest.Url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            download.EnsureSuccessStatusCode();
            if (download.Content.Headers.ContentLength is > MaximumInstallerBytes)
                throw new InvalidDataException("更新包超过允许大小。");

            await using (var input = await download.Content.ReadAsStreamAsync().ConfigureAwait(false))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await input.ReadAsync(buffer).ConfigureAwait(false)) > 0)
                {
                    total += read;
                    if (total > MaximumInstallerBytes) throw new InvalidDataException("更新包超过允许大小。");
                    await output.WriteAsync(buffer.AsMemory(0, read)).ConfigureAwait(false);
                }
                await output.FlushAsync().ConfigureAwait(false);
            }

            if (!await VerifyAsync(temporary, manifest).ConfigureAwait(false))
                throw new InvalidDataException("更新包校验失败。");
            File.Move(temporary, target, true);
            CleanupOldInstallers(target);
            return new PreparedUpdate(manifest.Version, target);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public bool LaunchInstaller(PreparedUpdate update)
    {
        if (!File.Exists(update.InstallerPath)) return false;
        var installedExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WorkGuard", "WorkGuard.exe");
        var commandFile = Path.Combine(_updatesDirectory, "apply-update.cmd");
        var installer = update.InstallerPath.Replace("%", "%%");
        var exe = installedExe.Replace("%", "%%");
        var script = $"@echo off\r\ntimeout /t 2 /nobreak >nul\r\n\"{installer}\" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS\r\nif errorlevel 1 exit /b %errorlevel%\r\nstart \"\" \"{exe}\"\r\ndel \"%~f0\"\r\n";
        File.WriteAllText(commandFile, script, new UTF8Encoding(false));
        Process.Start(new ProcessStartInfo("cmd.exe", $"/d /s /c \"\"{commandFile}\"\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _updatesDirectory
        });
        return true;
    }

    private PreparedUpdate? FindPreparedUpdate()
    {
        var current = CurrentVersion();
        return Directory.Exists(_updatesDirectory)
            ? Directory.EnumerateFiles(_updatesDirectory, "WorkGuard-*-setup.exe")
                .Select(path => new { Path = path, Version = ExtractVersion(path) })
                .Where(x => x.Version is not null && CompareSemanticVersions(x.Version!, current) > 0)
                .OrderByDescending(x => x.Version, SemanticVersionComparer.Instance)
                .Select(x => new PreparedUpdate(x.Version!, x.Path))
                .FirstOrDefault()
            : null;
    }

    private static async Task<bool> VerifyAsync(string path, UpdateManifest manifest)
    {
        var info = new FileInfo(path);
        if (manifest.Size <= 0 || info.Length != manifest.Size || info.Length > MaximumInstallerBytes) return false;
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);
        return Convert.ToHexString(hash).Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateManifest(UpdateManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Version) || manifest.Version.Length > 64) throw new InvalidDataException("更新版本无效。");
        if (!Uri.TryCreate(manifest.Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("yearliny.github.io", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("更新地址不受信任。");
        if (manifest.Sha256 is null || manifest.Sha256.Length != 64 || manifest.Sha256.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("更新校验值无效。");
        if (manifest.Size <= 0 || manifest.Size > MaximumInstallerBytes) throw new InvalidDataException("更新包大小无效。");
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private sealed record UpdateManifest(string Version, string Url, string Sha256, long Size);

    internal static string CurrentVersion()
    {
        var value = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(App).Assembly.GetName().Version?.ToString(3)
            ?? "0.0.0";
        var plus = value.IndexOf('+');
        return plus >= 0 ? value[..plus] : value;
    }

    internal static int CompareSemanticVersions(string left, string right)
    {
        var a = ParseVersion(left);
        var b = ParseVersion(right);
        var core = a.Core.CompareTo(b.Core);
        if (core != 0) return core;
        if (a.PreRelease.Count == 0 && b.PreRelease.Count == 0) return 0;
        if (a.PreRelease.Count == 0) return 1;
        if (b.PreRelease.Count == 0) return -1;
        for (var i = 0; i < Math.Max(a.PreRelease.Count, b.PreRelease.Count); i++)
        {
            if (i >= a.PreRelease.Count) return -1;
            if (i >= b.PreRelease.Count) return 1;
            var x = a.PreRelease[i]; var y = b.PreRelease[i];
            var xn = int.TryParse(x, out var xi); var yn = int.TryParse(y, out var yi);
            var part = xn && yn ? xi.CompareTo(yi) : xn ? -1 : yn ? 1 : string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            if (part != 0) return part;
        }
        return 0;
    }

    private static (Version Core, List<string> PreRelease) ParseVersion(string value)
    {
        var clean = value.Split('+', 2)[0];
        var parts = clean.Split('-', 2);
        if (!Version.TryParse(parts[0], out var core)) throw new InvalidDataException("版本号格式无效。");
        return (core, parts.Length == 2 ? parts[1].Split('.', StringSplitOptions.RemoveEmptyEntries).ToList() : []);
    }

    private static string SafeVersion(string value) => new(value.Where(c => char.IsLetterOrDigit(c) || c is '.' or '-').ToArray());
    private static string? ExtractVersion(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        const string prefix = "WorkGuard-"; const string suffix = "-setup";
        return name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
            ? name[prefix.Length..^suffix.Length] : null;
    }

    private void CleanupOldInstallers(string keep)
    {
        foreach (var file in Directory.EnumerateFiles(_updatesDirectory, "WorkGuard-*-setup.exe"))
            if (!file.Equals(keep, StringComparison.OrdinalIgnoreCase)) try { File.Delete(file); } catch (IOException) { }
    }

    public void Dispose() => _http.Dispose();

    private sealed class SemanticVersionComparer : IComparer<string?>
    {
        public static SemanticVersionComparer Instance { get; } = new();
        public int Compare(string? x, string? y) => CompareSemanticVersions(x ?? "0.0.0", y ?? "0.0.0");
    }
}
