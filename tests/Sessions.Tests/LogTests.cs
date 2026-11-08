using System.IO;
using Sessions.Core;

namespace Sessions.Tests;

public class LogTests
{
    [Fact]
    public void Write_AppendsLine_ToInjectedPath()
    {
        string dir = Path.Combine(Path.GetTempPath(), "SessionsTests", Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "log.txt");

        try
        {
            Log.Write(path, "first");
            Log.Write(path, "second");

            string[] lines = File.ReadAllLines(path);
            Assert.Equal(2, lines.Length);
            Assert.EndsWith("first", lines[0]);
            Assert.EndsWith("second", lines[1]);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
