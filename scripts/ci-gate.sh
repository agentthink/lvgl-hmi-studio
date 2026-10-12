#!/usr/bin/env bash
# ============================================================
# ci-gate.sh — 统一的仓库质量门禁（本地与 CI 共用）
#
# 与 verify.sh 的分工：
#   ci-gate.sh   —— 快速、无外部依赖的阻断门禁（语法 / 构建 / 测试 / 固件 host 测试）
#   verify.sh    —— 端到端验收（Golden 工程导出 + 真实 LVGL C 编译 + selftest + CTest）
#
# 退出码：0 = 全部通过；非 0 = 首个失败步骤的退出码。
#
# 环境变量：
#   CI_REQUIRE_FIRMWARE_TESTS=1  缺少 gcc 时也判定失败（CI 建议开启）
# ============================================================
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

CONFIG="${CONFIG:-Release}"
step=0
FAILED=0

banner() { step=$((step + 1)); echo; echo "===== [$step] $* ====="; }
die()    { echo "❌ $*" >&2; exit 1; }

# ── 负载记账（阶段 59 立的规矩，阶段 223 落地到门禁）──
# 「凡引用墙钟须同时记 /proc/loadavg，没有负载读数的墙钟只能同批内部比较」。
# 门禁日志从来没记过负载，而同一批连跑的负载从 1.89 爬到 5.09、UI 段随之 27 s → 36 s
# ⇒ 阶段 223 起把负载写进日志（开始 / 测试前 / 测试后三处），让墙钟可归因。
loadavg() { cut -d' ' -f1-3 /proc/loadavg 2>/dev/null || echo "n/a"; }
echo "负载 loadavg(开始): $(loadavg)"

# ── [1] Shell 语法检查（含固件 OTA/启动脚本与容器入口） ──
banner "bash -n 脚本语法检查"
# 用临时文件收集清单，而不是 `mapfile -t X < <(...)`：进程替换依赖 /dev/fd，
# 在受限沙箱 / 精简容器里会**静默**拿到空清单，使这一步变成永远通过的假门禁。
SCRIPT_LIST="$(mktemp)"
trap 'rm -f "$SCRIPT_LIST"' EXIT
if git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    git ls-files '*.sh' > "$SCRIPT_LIST"
else
    find . -name '*.sh' -not -path './firmware/Tina-*/*' -not -path './.git/*' > "$SCRIPT_LIST"
fi
script_count=0
while IFS= read -r f; do
    [ -n "$f" ] || continue
    if ! out=$(bash -n "$f" 2>&1); then
        echo "$out" >&2
        die "Shell 语法错误: $f"
    fi
    script_count=$((script_count + 1))
done < "$SCRIPT_LIST"
[ "$script_count" -gt 0 ] || die "未找到任何 .sh 脚本（清单为空，检查上面的收集命令）"
echo "✅ ${script_count} 个脚本语法正确"

# ── [1b] 构建脚本不得就地改写被跟踪的应用源码 ──
# 背景：tina/build.sh 曾用 `sed -i` 按板型改写 app/runtime/runtime_config.h，
# 每次构建都污染工作区、切板后源码与产物不一致（构建不可复现）。
# 板级差异现由 app/runtime/board_profile.h + 编译期 -D 注入表达。
if grep -rn "sed[[:space:]]\+-i" firmware/t113/tina/build.sh 2>/dev/null \
   | grep -qE "app/(runtime|drivers|comm|settings)/"; then
    grep -rn "sed[[:space:]]\+-i" firmware/t113/tina/build.sh | grep -E "app/(runtime|drivers|comm|settings)/" >&2
    die "build.sh 仍在用 sed -i 改写应用源码：请改为 board_profile.h + 编译期 -D 注入"
fi
echo "✅ 构建脚本未就地改写应用源码"

