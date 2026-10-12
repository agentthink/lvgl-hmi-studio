using Avalonia;
using HmiEditor.App.Controls;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

public sealed class CanvasSelectionPolicyTests
{
    [Fact]
    public void LeftToRight_RequiresFullContainmentAndSkipsLockedHidden()
    {
        var controls = new[]
        {
            new HmiControl { X = 10, Y = 10, Width = 20, Height = 20 },
            new HmiControl { X = 25, Y = 25, Width = 20, Height = 20 },
            new HmiControl { X = 12, Y = 12, Width = 10, Height = 10, IsLocked = true },
            new HmiControl { X = 12, Y = 12, Width = 10, Height = 10, IsVisible = false }
        };
        var selected = CanvasSelectionPolicy.SelectFromRubberBand(controls, new Rect(0, 0, 40, 40), true, 1, 0, 0);
        Assert.Single(selected);
    }

    [Fact]
    public void RightToLeft_SelectsIntersectingControl()
    {
        var control = new HmiControl { X = 30, Y = 30, Width = 20, Height = 20 };
        var selected = CanvasSelectionPolicy.SelectFromRubberBand(new[] { control }, new Rect(0, 0, 35, 35), false, 1, 0, 0);
        Assert.Single(selected);
    }
}
