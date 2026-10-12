using HmiEditor.App.Controls;
using HmiEditor.Core.Models;
using Xunit;

namespace HmiEditor.Core.Tests;

public class UndoRedoTests
{
    [Fact]
    public void Execute_AddsToUndoStack_AndClearsRedoStack()
    {
        var mgr = new UndoRedoManager();
        var list = new List<HmiControl>();
        var ctrl = new HmiControl { Name = "Test", Type = HmiControlType.Button };

        mgr.Execute(new AddControlCommand(list, ctrl));

        Assert.True(mgr.CanUndo);
        Assert.False(mgr.CanRedo);
        Assert.Single(list);
    }

    [Fact]
    public void Undo_RemovesFromCollection_AndEnablesRedo()
    {
        var mgr = new UndoRedoManager();
        var list = new List<HmiControl>();
        var ctrl = new HmiControl { Name = "Test", Type = HmiControlType.Button };

        mgr.Execute(new AddControlCommand(list, ctrl));
        Assert.Single(list);

        mgr.Undo();
        Assert.Empty(list);
        Assert.False(mgr.CanUndo);
        Assert.True(mgr.CanRedo);
    }

    [Fact]
    public void Redo_RestoresControl()
    {
        var mgr = new UndoRedoManager();
        var list = new List<HmiControl>();
        var ctrl = new HmiControl { Name = "Test", Type = HmiControlType.Button };

        mgr.Execute(new AddControlCommand(list, ctrl));
        mgr.Undo();
        mgr.Redo();

        Assert.Single(list);
        Assert.Contains(ctrl, list);
    }

    [Fact]
    public void NewExecute_ClearsRedoStack()
    {
        var mgr = new UndoRedoManager();
        var list = new List<HmiControl>();
        var ctrl1 = new HmiControl { Name = "Ctrl1", Type = HmiControlType.Button };
        var ctrl2 = new HmiControl { Name = "Ctrl2", Type = HmiControlType.Label };

        mgr.Execute(new AddControlCommand(list, ctrl1));
        mgr.Undo();
        Assert.True(mgr.CanRedo);

        mgr.Execute(new AddControlCommand(list, ctrl2));
        Assert.False(mgr.CanRedo);
        Assert.Single(list);
    }

    [Fact]
    public void MoveControl_RestoresOriginalPosition()
    {
        var mgr = new UndoRedoManager();
        var ctrl = new HmiControl { X = 100, Y = 200, Name = "MoveTest" };

        mgr.Execute(new MoveControlCommand(ctrl, 300, 400));
        Assert.Equal(300, ctrl.X);
        Assert.Equal(400, ctrl.Y);

        mgr.Undo();
        Assert.Equal(100, ctrl.X);
        Assert.Equal(200, ctrl.Y);
    }

    [Fact]
    public void SnapshotCommand_RestoresAndRedoesCompositeState()
    {
        var mgr = new UndoRedoManager();
        var first = new HmiControl { X = 10, Y = 20, Width = 50, Height = 30 };
        var second = new HmiControl { X = 100, Y = 120, Width = 60, Height = 40 };
        var before = (first.X, first.Y, second.X, second.Y);

        mgr.Execute(new SnapshotCommand("move selection",
            () => { first.X = before.Item1; first.Y = before.Item2; second.X = before.Item3; second.Y = before.Item4; },
            () => { first.X = 40; first.Y = 50; second.X = 130; second.Y = 150; }));

        Assert.Equal((40d, 50d, 130d, 150d), (first.X, first.Y, second.X, second.Y));
        mgr.Undo();
        Assert.Equal(before, (first.X, first.Y, second.X, second.Y));
        mgr.Redo();
        Assert.Equal((40d, 50d, 130d, 150d), (first.X, first.Y, second.X, second.Y));
    }
}
