using Avalonia;
using HmiEditor.App.Controls;
using Xunit;

namespace HmiEditor.Core.Tests;

public sealed class CanvasCoordinateContextTests
{
    [Fact]
    public void RoundTrip_PreservesModelCoordinatesAtZoomAndPan()
    {
        var context = new CanvasCoordinateContext(2.0, 35, -18);
        var model = new Point(123.5, 87.25);
        var screen = context.ModelToScreen(model);
        var restored = context.ScreenToModel(screen);
        Assert.Equal(model.X, restored.X, 8);
        Assert.Equal(model.Y, restored.Y, 8);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(300)]
    [InlineData(500)]
    public void RoundTrip_BatchControls_RemainsFast(int count)
    {
        var context = new CanvasCoordinateContext(1.5, 42, 17);
        var started = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < count; i++)
        {
            var point = new Point(i * 3.25, i * 1.75);
            var restored = context.ScreenToModel(context.ModelToScreen(point));
            Assert.Equal(point.X, restored.X, 8);
            Assert.Equal(point.Y, restored.Y, 8);
        }
        started.Stop();
        Assert.True(started.ElapsedMilliseconds < 1000, $"{count} coordinate conversions took {started.ElapsedMilliseconds}ms");
    }
}
