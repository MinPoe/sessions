using System.Collections.Generic;
using Sessions.Core;

namespace Sessions.App;

// View interactions the MainViewModel needs, kept behind an interface so the
// view model stays free of direct window/dialog references
public interface IShell
{
    // Opens the new-session dialog; returns the saved session, or null if cancelled
    Session? ShowNewSessionDialog();

    // Shows the outcome of restoring each entry
    void ShowRestoreResults(string sessionName, IReadOnlyList<RestoreResult> results);

    // Asks the user to confirm an action; true if they proceed
    bool Confirm(string title, string message, string confirmLabel, bool destructive);

    // File pickers; return null when cancelled
    string? AskSavePath(string suggestedFileName);
    string? AskOpenPath();

    // A simple informational message (e.g. an import error)
    void ShowMessage(string title, string message);
}
