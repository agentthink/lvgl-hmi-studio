using Avalonia;
using HmiEditor.App.Controls;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

public sealed class CanvasDragPreviewStateTests
{
    [Fact]
    public void Show_ExposesValidPreviewWithoutMutatingModel()
    {
        var state = new CanvasDragPreviewState();
        var rect = new Rect(10, 20, 80, 40);
        state.Show(HmiControlType.Button, rect, true);

        Assert.True(state.IsVisible);
        Assert.True(state.IsValid);
        Assert.Equal(HmiControlType.Button, state.ControlType);
        Assert.Equal(rect, state.ViewRect);
    }

    [Fact]
    public void Clear_RemovesPreviewAndValidity()
    {
        var state = new CanvasDragPreviewState();
        state.Show(HmiControlType.Label, new Rect(1, 2, 3, 4), false);
        state.Clear();

        Assert.False(state.IsVisible);
        Assert.False(state.IsValid);
        Assert.Null(state.ControlType);
        Assert.Null(state.ViewRect);
    }
}
