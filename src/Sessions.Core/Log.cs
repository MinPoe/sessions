using System.IO;

namespace Sessions.Core;

// Dead-simple append-only file log, one timestamped line per call.
public static class Log
{
    // %APPDATA%\Sessions\log.txt
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Sessions",
        "log.txt");

    public static void Write(string message) => Write(DefaultPath, message);

    public static void Write(string path, string message)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} {message}{Environment.NewLine}";
        File.AppendAllText(path, line);
    }
}
