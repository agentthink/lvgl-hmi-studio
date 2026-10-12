# 设计器控件硬编码与接口暴露排查报告

> 排查范围：HMI 设计器全部控件的渲染、属性编辑与代码生成实现
> 排查时间：2026-08-14
> 核心文件：`DesignCanvas.cs`（3102 行）、`PropertyPanelView.axaml/.cs`（956+2717 行）、`CodeGenerator.cs`（2581 行）、`AppDialogs.cs`、`HmiControl.cs`、`HmiTheme.cs`

---

## 一、总览

| 维度 | 结论 |
|---|---|
| 硬编码问题总数 | 约 **120+ 处**（颜色约 55 处、尺寸/系数约 40 处、固定文案约 10 处、魔法数字约 15 处） |
| 最严重系统性问题 | ① 设计器渲染兜底色（RGB）与生成器兜底色（hex）与主题模型（HmiTheme）**三套颜色语义互相脱节**；② Gauge/Knob 的 135°/270° 弧角在设计器与生成器**重复硬编码**；③ FontSize 默认值三处不一致（模型 14 / 设计器 12 / 生成器独立） |
| 接口暴露 | 数据获取、事件回调、预览值委托已较好暴露；**渲染器无插件扩展点、主题无法注入设计器渲染层、属性无即时校验接口、选中集无只读访问器**为主要缺口 |

严重程度定义：**高**=跨层一致性隐患/重复值易漂移/影响显示正确性；**中**=可抽常量、当前不影响功能但影响可维护性；**低**=单控件局部系数、可读性优化。

---

## 二、硬编码问题清单（按控件分组）

### A. 设计器渲染层 —— `DesignCanvas.cs`

#### A1. 通用分发器 `RenderControl`（L361–544）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议抽取方式 |
|---|---|---|---|---|
| 425 | `Color.FromRgb(30, 41, 59)` Dropdown/TextArea 无 BgColor 兜底 | 颜色 | 高 | 抽 `DesignPalette.SurfaceFallback` 常量，并与生成器 `#1e293b` 对齐 |
| 438 | `Color.FromRgb(45, 55, 75)` ParseColor 解析失败兜底 | 颜色 | 中 | 抽 `DesignPalette.BgFallback` |
| 448 | `Color.FromRgb(100, 120, 160)` 边框兜底 | 颜色 | 中 | 抽 `DesignPalette.BorderFallback` |
| 469 | `Color.FromRgb(56, 189, 248)` Slider/Bar 指示器兜底 | 颜色 | 高 | 与主题 PrimaryColor 统一，抽常量 |
| 493 | 同上（Slider knob 兜底，重复） | 颜色 | 高 | 同 469 |
| 495 | `Color.FromRgb(15, 23, 42)` knob 外圈描边 | 颜色 | 中 | 抽常量 |
| 541 | `control.FontSize > 0 ? control.FontSize : 12` 默认字号 | 尺寸 | 高 | **与模型 FontSize 默认 14 不一致**，应统一为模型默认值 |
| 503 | `control.Type.ToString()` 兜底文本 | 文案 | 低 | 抽默认文案表 |

#### A2. Button `RenderButton`（L579–641）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 610/628 | `Color.FromRgb(100, 120, 160)` 边框兜底（重复 2 次） | 颜色 | 中 | 与 448 统一常量 |
| 622 | `Color.FromRgb(45, 55, 75)` bg 兜底 | 颜色 | 中 | 与 438 统一 |
| 637 | `Colors.White` 文本兜底 | 颜色 | 中 | 抽 `DesignPalette.TextFallback` |
| 638 | `... : 12` 默认字号（重复） | 尺寸 | 高 | 统一为模型默认 14 |

#### A3. Switch `RenderSwitch`（L647–666）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 649 | `Color.FromRgb(45, 55, 75)` 轨道兜底 | 颜色 | 中 | 统一常量 |
| 650 | `Color.FromRgb(37, 99, 235)` 激活色兜底 | 颜色 | 高 | 与主题 PrimaryColor `#2563eb` **数值不一致**（37,99,235 ≠ 37,99,235？需核对，两处蓝色系并存） |
| 657 | `0.9` 滑钮直径系数、`0.05` 边距系数 | 魔法数字 | 中 | 抽 `SwitchLayout.KnobRatio` |
| 662 | `Color.FromRgb(200, 210, 225)` 未选中滑钮色 | 颜色 | 中 | 抽常量 |
| 664 | `Color.FromRgb(15, 23, 42)` 描边、`1 * z` 线宽 | 颜色/尺寸 | 中 | 抽常量 |

