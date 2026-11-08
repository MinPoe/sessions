using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Sessions.Core;

namespace Sessions.App;

// Backs the new-session dialog: enumerates windows, captures the selected ones
// through the adapter registry, and saves the resulting session
public sealed class NewSessionViewModel : ObservableObject
{
    private readonly JsonSessionStore _store;
    private readonly AdapterRegistry _registry;

    private string _name = string.Empty;
    private string? _nameError;
    private bool _isLoading = true;
    private bool _isSaved;

    public NewSessionViewModel(JsonSessionStore store, AdapterRegistry registry)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));

        SaveCommand = new RelayCommand(Save, () => !IsLoading && !IsSaved);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke());
    }

    public ObservableCollection<WindowRow> Windows { get; } = new();

    public ObservableCollection<string> Failures { get; } = new();

    // Set to the saved session when the capture succeeds; null while in progress
    public Session? SavedSession { get; private set; }

    public event Action? CloseRequested;

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                NameError = null;
            }
        }
    }

    public string? NameError
    {
        get => _nameError;
        private set
        {
            if (SetProperty(ref _nameError, value))
            {
                OnPropertyChanged(nameof(HasNameError));
            }
        }
    }

    public bool HasNameError => !string.IsNullOrEmpty(NameError);

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public bool IsSaved
    {
        get => _isSaved;
        private set => SetProperty(ref _isSaved, value);
    }

    public bool HasFailures => Failures.Count > 0;

    public ICommand SaveCommand { get; }
    public ICommand CloseCommand { get; }

    // Enumerate windows off the UI thread (WMI lookups are slow), then populate
    public async Task LoadWindowsAsync()
    {
        IsLoading = true;

        var rows = await Task.Run(() =>
            WindowEnumerator.Enumerate()
                .Select(w => new WindowRow(w, _registry.SelectForCapture(w)?.Id))
                .OrderByDescending(r => r.IsSupported)
                .ThenBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase)
                .ToList());

        Windows.Clear();
        foreach (WindowRow row in rows)
        {
            Windows.Add(row);
        }

        IsLoading = false;
    }

    private void Save()
    {
        Failures.Clear();
        OnPropertyChanged(nameof(HasFailures));

        var selected = Windows.Where(w => w.IsSelected).Select(w => w.Window).ToList();
        if (selected.Count == 0)
        {
            NameError = "Select at least one window to capture.";
            return;
        }

        SessionCaptureService service = new(
            _registry,
            () => _store.List().Sessions.Select(s => s.Name));

        SessionCaptureResult result;
        try
        {
            result = service.Capture(selected, Name.Trim());
        }
        catch (ArgumentException ex)
        {
            NameError = ex.Message;
            return;
        }

        _store.Save(result.Session);
        SavedSession = result.Session;

        if (result.Failures.Count == 0)
        {
            CloseRequested?.Invoke();
            return;
        }

        // Some windows couldn't be captured; show why and let the user close
        foreach (CaptureFailure failure in result.Failures)
        {
            Failures.Add($"{failure.Window.Title}: {failure.Reason}");
        }

        OnPropertyChanged(nameof(HasFailures));
        IsSaved = true;
    }
}
