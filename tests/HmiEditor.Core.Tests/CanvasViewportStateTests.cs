using Avalonia;
using HmiEditor.App.Controls;
using Xunit;

namespace HmiEditor.Core.Tests;

public sealed class CanvasViewportStateTests
{
    [Fact]
    public void ZoomAt_PreservesAnchorContentPosition()
    {
        var state = new CanvasViewportState(1, 20, 30);
        var anchor = new Point(400, 250);
        var next = state.ZoomAt(anchor, 2);
        var modelX = (anchor.X - state.PanX) / state.Zoom;
        var modelY = (anchor.Y - state.PanY) / state.Zoom;
        Assert.Equal(anchor.X, modelX * next.Zoom + next.PanX, 8);
        Assert.Equal(anchor.Y, modelY * next.Zoom + next.PanY, 8);
    }

    [Fact]
    public void ZoomAt_ClampsExtremeZoomValues()
    {
        var state = new CanvasViewportState(1, 0, 0);
        Assert.Equal(4, state.ZoomAt(new Point(0, 0), 99).Zoom);
        Assert.Equal(0.25, state.ZoomAt(new Point(0, 0), -1).Zoom);
    }
}