#### A4. CheckBox `RenderCheckBox`（L672–715）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 674 | `0.8`/`0.35` 勾选框尺寸系数 | 魔法数字 | 中 | 抽常量 |
| 676 | `2 * z` 框圆角 | 尺寸 | 低 | 抽常量 |
| 679 | `Color.FromRgb(37, 99, 235)` 勾选色兜底 | 颜色 | 高 | 统一主题 PrimaryColor |
| 682 | `Color.FromRgb(30, 40, 60)` 未选中底色兜底 | 颜色 | 中 | 抽常量 |
| 686 | `Color.FromRgb(100, 120, 160)` 边框兜底 | 颜色 | 中 | 统一常量 |
| 694–701 | `1.5`/`0.12`/`0.22`/`0.44`/`0.52`/`0.72`/`0.82`/`0.28` 对勾坐标比例 | 魔法数字 | 低 | 抽 `CheckMarkGeometry` 常量类 |
| 705 | `"CheckBox"` 兜底文案 | 文案 | 低 | 抽默认文案 |
| 710 | `6 * z` 文本与框间距 | 尺寸 | 中 | 抽常量 |

#### A5. Image `RenderImage`（L720–746）与 `TryDrawImage`（L757–788）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 730 | `Color.FromRgb(30, 41, 59)` 兜底 | 颜色 | 中 | 统一常量 |
| 736 | `Color.FromRgb(100, 120, 160)` 边框 | 颜色 | 中 | 统一常量 |
| 743 | `"图片"` 兜底文案 | 文案 | 中 | 走本地化 |
| 745 | `Color.FromRgb(148, 163, 184)` 占位文本色 | 颜色 | 中 | 抽常量 |
| 766–777 | `"contain"/"center"/"none"/"tile"` 字符串字面量 switch | 内嵌逻辑 | 高 | **与生成器 `LvglImageAlign` 的字符串映射重复**，应抽 `StretchMode` 枚举/共享解析器 |

#### A6. 文本绘制 `DrawAlignedText`（L875–902）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 885 | `const double pad = 4` 4px 内边距 | 尺寸 | 中 | 抽为共享 `TextLayout.Padding`，与生成器文本对齐语义同步 |

#### A7. Gauge `RenderGauge`（L933–988）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 935 | `Color.FromRgb(45, 55, 75)` 背景弧兜底、`12 * z` 弧宽 | 颜色/尺寸 | 中 | 抽常量 |
| 936 | `135, 270` 起始角/扫角 | 魔法数字 | 高 | **与生成器 `rotation(135)+bg_angles(0,270)` 重复**，抽共享 `ArcLayout` 常量 |
| 939 | `162` 默认活动弧度 | 魔法数字 | 中 | 抽常量 |
| 960 | `Color.FromRgb(37, 99, 235)` 活动弧兜底 | 颜色 | 高 | 统一主题色 |
| 966 | `Color.FromRgb(148, 163, 184)` 刻度兜底 | 颜色 | 中 | 抽常量 |
| 967/968 | `2 * z` 刻度线宽、`8 * z` 刻度长 | 尺寸 | 中 | 抽常量 |
| 986 | `14 * z` 标签字号 | 尺寸 | 中 | 抽常量 |

#### A8. Knob `RenderKnob`（L990–1023）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 993 | `0.8` 半径系数 | 魔法数字 | 低 | 抽常量 |
| 996 | `Color.FromRgb(45, 55, 75)`、`6 * z` | 颜色/尺寸 | 中 | 抽常量 |
| 1000 | `-45.0` 默认指针角 | 魔法数字 | 中 | 抽常量 |
| 1006/1013 | `-135.0`/`270.0` 弧角 | 魔法数字 | 高 | 与 Gauge 统一 `ArcLayout` |
| 1018 | `0.7` 指针长度系数 | 魔法数字 | 低 | 抽常量 |
| 1020 | `Color.FromRgb(37, 99, 235)` 指针兜底 | 颜色 | 高 | 统一主题色 |
| 1021 | `3 * z` 指针线宽 | 尺寸 | 低 | 抽常量 |

