using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using Sessions.Core;

namespace Sessions.App;

// An entry as shown in the detail list
public sealed class EntryRow
{
    public EntryRow(Entry entry)
    {
        Entry = entry;

        if (entry is GenericEntry generic)
        {
            Detail = string.IsNullOrWhiteSpace(generic.Arguments)
                ? Path.GetFileName(generic.ExePath)
                : $"{Path.GetFileName(generic.ExePath)}  {generic.Arguments}";
            Icon = IconLoader.ForExecutable(generic.ExePath);
        }
        else if (entry is VsCodeEntry vsCode)
        {
            Detail = vsCode.Path;
        }
        else if (entry is TerminalEntry terminal)
        {
            Detail = string.Join("  ", terminal.Tabs.Select(t =>
                t.ClaudeSessionId is not null ? $"{Path.GetFileName(t.Cwd.TrimEnd('\\', '/'))} (Claude)"
                : t.Command is not null ? $"{Path.GetFileName(t.Cwd.TrimEnd('\\', '/'))} (ssh)"
                : Path.GetFileName(t.Cwd.TrimEnd('\\', '/'))));
        }
        else
        {
            Detail = string.Empty;
        }

        Placement = entry.Placement is { } p
            ? $"{p.Width}×{p.Height} at {p.X}, {p.Y}"
            : "Default position";
    }

    public Entry Entry { get; }

    public string DisplayName => Entry.DisplayName;

    public string Detail { get; }

    public string Adapter => Entry.AdapterId;

    public string Placement { get; }

    public BitmapSource? Icon { get; }
}
