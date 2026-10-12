# Avalonia 资源目录重复键：同一个 `.axaml` 被声明成两种 item

> 2026-09-17。Release 门禁那一批 `ArgumentException` 失败的根因。定性路径是
> **先从症状排除「并发 / 用例」→ 再从「回退后失败面扩大」定性为产物问题 → 最后顺着
> 异常里的 key 追到构建任务与运行时**。修法一行，判据可复算。
>
> 结论先给：**`Assets/Icons.axaml` 被同时声明为 `AvaloniaResource` 和默认 glob 的
> `AvaloniaXaml`，而 Avalonia 会把后者折进前者 ⇒ `@(AvaloniaResource)` 里同一条路径出现两次
> ⇒ 资源目录索引里有两条同路径条目 ⇒ 运行时用 `ToDictionary` 装它时重复键抛异常。**

## 1. 症状

```
ArgumentException : An item with the same key has already been added.
Key: /Assets/Icons.axaml
   at Avalonia.Platform.StandardRuntimePlatformServices.Register(Assembly assembly)
   at Avalonia.Controls.AppBuilderBase`1.SetupUnsafe()
   at Avalonia.Headless.HeadlessUnitTestSession.EnsureIsolatedApplication()
```

- Release 门禁首现 **12 条** UI 测试失败（异常完全一致），全部死在**建 AppBuilder** 这一步；
- 把当轮落地的改动（「方案 A」测试集合串行）**回退**后，失败面**反而扩大**到 **20+ 条**
  —— 这一刻就可以判定：**与那次改动无关，是构建产物的问题**；
- `dotnet clean` + `dotnet build HmiEditor.slnx` 后恢复 **EXIT=0 / 90 s**。

## 2. 归因链（每一步都能查出处）

| # | 环节 | 出处 | 内容 |
|---|---|---|---|
| 1 | 文件先被默认 glob 收成 `AvaloniaXaml` | `~/.nuget/packages/avalonia/12.1.0/build/AvaloniaBuildTasks.props:7-10` | `<ItemGroup Condition="'$(EnableDefaultItems)'=='True'"><AvaloniaXaml Include="**\*.axaml" Exclude="$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder)" SubType="Designer" />` —— **注意：只判 `EnableDefaultItems`；Avalonia 12 里根本没有 `EnableDefaultAvaloniaItems` 这个属性，别去设它** |
| 2 | 同一个文件又被显式写成 `AvaloniaResource` | `src/HmiEditor.App/HmiEditor.App.csproj:40` | `<AvaloniaResource Include="Assets\Icons.axaml" />`（全仓仅此一处） |
| 3 | 两处 item 被合并，路径出现两次 | `.../build/AvaloniaBuildTasks.targets` 的 `GenerateAvaloniaResources` target | `<ItemGroup><AvaloniaResource Include="@(AvaloniaXaml)" /></ItemGroup>` 然后 `<GenerateAvaloniaResourcesTask Output=".../Avalonia/resources" Resources="@(AvaloniaResource)" />` |
| 4 | 运行时用重复键建字典 ⇒ 抛 | `Avalonia.Base/Platform/Internal/AssemblyDescriptor.cs` | `AvaloniaResources = index.ToDictionary(GetPathRooted, r => new AvaloniaResourceDescriptor(...));`，其中 `GetPathRooted(r) => r.Path![0] == '/' ? r.Path : '/' + r.Path` |

第 4 步就是异常里 `Key: /Assets/Icons.axaml` 的来源：key 只来自 `!AvaloniaResources`
这份目录，而 `GetPathRooted` 的存在恰恰说明**目录里存的是不带前导斜杠的 `Assets/Icons.axaml`**。

## 3. 直接证据（读数）

### 3.1 中间产物里确实是两条

`obj/<cfg>/net10.0/Avalonia/resources` 是 `!AvaloniaResources` 在「XAML 编译步骤之前」的形态。
用 `.refactor-tmp/parse_avares_catalog.py` 解析（该脚本自带 4 条自校验）：

```
### src/HmiEditor.App/obj/Release/net10.0/Avalonia/resources  (867198 字节)
    解析：ok（version=2，条目 49，载荷 865358，size 之和多出 9707 字节（= 重复条目自身，载荷已去重））
    '/Assets/Icons.axaml'   off=0       size=9707    <== 重复!
    '/App.axaml'            off=9707    size=55439
    '/Assets/Icons.axaml'   off=0       size=9707    <== 重复!
    '/MainWindow.axaml'     off=65146   size=53797
    ...
    '/!AvaloniaResourceXamlInfo'        off=858080  size=7278
    ⇒ *** 重复路径 1 条：{'/Assets/Icons.axaml': 2}
