# 缓动路径映射：三份同体实现 → 一份；顺带清掉图片对齐的不可达分支

> 2026-09-15。承接 `docs/2026-09-14-codegenerator-partial-split.md` 与
> `docs/2026-09-15-control-object-symbol-contract.md` 里登记的同一件遗留：
> 「统一两份 `GetPathCCode`」。
> 打开一看是**三份**（文档少算了一份），而且其中一份带出来一整套手写镜像。

## 1. 归因：`lv_anim_path_*` 映射的份数

| # | 位置 | 谁在用 |
|---|---|---|
| 1 | `AnimationConfig.GetPathCCode()`（Core 模型，实例方法） | 产物全局路径 `lv_anim_set_path_cb(&a, …)`、关键帧无自带 PathType 时的兜底注释 |
| 2 | `CodeGenerator.GetPathCCode(AnimationPathType)`（Mappings 私有静态） | 关键帧**自带** PathType 时的注释 |
| 3 | `AnimationEditorView.GetPathCCode(AnimationPathType)`（App 视图私有静态） | 动画编辑器的 **C 代码预览** |

三份**逐字相同**，任何一处补值另两处不会跟着动 —— 而且三份都不会有任何编译告警
（`switch` 表达式带 `_ =>` 兜底，加个枚举成员不会报"未覆盖"）。

## 2. 第二层（查第一份时撞出来的）：预览是一整套手写镜像

App 侧不只是复制了一个映射函数，`GenerateCCode` 把**整个发射逻辑**手抄了一遍（7 行 `lv_anim_set_*`），
而且抄的时候就和产物对不上：

| | 产物（`CodeGenerator.GenerateAnimationCode`） | 预览（`AnimationEditorView`，修前） |
|---|---|---|
| 控件变量 | `obj_{净化id}`，`CIdentifier.ForControlSymbol` | `ui_{id.Replace("-","_")}` —— **不同的命名规则** |
| 函数形状 | `static void start_anim_{id}(lv_obj_t* target)` + `apply_animations()` 入口 | 裸 `{ … }` 块，无包装函数、无入口 |
| 头部注释 | `/* Animation: … (trigger: …) */` | `/* Trigger: … \| Property: … \| Path: 中文名 */` |
| 关键帧注释 | `/*   50% -> 128.0  (lv_anim_path_bounce) */` | `/* 50% = 128.0 (lv_anim_path_bounce) */` |
| 动画禁用时 | 不发射（`// No animations configured.`） | 照常显示代码 |

**用户可见后果**：预览里抄出来的 `ui_pump` 在真实工程里**不存在**（真名是 `obj_pump`）。

## 3. 第三层：图片缩放模式分支里的不可达字符串比较

`CodeGenerator.ControlParameters.EmitImageParameters`（修前）：

```csharp
var imgAlign = string.IsNullOrEmpty(control.StretchMode)
    ? "LV_IMAGE_ALIGN_STRETCH"
    : LvglImageAlign(control.StretchMode);
if (imgAlign is "LV_IMAGE_ALIGN_CONTAIN" or "LV_IMAGE_ALIGN_COVER")   // ← COVER 那半永不可达
```

判据是**自己刚生成出来的字符串**。而 `LvglImageAlign` 只会返回
STRETCH / CONTAIN / TILE / CENTER（`StretchModeMap.Parse` 没有 Cover 语义，UI 的 `StretchCombo`
也只暴露 None / Fill / Contain），所以 `"LV_IMAGE_ALIGN_COVER"` 那一半**永远不成立**。
上一轮已用运行时断言把它钉成"已知事实"，本轮把分支改为按枚举判并删掉不可达半支。

## 4. 做法

