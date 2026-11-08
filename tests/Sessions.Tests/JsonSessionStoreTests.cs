using System.IO;
using Sessions.Core;

namespace Sessions.Tests;

public class JsonSessionStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly JsonSessionStore _store;

    public JsonSessionStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "SessionsTests", Guid.NewGuid().ToString("N"));
        _store = new JsonSessionStore(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static Session NewSession(Guid id, int schemaVersion = Session.CurrentSchemaVersion) => new(
        SchemaVersion: schemaVersion,
        Id: id,
        Name: "Test",
        CreatedAt: DateTimeOffset.UtcNow,
        Entries: new Entry[] { new GenericEntry("App", new Placement(0, 0, 100, 100, 1), "app.exe", "") });

    [Fact]
    public void Save_ThenLoad_ReturnsEqualSession()
    {
        Guid id = Guid.NewGuid();
        Session session = NewSession(id);

        _store.Save(session);
        Session loaded = _store.Load(id);

        Assert.Equal(session, loaded);
    }

    [Fact]
    public void Save_LeavesNoTempFileBehind()
    {
        Guid id = Guid.NewGuid();

        _store.Save(NewSession(id));

        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public void Load_MalformedJson_ThrowsNamingTheFile()
    {
        Guid id = Guid.NewGuid();
        Directory.CreateDirectory(_dir);
        string path = Path.Combine(_dir, id.ToString("D") + ".json");
        File.WriteAllText(path, "{ this is not valid json ");

        InvalidDataException ex = Assert.Throws<InvalidDataException>(() => _store.Load(id));
        Assert.Contains(path, ex.Message);
    }

    [Fact]
    public void Load_UnknownSchemaVersion_ThrowsNamingTheFile()
    {
        Guid id = Guid.NewGuid();
        _store.Save(NewSession(id, schemaVersion: 999));

        string path = Path.Combine(_dir, id.ToString("D") + ".json");
        InvalidDataException ex = Assert.Throws<InvalidDataException>(() => _store.Load(id));
        Assert.Contains(path, ex.Message);
        Assert.Contains("999", ex.Message);
    }
}
