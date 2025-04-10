using System.Buffers;
using System.Text;
using Lua;
using Lua.CodeAnalysis.Syntax;
using Lua.Standard;


namespace MyLuaTests
{
    [TestClass]
    public class CommonTest
    {
        [TestMethod]
        public async ValueTask TestInternalFunction()
        {
            /* 来自项目仓库的错误反馈
             * https://github.com/nuskey8/Lua-CSharp/issues/85
             * Lua-CSharp 将报告调用一个为 nil 的函数
             * 
             */
            const string code = @"
local fn
do
    function fn(text)
        print('hello ' .. text)
    end
    
    -- Error here
    fn('world')
end
";
            const string code_fix = @"
--local fn  -- 似乎 Lua-CSharp 不能正确处理重名
do
    function fn(text)
        print('hello ' .. text)
    end
    
    -- Error here
    fn('world')
end
";
            var lua = LuaState.Create();
            lua.OpenStandardLibraries();

            await lua.DoStringAsync(code_fix);
        }
        [TestMethod]
        public void TestInternalFunction_NLua()
        {
            /* 来自项目仓库的错误反馈
             * https://github.com/nuskey8/Lua-CSharp/issues/85
             * 使用 NLua 作为对照
             */

            const string code = @"
local fn
do
    function fn(text)
        print('hello ' .. text)
    end
    
    -- Error here
    fn('world')
end
";
            using NLua.Lua nLuaState = new();
            nLuaState.State.Encoding = Encoding.UTF8;
            nLuaState.DoString(code);
        }
        [TestMethod]
        public void TestInternalFunction_MoonSharp()
        {
            /* 来自项目仓库的错误反馈
             * https://github.com/nuskey8/Lua-CSharp/issues/85
             * 使用 MoonSharp 作为对照
             */

            const string code = @"
local fn
do
    function fn(text)
        print('hello ' .. text)
    end
    
    -- Error here
    fn('world')
end
";
            var moonSharp = new MoonSharp.Interpreter.Script(MoonSharp.Interpreter.CoreModules.Preset_Default);
            moonSharp.DoString(code);
        }


        [TestMethod]
        public async ValueTask DoStringAsync()
        {
            var lua = LuaState.Create();

            /* 这里库的实现和语法实现分开了，对 Unity 来说可能需要重新实现部分 Lua 标准库函数
             * 实现了大部分 Lua 5.2 特性，也就是说，语法部分没有整数支持
             */
            lua.OpenStandardLibraries();

            var customTable = new LuaTable();
            lua.Environment["custom"] = customTable;

            customTable["myDouble"] = new Lua.LuaFunction(MyDouble_Wrap);
            customTable["myDoubleAsync"] = new Lua.LuaFunction(MyDoubleAsync_Wrap);

            /* Lua-CSharp 可以正确处理异步过程，因为整个执行过程是异步方法
             * ……这么干可能有制约，但 Lua 侧的代码会极其简单，总的来说因此增加的问题远远少于因此解决的问题
             * 目前这个库只能手动注册方法，参数分别是:
             *   - 入参缓冲区（框架内部传递）
             *   - 出参缓冲区（可选从 DoStringAsync 手动传递，否则从内部数组池获取）
             *   - 异步中断令牌
             * 返回值：
             *   - 函数返回值的个数（Lua 接受多返回值）
             */

            await lua.DoStringAsync("""
print(custom.myDouble(1))
print(custom.myDouble(2))
print(custom.myDoubleAsync(3))
print(custom.myDoubleAsync(4))
print(custom.myDouble(5))
""");
        }
        private static ValueTask<int> MyDouble_Wrap(LuaFunctionExecutionContext context, Memory<LuaValue> memory, CancellationToken token)
        {
            var val = context.GetArgument<int>(0);
            memory.Span[0] = MyDouble(val);
            return new ValueTask<int>(result: 1);
        }

        private static async ValueTask<int> MyDoubleAsync_Wrap(LuaFunctionExecutionContext context, Memory<LuaValue> memory, CancellationToken token)
        {
            var val = context.GetArgument<int>(0);
            var res = await MyDoubleAsync(val);
            memory.Span[0] = res;
            return 1;
        }

        static int MyDouble(int val)
        {
            return val * 2;
        }

        static async ValueTask<int> MyDoubleAsync(int val)
        {
            await Task.Delay(1000);
            return val * 2;
        }