| 层 | 改动 |
|---|---|
| 唯一实现 | `AnimationPathHelper.GetPathCCode(AnimationPathType)`（`AnimationPathType.cs`，和 `GetDisplayName` 同处） |
| Core 模型 | `AnimationConfig.GetPathCCode()` → 薄委托 |
| CodeGen | 删私有副本；关键帧调用点改 `AnimationPathHelper.GetPathCCode(kf.PathType.Value)` |
| App 预览 | `GenerateCCode` → `new CodeGenerator().GenerateAnimationCode(new[] { anim })`；删掉 `GetControlVarName` 与私有映射 —— **预览即产物** |
| 图片对齐 | `LvglImageAlign(string?)` 降为薄适配器，新增 `LvglImageAlign(StretchModeKind)`；`EmitImageParameters` 先 `var kind = StretchModeMap.Parse(…)`，分支判 `kind == StretchModeKind.Contain` |

> 顺带删掉了 `string.IsNullOrEmpty(control.StretchMode) ? … : …` 的三元短路 ——
> `Parse(null/空)` 本来就落 Stretch，短路是多余的（由全输入包络用例钉住等价性）。

## 5. 证据

### 5.1 守卫

| 守卫 | 数量 | 断什么 |
|---|---|---|
| `AnimationPathSingleSourceTests`（新） | 31 | 期望表覆盖**每个枚举成员**；`AnimationConfig` 与唯一实现同值；两个调用点（`lv_anim_set_path_cb` / 关键帧注释）各自命中；无自带 PathType 时回落动画全局路径；未定义枚举值兜底合法；**产品侧名字互不相同** |
| `ControlEmitterCoverageTests` | 5 → 12 | 图片缩放的**全输入包络**：null/空/Fill/Stretch/None/Center/Tile/Cover/cover/未知/Contain/contain → 精确对齐宏 + 有无版本兜底 |

### 5.2 产物 A/B（同提交口径，`tools/ReproHarness`）

**先把语料补齐再说结论** —— 原语料图片只喂 `Fill / null / None`，**Contain 一次都没出现**，
而 Contain 正是本次唯一改到的条件分支。不补的话「0 差异」是假的。补法：

| 位置 | 修前 | 修后 |
|---|---|---|
| `MakeControl` 的 Image variant | `0=Fill, 1=null, 2=None` | `0=Fill, 1=Contain, 2=None` |
| 追加 3 个图片控件 | —— | `img_sm_0/1/2` = null / Tile / Center（主循环每类型只有 3 个 variant，装不下 6 条输入） |

命中证明（跑在**当前树**上，先证明"比到了"）：

