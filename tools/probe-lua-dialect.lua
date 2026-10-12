-- 方言探针：用「真实目标候选」的 Lua 跑一遍 LuaDialect.Features 里的每条写法。
--
-- 为什么要单独一个探针：编辑器用的解析器（MoonSharp 2.0.0 = Lua 5.2）和设备上真正跑的 Lua
-- **不是同一个东西**。两者对同一段源码的判定可能相反，而"编辑器放行、设备炸"这个方向
-- 用户在编辑器里完全看不出来。这个探针把那一侧的事实测出来。
--
-- 两列分开报，缺一不可：
--   compile —— loadstring 只做词法/语法分析。**编辑器的真解析等价于这一列。**
--   run     —— pcall 真执行。库 / 字段不存在只在这一列暴露。
-- 也就是说：compile=OK 而 run=ERR 的写法，编辑器**永远看不见**（无论怎么对齐版本）。
--
-- 用法：tools/probe-lua-dialect.sh [lua-binary]
-- 每条 `Example` 在 src/HmiEditor.Core/Services/LuaDialect.cs 的 Features 表里**逐字**存在，
-- 两边不许漂移（LuaDialectTests 有判据）。末尾两条是**正控** —— 若连它们都不通过，
-- 说明探针装错了（喂进来的根本不是 Lua），这时**非零退出**，免得被读成"这个写法有问题"。

local cases = {
  { "goto / 标签",              [[goto done ::done::]] },
  { "bit32 库",                 [[bit32.band(1, 3)]] },
  { "table.pack",               [[table.pack(1, 2)]] },
  { "table.unpack",             [[table.unpack({1, 2})]] },
  { "整除运算符 //",            [[local a = 7 // 2]] },
  { "按位运算符 & | ~ << >>",   [[local a = 1 & 3]] },
  { "math.type",                [[math.type(1)]] },
  { "utf8 库",                  [[utf8.len("x")]] },
  { "<const> / <close> 属性",   [[local x <const> = 1]] },
  { "中文标识符",               [[local 变量 = 1]] },

  { "正控 幂运算",              [[local a = 2^3]] },
  { "正控 数值 for",            [[for i = 1, 3 do local _ = i end]] },
}

local compileErr, runErr, compileOk, runOk = 0, 0, 0, 0

for _, c in ipairs(cases) do
  local name, code = c[1], c[2]
  local chunk, cerr = loadstring(code)
  if not chunk then
    compileErr = compileErr + 1
    print(string.format("CASE\t%s\tcompile=ERR\trun=-\t%s", name, tostring(cerr)))
  else
    compileOk = compileOk + 1
    local ok, rerr = pcall(chunk)
    if ok then
      runOk = runOk + 1
      print(string.format("CASE\t%s\tcompile=OK\trun=OK\t-", name))
    else
      runErr = runErr + 1
      print(string.format("CASE\t%s\tcompile=OK\trun=ERR\t%s", name, tostring(rerr)))
    end
  end
end

-- 正控自检：这两条必须既编译通过又跑得通。不通过 ⇒ 探针本身坏了，非零退出。
if runOk < 2 then
  print("PROBE_BROKEN\t正控（幂运算 / 数值 for）未通过 —— 探针或解释器装错了，本结果不可用")
  os.exit(2)
end

print(string.format("SUMMARY\tversion=%s\tcases=%d\tcompile_ok=%d\tcompile_err=%d\trun_ok=%d\trun_err=%d",
  _VERSION, #cases, compileOk, compileErr, runOk, runErr))