#### A9. Led `RenderLed`（L1025–1057）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 1028 | `0.7` 半径系数 | 魔法数字 | 低 | 抽常量 |
| 1037 | `"ON"` 判定字符串 | 文案 | 中 | 抽常量（与生成器 Bool 显示语义统一） |
| 1042 | `Color.FromRgb(34, 197, 94)` 绿色兜底 | 颜色 | 中 | 与主题 SuccessColor `#22c55e` 不一致，统一 |
| 1043 | `Color.FromRgb(80, 90, 110)` 灭态色 | 颜色 | 中 | 抽常量 |
| 1045 | `255.0` 亮度范围、`0.35`/`0.65` 系数 | 魔法数字 | 低 | 抽常量 |
| 1052 | `0.3` 光晕透明度、`1.5` 光晕半径系数 | 魔法数字 | 低 | 抽常量 |

#### A10. Spinner `RenderSpinner`（L1059–1071）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 1062 | `0.6` 半径系数 | 魔法数字 | 低 | 抽常量 |
| 1064 | `Color.FromRgb(56, 189, 248)` 兜底 | 颜色 | 中 | 统一常量 |
| 1068 | `0, 270` 弧角 | 魔法数字 | 中 | 抽常量 |
| 1070 | `"⏳"` 固定文案、`16 * z` 字号 | 文案/尺寸 | 低 | 抽常量 |

#### A11. Line `RenderLineShape`（L1073–1088）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 1075 | `Colors.White` 兜底 | 颜色 | 中 | 与生成器 `#ffffff` 统一 |
| 1076–1081 | `"Round"/"Square"` 字符串字面量 | 内嵌逻辑 | 中 | 抽枚举 |
| 1083 | `2` 默认线宽 | 尺寸 | 中 | 与生成器 L2124 默认 2 统一 |

#### A12. Keyboard `RenderKeyboardPlaceholder`（L1090–1105）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 1092 | `Color.FromRgb(30, 40, 60)` 兜底 | 颜色 | 中 | 抽常量 |
| 1095 | `Color.FromRgb(55, 65, 85)` 键帽色 | 颜色 | 中 | 抽常量 |
| 1096–1103 | `4`×`5` 行列数、`2` 间距、`2` 键圆角 | 魔法数字 | 中 | 抽 `KeyboardLayout` 常量 |

#### A13. TabView `RenderTabViewPlaceholder`（L1107–1134）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 1109 | `28 * z` 页签高度 | 尺寸 | 中 | 抽常量 |
| 1110 | `Color.FromRgb(25, 35, 50)` 背景 | 颜色 | 中 | 抽常量 |
| 1116 | `"Tab1"` 兜底文案 | 文案 | 中 | 走本地化 |
| 1118 | `60 * z` 最小页签宽、`8` 最大显示数 | 魔法数字 | 中 | 抽常量 |
| 1124/1125 | `Color.FromRgb(37,99,235)` 激活 / `(30,40,60)` 未激活 | 颜色 | 中 | 抽常量 |
| 1131 | `Color.FromRgb(20, 28, 42)` 内容区 | 颜色 | 中 | 抽常量 |

#### A14. Table `RenderTablePlaceholder`（L1136–1266）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 1140 | `Color.FromRgb(30, 41, 59)` 兜底 | 颜色 | 中 | 与生成器 `#1e293b` 统一 |
| 1174/1217/1230 | `11 * z` 字号（重复 3 次） | 尺寸 | 中 | 抽常量 |
| 1192 | `Color.FromRgb(51, 65, 85)` 网格线兜底 | 颜色 | 高 | 与生成器 `#334155`（=51,65,85）一致，但应抽常量避免漂移 |
| 1193 | `0.5 * z` 网格线宽 | 尺寸 | 中 | 抽常量 |
| 1242 | `Math.Clamp(cell.DecimalPlaces, 0, 6)` 最大小数位 | 魔法数字 | 高 | 与生成器 L662 重复，抽 `BindingFormat.MaxDecimals` |
| 1243 | `$"{{{cell.VariableId}}}"` 占位符格式 | 文案 | 中 | 抽常量 |
| 1266 | `max + 16` 内边距、`fontSize * 3` 最小列宽 | 魔法数字 | 中 | 抽常量 |

