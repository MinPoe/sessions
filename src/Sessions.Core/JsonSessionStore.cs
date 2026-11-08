using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Sessions.Core;

// One session file that failed to load, with the reason
public sealed record SessionLoadError(string Path, string Message);

// Everything that loaded, plus the files that didn't
public sealed record SessionListResult(
    IReadOnlyList<Session> Sessions,
    IReadOnlyList<SessionLoadError> Errors);

// Persists sessions as one JSON file per id under a directory,
// writes are atomic: a temp file is written, then moved over the target
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

    public Session Load(Guid id) => ReadAndValidate(PathFor(id));

    // Reads and validates a session file at an arbitrary path (malformed JSON
    // and unknown schema version both throw, naming the file)
    private static Session ReadAndValidate(string path)
    {
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

    // Copies a saved session's JSON file to destinationPath
    public void Export(Guid id, string destinationPath)
    {
        string source = PathFor(id);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"No session file to export at '{source}'.", source);
        }

        File.Copy(source, destinationPath, overwrite: true);
    }

    // Reads and validates a session file from anywhere, gives it a fresh Id when
    // one with its Id is already stored, saves it, and returns what was saved
    public Session Import(string sourcePath)
    {
        Session imported = ReadAndValidate(sourcePath);

        if (File.Exists(PathFor(imported.Id)))
        {
            imported = imported with { Id = Guid.NewGuid() };
        }

        Save(imported);
        return imported;
    }

    // Loads every *.json in the directory, collecting per-file failures
    // instead of letting one bad file sink the whole listing
    public SessionListResult List()
    {
        if (!Directory.Exists(_directory))
        {
            return new SessionListResult(Array.Empty<Session>(), Array.Empty<SessionLoadError>());
        }

        List<Session> sessions = new();
        List<SessionLoadError> errors = new();

        foreach (string path in Directory.EnumerateFiles(_directory, "*.json").OrderBy(p => p))
        {
            if (!Guid.TryParse(Path.GetFileNameWithoutExtension(path), out Guid id))
            {
                errors.Add(new SessionLoadError(path, "File name is not a session id."));
                continue;
            }

            try
            {
                sessions.Add(Load(id));
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                errors.Add(new SessionLoadError(path, ex.Message));
            }
        }

        return new SessionListResult(sessions, errors);
    }

    public void Delete(Guid id)
    {
        string path = PathFor(id);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"No session file to delete at '{path}'.", path);
        }

        File.Delete(path);
    }

    private string PathFor(Guid id) => Path.Combine(_directory, id.ToString("D") + ".json");
}
