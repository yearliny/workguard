using System.Text.Json;
using WorkGuard.Core;

namespace WorkGuard.Data;

public sealed class DayStats
{
    public DateOnly Date { get; set; }
    public double ActiveSeconds { get; set; }
    public double LongestSeconds { get; set; }
    public int EyeBreaks { get; set; }
    public int MovementBreaks { get; set; }
    public int OfficeBreaks { get; set; }
}

public sealed class StoredState
{
    public int SchemaVersion { get; set; } = 1;
    public Preferences Preferences { get; set; } = new();
    public List<DayStats> Days { get; set; } = [];
    public DateOnly? LastOfficeReminderDate { get; set; }
}

public sealed class LocalStore(string directory)
{
    private readonly string _path = Path.Combine(directory, "state.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string? LoadWarning { get; private set; }
    public bool ReadOnly { get; private set; }
    public bool CanRestoreBackup => File.Exists(_path + ".bak");

    public StoredState Load()
    {
        if (!File.Exists(_path)) return new();
        try
        {
            var state = JsonSerializer.Deserialize<StoredState>(File.ReadAllText(_path))
                ?? throw new JsonException("Empty state");
            if (state.SchemaVersion != 1)
            {
                ReadOnly = true;
                LoadWarning = "数据来自其他版本，本次以只读方式运行，避免覆盖。请使用更新版本。";
                return new();
            }
            if (state.Preferences is null || state.Days is null ||
                state.Days.Any(d => d is null || !double.IsFinite(d.ActiveSeconds) ||
                    !double.IsFinite(d.LongestSeconds) || d.ActiveSeconds < 0 || d.LongestSeconds < 0 ||
                    d.EyeBreaks < 0 || d.MovementBreaks < 0 || d.OfficeBreaks < 0) ||
                state.Days.Select(d => d.Date).Distinct().Count() != state.Days.Count)
                throw new JsonException("Invalid state");
            state.Preferences = state.Preferences.Validate();
            return state;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            // Preserve the original. A new run must not silently overwrite unreadable data.
            ReadOnly = true;
            LoadWarning = "本地数据无法读取，本次统计不会保存。原文件已保留，请在退出后备份并检查。";
            return new();
        }
    }

    public void Save(StoredState state)
    {
        if (ReadOnly) throw new IOException("本地数据处于只读保护状态，请先恢复备份或检查数据文件。");
        Directory.CreateDirectory(directory);
        var temporary = _path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            JsonSerializer.Serialize(stream, state, JsonOptions);
            stream.Flush(flushToDisk: true);
        }
        if (File.Exists(_path)) File.Replace(temporary, _path, _path + ".bak");
        else File.Move(temporary, _path);
    }

    public static DayStats Day(StoredState state, DateOnly date)
    {
        var day = state.Days.FirstOrDefault(d => d.Date == date);
        if (day is not null) return day;
        day = new DayStats { Date = date };
        state.Days.Add(day);
        return day;
    }

    public StoredState RestoreBackup()
    {
        var backup = _path + ".bak";
        var text = File.ReadAllText(backup);
        // Validate through the same loader in an isolated folder before touching any existing data.
        var staging = Path.Combine(directory, "restore-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            File.WriteAllText(Path.Combine(staging, "state.json"), text);
            var reader = new LocalStore(staging);
            var restored = reader.Load();
            if (reader.ReadOnly) throw new IOException("备份也无法读取，原数据保持不变。");
            var temp = _path + ".restore.tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(text);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(_path)) File.Replace(temp, _path, _path + ".preserved-" + Guid.NewGuid().ToString("N"));
            else File.Move(temp, _path);
            ReadOnly = false;
            LoadWarning = null;
            return restored;
        }
        finally { Directory.Delete(staging, recursive: true); }
    }
}
