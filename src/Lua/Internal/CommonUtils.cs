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
}
