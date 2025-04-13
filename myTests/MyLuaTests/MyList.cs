using System.Runtime.CompilerServices;
using Lua;
using Lua.Extensions.Collections;
using Lua.Runtime;
using Lua.Standard;

namespace MyLuaTests
{
    [TestClass]
    public class MyListTest
    {
        [TestMethod]
        public async ValueTask TestMyList()
        {
            const string code = """
local list = __list()

--[[
    尝试为列表注册实例方法，但没有成功。
    将方法放置在 __index 元方法中索引，调用时参数只有方法参数，没有隐式的 this
    适应 Lua 的习惯，使用 # 运算符（对列表来说，时间复杂度 O(1)）和重载 + 运算符实现
    要合并两个列表，考虑重载 .. 运算符
]]

list[#list + 1] = 1
list[#list + 1] = 2
list = list + 114514

assert(list.count == 3)
assert(#list == 3)

local list_1 = __list()
list_1[#list_1 + 1] = 514
list = list..list_1..list_1

print('----for loop----')
-- for loop
local len = #list
for i = 1, len do
    print(list[i])
end
print('')

print('----for pairs----')
-- foreach
for i, v in pairs(list) do
    print(i, v)
end
print('')

print('----for ipairs----')
-- foreach
for i, v in ipairs(list) do
    print(i, v)
end
print('')
""";

            var lua = LuaState.Create();
            lua.OpenStandardLibraries();

            lua.Environment["__list"] = MyList.__func_create;
            await lua.DoStringAsync(code, "test-my-list");
        }

        static async ValueTask RunWithExtLib(string code, [CallerMemberName] string chunkName = "undefined")
        {
            var lua = LuaState.Create();
            lua.OpenStandardLibraries();
            lua.OpenCollectionsExtLib();
            await lua.DoStringAsync(code, chunkName);
        }

        [TestMethod]
        public async ValueTask TestMyList1() => await RunWithExtLib("""
local li = list.new()

li[#li + 1] = 1
li[#li + 1] = 2
list.add(li, 114514)

assert(li.count == 3)
assert(#li == 3)

local li_1 = list()
li_1[#li_1 + 1] = 514
li_1 = li..li_1..{ 1919810 }

print('----for loop----')
-- for loop
local len = #li
for i = 1, len do
    print(li[i])
end
print('')

print('----for pairs----')
-- foreach
for i, v in pairs(li) do
    print(i, v)
end
print('')

print('----for ipairs----')
-- foreach
for i, v in ipairs(li) do
    print(i, v)
end
print('')
""");
    }

    public sealed class MyList(List<LuaValue> list) : Lua.ILuaUserData, ILuaValueSequence
    {
        static LuaTable? s_metaTable;
        static MyList()
        {
            s_metaTable = new LuaTable(0, 8);
            s_metaTable[Metamethods.Index] = __func_index;
            s_metaTable[Metamethods.NewIndex] = __func_newindex;


            s_metaTable[Metamethods.Len] = __func_len;
            s_metaTable[Metamethods.Add] = __func_add;
            s_metaTable[Metamethods.Concat] = __func_concat;

            //s_metaTable[Metamethods.IPairs] = __func_ipairs;
        }

        #region shared methods
        public static readonly LuaFunction __func_create = new("create", (ctx, buffer, token) =>
        {
            var arg_initCapacity = ctx.ArgumentCount == 0 ? 8 : ctx.GetArgument<int>(0);
            buffer.Span[0] = new(Create(arg_initCapacity));
            return new(1);
        });

        #endregion

        #region metatable
        static readonly LuaFunction __func_add = new("__add", (ctx, buffer, token) =>
        {
            var userData = ctx.GetArgument<MyList>(0);
            var value = ctx.GetArgument(1);
            userData.m_body.Add(value);

            buffer.Span[0] = userData;
            return new(1);
        });

        static readonly LuaFunction __func_len = new("__len", (ctx, buffer, token) =>
        {
            var userData = ctx.GetArgument<MyList>(0);
            buffer.Span[0] = userData.Count;
            return new(1);
        });

