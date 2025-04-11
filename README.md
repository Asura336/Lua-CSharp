# `Lua-CSharp` (develop)



这个分支包含一些自定义的修复，会尝试合并原工程的修复和特性等改动……如果我认为有价值的话。

如果原作者干活很快的话就不会有这些东西了 :(



## 已经实现的修复：

- https://github.com/nuskey8/Lua-CSharp/pull/108
- https://github.com/nuskey8/Lua-CSharp/pull/107
    - 使脚本可以正确处理新增测试用例中嵌套的 `and`、`or` 运算符
    - 我没有完全照搬这个修复，因为照搬会导致一些其它的异常
        - 具体来说主要修改了 `src/Lua/CodeAnalysis/Compilation/FunctionCompilationContext.cs` 的内容，一些地方我并不确定
- https://github.com/nuskey8/Lua-CSharp/pull/99/files
- https://github.com/nuskey8/Lua-CSharp/pull/102/files
- https://github.com/nuskey8/Lua-CSharp/pull/101/files



## 可能导致不兼容的部分



### `Lua.Runtime.Instruction` 

在原有的实现，这个结构是 4 字节，但有一条测试用例（`tests/Lua.Tests/tests-lua/verybig.lua`）在初始化 Lua Table 时包含很多参数，在生成语法树时包含了所有信息，但在后续步骤里会有其它的参数覆盖了 `NEWTABLE` 指令，导致执行出现异常。这个错误记录在： [Using tableconstructor with more than 50 elements results in an error at runtime · Issue #76 · nuskey8/Lua-CSharp](https://github.com/nuskey8/Lua-CSharp/issues/76) 

Lua 在初始化表时如果有很多参数，会尝试将多参数拆分成多个 `SETLIST` 指令，原有的实现中如果拆分次数多于 1 就会让其它指令覆盖最初的 `NEWTABLE` 指令。为了修复这个异常，我扩增了 `Instruction` 结构的尺寸，从 4 字节增加到 8 字节，相应的内部字段也从 `UInt8` 扩增到 `UInt16`，并在 `LuaCompile::VisitTableConstructorExpressionNode(TableConstructorExpressionNode, ScopeCompilationContext)` 中略微修改了生成 `SETLIST` 指令时写入的栈位置。

这会增加一些内存开销，但对 x64 CPU 来说应该不会变得更慢，而且这让测试用例通过了。



## 扩展功能（画饼时间）

就像 `Lua-CSharp` 这个库的初衷是像 `MoonSharp` 一样实现一个便利的互操作脚本接口，提供一些便利的功能可能比兼容原有的 Lua 更有益。



### list

// 施工中



### map

// 施工中



### set

// 施工中