# ── [1c] 产品/工具代码不得硬编码开发机家目录 ──
# 背景：tools/MockHmiGen 曾写死 /home/<开发者>/...（默认输出目录 + "下一步"提示语），
# 后果是①在他人机器/CI 上凭空创建 /home/<别人>/文档 并写文件；②产物路径随机器漂移，
# 导出的模拟器工程不可复现。同一模式还牵出 host_test 的字体绝对路径与两个烧录脚本的
# 本机默认值（现均改为 $HOME / 仓库相对定位 / 构建期注入 / 环境变量可覆盖）。
#
# ⚠ **覆盖面取决于「是否被 git 跟踪」**（阶段 119 实测，负控 N4）：
#   本检查用 `git ls-files` 收集清单 ⇒ **未跟踪的新文件根本不在扫描面内**。
#   实测同一份注入（新脚本里加一行引号内的 `/home/<别人>/…`）：
#     · 未跟踪时 **绿**（已检查 668 个文件，计数纹丝不动）；
#     · `git add` 之后 **红**，并点名那一条（计数 669）。
#   ⇒ **新增文件的那一批，`git add` 必须早于门禁跑**，否则这一步对它是空转。
#   这不是可以忽略的细节：「门禁跑在代码落盘之后、提交之前」这句纪律里，
#   「落盘」对**新文件**而言 ＝ **进索引**，不是「文件存在于磁盘上」。
banner "路径卫生（无硬编码开发机家目录）"
bash scripts/check-path-hygiene.sh

# ── [1d] 控件事件不得"XAML 声明 + code-behind 再 += 一遍" ──
#
# 背景（2026-09-21，登记表 #43）：Avalonia 的 `Click += H` **不去重** —— 实测
#   `Click += H` 两次 ⇒ H 执行 2 次；`Click += H` 与 `AddHandler(ClickEvent, H)` 混用也是 2 次。
#   `FaceplateLibraryView` 的 `.axaml` 写了 `Click="OnAiTemplateClicked"`，`.axaml.cs` 又写了
#   `BtnAiTemplate.Click += OnAiTemplateClicked` ⇒ 同一个 handler 跑 2 遍，叠加视图事件的双路消费
#   后，"点一次 AI" 弹出 **4 个**对话框。
#   首版扫描口径写窄了（只扫 `Btn*` + `Click`）⇒ 只发现 8 处；放宽到"任意 x:Name 控件的任意事件"
#   后实测 **12 处 / 5 文件**，另有 4 处是 `SearchBox.TextChanged`、`TemplateListBox.SelectionChanged`
#   这种**非按钮**事件（它们幂等，用户看不见，但同样违反"一个事件只接一路"）。
#
# 为什么必须进门禁而不是只留脚本：这类缺陷的症状（弹窗数量翻倍）不会让任何编译/测试失败，
#   只会在真机上被人肉发现；而"再 += 一次"是随手就能写出来的（XAML 与 code-behind 在两个文件里）。
#
# 为什么用 python3 而这一步之前没有这个依赖：本检查要同时解析 .axaml 的**跨行元素**
#   （属性可能跨 5 行）与 .axaml.cs 的接线语句，bash/awk 写出来远不如脚本可维护。
#   依赖是 **fail-closed** 的：下面 `command -v python3` 找不到就直接判失败，
#   不会静默跳过变成假门禁（本文件 [1] 步已经踩过"清单为空 ⇒ 永远通过"的坑）。
#   ⚠ 该检查是**单向判据**：它只保证"XAML 声明过的那个事件"没被重复接 —— 反过来
#     （code-behind 接了、XAML 没声明）是**合法且常见**的（代码构建的控件只能这么接），
#     所以不能把它反过来判，否则一片假红。
command -v python3 >/dev/null 2>&1 || die "本步需要 python3（重复接线静态检查），未找到"
if ! python3 scripts/check-xaml-duplicate-wiring.py; then
    die "存在重复接线：同一控件事件既在 .axaml 里声明、又在 .axaml.cs 里 += 了一次"
fi

