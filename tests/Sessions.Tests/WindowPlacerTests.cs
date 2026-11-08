using Sessions.Core;

namespace Sessions.Tests;

public class WindowPlacerTests
{
    // A 1920x1040 work area (taskbar at the bottom).
    private static readonly ScreenRect Work = new(0, 0, 1920, 1040);

    [Fact]
    public void OnScreenRectangle_IsUnchanged()
    {
        Placement p = new(100, 100, 800, 600, 1);

        Placement clamped = WindowPlacer.ClampToWorkArea(p, Work);

        Assert.Equal(p, clamped);
    }

    [Fact]
    public void NegativeOffScreen_SlidesBackToOrigin()
    {
        Placement p = new(-500, -300, 800, 600, 1);

        Placement clamped = WindowPlacer.ClampToWorkArea(p, Work);

        Assert.Equal(0, clamped.X);
        Assert.Equal(0, clamped.Y);
        Assert.Equal(800, clamped.Width);
        Assert.Equal(600, clamped.Height);
    }

    [Fact]
    public void PastRightAndBottom_SlidesToFit()
    {
        Placement p = new(1800, 1000, 800, 600, 1);

        Placement clamped = WindowPlacer.ClampToWorkArea(p, Work);

        Assert.Equal(1920 - 800, clamped.X);
        Assert.Equal(1040 - 600, clamped.Y);
    }

    [Fact]
    public void LargerThanWorkArea_ShrinksAndPins()
    {
        Placement p = new(50, 50, 4000, 3000, 1);

        Placement clamped = WindowPlacer.ClampToWorkArea(p, Work);

        Assert.Equal(0, clamped.X);
        Assert.Equal(0, clamped.Y);
        Assert.Equal(1920, clamped.Width);
        Assert.Equal(1040, clamped.Height);
    }

    [Fact]
    public void ShowCmd_IsPreserved()
    {
        Placement p = new(-10, -10, 100, 100, ShowCmd: 3);

        Placement clamped = WindowPlacer.ClampToWorkArea(p, Work);

        Assert.Equal(3, clamped.ShowCmd);
    }
}