        static readonly LuaFunction __func_index = new("__index", (ctx, buffer, token) =>
        {
            var userData = ctx.GetArgument<MyList>(0);

            // if index
            var arg1 = ctx.GetArgument(1);
            int index = -1;
            bool argIsIndex = false;
            switch (arg1.Type)
            {
                case LuaValueType.Number when arg1.TryRead(out index):
                    argIsIndex = true;
                    break;
                case LuaValueType.String when arg1.TryRead(out string t) && int.TryParse(t, out index):
                    argIsIndex = true;
                    break;
            }
            if (argIsIndex)
            {
                --index;  // Lua 的索引从 1 开始
                if (index < 0 || index >= userData.Count)
                {
                    throw new LuaRuntimeException(ctx.State.GetTraceback(),
                        $"Index out of range: index={index}, list.Count={userData.Count}");
                }

                buffer.Span[0] = userData[index];
                return new(1);
            }

            // methods
            if (arg1.TryRead(out string method))
            {
                var res = method switch
                {
                    "count" => userData.Count,
                    // 如果要支持成员方法...
                    // 需要传递隐式的 this 引用
                    // 这里返回一张新表，表成员包含 userData，其元表的 __call 函数映射到对应的过程？
                    // 但是每次调用方法生成一个新表总觉得有点……
                    // 或者干脆放弃这种做法，用类似于 ClassName.MethodName(obj, param...) 的写法
                    _ => LuaValue.Nil,
                };
                buffer.Span[0] = res;
                return new(1);
            }
            else
            {
                LuaRuntimeException.BadArgument(ctx.State.GetTraceback(), 2, "__index");
                return new(0);
            }
        });

        /// <summary>
        /// newindex 是写入索引的过程，Lua 的习惯是直接写入键值对，对列表来说可以用 list[#list + 1] = value 来添加对象，
        ///行为的语义实质上是 insert
        /// </summary>
        static readonly LuaFunction __func_newindex = new("__newindex", (ctx, buffer, token) =>
        {
            var userData = ctx.GetArgument<MyList>(0);

            // if index
            var arg1 = ctx.GetArgument(1);
            int index = -1;
            bool argIsIndex = false;
            switch (arg1.Type)
            {
                case LuaValueType.Number when arg1.TryRead(out index):
                    argIsIndex = true;
                    break;
                case LuaValueType.String when arg1.TryRead(out string t) && int.TryParse(t, out index):
                    argIsIndex = true;
                    break;
            }
            if (argIsIndex)
            {
                --index;  // Lua 的索引从 1 开始
                if (index < 0 || index > userData.Count)
                {
                    throw new LuaRuntimeException(ctx.State.GetTraceback(),
                        $"Index out of range: index={index}, list.Count={userData.Count}");
                }

                var value = ctx.GetArgument(2);
                if (index == userData.Count)
                {
                    userData.m_body.Add(value);
                }
                else
                {
                    userData[index] = value;
                }
                buffer.Span[0] = value;
                return new(0);
            }

            // methods
            if (arg1.TryRead(out string method))
            {
                var res = method switch
                {
                    "count" => throw new LuaRuntimeException(ctx.State.GetTraceback(), "cannot set list.count"),
                    _ => LuaValue.Nil,
                };
            }
            return new(0);
        });

        static readonly LuaFunction __func_concat = new("concat", (ctx, buffer, t) =>
        {
            var @this = ctx.GetArgument<MyList>(0);
            var another = ctx.GetArgument<ILuaValueSequence>(1);

            for (var p = LuaValue.Nil;
            another.TryGetNext(p, out var next);
            p = next.Key)
            {
                @this.m_body.Add(next.Value);
            }

            buffer.Span[0] = @this;
            return new(1);
        });
        #endregion




        readonly List<LuaValue> m_body = list;

        MyList(int initCapacity = 8) : this(new List<LuaValue>(initCapacity))
        {

        }

        public List<LuaValue> Body => m_body;


        public int Count => m_body.Count;


        public LuaValue this[int index]
        {
            get => m_body[index];
            set => m_body[index] = value;
        }


        public static MyList FromList(List<LuaValue> list) => new(list);
        public static MyList Create(int capacity = 8) => new(capacity);

        public LuaTable? Metatable { get => s_metaTable; set => s_metaTable = value; }

        public static implicit operator LuaValue(MyList list) => new(list);

        public bool TryGetNext(in LuaValue key, out KeyValuePair<LuaValue, LuaValue> pair)
        {
            pair = default;

            int index;
            if (key.Type is LuaValueType.Nil)
            {
                index = 0;
                pair = new KeyValuePair<LuaValue, LuaValue>(index, m_body[index]);
                return true;
            }
            else if (!key.TryRead(out index))
            {
                return false;
            }

            index++;
            if (index >= 0 && index < m_body.Count)
            {
                pair = new KeyValuePair<LuaValue, LuaValue>(index, m_body[index]);
                return true;
            }

            return false;
        }

        public bool TryGetValue(in LuaValue key, out LuaValue value)
        {
            value = default;
            if (key.TryRead(out int index))
            {
                index--;
                if (index >= 0 && index < m_body.Count)
                {
                    value = m_body[index];
                    return true;
                }
            }
            return false;
        }
    }
}
