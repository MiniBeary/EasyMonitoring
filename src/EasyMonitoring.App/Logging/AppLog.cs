namespace EasyMonitoring.App.Logging;

internal static class AppLog
{
    private static readonly object Gate = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EasyMonitoring");
    private static readonly string LogPath = Path.Combine(LogDirectory, "EasyMonitoring.log");

    public static void Error(string message, Exception? exception = null)
    {
        Write("ERROR", message, exception);
    }

    public static void Warning(string message, Exception? exception = null)
    {
        Write("WARN", message, exception);
    }

    public static void Info(string message)
    {
        Write("INFO", message, null);
    }

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);

                var line = $"{DateTimeOffset.Now:O} {level} {message}";
                if (exception is not null)
                {
                    line += Environment.NewLine + exception;
                }

                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
        }
        catch
        {
        }
    }
}
