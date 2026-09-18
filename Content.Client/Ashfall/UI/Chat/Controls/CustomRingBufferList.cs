using System.Collections;
using System.Runtime.CompilerServices;
using Robust.Shared.Utility;

namespace Content.Client.Ashfall.UI.Chat.Controls;

public sealed class CustomRingBufferList<T> : IList<T>
{
    private T[] _items;
    private int _read;
    private int _write;

    public CustomRingBufferList(int capacity)
    {
        _items = new T[capacity];
    }

    public CustomRingBufferList()
    {
        _items = [];
    }

    public int Capacity => _items.Length;

    private bool IsFull => _items.Length == 0 || NextIndex(_write) == _read;

    public void Add(T item)
    {
        if (IsFull)
            Expand();

        DebugTools.Assert(!IsFull);

        _items[_write] = item;
        _write = NextIndex(_write);
    }

    public void Clear()
    {
        _read = 0;
        _write = 0;
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            Array.Clear(_items);
    }

    public bool Contains(T item)
    {
        return IndexOf(item) >= 0;
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);

        CopyTo(array.AsSpan(arrayIndex));
    }

    private void CopyTo(Span<T> dest)
    {
        if (dest.Length < Count)
            throw new ArgumentException("Not enough elements in destination!");

        var i = 0;
        foreach (var item in this)
        {
            dest[i++] = item;
        }
    }

    public bool Remove(T item)
    {
        var index = IndexOf(item);
        if (index < 0)
            return false;

        RemoveAt(index);
        return true;
    }

    public int Count
    {
        get
        {
            var length = _write - _read;
            if (length >= 0)
                return length;

            return length + _items.Length;
        }
    }

    public bool IsReadOnly => false;

    public int IndexOf(T item)
    {
        var i = 0;
        foreach (var containedItem in this)
        {
            if (EqualityComparer<T>.Default.Equals(item, containedItem))
                return i;

            i += 1;
        }

        return -1;
    }

    public void Insert(int index, T item)
    {
        throw new NotSupportedException();
    }

    public void RemoveAt(int index)
    {
        var length = Count;
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, length);

        if (index == 0)
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                _items[_read] = default!;

            _read = NextIndex(_read);
        }
        else if (index == length - 1)
        {
            _write = WrapInv(_write - 1);

            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                _items[_write] = default!;
        }
        else
        {
            var realIdx = RealIndex(index);
            var origValue = _items[realIdx];
            T result;

            if (realIdx < _read)
            {
                DebugTools.Assert(_write < _read);

                result = ShiftDown(_items.AsSpan()[realIdx.._write], default!);
            }
            else if (_write < _read)
            {
                var fromEnd = ShiftDown(_items.AsSpan(0, _write), default!);
                result = ShiftDown(_items.AsSpan(realIdx), fromEnd);
            }
            else
            {
                result = ShiftDown(_items.AsSpan()[realIdx.._write], default!);
            }

            DebugTools.Assert(EqualityComparer<T>.Default.Equals(origValue, result));

            _write = WrapInv(_write - 1);
        }
    }

    private static T ShiftDown(Span<T> span, T substitution)
    {
        if (span.Length == 0)
            return substitution;

        var first = span[0];
        span[1..].CopyTo(span[..^1]);
        span[^1] = substitution!;
        return first;
    }

    private T GetSlot(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);

        return _items[RealIndex(index)];
    }

    public T this[int index]
    {
        get => GetSlot(index);
        set => _items[RealIndex(index)] = value;
    }

    private int RealIndex(int index)
    {
        return Wrap(index + _read);
    }

    private int NextIndex(int index) => Wrap(index + 1);

    private int Wrap(int index)
    {
        if (index >= _items.Length)
            index -= _items.Length;

        return index;
    }

    private int WrapInv(int index)
    {
        if (index < 0)
            index = _items.Length - 1;

        return index;
    }

    private void Expand()
    {
        var prevSize = _items.Length;
        var newSize = Math.Max(4, prevSize * 2);
        Array.Resize(ref _items, newSize);

        if (_write >= _read)
            return;

        var toCopy = _items.AsSpan(0, _write);
        var copyDest = _items.AsSpan(prevSize);
        toCopy.CopyTo(copyDest);

        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            toCopy.Clear();

        _write += prevSize;
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(this);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public struct Enumerator : IEnumerator<T>
    {
        private readonly CustomRingBufferList<T> _ringBufferList;
        private int _readPos;

        internal Enumerator(CustomRingBufferList<T> ringBufferList)
        {
            _ringBufferList = ringBufferList;
            _readPos = _ringBufferList._read - 1;
        }

        public bool MoveNext()
        {
            _readPos = _ringBufferList.NextIndex(_readPos);
            return _readPos != _ringBufferList._write;
        }

        public void Reset()
        {
            this = new Enumerator(_ringBufferList);
        }

        public ref T Current => ref _ringBufferList._items[_readPos];

        T IEnumerator<T>.Current => Current;
        object? IEnumerator.Current => Current;

        void IDisposable.Dispose()
        {
        }
    }
}
