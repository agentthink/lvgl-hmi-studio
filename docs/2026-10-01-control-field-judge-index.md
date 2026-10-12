# 逐字段索引：58 个面板字段 → 消费点 → 判据文件（阶段 211）
## —— 把「这个字段有没有人看着」从**发射器粒度**细化到**字段粒度**

> 承阶段 203/204（`docs/2026-10-01-control-field-consumption.md` /
> `docs/2026-10-01-emitter-judge-pointers.md`）：那两阶段先证明"面板写回的 58 个字段**每个**都被
> 某个发射器读了"，再证明"那些发射器**每个**都有具名判据"。这一张表把两者**接起来** ——
> 直接回答"**`TxtValueSuffix` 这种字段，到底哪条判据看着它**"。

## 口径

- **消费点** = 该字段在 `HmiEditor.CodeGen` 里的**第一个真实读取点**所在的方法（与阶段 204 同一把尺子）。
- **判据文件** = 阶段 204 的发射器指针表里那个方法的 `JudgeFile`（位于 `tests/HmiEditor.Core.Tests/`）。
- 两个字段走**间接**路：`Rotation` / `RotationDirection` —— 生成器写的是 `control.EffectiveRotation()`，
  字段名不出现在生成代码里（所以消费点列写 `(间接) EffectiveRotation()`）。
- **判据**（`ControlEmitterJudgePointersTests.ControlFieldJudgeIndex_MatchesTheDoc`）三道：
  ① 每个面板字段都**解析得出判据**（不许有 `—` / `(未登记)`）；
  ② 这张表与代码**双向闭合**（新加一个面板字段、或把某字段的消费点挪走 ⇒ 红，逼着同步本文档）；
  ③ 指名的判据文件必须**真实存在**。

