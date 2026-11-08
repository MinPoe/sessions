using Sessions.Core;

namespace Sessions.App;

// A session as shown in the sidebar list
public sealed class SessionRow
{
    public SessionRow(Session session)
    {
        Session = session;
    }

    public Session Session { get; }

    public Guid Id => Session.Id;

    public string Name => Session.Name;

    public string Subtitle
    {
        get
        {
            int count = Session.Entries.Count;
            string apps = count == 1 ? "1 app" : $"{count} apps";
            return $"{apps} · {Session.CreatedAt.LocalDateTime:d MMM yyyy}";
        }
    }
}
