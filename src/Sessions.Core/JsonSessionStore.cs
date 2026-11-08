using System.IO;
using System.Text.Json;

namespace Sessions.Core;

// Persists sessions as one JSON file per id under a directory.
// Writes are atomic: a temp file is written, then moved over the target.
public sealed class JsonSessionStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _directory;

    public JsonSessionStore(string directory)
    {
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
    }

    // %APPDATA%\Sessions\sessions
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Sessions",
        "sessions");

    public void Save(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        Directory.CreateDirectory(_directory);

        string target = PathFor(session.Id);
        string temp = target + ".tmp";

        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(session, Options);
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, target, overwrite: true);
    }

    public Session Load(Guid id)
    {
        string path = PathFor(id);
        string json = File.ReadAllText(path);

        Session? session;
        try
        {
            session = JsonSerializer.Deserialize<Session>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Session file '{path}' contains malformed JSON.", ex);
        }

        if (session is null)
        {
            throw new InvalidDataException($"Session file '{path}' deserialized to null.");
        }

        if (session.SchemaVersion != Session.CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Session file '{path}' has unknown schema version {session.SchemaVersion}; " +
                $"expected {Session.CurrentSchemaVersion}.");
        }

        return session;
    }

    private string PathFor(Guid id) => Path.Combine(_directory, id.ToString("D") + ".json");
}
