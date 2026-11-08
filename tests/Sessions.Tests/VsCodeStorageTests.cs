using Sessions.Core;

namespace Sessions.Tests;

public class VsCodeStorageTests
{
    // The real sample captured in docs/spikes/vscode.md
    private const string FolderStorage =
        """
        {
          "windowsState": {
            "lastActiveWindow": {
              "folder": "file:///c%3A/Users/ZEPHYRUS%20G14/Desktop/sessions",
              "uiState": { "mode": 0, "x": 548, "y": 87, "width": 1200, "height": 800 }
            },
            "openedWindows": []
          }
        }
        """;

    private const string WorkspaceStorage =
        """
        {
          "windowsState": {
            "lastActiveWindow": {
              "workspace": { "id": "abc", "configPath": "file:///c%3A/Users/me/proj/foo.code-workspace" }
            },
            "openedWindows": []
          }
        }
        """;

    [Theory]
    [InlineData("file:///c%3A/Users/ZEPHYRUS%20G14/Desktop/sessions", @"c:\Users\ZEPHYRUS G14\Desktop\sessions")]
    [InlineData("file:///c%3A/Users/me/proj/foo.code-workspace", @"c:\Users\me\proj\foo.code-workspace")]
    public void DecodeFileUri_DecodesDriveLocalPaths(string uri, string expected)
    {
        Assert.Equal(expected, VsCodeStorage.DecodeFileUri(uri));
    }

    [Fact]
    public void DecodeFileUri_NonFileUri_ReturnsNull()
    {
        Assert.Null(VsCodeStorage.DecodeFileUri("https://example.com"));
        Assert.Null(VsCodeStorage.DecodeFileUri(null));
    }

    [Fact]
    public void ParseWindows_FolderWindow_YieldsDecodedFolder()
    {
        var windows = VsCodeStorage.ParseWindows(FolderStorage);

        VsCodeWindow only = Assert.Single(windows);
        Assert.False(only.IsWorkspace);
        Assert.Equal(@"c:\Users\ZEPHYRUS G14\Desktop\sessions", only.Path);
        Assert.Equal("sessions", only.DisplayName);
    }

    [Fact]
    public void ParseWindows_WorkspaceWindow_YieldsConfigPath()
    {
        var windows = VsCodeStorage.ParseWindows(WorkspaceStorage);

        VsCodeWindow only = Assert.Single(windows);
        Assert.True(only.IsWorkspace);
        Assert.Equal(@"c:\Users\me\proj\foo.code-workspace", only.Path);
        Assert.Equal("foo (Workspace)", only.DisplayName);
    }

    [Fact]
    public void ParseWindows_EmptyWindow_IsSkipped()
    {
        const string json = """{ "windowsState": { "lastActiveWindow": { "uiState": {} }, "openedWindows": [] } }""";
        Assert.Empty(VsCodeStorage.ParseWindows(json));
    }

    [Fact]
    public void ParseWindows_NoWindowsState_IsEmpty()
    {
        Assert.Empty(VsCodeStorage.ParseWindows("{}"));
    }

    [Theory]
    [InlineData("AdapterRegistry.cs - sessions - Visual Studio Code", "sessions")]
    [InlineData("sessions - Visual Studio Code", "sessions")]
    [InlineData("Program.cs - foo (Workspace) - Visual Studio Code", "foo (Workspace)")]
    public void RootNameFromTitle_ExtractsRootName(string title, string expected)
    {
        Assert.Equal(expected, VsCodeStorage.RootNameFromTitle(title));
    }

    [Theory]
    [InlineData("Some Other App")]
    [InlineData("")]
    public void RootNameFromTitle_NonVsCode_ReturnsNull(string title)
    {
        Assert.Null(VsCodeStorage.RootNameFromTitle(title));
    }

    [Fact]
    public void Resolve_SingleFolderMatch_ReturnsPath()
    {
        var windows = VsCodeStorage.ParseWindows(FolderStorage);
        string? path = VsCodeStorage.Resolve("AdapterRegistry.cs - sessions - Visual Studio Code", windows);
        Assert.Equal(@"c:\Users\ZEPHYRUS G14\Desktop\sessions", path);
    }

    [Fact]
    public void Resolve_WorkspaceMatch_ReturnsConfigPath()
    {
        var windows = VsCodeStorage.ParseWindows(WorkspaceStorage);
        string? path = VsCodeStorage.Resolve("Program.cs - foo (Workspace) - Visual Studio Code", windows);
        Assert.Equal(@"c:\Users\me\proj\foo.code-workspace", path);
    }

    [Fact]
    public void Resolve_NoMatch_ReturnsNull()
    {
        var windows = VsCodeStorage.ParseWindows(FolderStorage);
        Assert.Null(VsCodeStorage.Resolve("x - other - Visual Studio Code", windows));
    }

    [Fact]
    public void ParseWindows_SameFolderInActiveAndOpened_IsDeduped()
    {
        // openedWindows repeats lastActiveWindow, VS Code's real shape, the folder must
        // count once or a title matching it looks ambiguous and fails to resolve
        const string json =
            """
            {
              "windowsState": {
                "lastActiveWindow": { "folder": "file:///c%3A/Users/ZEPHYRUS%20G14/Desktop/eth-uvm-tb" },
                "openedWindows": [
                  { "folder": "file:///c%3A/Users/ZEPHYRUS%20G14/Desktop/sessions" },
                  { "folder": "file:///c%3A/Users/ZEPHYRUS%20G14/Desktop/eth-uvm-tb" }
                ]
              }
            }
            """;
        var windows = VsCodeStorage.ParseWindows(json);

        Assert.Equal(2, windows.Count);
        string? path = VsCodeStorage.Resolve("eth_env.svh - eth-uvm-tb - Visual Studio Code", windows);
        Assert.Equal(@"c:\Users\ZEPHYRUS G14\Desktop\eth-uvm-tb", path);
    }

    [Fact]
    public void Resolve_AmbiguousSameBaseName_ReturnsNull()
    {
        // Two different folders that share the base name "app" collide on rootName
        const string json =
            """
            {
              "windowsState": {
                "lastActiveWindow": { "folder": "file:///c%3A/a/app" },
                "openedWindows": [ { "folder": "file:///c%3A/b/app" } ]
              }
            }
            """;
        var windows = VsCodeStorage.ParseWindows(json);
        Assert.Equal(2, windows.Count);
        Assert.Null(VsCodeStorage.Resolve("main.cs - app - Visual Studio Code", windows));
    }
}
