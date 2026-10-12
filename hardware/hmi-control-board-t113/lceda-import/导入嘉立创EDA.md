# 导入嘉立创 EDA 专业版（KiCad 6 格式）

本目录是 **KiCad 6.0 兼容格式**的降级副本，用于导入嘉立创 EDA 专业版。

> 嘉立创 EDA 专业版（桌面端）的「导入 KiCad」功能基于 KiCad 6 代码，只认
> **KiCad 6.0** 格式（符号库 `version 20200904`、原理图 `version 20211108`）。
> 原工程是 KiCad 10（`20251024`/`20241122`），直接导入会报「格式不正确」。
>
> 本目录已用脚本降级：去掉 v8+ 的 `(embedded_fonts no)`、引脚 name/number
> 嵌套 `(effects)`、原理图内嵌 `(lib_symbols)` 缓存；版本号改为 KiCad 6。
> 网络结构、引脚映射与原工程完全一致（已在原工程用 netlist 逐网核验，0 BAD）。

## 导入方法一：KiCad 6 工程导入（推荐先试）

1. 打开 **嘉立创 EDA 专业版**（桌面端）。
2. 菜单：**文件 → 导入 → KiCad**
3. 选择本目录的 `hmi-board.kicad_pro`。
4. 若提示找不到符号库 `hmi`：确认 `sym-lib-table` 与本目录同级，
   或手动把 `hmi_lib.kicad_sym` 指向工程目录。
5. 导入后跑 **设计管理器 → 检查 ERC**，确认无真实电气错误。

## 导入方法二：网表导入（方法一失败时的备份）

若嘉立创仍不认 KiCad 工程文件，可改用网表导入：

1. 嘉立创里 **新建工程**（空白）。
2. 从嘉立创自带元件库（或元件商城）放置所需元件：
   电阻/电容/接插件/USB/晶振等常用封装都有。
3. 菜单：**文件 → 导入 → 网表**，选 `hmi-board.kicad_sch.net`
   （KiCad s-expr 格式网表，从原工程导出，包含全部 148 元件与网络连接）。
4. 网表会按 `U6`/`R1`/`C1` 等位号自动连线。

> 注：网表 `hmi-board.kicad_sch.net` 是用原工程（KiCad 10，含内嵌符号缓存）
> 由 `kicad-cli sch export netlist` 导出的，格式嘉立创网表导入器可识别。

## 文件清单

| 文件 | 说明 |
|---|---|
| `hmi-board.kicad_pro` | KiCad 6 工程文件 |
| `*.kicad_sch` | 7 张原理图（根图 + 6 子系统） |
| `hmi_lib.kicad_sym` | 自定义符号库（KiCad 6 格式） |
| `sym-lib-table` | 工程符号库表（`${KIPRJMOD}/hmi_lib.kicad_sym`） |
| `hmi-board.kicad_sch.net` | 网表（备用导入） |
| `hmi-board.xml` | 网表 XML 格式（备用） |
| `hmi-board.csv` | BOM 物料清单（149 行） |

## 重要提醒

- **元件封装为空**：Footprint 字段当前全空，导入后需逐个或批量匹配封装。
- **面板 50P FPC / RTL8201F / GL850G 引脚**：投产前仍需按数据手册逐脚复核
  （见父目录 `README.md` 第 5 节）。
- 嘉立创 EDA 与 KiCad 封装库不通用，最终 PCB 需重新指定封装。
- 若嘉立创导入时报具体某行语法错误，把报错贴回来，我针对性修 `convert_kicad6.py`。
- 若方法一/二都不行，最后方案是在嘉立创里新建工程、按 `hmi-board.csv` 手工放置
  元件 + 导入网表连线（工作量大但 100% 可行）。
