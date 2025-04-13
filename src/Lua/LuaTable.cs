using System.Runtime.CompilerServices;
using Lua.Internal;

namespace Lua;

public sealed class LuaTable : ILuaValueSequence
{
    public LuaTable() : this(8, 8)
    {
    }

    public LuaTable(int arrayCapacity, int dictionaryCapacity)
    {
        array = new LuaValue[arrayCapacity];
        dictionary = new(dictionaryCapacity);
    }

    LuaValue[] array;
    readonly LuaValueDictionary dictionary;
    LuaTable? metatable;

    internal LuaValueDictionary Dictionary => dictionary;
    //private const int MaxArraySize = 1 << 24;
    private const int MaxDistance = 1 << 12;


    public LuaValue this[in LuaValue key]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ThrowIfIndexIsNil(key);
            if (TryGetInteger(key, out var index))
            {
                if (index > 0 && index <= array.Length)
                {
                    // Arrays in Lua are 1-origin...
                    return array[index - 1];
                }
            }

            if (dictionary.TryGetValue(key, out var value)) return value;
            return LuaValue.Nil;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set
        {
            if (key.TryReadNumber(out var d))
            {
                ThrowIfIndexIsNaN(d);
                if (MathEx.IsInteger(d))
                {
                    /* 算是 Lua 的一个设计缺陷，数组和字典都使用 Table，但在一些场合下需要让数组退化成字典
                     * C 的 Lua 实现是散列表，MoonSharp 也使用类似的方案，但这里使用的数组和字典
                     * 如果添加的键是数字，这里的实现会默认把 Table 当作数组处理，不恰当的赋值下可能让数组扩容到很夸张的地步
                     * 考虑添加扩展用自定义的列表、集合、字典替代 Lua Table
                     */

                    int index = (int)d;

                    var distance = index - array.Length;
                    if (distance > MaxDistance)
                    {
                        dictionary[key] = value;
                        return;
                    }

                    if (0 < index && index < Math.Max(array.Length * 2 - 1, 8))
                    {
                        if (array.Length < index) { EnsureArrayCapacity(index); }
                        array[index - 1] = value;
                        return;
                    }
                }
            }

            dictionary[key] = value;
        }
    }

    //public int HashMapCount
    //{
    //    get => dictionary.Count - dictionary.NilCount;
    //}

    public int ArrayLength
    {
        get
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i].Type is LuaValueType.Nil) return i;
            }

            return array.Length;
        }
    }

    public LuaTable? Metatable
    {
        get => metatable;
        set => metatable = value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(in LuaValue key, out LuaValue value)
    {
        if (key.Type is LuaValueType.Nil)
        {
            value = default;
            return false;
        }

        if (TryGetInteger(key, out var index))
        {
            if (index > 0 && index <= array.Length)
            {
                value = array[index - 1];
                return value.Type is not LuaValueType.Nil;
            }
        }

        return dictionary.TryGetValue(key, out value) && value.Type is not LuaValueType.Nil;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ref LuaValue FindValue(in LuaValue key)
    {
        ThrowIfIndexIsNil(key);
        if (TryGetInteger(key, out var index))
        {
            if (index > 0 && index <= array.Length)
            {
                return ref array[index - 1];
            }
        }

        return ref dictionary.FindValue(key, out _);
    }

    //public bool ContainsKey(in LuaValue key)
    //{
    //    if (key.Type is LuaValueType.Nil)
    //    {
    //        return false;
    //    }

    //    if (TryGetInteger(key, out var index))
    //    {
    //        return index > 0 && index <= array.Length &&
    //               array[index - 1].Type != LuaValueType.Nil;
    //    }

    //    return dictionary.TryGetValue(key, out var value) && value.Type is not LuaValueType.Nil;
    //}

    public LuaValue RemoveAt(int index)
    {
        var arrayIndex = index - 1;
        var value = array[arrayIndex];

        if (arrayIndex < array.Length - 1)
        {
            array.AsSpan(arrayIndex + 1).CopyTo(array.AsSpan(arrayIndex));
        }

        array[^1] = default;

        return value;
    }

    public void Insert(int index, in LuaValue value)
    {
        if (index <= 0 || index > array.Length + 1)
        {
            throw new IndexOutOfRangeException();
        }

        var arrayIndex = index - 1;

        var distance = index - array.Length;
        if (distance > MaxDistance)
        {
            dictionary[index] = value;
            return;
        }

        if (index > array.Length || array[^1].Type != LuaValueType.Nil)
        {
            EnsureArrayCapacity(array.Length + 1);
        }

        if (arrayIndex != array.Length - 1)
        {
            array.AsSpan(arrayIndex, array.Length - arrayIndex - 1).CopyTo(array.AsSpan(arrayIndex + 1));
        }

        array[arrayIndex] = value;
    }

    public bool TryGetNext(in LuaValue key, out KeyValuePair<LuaValue, LuaValue> pair)
    {
        var index = -1;
        if (key.Type is LuaValueType.Nil)
        {
            index = 0;
        }
        else if (TryGetInteger(key, out var integer) && integer > 0 && integer <= array.Length)
        {
            index = integer;
        }

        if (index != -1)
        {
            var span = array.AsSpan(index);
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i].Type is not LuaValueType.Nil)
                {
                    pair = new(index + i + 1, span[i]);
                    return true;
                }
            }

            foreach (var kv in dictionary)
            {
                if (kv.Value.Type is not LuaValueType.Nil)
                {
                    pair = kv;
                    return true;
                }
            }
        }
        else
        {
            if (dictionary.TryGetNext(key, out pair))
            {
                return true;
            }
        }

        pair = default;
        return false;
    }

    public void Clear()
    {
        dictionary.Clear();
    }

    public Memory<LuaValue> GetArrayMemory()
    {
        return array.AsMemory();
    }

    public Span<LuaValue> GetArraySpan()
    {
        return array.AsSpan();
    }

    internal void EnsureArrayCapacity(int newCapacity)
    {
        if (array.Length >= newCapacity) return;

        var prevLength = array.Length;
        var newLength = array.Length;
        if (newLength == 0) newLength = 8;
        if (newLength < newCapacity) { newLength = CommonUtils.CeilPow2(newCapacity); }

        const long sizeofLuaVal = 24;
        const long maxArraySize = 2 * 1024 * 1024 * 1024L;
        if (newLength * sizeofLuaVal > maxArraySize)
        {
            throw new LuaException($"Too large LuaTable::array, Length={newLength}({newLength * sizeofLuaVal} bytes)");
        }

        Array.Resize(ref array, newLength);

        using var indexList = new PooledList<(int, LuaValue)>(dictionary.Count);

        // Move some of the elements of the hash part to a newly allocated array
        foreach (var kv in dictionary)
        {
            if (TryGetInteger(kv.Key, out var index))
            {
                if (index > prevLength && index <= newLength)
                {
                    indexList.Add((index, kv.Value));
                }
            }
        }

        foreach ((var index, var value) in indexList.AsSpan())
        {
            dictionary.Remove(index);
            array[index - 1] = value;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static bool TryGetInteger(in LuaValue value, out int integer)
    {
        if (value.TryReadNumber(out var num) && MathEx.IsInteger(num))
        {
            integer = (int)num;
            return true;
        }

        integer = default;
        return false;
    }

    static void ThrowIfIndexIsNil(in LuaValue val)
    {
        if (val.Type == LuaValueType.Nil) { throw new ArgumentException("the table index is nil"); }
    }

    static void ThrowIfIndexIsNaN(double val)
    {
        if (double.IsNaN(val)) { throw new ArgumentException("the table index is NaN"); }
    }
}