        [TestMethod]
        public async ValueTask ArrayTest()
        {
            const string code = @"
print('testing tables, next, and for')

local a = {}

print('make sure table has lots of space in hash part')
for i=1,100 do a[i..""+""] = true end
for i=1,100 do a[i..""+""] = nil end
print('fill hash part with numeric indices testing size operator')
for i=1,100 do
  a[i] = true
  --assert(#a == i)
end

print('testing ipairs')
local x = 0
for k,v in ipairs{10,20,30;x=12} do
  x = x + 1
  assert(k == x and v == x * 10)
end

for _ in ipairs{x=12, y=24} do assert(nil) end

print('test for \'false\' x ipair')
x = false
local i = 0
for k,v in ipairs{true,false,true,false} do
  i = i + 1
  x = not x
  assert(x == v)
end
assert(i == 4)

print('iterator function is always the same')
assert(type(ipairs{}) == 'function' and ipairs{} == ipairs{})

if T then  --[
print('testing table sizes')

local function log2 (x) return math.log(x, 2) end

local function mp2 (n)   print('minimum power of 2 >= n')
  local mp = 2^math.ceil(log2(n))
  assert(n == 0 or (mp/2 < n and n <= mp))
  return mp
end

local function fb (n)
  local r, nn = T.int2fb(n)
  assert(r < 256)
  return nn
end

print('test fb function')
local a = 1
local lim = 2^30
while a < lim do
  local n = fb(a)
  assert(a <= n and n <= a*1.125)
  a = math.ceil(a*1.3)
end

 
local function check (t, na, nh)
  local a, h = T.querytab(t)
  if a ~= na or h ~= nh then
    print(na, nh, a, h)
    assert(nil)
  end
end


print('testing C library sizes')
do
  local s = 0
  for _ in pairs(math) do s = s + 1 end
  check(math, 0, mp2(s))
end


print('testing constructor sizes')
local lim = 40
local s = 'return {'
for i=1,lim do
  s = s..i..','
  local s = s
  for k=0,lim do 
    local t = load(s..'}')()
    assert(#t == i)
    check(t, fb(i), mp2(k))
    s = string.format('%sa%d=%d,', s, k, k)
  end
end


print('tests with unknown number of elements')
local a = {}
for i=1,lim do a[i] = i end   print('build auxiliary table')
for k=0,lim do
  local a = {table.unpack(a,1,k)}
  assert(#a == k)
  check(a, k, 0)
  a = {1,2,3,table.unpack(a,1,k)}
  check(a, k+3, 0)
  assert(#a == k + 3)
end


print('testing tables dynamically built')
local lim = 130
local a = {}; a[2] = 1; check(a, 0, 1)
a = {}; a[0] = 1; check(a, 0, 1); a[2] = 1; check(a, 0, 2)
a = {}; a[0] = 1; a[1] = 1; check(a, 1, 1)
a = {}
for i = 1,lim do
  a[i] = 1
  assert(#a == i)
  check(a, mp2(i), 0)
end

a = {}
for i = 1,lim do
  a['a'..i] = 1
  assert(#a == 0)
  check(a, 0, mp2(i))
end

a = {}
for i=1,16 do a[i] = i end
check(a, 16, 0)
if not _port then
  for i=1,11 do a[i] = nil end
  for i=30,50 do a[i] = nil end   print('force a rehash (?)')
  check(a, 0, 8)   print('only 5 elements in the table')
  a[10] = 1
  for i=30,50 do a[i] = nil end   print('force a rehash (?)')
  check(a, 0, 8)   print('only 6 elements in the table')
  for i=1,14 do a[i] = nil end
  for i=18,50 do a[i] = nil end   print('force a rehash (?)')
  check(a, 0, 4)   print('only 2 elements ([15] and [16])')
end

print('reverse filling')
for i=1,lim do
  local a = {}
  for i=i,1,-1 do a[i] = i end   print('fill in reverse')
  check(a, mp2(i), 0)
end

print('size tests for vararg')
lim = 35
function foo (n, ...)
  local arg = {...}
  check(arg, n, 0)
  assert(select('#', ...) == n)
  arg[n+1] = true
  check(arg, mp2(n+1), 0)
  arg.x = true
  check(arg, mp2(n+1), 1)
end
local a = {}
for i=1,lim do a[i] = true; foo(i, table.unpack(a)) end

end  --]


print('test size operation on empty tables')
assert(#{} == 0)
assert(#{nil} == 0)
assert(#{nil, nil} == 0)
assert(#{nil, nil, nil} == 0)
assert(#{nil, nil, nil, nil} == 0)
print'+'


local nofind = {}

a,b,c = 1,2,3
a,b,c = nil


print('next uses always the same iteraction function')
assert(next{} == next{})

local function find (name)
  local n,v
  while 1 do
    n,v = next(_G, n)
    if not n then return nofind end
    assert(v ~= nil)
    if n == name then return v end
  end
end

local function find1 (name)
  for n,v in pairs(_G) do
    if n==name then return v end
  end
  return nil  print('not found')
end


assert(print==find(""print"") and print == find1(""print""))
assert(_G[""print""]==find(""print""))
assert(assert==find1(""assert""))
assert(nofind==find(""return""))
assert(not find1(""return""))
_G[""ret"" .. ""urn""] = nil
assert(nofind==find(""return""))
_G[""xxx""] = 1
assert(xxx==find(""xxx""))

print('+')

a = {}
for i=0,10000 do
  if math.fmod(i,10) ~= 0 then
    a['x'..i] = i
  end
end

n = {n=0}
for i,v in pairs(a) do
  n.n = n.n+1
  assert(i and v and a[i] == v)
end
assert(n.n == 9000)
a = nil


local function checknext (a)
  local b = {}
  do local k,v = next(a); while k do b[k] = v; k,v = next(a,k) end end
  for k,v in pairs(b) do assert(a[k] == v) end
  for k,v in pairs(a) do assert(b[k] == v) end
end

checknext{1,x=1,y=2,z=3}
checknext{1,2,x=1,y=2,z=3}
checknext{1,2,3,x=1,y=2,z=3}
checknext{1,2,3,4,x=1,y=2,z=3}
checknext{1,2,3,4,5,x=1,y=2,z=3}

assert(#{} == 0)
assert(#{[-1] = 2} == 0)
assert(#{1,2,3,nil,nil} == 3)
for i=0,40 do
  local a = {}
  for j=1,i do a[j]=j end
  assert(#a == i)
end

print('\'maxn\' is now deprecated, but it is easily defined in Lua')
function table.maxn (t)
  local max = 0
  for k in pairs(t) do
    max = (type(k) == 'number') and math.max(max, k) or max
  end
  return max
end

assert(table.maxn{} == 0)
assert(table.maxn{[""1000""] = true} == 0)
assert(table.maxn{[""1000""] = true, [24.5] = 3} == 24.5)
assert(table.maxn{[1000] = true} == 1000)
assert(table.maxn{[10] = true, [100*math.pi] = print} == 100*math.pi)

table.maxn = nil

print('int overflow')
a = {}
for i=0,50 do a[math.pow(2,i)] = true end
assert(a[#a])

print('+')


print('erasing values')
local t = {[{1}] = 1, [{2}] = 2, [string.rep(""x "", 4)] = 3,
           [100.3] = 4, [4] = 5}

local n = 0
for k, v in pairs(t) do
  print(k, v)
  n = n+1
  assert(t[k] == v)
  t[k] = nil
  collectgarbage()
  assert(t[k] == nil)
end
assert(n == 5)


local function test (a)
  assert(not pcall(table.insert, a, 2, 20));
  table.insert(a, 10); table.insert(a, 2, 20);
  table.insert(a, 1, -1); table.insert(a, 40);
  table.insert(a, #a+1, 50)
  table.insert(a, 2, -2)
  assert(not pcall(table.insert, a, 0, 20));
  assert(not pcall(table.insert, a, #a + 2, 20));
  assert(table.remove(a,1) == -1)
  assert(table.remove(a,1) == -2)
  assert(table.remove(a,1) == 10)
  assert(table.remove(a,1) == 20)
  assert(table.remove(a,1) == 40)
  assert(table.remove(a,1) == 50)
  assert(table.remove(a,1) == nil)
  assert(table.remove(a) == nil)
print(' assert(table.remove(a, #a) == nil)')
end

a = {n=0, [-7] = ""ban""}
test(a)
assert(a.n == 0 and a[-7] == ""ban"")

a = {[-7] = ""ban""};
test(a)
assert(a.n == nil and #a == 0 and a[-7] == ""ban"")

a = {[-1] = ""ban""}
test(a)
assert(#a == 0 and table.remove(a) == nil and a[-1] == ""ban"")

print('a = {[0] = ""ban""}')
print('assert(#a == 0 and table.remove(a) == ""ban"" and a[0] == nil)')

table.insert(a, 1, 10); table.insert(a, 1, 20); table.insert(a, 1, -1)
assert(table.remove(a) == 10)
assert(table.remove(a) == 20)
assert(table.remove(a) == -1)
assert(table.remove(a) == nil)

a = {'c', 'd'}
table.insert(a, 3, 'a')
table.insert(a, 'b')
assert(table.remove(a, 1) == 'c')
assert(table.remove(a, 1) == 'd')
assert(table.remove(a, 1) == 'a')
assert(table.remove(a, 1) == 'b')
assert(table.remove(a, 1) == nil)
assert(#a == 0 and a.n == nil)

a = {10,20,30,40}
assert(table.remove(a, #a + 1) == nil)
assert(not pcall(table.remove, a, 0))
assert(a[#a] == 40)
assert(table.remove(a, #a) == 40)
assert(a[#a] == 30)
assert(table.remove(a, 2) == 20)
assert(a[#a] == 30 and #a == 2)
print('+')

a = {}
for i=1,1000 do
  a[i] = i; a[i-1] = nil
end
assert(next(a,nil) == 1000 and next(a,1000) == nil)

assert(next({}) == nil)
assert(next({}, nil) == nil)

for a,b in pairs{} do error""not here"" end
for i=1,0 do error'not here' end
for i=0,1,-1 do error'not here' end
a = nil; for i=1,1 do assert(not a); a=1 end; assert(a)
a = nil; for i=1,1,-1 do assert(not a); a=1 end; assert(a)

if not _port then
  print(""testing precision in numeric for"")
  local a = 0; for i=0, 1, 0.1 do a=a+1 end; assert(a==11)
  a = 0; for i=0, 0.999999999, 0.1 do a=a+1 end; assert(a==10)
  a = 0; for i=1, 1, 1 do a=a+1 end; assert(a==1)
  a = 0; for i=1e10, 1e10, -1 do a=a+1 end; assert(a==1)
  a = 0; for i=1, 0.99999, 1 do a=a+1 end; assert(a==0)
  a = 0; for i=99999, 1e5, -1 do a=a+1 end; assert(a==0)
  a = 0; for i=1, 0.99999, -1 do a=a+1 end; assert(a==1)
end

print('conversion')
a = 0; for i=""10"",""1"",""-2"" do a=a+1 end; assert(a==5)


collectgarbage()


print('testing generic \'for\'')

local function f (n, p)
  local t = {}; for i=1,p do t[i] = i*10 end
  return function (_,n)
           if n > 0 then
             n = n-1
             return n, table.unpack(t)
           end
         end, nil, n
end

local x = 0
for n,a,b,c,d in f(5,3) do
  x = x+1
  assert(a == 10 and b == 20 and c == 30 and d == nil)
end
assert(x == 5)



print('testing __pairs and __ipairs metamethod')
a = {}
do
  local x,y,z = pairs(a)
  assert(type(x) == 'function' and y == a and z == nil)
end

local function foo (e,i)
  assert(e == a)
  if i <= 10 then return i+1, i+2 end
end

local function foo1 (e,i)
  i = i + 1
  assert(e == a)
  if i <= e.n then return i,a[i] end
end

setmetatable(a, {__pairs = function (x) return foo, x, 0 end})

local i = 0
for k,v in pairs(a) do
  i = i + 1
  assert(k == i and v == k+1)
end

a.n = 5
a[3] = 30

a = {n=10}
setmetatable(a, {__len = function (x) return x.n end,
                 __ipairs = function (x) return function (e,i)
                             if i < #e then return i+1 end
                           end, x, 0 end})
i = 0
for k,v in ipairs(a) do
  i = i + 1
  assert(k == i and v == nil)
end
assert(i == a.n)

print""OK""

";

            var lua = LuaState.Create();
            lua.OpenStandardLibraries();

            await lua.DoStringAsync(code);
        }

        [TestMethod]
        public async ValueTask VeryBigTest()
        {
            const string code = @"
print ""testing RK""

-- testing opcodes with RK arguments larger than K limit
local function foo ()
  local dummy = {
     -- fill first 256 entries in table of constants
     1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
     17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
     33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48,
     49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64,
     65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80,
     81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96,
     97, 98, 99, 100, 101, 102, 103, 104,
     105, 106, 107, 108, 109, 110, 111, 112,
     113, 114, 115, 116, 117, 118, 119, 120,
     121, 122, 123, 124, 125, 126, 127, 128,
     129, 130, 131, 132, 133, 134, 135, 136,
     137, 138, 139, 140, 141, 142, 143, 144,
     145, 146, 147, 148, 149, 150, 151, 152,
     153, 154, 155, 156, 157, 158, 159, 160, 
  }
  assert(24.5 + 0.6 == 25.1)
  local t = {foo = function (self, x) return x + self.x end, x = 10}
  t.t = t
  assert(t:foo(1.5) == 11.5)
  assert(t.t:foo(0.5) == 10.5)   -- bug in 5.2 alpha
  assert(24.3 == 24.3)
  assert((function () return t.x end)() == 10)
end


foo()
";

            const string code_short = @"
  local short = {
    -- some entries
    1, 2, 3, 4, 5,
  }

for i=1, #short do
    assert(i == short[i])
end
";
            const string code_dummy = @"
  local dummy = {
     -- fill first 256 entries in table of constants
     1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
     17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
     33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48,
     49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64,
     65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80,
     81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96,
     97, 98, 99, 100, 101, 102, 103, 104,
     105, 106, 107, 108, 109, 110, 111, 112,
     113, 114, 115, 116, 117, 118, 119, 120,
     121, 122, 123, 124, 125, 126, 127, 128,
     129, 130, 131, 132, 133, 134, 135, 136,
     137, 138, 139, 140, 141, 142, 143, 144,
     145, 146, 147, 148, 149, 150, 151, 152,
     153, 154, 155, 156, 157, 158, 159, 160,
     161, 162, 163, 164, 165, 166, 167, 168,
     169, 170, 171, 172, 173, 174, 175, 176,
     177, 178, 179, 180, 181, 182, 183, 184,
     185, 186, 187, 188, 189, 190, 191, 192,
     193, 194, 195, 196, 197, 198, 199, 200,
     201, 202, 203, 204, 205, 206, 207, 208,
     209, 210, 211, 212, 213, 214, 215, 216,
     217, 218, 219, 220, 221, 222, 223, 224,
     225, 226, 227, 228, 229, 230, 231, 232,
     233, 234, 235, 236, 237, 238, 239, 240,
     241, 242, 243, 244, 245, 246, 247, 248,
     249, 250, 251, 252, 253, 254, 255, 256,
  }

for i=1, #dummy do
    assert(i == dummy[i])
end
";

            /* 过长的 Table 解析失败，虚拟的栈内没有预期中的表
             * 1~99 是正常的
             */

            var lua = LuaState.Create();
            lua.OpenStandardLibraries();

            //await lua.DoStringAsync(code_dummy);


            await ShowCompileInfo(lua, code_short, "code-short");
            await ShowCompileInfo(lua, code_dummy, "very-big");
        }
        static async Task ShowCompileInfo(LuaState lua, string code, string chunkName)
        {
            Console.WriteLine($"Begin {chunkName}");
            var syntaxTree = LuaSyntaxTree.Parse(code, chunkName);
            var syntaxTreeNodes = syntaxTree.Nodes;
            Console.WriteLine($"syntaxTreeNodes: {syntaxTreeNodes.Length}");
            for (int i = 0; i < syntaxTreeNodes.Length; i++)
            {
                var node = syntaxTreeNodes[i];
                Console.WriteLine($"node[{i}]: {node.GetType()}");
            }

            var chunk = Lua.CodeAnalysis.Compilation.LuaCompiler.Default.Compile(syntaxTree, chunkName);
            var chunkInstructions = chunk.Instructions;
            Console.WriteLine($"chunkInstructions: {chunkInstructions.Length}");
            for (int i = 0; i < chunkInstructions.Length; i++)
            {
                ref readonly var inst = ref chunkInstructions[i];
                Console.WriteLine($"inst[{i}]: {inst}");
            }

            var buffer = ArrayPool<LuaValue>.Shared.Rent(65535);
            Array.Clear(buffer);
            await lua.RunAsync(chunk, buffer);
            ArrayPool<LuaValue>.Shared.Return(buffer);

            Console.WriteLine($"End {chunkName}");
        }

        static async ValueTask SimpleRunLua(string code, string chunkName)
        {
            var lua = LuaState.Create();
            lua.OpenStandardLibraries();
            await lua.DoStringAsync(code, chunkName);
        }

        /// <summary>
        /// https://github.com/nuskey8/Lua-CSharp/issues/77
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async ValueTask TestOr() => await SimpleRunLua("""
function clamp(x, min, max)
    return x < min and min or (x > max and max or x)
end

-- function clamp(t, min, max)
--     if (t < min) then return min end
--     if (t > max) then return max end
--     return t
-- end

local x = clamp(0, 1, 25)
assert(x == 1)
""", "test_or");


        /// <summary>
        /// https://github.com/nuskey8/Lua-CSharp/pull/107
        /// 
        /// 用例用于验证异常回归
        /// 这个 pr 的修复会导致其它地方出现异常回归，没有实装
        /// </summary>
        /// <returns></returns>
        [TestMethod]
        public async ValueTask TestOr1() => await SimpleRunLua("""
sun = {}
sun.mass = 1

local bodies = { sun, sun, sun, sun, sun }

local function test_local(b, len)
    for i = 1, len do
        local bi = b[i]
        local bim = bi.mass
        print(bi.mass)
    end
end

local len = #bodies
test_local(bodies, len)

print"OK"
""", "test_or_1");
    }
}