```

三个细节都对得上：

- 两条的 `off`/`size` **完全相同**（同一个载荷被引用两次）—— 生成器**载荷去重、索引不去重**，
  所以 `size` 之和比载荷多出的 **9707** 字节正好等于重复条目自身（第二个独立签名）；
- 它排在 `@(AvaloniaResource)` 的**最前面两条**，与「显式声明在前、折叠的 glob 在后」一致；
- 目录格式自校验：全部 49 条恰好消费完索引区、`entryCount` 头 = 49、逐条不越界。

### 3.2 程序集里反而看不到原始条目 —— 这就是「平时不炸」的原因

同一个 `HmiEditor.App.dll` 里，字符串 `/Assets/Icons.axaml` 只出现 **3 次**，而且全部带前缀：

```
@1914216: b'...PrototypeCanvas.axaml\x00Build:/Assets/Icons.axaml\x00Populate:/Assets/Icons.'
@1914245: b'...ts/Icons.axaml\x00Populate:/Assets/Icons.axaml\x00NamespaceInfo:/Assets/I'
@1914279: b'...ons.axaml\x00NamespaceInfo:/Assets/Icons.axaml\x00Build:/Presets/Ide/Defa'
```

这三条是**清单资源名**（编译后的 XAML 体 / populate 委托 / 命名空间信息），存在元数据的
`#Strings` 堆里，进的是 `AssemblyDescriptor.Resources`（按含前缀的**全名**做键，天然不冲突）。
**没有任何一条是不带前缀的原始条目** —— 说明 XAML 编译步骤会用编译条目盖掉原始条目。

⇒ 机制成立：**平时炸不了（原始条目被盖掉），但只要增量构建把「盖掉」这一步错开
（例如单独 `dotnet build src/HmiEditor.App/HmiEditor.App.csproj`），程序集里就会剩下
带重复键的原始目录**。这也是为什么它表现为「偶发、clean 重建即好」。

> ⚠️ 反面教训：我一开始写的判据是「数 dll 里 `/Assets/Icons.axaml` 出现几次，>1 就是污染」。
> **那个判据是错的** —— 健康 dll 里恒为 3 次（三个前缀条目），它既不知道 key 只来自目录、
> 也不知道目录里存的是不带斜杠的路径。判据比结论更容易错，所以 §6 的脚本必须带自校验。

## 4. 修法

删掉那条冗余声明（全仓仅此一处），并留下原因注释：

```diff
   <ItemGroup>
     <RdXmlFile Include="rd.xml" />
-
-    <!-- 图标资源字典：收纳顶栏/绘图工具栏/Toolbox 所有几何图标 -->
-    <AvaloniaResource Include="Assets\Icons.axaml" />
+
+    <!-- Assets/Icons.axaml（图标资源字典）**刻意不在这里声明**：默认 glob 已收走它，
+         再声明一次会让 @(AvaloniaResource) 里出现两条同路径 ⇒ 资源目录重复键 ⇒
+         ArgumentException。详见 docs/2026-09-17-avalonia-resource-duplicate-key.md -->
   </ItemGroup>
```

为什么删掉是安全的（两条，都可核）：

1. **不失去资源**：文件仍被默认 glob 收成 `AvaloniaXaml`，照样编译、照样进目录
   （`Build:` / `Populate:` / `NamespaceInfo:` 三条一个不少）；
2. **不依赖原始条目**：唯一引用点 `src/HmiEditor.App/App.axaml:21`
   `<ResourceInclude Source="avares://HmiEditor.App/Assets/Icons.axaml" />` 是**编译 XAML**，
   由编译器解析成编译条目，不需要原始条目存在。

## 5. 验证

