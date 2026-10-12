using HmiEditor.Core.Models;
using HmiEditor.App.Resources;

namespace HmiEditor.App.Controls;

/// <summary>可撤销操作的接口。</summary>
public interface IUndoableCommand
{
    void Execute();
    void Undo();
    string Description { get; }
}

/// <summary>撤销/重做管理器 —— 使用双栈 Command 模式。</summary>
public class UndoRedoManager
{
    private readonly Stack<IUndoableCommand> _undoStack = new(100);
    private readonly Stack<IUndoableCommand> _redoStack = new(100);

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string? UndoDescription =>
        _undoStack.TryPeek(out var cmd) ? cmd.Description : null;

    public string? RedoDescription =>
        _redoStack.TryPeek(out var cmd) ? cmd.Description : null;

    public event EventHandler? StateChanged;

    public void Execute(IUndoableCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (!CanUndo) return;
        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (!CanRedo) return;
        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>基于快照的编辑命令。编辑器可在拖拽/批量编辑结束时提交一次。</summary>
public sealed class SnapshotCommand : IUndoableCommand
{
    private readonly Action _redo;
    private readonly Action _undo;
    public string Description { get; }

    public SnapshotCommand(string description, Action undo, Action redo)
    {
        Description = description;
        _undo = undo;
        _redo = redo;
    }

    public void Execute() => _redo();
    public void Undo() => _undo();
}


/// <summary>移动控件的可撤销命令。</summary>
public class MoveControlCommand : IUndoableCommand
{
    private readonly HmiControl _control;
    private readonly double _oldX, _oldY;
    private readonly double _newX, _newY;

    public string Description => AppLocalization.Service.Format("undo.move", Services.ControlNameDefaults.Display(_control));

    public MoveControlCommand(HmiControl control, double newX, double newY)
    {
        _control = control;
        _oldX = control.X; _oldY = control.Y;
        _newX = newX; _newY = newY;
    }

    public void Execute()
    {
        _control.X = _newX;
        _control.Y = _newY;
    }

    public void Undo()
    {
        _control.X = _oldX;
        _control.Y = _oldY;
    }
}

/// <summary>添加控件的可撤销命令。</summary>
public class AddControlCommand : IUndoableCommand
{
    private readonly IList<HmiControl> _collection;
    private readonly HmiControl _control;

    public string Description => AppLocalization.Service.Format("undo.add", Services.ControlNameDefaults.Display(_control));

    public AddControlCommand(IList<HmiControl> collection, HmiControl control)
    {
        _collection = collection;
        _control = control;
    }

    public void Execute()
    {
        if (!_collection.Contains(_control))
            _collection.Add(_control);
    }
    public void Undo() => _collection.Remove(_control);
}

/// <summary>删除控件的可撤销命令。</summary>
public class DeleteControlCommand : IUndoableCommand
{
    private readonly IList<HmiControl> _collection;
    private readonly HmiControl _control;
    private readonly int _index;

    public string Description => AppLocalization.Service.Format("undo.delete", Services.ControlNameDefaults.Display(_control));

    public DeleteControlCommand(IList<HmiControl> collection, HmiControl control)
    {
        _collection = collection;
        _control = control;
        _index = collection.IndexOf(control);
    }

    public void Execute() => _collection.Remove(_control);
    public void Undo() => _collection.Insert(_index, _control);
}

/// <summary>属性修改的可撤销命令。</summary>
public class PropertyChangeCommand : IUndoableCommand
{
    private readonly HmiControl _control;
    private readonly string _propertyName;
    private readonly object _oldValue;
    private readonly object _newValue;

    public string Description => AppLocalization.Service.Format("undo.modify", Services.ControlNameDefaults.Display(_control), _propertyName);

    public PropertyChangeCommand(HmiControl control, string propertyName,
        object oldValue, object newValue)
    {
        _control = control;
        _propertyName = propertyName;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public void Execute() => SetValue(_newValue);
    public void Undo() => SetValue(_oldValue);

    private void SetValue(object value)
    {
        var prop = typeof(HmiControl).GetProperty(_propertyName);
        prop?.SetValue(_control, value);
    }
}