# ── [1e] 宿主不得直订面板视图事件（唯一消费路径是 DockLayoutState 总线）──
#
# 与 [1d] 是**同一族缺陷的两种形态**（都属登记表 #43/#46）：[1d] 管"同一个控件的同一个事件
#   被 .axaml 与 .axaml.cs 各接一路"；这里管"同一个**视图事件**被'视图转发到静态总线'
#   与'宿主直订视图实例'各消费一路"。两者的共同根因：CLR/Avalonia 的 `+=` **都不去重**。
#
# 背景（2026-09-21，登记表 #46）：面板视图在自己的构造函数里把内部事件转发到静态
#   `DockLayoutState`（当年的用意是"Dock 重建换了实例后宿主仍能收到"，见
#   Wiring.DockAggregate.cs 的方法头），而宿主 `Wiring.*.cs` 又**同时**直订视图实例
#   ⇒ 每次触发执行两遍。实测后果：双击模板创建 **2 个**实例、预览态点控件 `ExecuteAction`
#   跑 **2 遍**、状态栏被写两遍、字体子集化对话框弹 2 个 —— 其中好几处**会写坏数据**（非幂等）。
#   最外层读数：`BtnAiTemplate` 弹窗数 **+4（双接线 × 双路）→ +2 → +1**。
#
# 为什么必须进门禁、而不是"删干净就完事"：直订那一路是**随手就能加回来**的
#   （写法自然、位置随意），而加回来之后**编译不红、测试也多半不红** ——
#   症状（弹两次窗 / 写两遍状态）只能在真机上被人肉发现。本阶段就是靠
#   `DockPanelInvocationUiTests` 的弹窗数量断言才抓到最外层那一路。
#
# 判据形态：**接收者是不是面板视图**由声明推导（`private FaceplateLibraryView FaceplatePanel => …`），
#   不猜名字后缀 —— 首版用"标识符以 Panel 结尾"，实测**误报 4 处**
#   （`NumLeftPanel/NumRightPanel/NumBottomPanel` 是数值框、`ChartPanel` 是趋势图控件）。
#   ⚠ 单向判据：视图转发到总线那一侧正是要保留的，不能反判；宿主订阅 `DockLayoutState.*` 一律合法。
#   fail-closed 两层：① python3 依赖已在上面的 `command -v` 里确认过（本段复用同一次检查）；
#   ② 脚本内置 `MIN_VIEW_MEMBERS` 下限 —— 声明格式一变、正则失效，推导出的成员集合会塌掉，
#      那会让判据退化成"永远 0 违规"的假门禁，所以**成员太少直接判失败**。
#   自带 `--selftest`（8 档：3 档该红 + 5 档该绿，含白名单与"名字碰巧同缀"两类误报）。
if ! python3 scripts/check-dock-bus-only-subscription.py; then
    die "存在宿主直订面板视图事件：面板事件只能经 DockLayoutState 消费（缺陷 #46）"
fi

