
namespace Lua
{
    /// <summary>
    /// Lua 中的表会扮演很多角色，抽象一个接口用于让解释器调用 for ipair 之类的遍历过程
    /// </summary>
    public interface ILuaValueSequence
    {
        LuaTable? Metatable { get; set; }

        bool TryGetNext(in LuaValue key, out KeyValuePair<LuaValue, LuaValue> pair);
        bool TryGetValue(in LuaValue key, out LuaValue value);
    }

    public static class LuaValueSequanceExtensions
    {
        public static LuaValue ToLuaValue(this ILuaValueSequence self) => self switch
        {
            LuaTable table => new LuaValue(table),
            ILuaUserData userData => new LuaValue(userData),
            _ => throw new NotImplementedException(),
        };
    }
}