#### A15. Svg `RenderSvg`（L1275–1308）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 1280 | `Color.FromRgb(80, 100, 140)` 占位描边 | 颜色 | 低 | 抽常量 |
| 1285–1286 | `100` 默认 viewBox 宽高 | 魔法数字 | 中 | 与模型 `SvgViewBoxWidth=100` 对齐抽常量 |
| 1295/1296 | `"none"` 字符串字面量 | 文案 | 低 | 抽常量 |

#### A16. Tank `RenderTank`（L1316–1382）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 1318 | `4.0` 内边距 | 尺寸 | 中 | 抽常量 |
| 1327 | `Color.FromRgb(100, 120, 160)` 罐壁兜底、`1.5` 最小线宽 | 颜色/尺寸 | 中 | 抽常量 |
| 1334 | `0.5` 默认液位 | 魔法数字 | 中 | 抽常量 |
| 1340 | `Color.FromRgb(14, 165, 233)` 液体兜底、`0.75` 透明度 | 颜色 | 中 | 抽常量 |
| 1346 | `Color.FromRgb(56, 189, 248)` 液面高光 | 颜色 | 中 | 抽常量 |
| 1355 | `5 * z` 刻度长 | 尺寸 | 低 | 抽常量 |
| 1363/1368 | `"100%"`/`"0%"` 固定文案、`9 * z` 字号 | 文案/尺寸 | 中 | 走本地化 |
| 1380 | `10 * z` 位号字号 | 尺寸 | 低 | 抽常量 |

#### A17. Pipe `RenderPipe`（L1388–1440）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 1390 | `Color.FromRgb(100, 116, 139)` 管道兜底、`3`/`4` 线宽 | 颜色/尺寸 | 中 | 抽常量 |
| 1393 | `Color.FromRgb(56, 189, 248)` 箭头兜底 | 颜色 | 中 | 抽常量 |
| 1401–1413 | `6`/`8`/`4` 箭头尺寸 | 魔法数字 | 低 | 抽常量 |
| 1419 | `Color.FromRgb(14, 165, 233)` 流动兜底、`0.9` 透明度/线宽 | 颜色/魔法数字 | 中 | 抽常量 |
| 1421 | `8` 段长、`6` 间距 | 魔法数字 | 低 | 抽常量 |
| 1438 | `9 * z` 字号 | 尺寸 | 低 | 抽常量 |

#### A18. Valve `RenderValve` + 四种阀型（L1446–1525）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 1449 | `2`/`3` 线宽 | 尺寸 | 低 | 抽常量 |
| 1451 | `Color.FromRgb(239, 68, 68)` 关闭红兜底 | 颜色 | 中 | 与主题 ErrorColor `#ef4444`（=239,68,68）一致，抽常量 |
| 1452 | `Color.FromRgb(34, 197, 94)` 打开绿兜底 | 颜色 | 中 | 与主题 SuccessColor `#22c55e` 一致，抽常量 |
| 1472/1475/1485/1496/1507–1519 | `0.55`/`0.7`/`0.5`/`0.25`/`0.8`/`0.6`/`1.6`/`0.9` 各阀型几何系数 | 魔法数字 | 低 | 抽 `ValveGeometry` 常量类 |

#### A19. 画布主入口 `Render`（L206–306）

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 236 | `Color.FromRgb(25, 30, 45)` 屏幕背景兜底 | 颜色 | 中 | 与主题 BackgroundColor `#131c31` 对齐 |
| 244 | `Color.FromRgb(60, 70, 90)` 屏幕边界线、`1` 线宽 | 颜色/尺寸 | 中 | 抽常量 |
| 278–279 | `Color.FromRgb(56, 189, 248)` 框选色、`0.15` 透明度 | 颜色 | 中 | 抽常量 |
| 296/298/299 | `13` 横幅字号、`24`/`26` 宽高增量 | 尺寸 | 中 | 抽常量 |
| 303/304 | `Color.FromRgb(34, 197, 94)` 横幅底色、`0.9` 透明度、`12` x 偏移 | 颜色/尺寸 | 中 | 抽常量 |
| 149 | `PasteOffsetStep = 20.0` 粘贴偏移 | 魔法数字 | 低 | 已抽常量，值可入配置 |