# ── [1f] 不得用「位置」在容器的 .Children 里定位控件 ──
#
# 与 [1d]/[1e] 同源（都是"接线写错一维就静默失效"），错的那一维不同：
#   [1d] 同一个**控件事件**被接两路（.axaml 声明 + .axaml.cs 再 +=）；
#   [1e] 同一个**视图事件**被接两路（总线转发 + 宿主直订）；
#   [1f] 只接了一路，但那一路**找错了控件** —— 用位置（`Children[N]` / `First()`）而不是引用或名字。
#
# 背景（登记表 #57）：阶段 75 修 LuaHelpDialog 的关闭按钮时踩到 ——
#   `grid.Children[1] is Button closeBtn`，往那个 Grid 插一个控件 ⇒ `is Button` 失配 ⇒ **整段跳过**
#   （按钮点不动、不抛不报）。阶段 76 全仓扫描后又找到同型 3 处，都在 GitDialogService：
#   `((Button)p2.Children[2]).Click += …` 与
#   `var buttons = panel.Children.OfType<StackPanel>().First().Children;` 再 `((Button)buttons[1])`。
#   后者的失效面更重：往外层面板插一个 StackPanel，「取消/提交」会**悄悄互换**（硬转不抛、接错按钮）。
#
# 为什么必须进门禁：症状是"按钮点不动 / 点了错的按钮"——编译不红、测试也多半不红
#   （要真显示窗才观察得到，而那条路径在 headless 下会挂死，见阶段 73），只能在真机上被人肉发现；
#   而 `Children[N]` 是随手就能写出来的。
#
# 判据形态：**两级，两档都硬判为 0**（阶段 77 起）。
#   · wire 类（命中所在语句还做了 `+=`/`-=` 或硬转 `(T)`）⇒ **硬判为 0**：后果是功能静默断线，
#     不许进豁免表（脚本里 `WIRE_ALLOWED` 刻意留空，要豁免必须先改脚本 = 强制写理由）。
#   · read 类（只做 `is`/`as`/取值）⇒ 阶段 76 曾是**基线登记制**（登记了 7 处，后果限于
#     "那一行停在旧值"，比 wire 类轻）；阶段 77 把三份视图的列表行改成"构造时捕获控件的行类型"
#     （AlarmGroupRow / AnimationRow / RecipeParamRow）之后**清零** ⇒ 现在同样**硬判为 0**，
#     `BASELINE` 空置；双向差集机制保留（基线一旦非空，表外新增与表内失效各自都会红）。
#   · **形态 F4（阶段 78 补齐）＝「位置写入」**：`RemoveAt(<字面整数>)` / `Insert(<字面整数>, …)`。
#     `Children[2]` 读与 `RemoveAt(2)` 写是**同一个假设**（"前 2 个是固定保留区"），
#     而阶段 76 只守了读的那一半 ⇒ 往静态区加一个元素就会被**静默删掉**。
#     实测落地场景：`PropertyPanelView.Faceplate` 的 `while (Count > 2) RemoveAt(2)` —— 已改成
#     持引用的动态容器 `FaceplateParamHost.Children.Clear()`（XAML 里给它自己的 StackPanel）。
#   ⚠ **基线清空顺手拿掉了一条 fail-closed，必须换承担者**（别省这一步）：此前"表非空"本身
#     就是正则失效的探测器 —— 正则一坏，表内条目集体"消失"⇒ 红；表空之后正则坏掉就是 0 命中
#     ⇒ **绿**（假门禁）。⇒ 正控改由脚本里的 `meta_positive_control()` 承担：**每次运行**都在
#     最小临时树上验证**四档**已知坏形态（F1/F2/F3/F4）真的能被命中，少一档直接判失败。
#     它比原来那条更强 —— 原来只护 read 类，这条两类都护、且不再依赖"表非空"。
#   ⚠ 覆盖范围（脚本 docstring 里写明它看不见什么）。阶段 78 全仓清点后**更正了一处定性**：
#     「容器类型导航」（`Content is X` / `.Child is X`）**不该硬判** —— 实测 3 处，全部是
#     「取 ContentControl 的**唯一**子元素」的类型探针（语义上不存在"第几个"）；「按运行期类型
#     读取」（`is`/`as`/`OfType<T>`）同样大量且合法。真正该守的是 `.Children` 上的**位置用法**，
#     读与写两半都在。**不判**：`Items.OfType<T>()`、"按身份找 + 按位置放回"
#     （`IndexOf` → `RemoveAt(i)`/`Insert(i, …)`）、`Children.Count/Add/Clear`、对象初始化器。
#   ⚠ **已知绕过路径**：**变量下标**的删改（`for (i = Count-1; i >= 2; i--) RemoveAt(i)`）——
#     形状判据只认字面整数 ⇒ 它会**绿**通过而行为照样坏。用**行为判据**兜：
#     `tests/HmiEditor.App.UiTests/FaceplateParamRefreshUiTests` 往静态区注入探针元素、驱动一次
#     刷新、断言探针**存活**（阶段 78 的 NC2 实测了这对：形状绿而行为红 ⇒ 两层互补）。
#   ⚠ `--selftest` 14 档：7 档该红（3 档 wire 形态 + 读取类"表外新增" + **F4 位置写入**
#     + 元判据自身的负控 + 基线内条目"表内失效"）、7 档该绿（持引用 / FindControl / 初始化器 /
#     `Items.OfType` 范围外 / 注释里的反例 / 基线内 read / **按身份 IndexOf→RemoveAt(i)**）。
if ! python3 scripts/check-index-based-control-lookup.py; then
    die "存在「按位置在 .Children 里定位控件」的写法（下标读 / RemoveAt 字面下标写）：改为构造时持有引用，或 FindControl（#57 / 阶段 78）"
fi

# ── [1h] LVGL event user_data 必须是稳定存储 ──
#
# 背景（阶段 120）：`soft_keyboard.c` 的 `kb_make_key` 把调用者栈上的 `kb_action_t` 地址
#   交给 `lv_obj_add_event_cb`，而**点击发生在函数返回之后** ⇒ user_data 悬空。
#   实测后果：每个按键的 user_data 都指向同一个被复用的栈槽 ⇒ 按哪个键都可能执行
#   "最后写入的那条动作"（未定义行为）。
#
# 为什么必须进门禁：这类缺陷**不编译错、不测试红、不崩溃**（在特定优化下才现形），
#   而"把局部量地址交出去"是随手就能写出来的。判据 = 第 4 个实参不得是
#   `&<标识符>`（除非该标识符是文件级 static）或"裸的当前函数形参"。
#   自带 `--selftest` 7 档（3 红 4 绿），并已实测能点名修复前的那一行（`soft_keyboard.c:342`）。
if ! python3 scripts/check-lvgl-userdata-lifetime.py; then
    die "存在把栈上局部量/形参当 LVGL event user_data 的写法：改为静态池等稳定存储（阶段 120）"
fi

