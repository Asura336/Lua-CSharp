namespace Lua.Extensions.Collections;

/// <summary>
/// 标记集合类的接口
/// </summary>
public class CollectionLibrary
{
    public static readonly CollectionLibrary Instance = new();

    public CollectionLibrary()
    {
        Functions = [];
    }

    public readonly LuaFunction[] Functions;
}

public static partial class ExtensionLibExtensions
{
    public static void OpenCollectionsExtLib(this LuaState state)
    {
        state.Environment["list"] = LuaListSharedMethods.s_methodTable;
    }
}