---

### B. 属性面板 —— `PropertyPanelView.axaml.cs`

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 1775–1786 | 动作类型中文映射（"取反/置位/复位/按1松0/按0松1/设置变量/切换屏幕/执行脚本/显示报警/显示趋势/登录/登出"） | 文案 | 高 | **未走本地化**，且与 CodeGenerator 的动作处理逻辑分散，应抽 `ActionTypeMeta`（枚举+显示名+本地化 key） |
| 1792 | `"?"` 未设置目标兜底 | 文案 | 低 | 走本地化 |
| 1807 | `$"{i+1}.{typeName}:{chipTarget}"` 显示格式 | 文案 | 低 | 抽模板 |
| 2152 | `Color.FromRgb(42, 45, 56)` 参数目录标签背景 | 颜色 | 中 | 用主题资源 `Theme.*` |
| 2159 | `Color.FromRgb(155, 161, 174)` 目录标签文字 | 颜色 | 中 | 用主题 `TextMuted` |
| 2160 | `"Consolas, monospace"` 字体 | 字体 | 中 | 抽主题字体常量 |
| 2153–2155 | `CornerRadius(2)`、`Thickness(4,1)`、`FontSize 9` | 尺寸 | 中 | 用主题资源 |
| 2217–2218 | `double.MinValue/MaxValue` 默认 min/max | 魔法数字 | 中 | 抽 `ParameterRange.Default` |
| 2219–2220 | `SmallChange=1`、`LargeChange=10` | 魔法数字 | 中 | 抽常量 |

> 注：`PropertyPanelView.axaml`（XAML）**未发现硬编码颜色/文案**，已全部使用 `{StaticResource HmiXxxBrush}` 与本地化 `{Binding [key]}`，是做得规范的部分。

---

### C. 代码生成器 —— `CodeGenerator.cs`

| 行号 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| 607 | `% 360` 绑定模拟相位 | 魔法数字 | 低 | 抽常量 |
| 612 | `(max - min) * 0.45` 振幅系数 | 魔法数字 | 中 | 抽 `SimBinding.AmplitudeRatio` |
| 622/630/637/645 | `0.017453f`（π/180）重复 4 次 | 魔法数字 | 中 | 抽 `const float Deg2Rad` |
| 662 | `Math.Clamp(cell.DecimalPlaces, 0, 6)` 最大小数位 | 魔法数字 | 高 | 与设计器 L1242 重复，抽共享常量 |
| 663 | `cell.Row * 31 + cell.Col * 17` 相位魔法数字 | 魔法数字 | 低 | 抽常量 |
| 666 | `50.0f + 45.0f * sinf(...)` 表格绑定默认值 | 魔法数字 | 中 | 抽 `SimBinding.TableDefault` |
| 1991 | `"2563eb"` Gauge 指示器兜底 | 颜色 | 高 | 与设计器 `37,99,235` 蓝色系并存，统一主题 PrimaryColor |
| 2003 | `"0xe2e8f0"` Gauge 文本兜底 | 颜色 | 中 | 统一主题 TextColor `#e2e8f0` |
| 2014 | `"2563eb"` Knob 兜底（重复 1991） | 颜色 | 高 | 统一常量 |
| 2026 | `60` Spinner 弧角 | 魔法数字 | 低 | 抽常量 |
| 2047 | `"#334155"` 网格线兜底 | 颜色 | 高 | 与设计器 `(51,65,85)` 一致，抽共享常量 |
| 2058 | `"#1e293b"` 单元格兜底 | 颜色 | 高 | 与设计器 `(30,41,59)` 一致，抽共享常量 |
| 2123 | `"#ffffff"` Line 兜底 | 颜色 | 中 | 与设计器 `Colors.White` 统一 |
| 2124 | `2` Line 默认线宽 | 尺寸 | 中 | 与设计器 L1083 统一 |

