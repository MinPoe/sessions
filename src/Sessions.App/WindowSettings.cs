using System.IO;
using System.Text.Json;

namespace Sessions.App;

// The main window's remembered placement, persisted between runs so the app
// reopens at the size and spot the user left it
public sealed record WindowSettings(
    double Left,
    double Top,
    double Width,
    double Height,
    bool Maximized)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    // %APPDATA%\Sessions\window.json
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Sessions",
        "window.json");

    // Reads the saved placement, or null when there is none or it can't be read
    public static WindowSettings? Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<WindowSettings>(File.ReadAllText(FilePath), Options)
                : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Options));
        }
        catch (IOException)
        {
            // Remembering the window size is best-effort, never block exit on it
        }
    }
}
