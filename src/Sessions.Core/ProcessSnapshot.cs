using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace Sessions.Core;

// One process in a snapshot, Name is the image name (e.g. "claude.exe")
public sealed record ProcessInfo(uint Pid, uint ParentProcessId, string Name, string? CommandLine);

// A point-in-time view of the running processes, built once per capture so the
// parent walks stay consistent, the pure walks are testable from a hand-built list
public sealed class ProcessSnapshot
{
    private readonly IReadOnlyDictionary<uint, ProcessInfo> _byPid;

    public ProcessSnapshot(IEnumerable<ProcessInfo> processes)
    {
        // A snapshot can momentarily hold two rows for one pid, keep the first
        Dictionary<uint, ProcessInfo> byPid = new();
        foreach (ProcessInfo process in processes)
        {
            byPid.TryAdd(process.Pid, process);
        }

        _byPid = byPid;
    }

    public IReadOnlyCollection<ProcessInfo> Processes => (IReadOnlyCollection<ProcessInfo>)_byPid.Values;

    // True when the snapshot still holds this pid, a record for a missing pid is a
    // dead shell to drop
    public bool Contains(uint pid) => _byPid.ContainsKey(pid);

    public ProcessInfo? Get(uint pid) => _byPid.TryGetValue(pid, out ProcessInfo? info) ? info : null;

    // True when ancestorPid sits somewhere on pid's parent chain, the chain is
    // guarded against loops and a self-parent (pid reuse can forge either)
    public bool IsDescendantOf(uint pid, uint ancestorPid)
    {
        uint current = pid;
        for (int guard = 0; guard < 64; guard++)
        {
            if (!_byPid.TryGetValue(current, out ProcessInfo? info))
            {
                return false;
            }

            uint parent = info.ParentProcessId;
            if (parent == ancestorPid)
            {
                return true;
            }

            if (parent == 0 || parent == current)
            {
                return false;
            }

            current = parent;
        }

        return false;
    }

    // Every process whose parent chain passes through rootPid
    public IEnumerable<ProcessInfo> Descendants(uint rootPid) =>
        _byPid.Values.Where(p => IsDescendantOf(p.Pid, rootPid));

    // Reads the live process list via WMI, Name and ParentProcessId are always
    // present, CommandLine can be null when WMI withholds it
    public static ProcessSnapshot Capture()
    {
        List<ProcessInfo> processes = new();

        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessId, ParentProcessId, Name, CommandLine FROM Win32_Process");

        foreach (ManagementBaseObject mo in searcher.Get())
        {
            using (mo)
            {
                uint pid = Convert.ToUInt32(mo["ProcessId"]);
                uint parent = mo["ParentProcessId"] is { } p ? Convert.ToUInt32(p) : 0;
                string name = mo["Name"] as string ?? string.Empty;
                string? commandLine = mo["CommandLine"] as string;
                if (string.IsNullOrEmpty(commandLine))
                {
                    commandLine = null;
                }

                processes.Add(new ProcessInfo(pid, parent, name, commandLine));
            }
        }

        return new ProcessSnapshot(processes);
    }
}
