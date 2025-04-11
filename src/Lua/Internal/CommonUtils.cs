using System.Runtime.CompilerServices;

namespace Lua.Internal
{
    internal static class CommonUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CeilPow2(int x)
        {
            x -= 1;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x + 1;
        }
    }

    internal static class CollectionMarshalUtils
    {
        record ListDataHelper<T>
        {
            public T[]? _items;
            public int _size;
            public int _version;
        }

        public static T[]? UnsafeGetItems<T>(this List<T> target)
        {
            if (target is null) { return null; }
            var listData = Unsafe.As<List<T>, ListDataHelper<T>>(ref target);
            return listData._items;
        }

        public static Span<T> UnsafeGetSpan<T>(this List<T> target, int start, int length)
        {
            if (target.Count > start + length)
            {
                throw new ArgumentException($"argument out of range: start({start}) + length({length}) = {start + length} > count({target.Count})");
            }
            var array = target.UnsafeGetItems();
            return array.AsSpan(start, length);
        }

        public static Span<T> UnsafeGetSpan<T>(this List<T> target)
        {
            return target.UnsafeGetSpan(0, target.Count);
        }

        public static Span<T> UnsafeGetSpan<T>(this List<T> target, int start)
        {
            return target.UnsafeGetSpan(0, target.Count).Slice(start);
        }
    }
}
