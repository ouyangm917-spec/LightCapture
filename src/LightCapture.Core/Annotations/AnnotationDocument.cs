using System.Collections.Immutable;

namespace LightCapture.Core.Annotations;

/// <summary>仅保存不可变图元快照；一次拖动只提交一次，避免每个鼠标采样都进入撤销历史。</summary>
public sealed class AnnotationDocument
{
    private ImmutableArray<Annotation> _items = [];
    private readonly List<ImmutableArray<Annotation>> _undo = [];
    private readonly Stack<ImmutableArray<Annotation>> _redo = [];
    public ImmutableArray<Annotation> Items => _items;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public int NextStepNumber => _items.Where(a => a.Kind == AnnotationKind.Step).Select(a => a.StepNumber).DefaultIfEmpty(0).Max() + 1;
    public event EventHandler? Changed;

    public void Add(Annotation item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Kind == AnnotationKind.Select) throw new ArgumentException("选择工具不是图元。", nameof(item));
        if (_items.Any(existing => existing.Id == item.Id)) throw new ArgumentException("图元标识重复。", nameof(item));
        if (item.Kind == AnnotationKind.Step) item = item with { StepNumber = NextStepNumber };
        Commit(_items.Add(item));
    }

    public void Update(Annotation item)
    {
        var index = _items.FindIndex(a => a.Id == item.Id);
        if (index < 0 || _items[index] == item) return;
        Commit(_items.SetItem(index, item));
    }

    public void Delete(Guid id)
    {
        var next = _items.RemoveAll(a => a.Id == id);
        if (next.Length != _items.Length) Commit(next);
    }

    public void Undo()
    {
        if (!CanUndo) return;
        _redo.Push(_items);
        _items = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (!CanRedo) return;
        _undo.Add(_items);
        _items = _redo.Pop();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Commit(ImmutableArray<Annotation> next)
    {
        _undo.Add(_items);
        if (_undo.Count > 100) _undo.RemoveAt(0);
        _redo.Clear();
        _items = next;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

internal static class ImmutableArrayExtensions
{
    public static int FindIndex<T>(this ImmutableArray<T> items, Func<T, bool> predicate)
    {
        for (var i = 0; i < items.Length; i++) if (predicate(items[i])) return i;
        return -1;
    }
}