| 字段 | 消费点 | 判据文件 |
|---|---|---|
| `BgColor` | `EmitSliderParameters` | `ControlEmitterCoverageTests.cs` |
| `BorderColor` | `EmitTableParameters` | `ControlEmitterCoverageTests.cs` |
| `BorderWidth` | `EmitGaugeParameters` | `ControlEmitterCoverageTests.cs` |
| `Brightness` | `EmitLedParameters` | `ControlEmitterCoverageTests.cs` |
| `ColumnCount` | `EmitTableParameters` | `ControlEmitterCoverageTests.cs` |
| `ColumnWidths` | `TableColumnWidthExpr` | `ControlEmitterCoverageTests.cs` |
| `FontFamily` | `BuiltInFontSizeFor` | `TextEmissionFaceTests.cs` |
| `FontSize` | `BuiltInFontSizeFor` | `TextEmissionFaceTests.cs` |
| `HeaderTexts` | `EmitTableParameters` | `ControlEmitterCoverageTests.cs` |
| `Height` | `AppendTransform` | `TransformFeatureTests.cs` |
| `ImageAssetId` | `GenerateVisualStates` | `VisualStateCodeGenConsumptionTests.cs` |
| `IndicatorColor` | `GenerateControlParameters` | `ControlEmitterCoverageTests.cs` |
| `IsChecked` | `GenerateControlParameters` | `ControlEmitterCoverageTests.cs` |
| `KeyboardMode` | `EmitTextAreaParameters` | `ControlEmitterCoverageTests.cs` |
| `LabelLongMode` | `EmitLabelParameters` | `ControlEmitterCoverageTests.cs` |
| `LineCap` | `EmitLineParameters` | `ControlEmitterCoverageTests.cs` |
| `MajorTickCount` | `EmitGaugeParameters` | `ControlEmitterCoverageTests.cs` |
| `MaxLength` | `EmitTextAreaParameters` | `ControlEmitterCoverageTests.cs` |
| `MaxValue` | `EmitSliderParameters` | `ControlEmitterCoverageTests.cs` |
| `MinValue` | `EmitSliderParameters` | `ControlEmitterCoverageTests.cs` |
| `MirrorX` | `AppendTransform` | `TransformFeatureTests.cs` |
| `MirrorY` | `AppendTransform` | `TransformFeatureTests.cs` |
| `Name` | `GenerateActionCallbacks` | `ActionCallbacksCodeGenTests.cs` |
| `Opacity` | `GenerateControlParameters` | `ControlEmitterCoverageTests.cs` |
| `Options` | `EmitDropdownParameters` | `ControlEmitterCoverageTests.cs` |
| `PasswordMode` | `EmitTextAreaParameters` | `ControlEmitterCoverageTests.cs` |
| `PipeDirection` | `GeneratePipeCode` | `ProcessControlCodeGenTests.cs` |
| `PipeFlowDirection` | `GeneratePipeCode` | `ProcessControlCodeGenTests.cs` |
| `PipeFlowSpeed` | `GeneratePipeCode` | `ProcessControlCodeGenTests.cs` |
| `PlaceholderText` | `EmitTextAreaParameters` | `ControlEmitterCoverageTests.cs` |
| `Radius` | `EmitSliderParameters` | `ControlEmitterCoverageTests.cs` |
| `Rotation` | `(间接) EffectiveRotation()` | `TransformFeatureTests.cs` |
| `RotationDirection` | `(间接) EffectiveRotation()` | `TransformFeatureTests.cs` |
| `RowCount` | `EmitTableParameters` | `ControlEmitterCoverageTests.cs` |
| `RowHeight` | `EmitTableParameters` | `ControlEmitterCoverageTests.cs` |
| `SelectedIndex` | `EmitDropdownParameters` | `ControlEmitterCoverageTests.cs` |
| `ShowPointer` | `EmitSliderParameters` | `ControlEmitterCoverageTests.cs` |
| `ShowTicks` | `AppendSliderTicks` | `SliderBarFeatureCodeGenTests.cs` |
| `SpinSpeed` | `EmitSpinnerParameters` | `ControlEmitterCoverageTests.cs` |
| `StepValue` | `SliderTickCount` | `SliderBarFeatureCodeGenTests.cs` |
| `StretchMode` | `EmitImageParameters` | `ControlEmitterCoverageTests.cs` |
| `TabTitles` | `EmitTabViewParameters` | `ControlEmitterCoverageTests.cs` |
| `TankShowScale` | `GenerateTankCode` | `ProcessControlCodeGenTests.cs` |
| `TankTickCount` | `GenerateTankCode` | `ProcessControlCodeGenTests.cs` |
| `Text` | `EmitGaugeParameters` | `ControlEmitterCoverageTests.cs` |
| `TextAlign` | `EmitDropdownParameters` | `ControlEmitterCoverageTests.cs` |
| `TextColor` | `GenerateControlParameters` | `ControlEmitterCoverageTests.cs` |
| `TextVAlign` | `GenerateScreenCode` | `ControlEmitterCoverageTests.cs` |
| `ValueDecimalPlaces` | `GenerateScreenCode` | `ControlEmitterCoverageTests.cs` |
| `ValueDisplayType` | `GenerateScreenCode` | `ControlEmitterCoverageTests.cs` |
| `ValueMode` | `EmitSliderParameters` | `ControlEmitterCoverageTests.cs` |
| `ValuePrefix` | `GenerateScreenCode` | `ControlEmitterCoverageTests.cs` |
| `ValueScaleFactor` | `GenerateScreenCode` | `ControlEmitterCoverageTests.cs` |
| `ValueSuffix` | `GenerateScreenCode` | `ControlEmitterCoverageTests.cs` |
| `ValveType` | `GenerateValveCode` | `ProcessControlCodeGenTests.cs` |
| `Width` | `AppendTransform` | `TransformFeatureTests.cs` |
| `X` | `SafeClampPosition` | `ScreenSafeAreaExportTests.cs` |
| `Y` | `SafeClampPosition` | `ScreenSafeAreaExportTests.cs` |

## 这张表怎么读

- 想改**某个字段**的行为：先查它落在哪个发射器，再照着右列的判据文件改/加断言。
- 想加**新字段**：面板里 `ValidateAndWrite(nameof(HmiControl.X))` 一加，这张表就会红（②），
  逼着你确认"它有没有被判据看着"，然后把它同步进来。
- 想动**发射器**：`docs/2026-10-01-emitter-judge-pointers.md` 那张指针表管着"方法 → 判据"；
  本表管着"字段 → 方法 → 判据"。两张一起改。

## 仍未做

- 本表只到"**哪条判据**"这一层，不保证那条判据**覆盖了该字段的每一种取值**（那是逐字段的**取值**账，
  目前只有阶段 203 挑出来的十几个重点字段做过）。
- `Consumer` 列是**第一个**读取点 —— 有的字段被不止一处读（阶段 204 的 `SharedConsumers` 说明了这一点），
  表里不重复列。