---

### D. 模型默认值（`HmiControl.cs` / `HmiTheme.cs`）

模型默认值已集中定义，属可接受的"配置"，但存在**与渲染兜底不一致**的问题：

| 位置 | 默认值 | 问题 |
|---|---|---|
| `HmiControl.FontSize = 14` | 14 | 设计器渲染兜底用 `12`，**不一致** |
| `HmiControl.MajorTickCount = 6` | 6 | 设计器/生成器用 `Math.Max(2, ...)` 兜底，语义重复 |
| `HmiControl.SvgViewBoxWidth/Height = 100` | 100 | 设计器 `RenderSvg` 又写死 `100`，重复 |
| `HmiTheme.PrimaryColor = "#2563eb"` 等 | 主题色 | 设计器渲染层**完全不消费 HmiTheme**，兜底色自成一套 RGB |

---

### E. 弹窗/UI 组件 —— `AppDialogs.cs` 等

| 位置 | 硬编码内容 | 类别 | 严重度 | 建议 |
|---|---|---|---|---|
| AppDialogs.cs L50–51 | 确认框 `Width=380, Height=180` | 尺寸 | 中 | 抽 `DialogMetrics` 常量 |
| AppDialogs.cs L117/238 | 按钮 `Height=32` | 尺寸 | 中 | 抽常量 |
| AppDialogs.cs L161–162 | 输入框 `Width=400, Height=190` | 尺寸 | 中 | 抽常量 |

---

## 三、接口暴露情况核查

### 已完整暴露的接口

| 接口类别 | 具体内容 | 位置 |
|---|---|---|
| **数据获取（属性）** | `Screen`、`ZoomLevel`、`Adorner`、`IsPreviewMode`、`PreviewResolution`（均为 StyledProperty + CLR 包装） | DesignCanvas L25–92 |
| **事件回调** | `SelectionChanged`、`ControlPropertyChanged`、`ControlDropped`、`TemplateDropped`、`ResourceDropped`、`PreviewControlClicked`（共 6 个） | DesignCanvas L153–179 |
| **预览值注入** | `PreviewValueProvider`（`Func<string,double?>`）、`PreviewValueTypeProvider`（`Func<string,string?>`）—— 供预览模式模拟绑定值，只读隔离 | DesignCanvas L103–109 |
| **状态管理** | `UndoRedoManager`（撤销/重做） | DesignCanvas L184 |
| **校验（硬件层）** | `IHardwareTarget.ValidateProject(HmiProject)` → `ValidationResult` | Hmi.Hardware.Contracts |
| **属性变更通知** | `PropertyChanged` 事件（属性面板 → 宿主） | PropertyPanelView.axaml.cs |

### 尚未暴露 / 暴露不完整的接口

