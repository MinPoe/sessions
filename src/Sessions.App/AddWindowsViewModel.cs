using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Sessions.Core;

namespace Sessions.App;

// Backs the add-windows dialog: enumerates open windows and returns the ones the
// user ticks, the caller captures and appends them to an existing session, no name
// and no save here
public sealed class AddWindowsViewModel : ObservableObject
{
    private readonly AdapterRegistry _registry;

    private bool _isLoading = true;
    private string? _error;

    public AddWindowsViewModel(AdapterRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));

        AddCommand = new RelayCommand(Add, () => !IsLoading);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke());
    }

    public ObservableCollection<WindowRow> Windows { get; } = new();

    // The windows the user chose, set when Add succeeds, null while in progress
    public IReadOnlyList<WindowInfo>? SelectedWindows { get; private set; }

    public event Action? CloseRequested;

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public string? Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(Error);

    public ICommand AddCommand { get; }
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

    private void Add()
    {
        var selected = Windows.Where(w => w.IsSelected).Select(w => w.Window).ToList();
        if (selected.Count == 0)
        {
            Error = "Select at least one window to add.";
            return;
        }

        SelectedWindows = selected;
        CloseRequested?.Invoke();
    }
}