| 项 | 改前 | 改后 |
|---|---|---|
| 中间产物索引条目数 | 49（含 2 条 `/Assets/Icons.axaml`） | **48（无重复路径）** |
| 载荷长度 | 865358 | **865358（不变）** |
| `size` 之和超出载荷 | 9707（= 重复条目） | **0** |
| `dotnet build HmiEditor.slnx -c Release` | — | **0 警告 0 错误** |
| Release 全量测试 | — | **EXIT=0 / 121 s**（含 142 项 UI 测试，覆盖 `App.axaml` 的资源装载） |
| `bash scripts/ci-gate.sh`（6 步，**干净树**） | — | **EXIT=0 / 330 s**（大队 2527 全绿 + git 8/8 + 固件 host 8 步全过） |

「48 条 + 载荷不变」这个组合说明：删掉的只是**多余的索引条目**，载荷本来就只存了一份
—— 也就是这次改动**没有动任何实际内容**，纯属把重复键从源头去掉。

**一处归属更正（2026-09-17 晚补）**：本表初稿里那行门禁读数写的是 **EXIT=0 / 252 s**。按 mtime 回查，那一跑出自 `.refactor-tmp/planA-run.log:1255`（**19:38** 跑完），当时「方案 A」仍落地在仓库里，而且它**比本修复落地（`.refactor-tmp/finalize.log` 的 C 段，19:48）还早 10 分钟**⇒ 它证明的只是「那一刻门禁恰好是绿的」，**不能**用来证明本修复修好了门禁（事实上本修复要解决的正是那批**间歇出现**的 12 条 `ArgumentException` —— 「有一次绿」对间歇缺陷毫无说服力）。本修复后的**干净树**门禁证据由 20:38 那一跑补上：**EXIT=0 / 330 s**，见 `.refactor-tmp/gate-check.log`。教训：**给一个间歇性缺陷配证据时，必须先确认那次「绿」跑在修复之后** —— 本表初稿是从同一批日志里顺手抄的读数，抄的时候没核对时间顺序。

## 6. 判据工具（可复算）

`.refactor-tmp/parse_avares_catalog.py`：解析目录并逐条列出，四条自校验
（恰好消费完索引区 / `entryCount` 头一致 / `offset+size` 不越界 / 去重后 `size` 之和 == 载荷），
任何一条不满足就报「格式不符」而**不假装解析成功**。

```bash
python3 .refactor-tmp/parse_avares_catalog.py            # 默认扫 Debug/Release 两个中间产物
python3 .refactor-tmp/parse_avares_catalog.py <file>     # 也可指定任意目录文件
```

判据（可直接用于将来任何「同一个 axaml 被声明两次」的回归）：

> 中间产物里**不得出现重复路径**；出现即 `AssemblyDescriptor.ToDictionary` 会抛。

## 7. 契约与教训

**契约**

1. 同一个 `.axaml` **不要**同时声明为 `AvaloniaXaml` 与 `AvaloniaResource`。`.axaml` 交给默认
   glob 就好；只有**非 XAML 的二进制/文本资源**才该写 `<AvaloniaResource>`（它们不会被编译）。
2. 别去找 `EnableDefaultAvaloniaItems` —— Avalonia 12.1.0 没有这个属性（`AvaloniaBuildTasks.props`
   只判 `EnableDefaultItems`）。
3. 诊断「资源加载类」异常时，`ArgumentException` 里的 **key 就是目录里的路径**，
   直接拿它去中间产物里对账，比在代码里读图找引用快得多。

**教训**

1. **「回退后失败面扩大」= 产物问题的强信号**，此时该做的是 `dotnet clean` + 全量重建，
   而不是继续推理「是哪个改动害的」。
2. **判据也会错。** 我第一版判据（数字节）基于一个错误模型（以为 key 是直接扫出来的），
   结论完全跑偏。凡是要用来「定性」的判据，先给它一个**自校验**和**正反例**。
3. **错误信息指向文件名，不等于指向声明处。** 顺着 `Key: /Assets/Icons.axaml` 只能走到
   `AssemblyDescriptor`；要再往**构建期**走一步，才能看到「两种 item 声明同一个文件」。
