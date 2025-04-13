namespace Lua.Extensions
{
    internal static class InternalLuaValueArgUtils
    {
        /// <summary>
        /// arg => { integer which >= 0 }
        /// </summary>
        /// <param name="arg"></param>
        /// <param name="v"></param>
        /// <returns></returns>
        public static bool TryGetPositiveInteger(this in LuaValue arg, out int v)
        {
            v = -1;
            return arg.TryReadDouble(out var parsedNumber)
                 && MathEx.IsInteger(parsedNumber)
                 && (v = (int)parsedNumber) >= 0;
        }
    }
}
