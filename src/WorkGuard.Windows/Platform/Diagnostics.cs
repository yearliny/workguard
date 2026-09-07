namespace WorkGuard.Windows.Platform;

internal static class Diagnostics
{
    public static void Record(Exception error)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WorkGuard");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "diagnostics.log");
            if (File.Exists(path) && new FileInfo(path).Length > 128 * 1024) File.Move(path, path + ".previous", true);
            // Exception messages may include user content. Store only type, code and stack frames.
            File.AppendAllText(path, $"{DateTimeOffset.UtcNow:O} | {typeof(App).Assembly.GetName().Version} | {error.GetType().Name} | {error.HResult}\n{error.StackTrace}\n");
        }
        catch { /* Diagnostics must never cause another failure. */ }
    }
}