# ── [1g] 收口工具链的自检 ──
#
# 背景（阶段 119）：**收口**这一整套纪律（跑门禁并把退出码写进日志、跨版本日志对拍、
#   从日志抽读数、量动作面残余）原先靠 `.workbuddy/tmp/` 里的一次性脚本，**不在版本库里** ——
#   于是「上一阶段那条 0 行差异」事后**不可复算**：工具已经没了。本阶段把它们收进
#   `scripts/`：`gate-run.sh` / `gate-diff.py` / `gate-readings.py` / `face-residual.py`。
#
# 为什么必须进门禁（本文件 [1f] 那句的老话）：**不加进门的判据等于没有判据**。
#   这四个工具自己带 `--selftest`，而 `--selftest` 要是没人跑，它就会随工具一起腐烂 ——
#   而它们正是「判断别的东西对不对」的量具。量具自己读错**不会报错**：阶段 118 实测，
#   同一个采集脚本把行数多算 1（850 / 真值 849）、把 10 条程序集行一条都没匹配上却照打
#   「合计 0」。⇒ 这里每次门禁都把这四个自检跑一遍（毫秒级，`gate-run --selftest` 用临时目录）。
#
# ⚠ 刻意**放进步 [1] 之内**、不新开 banner：步数是日志的一条**口径**（`===== [N]` 的个数），
#   18 份历史日志都是 `7 步`；新开一步会让「读数对拍」当场失去可比性。放进步 [1] 则
#   步数仍是 7，只有行数增加（可由 `gate-readings.py` 逐项读出）。
for tool in scripts/gate-readings.py scripts/gate-diff.py scripts/face-residual.py; do
    command -v python3 >/dev/null 2>&1 || die "本步需要 python3（收口工具自检），未找到"
    if ! python3 "$tool" --selftest; then
        die "收口工具自检失败: $tool（量具坏了 ⇒ 它给出的读数一个都不许用）"
    fi
done
if ! sh scripts/gate-run.sh --selftest; then
    die "收口工具自检失败: scripts/gate-run.sh（退出码没落进日志 ⇒ 那一跑不算证据）"
fi

# ⚠ 同一族缺陷的**另一批**（阶段 120 补）：[1d]/[1e]/[1f] 三个静态守卫**早就自带
#   `--selftest`**（3 / 8 / 14 档负控），但门禁从来没跑过它们 —— 于是它们跟上面那四个
#   收口工具同病：**自检没人跑就会腐烂**，而它们判的正是"接线写错一维就静默失效"。
#   实测三个自检都是毫秒级、不碰仓库（在临时目录里造最小树），所以直接挂进来。
#   仍放进步 [1] 之内（**步数保持 7**，历史日志仍可比）。
for tool in scripts/check-xaml-duplicate-wiring.py \
            scripts/check-dock-bus-only-subscription.py \
            scripts/check-index-based-control-lookup.py \
            scripts/check-lvgl-userdata-lifetime.py; do
    command -v python3 >/dev/null 2>&1 || die "本步需要 python3（静态守卫自检），未找到"
    if ! python3 "$tool" --selftest; then
        die "静态守卫自检失败: $tool（守卫坏了 ⇒ 它的『0 违规』一个都不许信）"
    fi
done
# 路径卫生也补了自检（阶段 121）：正控（引号内的家目录必须命中）+ 负控（注释里裸写不命中）
if ! bash scripts/check-path-hygiene.sh --selftest; then
    die "静态守卫自检失败: scripts/check-path-hygiene.sh"
fi

# ── [2] 构建（0 error / 0 warning） ──
#
# `--no-incremental` 是**判据的一部分，不是优化**（2026-09-17 实测踩到）：
#   `dotnet build` 默认增量 —— 若这一步之前已有一次构建把改动过的项目编译过
#   （本地极常见：先跑定向测试、再跑门禁），这里就是一次 no-op，
#   **既不重新编译、也就不会重新打印那条 warning** ⇒ 门禁绿着放行一个带警告的树。
#   实测：`Tag(string)` 隐藏继承来的 `Control.Tag`（CS0108）在增量构建下"0 个警告"，
#   `--no-incremental` 下立刻现形（原因见 docs/project-status.md 阶段 20 的更正）。
#   这条让"0 警告"这个到处都在引用的读数**真的成立**。
#
# 警告在这里**判红**，不只打印：这份仓库的文档到处引用"0 警告 / 0 错误"，
#   而"打印出来但没人看"等于没有判据 —— 上一版就是这么放过了一条 CS0108 的
#   （见下方注释的实测）。基线既然是 0，就该由门禁来守。
banner "dotnet build ($CONFIG, 警告即失败)"
# NuGet 的 HTTP 缓存默认在 $HOME（受限环境里可能**只读**）⇒ 漏洞审计取不到数据会打 NU1900，
# 而 restore 的警告会**写进 project.assets.json 并在之后的每次构建重放** ⇒ 门禁的"警告即失败"
# 会被与代码无关的环境噪声判红（实测 42 行/次）。这里把缓存指到工作区内（obj/ 已被 .gitignore 覆盖），
# **不关审计**、也不改任何工程的构建配置；缓存本来就可写的环境里这只是换了个目录。
export NUGET_HTTP_CACHE_PATH="${NUGET_HTTP_CACHE_PATH:-$PWD/obj/nuget-http-cache}"
dotnet restore HmiEditor.slnx --nologo
BUILD_LOG="$(mktemp)"
dotnet build HmiEditor.slnx -c "$CONFIG" --no-restore --nologo -v minimal --no-incremental \
    2>&1 | tee "$BUILD_LOG"
