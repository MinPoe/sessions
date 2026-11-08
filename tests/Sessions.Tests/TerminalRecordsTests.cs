using System;
using System.Collections.Generic;
using Sessions.Core;

namespace Sessions.Tests;

public class TerminalRecordsTests
{
    // A WT-spawned shell(101), a defterm shell handed off to WT whose parent is
    // explorer(201 -> 300 explorer), both are live, plus a dead pid with no process
    private static ProcessSnapshot Snapshot() => new(new[]
    {
        new ProcessInfo(100, 1, "WindowsTerminal.exe", null),
        new ProcessInfo(101, 100, "pwsh.exe", null),
        new ProcessInfo(300, 1, "explorer.exe", null),
        new ProcessInfo(201, 300, "powershell.exe", null),
    });

    private static TerminalRecord Record(int pid) =>
        new(pid, $@"C:\cwd\{pid}", @"C:\pwsh.exe", WtSession: null, Updated: DateTimeOffset.UnixEpoch);

    [Fact]
    public void LiveShells_KeepsLiveShellsRegardlessOfParent()
    {
        // 201 is parented by explorer (a default-terminal handoff), it must still be
        // kept, the process tree does not link it to the terminal window
        var records = new[] { Record(101), Record(201) };

        var kept = TerminalRecords.LiveShells(records, Snapshot());

        Assert.Equal(new[] { 101, 201 }, Project(kept));
    }

    [Fact]
    public void LiveShells_DropsDeadPids()
    {
        // 999 has no process in the snapshot, it is a leftover record for a closed shell
        var records = new[] { Record(101), Record(999) };

        var kept = TerminalRecords.LiveShells(records, Snapshot());

        Assert.Equal(new[] { 101 }, Project(kept));
    }

    [Fact]
    public void LiveShells_SortsByPid()
    {
        var records = new[] { Record(201), Record(101) };

        var kept = TerminalRecords.LiveShells(records, Snapshot());

        Assert.Equal(new[] { 101, 201 }, Project(kept));
    }

    private static int[] Project(IEnumerable<TerminalRecord> records)
    {
        List<int> pids = new();
        foreach (TerminalRecord r in records)
        {
            pids.Add(r.Pid);
        }

        return pids.ToArray();
    }
}
