namespace Sessions.Core;

// Window placement in virtual-screen coordinates, plus the SW_* show command
public sealed record Placement(int X, int Y, int Width, int Height, int ShowCmd);