# 只用"行首位置的 `: warning <码>`"这个形状判，不用裸 `warning`：
#   · 末行汇总「0 个警告」不含 `: warning `，不会被误判；
#   · 中文环境下编译器诊断前缀仍是英文 `warning`/`error`（消息正文才本地化），
#     这里仍把「警告」一并写上，免得换个 MSBuild 版本/区域设置就漏判。
if grep -qE ": (warning|警告) " "$BUILD_LOG"; then
    echo "❌ 构建产生了警告（本仓库基线是 0 警告）：" >&2
    grep -E ": (warning|警告) " "$BUILD_LOG" | sort -u >&2
    rm -f "$BUILD_LOG"
    die "先把警告清掉，或改掉「基线 0 警告」这个前提"
fi
rm -f "$BUILD_LOG"
echo "✅ 构建通过（0 警告 / 0 错误）"

# ── [2b] 出包 / 预算实测工具：单独构建 + 出包最小自检 ──
#
# 为什么单独一步：tools/* 沿用既有约定**不在 HmiEditor.slnx 里**（T113KitBuilder/SimExport 同），
#   若门禁从不构建它们，「一键出包」的 CLI 接线可以腐烂而无人发现（实现逻辑有单测，但
#   Program.cs 的接线没有）。这里构建两个新工具，并让 ReleasePackager 在临时目录里
#   装配一个最小包、跑完整套自检 —— 不 publish、不碰仓库、不产出压缩包（秒级）。
#
# 为什么这里只把 **error** 判红、而不是像上面那样把 warning 也判红：
#   工具工程需要单独 restore，受限环境的 NuGet 审计/缓存会打印与代码无关的 NU1900；
#   "0 警告"的权威门禁是上面那次 slnx 构建。这里只保证工具**能构建、能跑通**。
banner "出包 / 预算实测工具（构建 + 出包自检）"
TOOL_LOG="$(mktemp)"
trap 'rm -f "$SCRIPT_LIST" "$TOOL_LOG"' EXIT
for tool in tools/ReleasePackager/ReleasePackager.csproj tools/BudgetCheck/BudgetCheck.csproj; do
    # 管道里 set -e 看不到 dotnet 的退出码（最后一段是 tee）——所以统一由下面的 error 扫描判红
    dotnet build "$tool" -c "$CONFIG" --nologo -v minimal 2>&1 | tee -a "$TOOL_LOG"
done
if grep -qE ": error " "$TOOL_LOG"; then
    grep -E ": error " "$TOOL_LOG" | sort -u >&2
    die "出包 / 预算实测工具构建失败"
fi
rm -f "$TOOL_LOG"
dotnet run --project tools/ReleasePackager/ReleasePackager.csproj -c "$CONFIG" --no-build -- --self-check
echo "✅ 出包工具链可用（版本清单取自固件头文件 + 目标插件）"

