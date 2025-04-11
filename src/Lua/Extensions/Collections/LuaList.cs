using System.Collections;
using Lua.Runtime;

namespace Lua.Extensions.Collections
{
    /// <summary>
    /// 标记在 Lua 中使用的列表，索引只能是数字
    /// </summary>
    public class LuaList : ILuaUserData, IEnumerable<LuaValue>
    {
        public const string k_method_count = "count";

        static readonly LuaTable s_metatable;

        static bool TryGetPositiveInteger(in LuaValue arg, out int v)
        {
            v = -1;
            return arg.TryReadDouble(out var parsedNumber)
                 && MathEx.IsInteger(parsedNumber)
                 && (v = (int)parsedNumber) >= 0;
        }

        internal static readonly LuaFunction __function_new = new("new", (ctx, buffer, t) =>
        {
            if (ctx.ArgumentCount == 0)
            {
                buffer.Span[0] = new LuaList();
                return new(1);
            }
            else
            {
                var arg0 = ctx.GetArgument(0);
                if (TryGetPositiveInteger(arg0, out var capacity))
                {
                    buffer.Span[0] = new LuaList(capacity);
                    return new(1);
                }
                else if (arg0.TryRead<IEnumerable<LuaValue>>(out var luaValues))
                {
                    buffer.Span[0] = new LuaList(new List<LuaValue>(luaValues));
                    return new(1);
                }

                LuaRuntimeException.BadArgument(ctx.State.GetTraceback(), 0, "new");
                return new(0);
            }
        });




        static readonly LuaFunction __func_index = new(Metamethods.Index, (ctx, buffer, t) =>
        {
            var @this = ctx.GetArgument<LuaList>(0);

            var arg1 = ctx.GetArgument(1);
            // if index
            if (TryGetPositiveInteger(arg1, out var index))
            {
                --index;  // Lua's table index from 1
                if (index < 0 || index >= @this.Count)
                {
                    throw new LuaRuntimeException(ctx.State.GetTraceback(),
                      $"Index out of range: index={index}, list.Count={@this.Count}");
                }

                buffer.Span[0] = @this[index];
                return new(1);
            }

            // or property
            if (arg1.TryReadString(out var method))
            {
                switch (method)
                {
                    case k_method_count:
                        buffer.Span[0] = @this.Count;
                        return new(1);
                }
            }

            // unhandled case
            throw new LuaRuntimeException(ctx.State.GetTraceback(),
                $"unhandled case at method {Metamethods.Index}");
        });

        static readonly LuaFunction __func_newindex = new(Metamethods.NewIndex, (ctx, buffer, t) =>
        {
            var @this = ctx.GetArgument<LuaList>(0);

            var arg1 = ctx.GetArgument(1);
            var arg2 = ctx.GetArgument(2);
            // if index
            if (TryGetPositiveInteger(arg1, out var index))
            {
                --index;  // Lua's table index from 1
                if (index < 0 || index > @this.Count)
                {
                    throw new LuaRuntimeException(ctx.State.GetTraceback(),
                      $"Index out of range: index={index}, list.Count={@this.Count}");
                }

                if (index == @this.Count)
                {
                    @this.m_list.Add(arg2);
                }
                else
                {
                    @this.m_list[index] = arg2;
                }
                buffer.Span[0] = arg2;
                return new(1);
            }

            // or property
            if (arg1.TryReadString(out var method))
            {
                switch (method)
                {
                    case k_method_count:
                        throw new LuaRuntimeException(ctx.State.GetTraceback(),
                            "property \"count\" is readonly");
                }
            }

            // unhandled case
            throw new LuaRuntimeException(ctx.State.GetTraceback(),
                $"unhandled case at method {Metamethods.NewIndex}");
        });

        static readonly LuaFunction __func_len = new(Metamethods.Len, (ctx, buffer, t) =>
        {
            var @this = ctx.GetArgument<LuaList>(0);
            buffer.Span[0] = @this.Count;
            return new(1);
        });

        static LuaList()
        {
            s_metatable = new LuaTable(0, 8);
            s_metatable[Metamethods.Index] = __func_index;
            s_metatable[Metamethods.NewIndex] = __func_newindex;
            s_metatable[Metamethods.Len] = __func_len;

        }

        internal readonly List<LuaValue> m_list;

        internal LuaList(List<LuaValue> existsList)
        {
            m_list = existsList;
        }

        public LuaList(int initCapacity = 8) : this(new List<LuaValue>(initCapacity))
        {
        }

        public LuaTable? Metatable
        {
            get => s_metatable;
            set => throw new NotImplementedException("This metatable is private");
        }

        public int Count => m_list.Count;
        public LuaValue this[int index]
        {
            get => m_list[index];
            set => m_list[index] = value;
        }

        public IEnumerator<LuaValue> GetEnumerator()
        {
            return ((IEnumerable<LuaValue>)m_list).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)m_list).GetEnumerator();
        }

        public static implicit operator LuaValue(LuaList @this) => new(@this);
    }
}