| # | 缺失接口 | 现状 | 缺失原因 | 补充方案 | 严重度 |
|---|---|---|---|---|---|
| 1 | **控件渲染器插件扩展点** | 所有 `Render*` 为 `private`/`static`，通过 `switch(control.Type)` 硬分发，新增控件必须改 `RenderControl` | 渲染逻辑内聚在画布类，未抽象 `IControlRenderer` | 抽 `IControlRenderer { bool Supports(HmiControlType); void Render(ctx, control, rect, z); }` + 渲染器注册表，插件可注册新渲染器 | 高 |
| 2 | **设计器渲染主题注入** | `DesignCanvas` 不接收 `HmiTheme`，兜底色全部硬编码 RGB，与项目主题脱节 | 设计器渲染层从未接入主题模型 | 增加 `ThemeProvider`/`HmiTheme` 依赖属性，`DesignPalette` 从主题派生兜底色 | 高 |
| 3 | **属性即时校验接口** | 属性面板无校验回调；数值输入（FontSize/Opacity 等）无合法性校验，越界值直接写模型 | 校验仅存在于硬件层 `ValidateProject`（提交时）与 AI 输出校验 | 增加 `IPropertyValidator { string? Validate(HmiControl, prop, value); }`，属性面板编辑时即时校验 | 高 |
| 4 | **选中集只读访问器** | `_selectedControls` 为 private，外部只能通过 `SelectionChanged` 事件被动接收，无法主动查询 | 历史接口仅事件化 | 暴露 `IReadOnlyList<HmiControl> SelectedControls { get; }` | 中 |
| 5 | **控件动作元数据/配置下发** | 动作类型（ToggleBit 等）的中文名硬编码在属性面板，无统一元数据，无服务端配置下发通道 | 无"配置下发"基础设施 | 抽 `ActionTypeMeta`（枚举+显示名+本地化）；若需服务端下发，增加 `IControlConfigProvider` 抽象（本地实现 + 远端实现） | 中 |
| 6 | **校验规则可扩展点** | 无 `IValidationRule` 集合，规则散落在 `AiOutputValidator` 与硬件 target 中 | 校验职责分散 | 抽统一 `IValidationRule` + 规则链，设计器/生成器/硬件复用 | 中 |
| 7 | **值格式化器接口** | 绑定值显示格式化（Bool→ON/OFF、小数位）散落在 `PreviewValueResolver`、`ResolveTableCellText`、生成器 `sim_refresh_bindings` 三处 | 无统一 `IValueFormatter` | 抽 `IValueFormatter`（按 ValueDisplayType/DataType 格式化），三处共用 | 中 |

---

## 四、修复优先级建议

### P0（立即处理，跨层一致性隐患）

1. **统一三套颜色体系**：设计器 RGB 兜底色 + 生成器 hex 兜底色 + `HmiTheme` 主题色 → 收敛为单一 `DesignPalette`/`CodeGenPalette`，均由 `HmiTheme` 派生。重点核对：`(45,55,75)` vs `#1a2332`、`(37,99,235)` vs `#2563eb`、`(34,197,94)` vs `#22c55e`、`(239,68,68)` vs `#ef4444`。
2. **统一 FontSize 默认值**：模型 14 / 设计器兜底 12 → 统一为 14，消除"设默认字号渲染反而变小"的隐蔽 bug。
3. **抽 Gauge/Knob 弧角常量**：`135°`/`270°` 在设计器与生成器重复出现，抽 `ArcLayout` 共享常量，防漂移。
4. **抽 StretchMode / ActionType 枚举**：消除字符串字面量 switch（`"contain"`、`"ToggleBit"` 等），改用枚举 + 单一解析器/元数据表。

### P1（接口扩展，消除结构性缺口）

5. **抽象 `IControlRenderer`** + 渲染器注册表，替换 `RenderControl` 的巨型 switch，提供插件扩展点。
6. **接入 `HmiTheme` 到设计器渲染层**，让兜底色可配置。
7. **增加 `IPropertyValidator`**，属性面板即时校验（数值范围、命名冲突、必填）。

### P2（可维护性，随迭代推进）

8. 抽 `TextLayout.Padding`（4px）、`BindingFormat.MaxDecimals`（6）、`SimBinding` 正弦波系数等共享常量。
9. 动作类型/占位文案/"图片"/"Tab1"/"100%" 等固定文案走本地化。
10. 抽 `DialogMetrics`、`KeyboardLayout`、`ValveGeometry` 等控件几何常量类。
11. 增加 `SelectedControls` 只读访问器、`IValueFormatter`、统一 `IValidationRule`。

---

## 五、结论

设计器控件的**功能已较完整**（25 种控件、6 个事件回调、预览值注入、撤销重做均已具备），但存在两类结构性问题：

1. **硬编码面广量大**：约 120+ 处，颜色占近半，且**设计器/生成器/主题三处颜色语义互相脱节**，是"设计器-模拟器显示不一致"类问题的温床（此前已实际引发表格背景、Line 控件、Slider 圆角等多次一致性 bug）。
2. **接口暴露不完整**：渲染器、主题、校验、值格式化均无扩展点，导致新增控件必须改核心类、插件无法扩展、校验职责分散。

建议优先落地 P0 四项（收敛颜色体系 + 统一默认值 + 抽弧角/枚举常量），可显著降低后续一致性 bug 的复发率；P1/P2 随迭代推进。