# ── [3] 全量测试（拆两段：大队 → 会 spawn git 的用例单独跑）──
#
# 为什么要拆（2026-09-17 实测，**纯 bash 可复现、与 .NET 无关**）：
#   本沙箱对「首次出现的**文件**路径」收约 60 ms（**目录创建本身几乎免费**，实测 1 ms；
#   首版量测把它记成 ~95 ms 是把 python3 进程启动算进了计时区间，已自纠），
#   而且这个开销是**全局串行**的：8 个进程各在自己的全新目录里建 20 个文件，
#   墙钟 = 8 × 单个的时间（20 核全程闲置；tmpfs 与 btrfs 表现相同；纯 `touch` 也复现，
#   见 docs/2026-09-17-sandbox-newpath-serialization.md）。
#   `git init` 在全新临时目录里要造 ~20 个新路径 ⇒ 恒定 1.2 s；当大队把队列灌满时，
#   同一个 `git` 子进程会从 1 ms 涨到 4~10 s（实测，0 失败）⇒ 撞穿
#   `GitWorkflowService.GitProcessWaitMs`(15 s) ⇒ `InitializeRepository` 返回 false
#   ⇒ `GitWorkflowServiceTests.InitializeRepository_SecondCall_Idempotent`（阻断项 11）红。
#
#   已验证**不是**解药的两条（别再花时间）：
#     · 调窗口：15 s 照样撞穿（该用例门禁内耗时 5 s ~ 6m42s，方差来自队列长度）；
#     · 调 xUnit 并行度：20 / 8 / 4 / 2 线程墙钟 84 / 83 / 90 / 88 s —— 同量级。
#       吞吐由那条串行资源决定，并行度换不到任何吞吐，也就换不到更短的队列等待。
#   唯一确定的杠杆是**让这 8 个用例在队列为空时跑**：单独跑实测 8/8 绿、7 s。
#   这条改动不放宽任何判据 —— 用例照跑、断言照旧，只是不再与大队争那条串行队列。
#
# ── 大队为什么要再排除两类：它们都**不能与大队同进程**（2026-09-21，阶段 52 追加第二类）──
#
# ① git 用例（8 个）：与大队争沙箱那条串行「新路径」资源（原因见上面的长注释）。
# ② 停靠面板用例 `DockPanelInvocationUiTests`：**宿主僵死** —— 零输出、零托管异常、零 CPU。
#    blame 数据收集器的原文是「指定的非活动时间(90 秒)已过」；VSTest 那句"测试主机进程崩溃"
#    是宿主不响应时的**兜底话术**，不是真的崩（同一个报告里就写着"非活动"）。
#    取证（同一命令去/留那一个类）：全量 **4/4 挂**（blame 报告**单独点名**该用例，只此一条）；
#    剔掉它 ⇒ **321/321 绿 / 20 s**；单独跑 ⇒ **3/3 绿**。
#    机制：`UiTestHost` 的 headless 会话是**进程级单例、绑死在第一个会话的 UI 线程上**
#    （见 UiTestHost.cs 的注释：同一进程里开不出第二套平台配置 —— 另一个渲染工程为此走
#    独立 testhost 进程）。本类是仓库里**唯一**会一次打开 12 个停靠面板、并在秒级持续占用
#    那条 UI 线程的用例。**根因（为什么同进程就会僵死）未定**，登记表 #45。
#    ⚠ **阶段 148 已退役这条排除**（复测五跑全绿、已不可复现 —— 见下方 [3] 里的注释与登记表 §70）：
#      本段**保留为历史现场**，别照它去加回排除；要加回去得先有新的僵死读数与 blame 点名。
#
# ⇒ 两类都**塞进下面同一步**「单独跑」，**不新增门禁步数** —— 那会让一堆文档里的"门禁 7 步"过期。
#    断言一律不放宽：照跑、断言照旧，只是不与大队同进程。
# ── 挂死防护：把「无限等」变成「点名失败」（2026-09-23，阶段 82 补记）──
#
# 背景（实测，两次）：本步曾经**挂死** —— 宿主 futex 阻塞、零输出、零 CPU 涨。
# 真根因是**一条判据在读 /tmp 顶层的 FIFO**（`JudgeReadingsTests` 里有完整注释），
# 但没有 per-test 超时时，门禁既不会红也不会自己结束，只能靠人发现、再手工
# `--blame-hang` 才拿得到现场（第一次差点被记成"环境噪声"）。
#
# 处置：三段测试都挂上 blame-hang —— 某个用例**静默**超过 `TEST_HANG_TIMEOUT`（默认 180s）时，
# VSTest **中止**这次运行并**点名**是哪个用例（「发生崩溃时运行的测试: …」），退出码非 0
# ⇒ 门禁红并有现场，而不是无限等。
#   · 180s 的依据：实测最慢单测 ≈15s（`HeadlessDispatchContractTests.Pump_NeverRuns…`）、
#     渲染类 5~6s ⇒ 180s ≈ 最慢的 12 倍，不会误伤正常的慢用例；而那次挂死是 18 分钟。
#   · `--blame-hang-dump-type none`：**不写 dump** —— 沙箱 /tmp 可能只有 10 MB，
#     写 dump 会把"挂死"换成另一种红（还是门禁自己造成的）。
#   · 负控（把等待压到 30s 再注入一个挂死用例）：`TEST_HANG_TIMEOUT=30s bash scripts/ci-gate.sh`。
TEST_HANG_TIMEOUT="${TEST_HANG_TIMEOUT:-180s}"
BLAME_HANG=(--blame-hang --blame-hang-timeout "$TEST_HANG_TIMEOUT" --blame-hang-dump-type none)

