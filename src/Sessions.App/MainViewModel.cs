using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Sessions.Core;

namespace Sessions.App;

// Drives the main window: lists saved sessions and the selected session's
// entries, and exposes the toolbar commands
public sealed class MainViewModel : ObservableObject
{
    private readonly IShell _shell;
    private readonly JsonSessionStore _store;
    private readonly AdapterRegistry _registry;

    private SessionRow? _selectedSession;
    private string _statusMessage = string.Empty;

    public MainViewModel(IShell shell, JsonSessionStore? store = null)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _store = store ?? new JsonSessionStore(JsonSessionStore.DefaultDirectory);
        _registry = new AdapterRegistry(
            new IAdapter[] { new VsCodeAdapter(), new TerminalAdapter() },
            new GenericAdapter());

        NewCommand = new RelayCommand(NewSession);
        LoadCommand = new RelayCommand(LoadSession, () => SelectedSession is not null);
        DeleteCommand = new RelayCommand(DeleteSession, () => SelectedSession is not null);
        ExportCommand = new RelayCommand(ExportSession, () => SelectedSession is not null);
        ImportCommand = new RelayCommand(ImportSession);

        RefreshSessions();
    }

    public ObservableCollection<SessionRow> Sessions { get; } = new();

    public ObservableCollection<EntryRow> Entries { get; } = new();

    public SessionRow? SelectedSession
    {
        get => _selectedSession;
        set
        {
            if (SetProperty(ref _selectedSession, value))
            {
                UpdateEntries();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool HasNoSessions => Sessions.Count == 0;

    public ICommand NewCommand { get; }
    public ICommand LoadCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }

    private void RefreshSessions(Guid? select = null)
    {
        SessionListResult result = _store.List();

        Sessions.Clear();
        foreach (Session session in result.Sessions.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Sessions.Add(new SessionRow(session));
        }

        OnPropertyChanged(nameof(HasNoSessions));

        SelectedSession = select is { } id
            ? Sessions.FirstOrDefault(s => s.Id == id) ?? Sessions.FirstOrDefault()
            : Sessions.FirstOrDefault();

        StatusMessage = result.Errors.Count switch
        {
            0 => string.Empty,
            1 => "1 session file could not be read.",
            var n => $"{n} session files could not be read.",
        };
    }

    private void UpdateEntries()
    {
        Entries.Clear();
        if (SelectedSession is null)
        {
            return;
        }

        foreach (Entry entry in SelectedSession.Session.Entries)
        {
            Entries.Add(new EntryRow(entry));
        }
    }

    private void NewSession()
    {
        Session? saved = _shell.ShowNewSessionDialog();
        if (saved is not null)
        {
            RefreshSessions(select: saved.Id);
        }
    }

    private void LoadSession()
    {
        if (SelectedSession is not { } row)
        {
            return;
        }

        RestoreRunner runner = new(_registry);
        var results = runner.Run(row.Session.Entries);
        _shell.ShowRestoreResults(row.Name, results);
    }

    private void DeleteSession()
    {
        if (SelectedSession is not { } row)
        {
            return;
        }

        bool proceed = _shell.Confirm(
            "Delete Session",
            $"Delete “{row.Name}”? This can’t be undone.",
            confirmLabel: "Delete",
            destructive: true);

        if (!proceed)
        {
            return;
        }

        _store.Delete(row.Id);
        RefreshSessions();
    }

    private void ExportSession()
    {
        if (SelectedSession is not { } row)
        {
            return;
        }

        string? path = _shell.AskSavePath($"{SafeFileName(row.Name)}.json");
        if (path is null)
        {
            return;
        }

        _store.Export(row.Id, path);
        StatusMessage = $"Exported “{row.Name}”.";
    }

    private void ImportSession()
    {
        string? path = _shell.AskOpenPath();
        if (path is null)
        {
            return;
        }

        try
        {
            Session imported = _store.Import(path);
            RefreshSessions(select: imported.Id);
            StatusMessage = $"Imported “{imported.Name}”.";
        }
        catch (Exception ex)
        {
            _shell.ShowMessage("Import Failed", ex.Message);
        }
    }

    private static string SafeFileName(string name)
    {
        char[] invalid = System.IO.Path.GetInvalidFileNameChars();
        string cleaned = new(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "session" : cleaned;
    }
}