```
24 个 norm 文件；5 个文件含 `#if LV_VERSION_CHECK(9, 3, 0)`；
fallback 行 `/* fallback: LVGL < 9.3 无 LV_IMAGE_ALIGN_CONTAIN */` 出现；
obj_img_0→STRETCH  obj_img_1→CONTAIN(+兜底)  obj_img_2→CENTER
obj_img_sm_0→STRETCH  obj_img_sm_1→TILE  obj_img_sm_2→CENTER
lv_anim_path_* 七种全出（linear/ease_in_out/ease_in/ease_out/bounce/overshoot/step）
```

| 口径 | 结果 |
|---|---|
| 归一化产物 `diff -r base/norm cur/norm` | **0/24 差异**（两侧各 24 文件） |
| `hashes.txt` | 全同 |

⇒ 纯收敛：对产物**逐字节零影响**。（App 预览不参与产物，A/B 看不到它，见 §6。）

### 5.3 负向验证（4 组，红集互不相同）

| # | 破坏（等价替换） | 实测红 | 红掉的断言 |
|---|---|---|---|
| NV-1 | 唯一实现里 `Step => "lv_anim_path_step"` 改成 `linear` | **5** | 四条调用点/契约用例（`ModelApi_And_Helper_Agree` / `EmitterPathCallback` / `KeyframeComment` / `KeyframeWithoutOwnPath`）+ `Names_AreDistinct` |
| NV-2 | `AnimationConfig.GetPathCCode` 不再委托、自带一份漂移副本 | **3** | `…Agree` + `EmitterPathCallback` + `KeyframeWithoutOwnPath`（**不含** `KeyframeComment` —— 该调用点走唯一实现）|
| NV-3 | 图片分支判据写错（`Contain` 判成 `Tile`） | **4** | `Image_Contain_…` + 全包络 3 例（Contain/contain 丢兜底、Tile 多兜底）|
| NV-4 | `StretchModeMap.Parse` 丢掉 `contain` 映射 | **3** | `Image_Contain_…` + 全包络 2 例 |

NV-1 与 NV-2 的红集**刻意不同**：这证明"关键帧自带 PathType"与"关键帧回落全局路径"是两个独立的调用点，
也证明模型 API 的委托关系被单独钉住。

> **NV-1 第一次只红了 4 条，暴露了守卫自己的漏洞**：`Names_AreDistinct` 当时只断言**测试侧期望表**
> 内部不重复 —— 对产品完全无感，产品漂移只能靠 `…Agree` 的"期望不符"间接打到。
> 补上产品侧断言（`Enum.GetValues<AnimationPathType>().Select(AnimationPathHelper.GetPathCCode)` 互不相同）
> 后才变成 5 红。**守卫也要被负向验证，否则它就是摆设。**

### 5.4 全量

**2136 / 2136 全绿**（Core **1604** / AI 237 / App.UiTests 91 / Comm 75 / Device 63 / Simulator 44 / Hardware 22）。
基线 2098 + 新增 38（新守卫 31 + 图片全包络替换净增 7）= 2136。Release 全量构建 0 警告 0 错误。

## 6. 有意保留的行为变化（用户可见）

预览文本**变了**，这是修缺陷的必然结果：

| 场景 | 修前预览 | 修后预览 |
|---|---|---|
| 普通动画 | 自造的裸 `{ lv_anim_set_var(&a, ui_pump); … }` | 真实产物片段（`start_anim_*` + `obj_pump` + `apply_animations()`）|
| 动画 `IsEnabled = false` | 照常显示代码 | `// No animations configured.` |

第二条是**有意**的：那正是该动画对产物的真实贡献（用户一眼看出"这个动画不会生成"）。
若更想要"无论启用与否都显示代码"，需要在发射器上开一个"忽略 IsEnabled"的参数 —— 那是新特性，
不属本次收敛。

## 7. 教训

1. **「第几份」要自己数，不要信文档。** 遗留清单写"两份 `GetPathCCode`"，实际三份，
   而第三份（App 预览）还带出一整套七行手写镜像。**同一个事实的多份实现，改动面永远比登记的大。**
2. **非产物路径最容易长出实现。** 预览/文档/示例这类"不参与构建"的地方没人拿产物校验它，
   于是可以长期和产物不一致（`ui_xxx` vs `obj_xxx`）。
   对策不是"再核对一遍"，而是**让它直接调真发射器**，从此没有第二处可漂移。
3. **字符串相等做分支 = 死代码温床。** 判据是"自己刚生成的字符串"时，一旦映射表变了，
   分支会静默失效而不是编译报错。**先解析成枚举，再按枚举分支** —— 加成员时编译器会问你。
4. **`diff = 0` 之前先证明"比到了"。** 本轮语料缺 Contain 这一条输入，
   而它恰好是唯一改到的分支 —— 不补语料，本次"零影响"的结论完全无法成立。
   （这是这条教训的**第三次现场**，见 `2026-09-14-*`、`2026-09-15-faceplate-expansion-single-source`。）
5. **`git stash` 遇到 safe-delete shim 会半途留下新文件。** `git stash push -u -- src tests` 报告
   `unable to unlink … 设备或资源忙`，**已跟踪文件的改动照常暂存**，未跟踪新文件留在原地。
   对"跑基线产物"无影响（tests 不参与构建），但 `git stash pop` 会因此保留 stash 条目
   → 核对 diffstat 一致后再 `git stash drop`，别直接删。