# ── 阶段 148：撤销对 `DockPanelInvocationUiTests` 的排除（登记 `#45`）──
# 该排除是阶段 52 因"本类与大队同进程时宿主僵死"临时加的（当时 A 档 4/4 僵死、B 档剔掉它 321/321 绿）。
# 阶段 148 复测：**已不可复现** —— 本类单跑 2/2 绿（1 s）、`App.UiTests` 全量（含本类）626/626 绿
# （4 次）、**门禁同款 solution 级**（含本类）EXIT=0 全绿。怀疑根因早被后续修复带走：
# `5cabdbb`（#46：宿主不再直订面板视图事件）+ `bf35e30`（#47：Dispatcher 归属哨兵）。
# ⇒ 撤销排除：本类回到大队里跑（少一次 `dotnet test` 调用）。⚠ 若它哪天又僵死，blame-hang 会点名它。
echo "负载 loadavg(测试前): $(loadavg)"
banner "dotnet test ($CONFIG) —— 大队"
dotnet test HmiEditor.slnx -c "$CONFIG" --no-build --no-restore --nologo \
    --logger 'console;verbosity=minimal' \
    "${BLAME_HANG[@]}" \
    --filter 'FullyQualifiedName!~GitWorkflowServiceTests'

echo "负载 loadavg(测试后): $(loadavg)"
banner "dotnet test ($CONFIG) —— 需要独占 / 空队列的用例单独跑"
dotnet test tests/HmiEditor.Core.Tests/HmiEditor.Core.Tests.csproj -c "$CONFIG" \
    --no-build --no-restore --nologo \
    --logger 'console;verbosity=minimal' \
    "${BLAME_HANG[@]}" \
    --filter 'FullyQualifiedName~GitWorkflowServiceTests'
echo "✅ 全量测试通过（两段之和 = 全量）"

# ── [4] 固件主机侧测试（无需硬件/交叉链） ──
#
# 该脚本的第 [9/9] 步是**配置守卫**（tests/check_lv_conf_flags.sh）—— 它不跑代码，
# 守的是 firmware/t113/config/lv_conf.h 的关键项：SVG 渐变开关必须为 1、
# LV_GRADIENT_MAX_STOPS 必须与 C# 侧 SvgGradientGeometry.MaxStops 同源。
# 为什么必须单独有这一步：上面 8 步虽然都带 -Iconfig，但**刻意不拉 lvgl.h**
# （见 hmi_log.h 独立成文件的原因）⇒ 没有任何一步会真去解析那份 lv_conf.h。
banner "设备树与现场生成一致 (scripts/gen-device-tree.sh --check)"
# ⚠ 为什么这一步必须在固件 host 测试**之前**：设备树的 screens.c/tags.* 是**生成物**
#   （③：不入库，构建前在开发机生成）。若它与现场生成漂移，固件步骤编的就是"来路不明的东西"，
#   过了也说明不了任何事 —— 先证明它确实是当前生成器 + 仓内示例工程的产物，再往下编。
if bash scripts/gen-device-tree.sh --check; then
    echo "✅ 设备树与现场生成逐字节一致"
else
    die "设备树与现场生成不一致 —— 跑 bash scripts/gen-device-tree.sh --write 后复核差异"
fi

banner "固件 host 测试 (firmware/t113/tests/run_host_tests.sh)"
if command -v gcc >/dev/null 2>&1; then
    bash firmware/t113/tests/run_host_tests.sh
    echo "✅ 固件 host 测试通过"
elif [ "${CI_REQUIRE_FIRMWARE_TESTS:-0}" = "1" ]; then
    die "CI_REQUIRE_FIRMWARE_TESTS=1 但未找到 gcc，固件 host 测试无法运行"
else
    echo "⚠ 未找到 gcc，跳过固件 host 测试（设 CI_REQUIRE_FIRMWARE_TESTS=1 可强制失败）"
fi

echo
echo "🎉 门禁全部通过（$step 步）"
