#!/usr/bin/env python3
"""
localize_view_tooltips.py - 批量本地化 XAML 文件中的 ToolTip.Tip 硬编码中文

工作流程:
1. 为每个文件预定义 ToolTip 中文文本 → 键名映射
2. 添加 JSON 键值（zh-CN 原文 + en-US 翻译）
3. 添加 Lang.cs 常量
4. 替换 axaml 中 ToolTip.Tip="中文" 为 ToolTip.Tip="{Binding [key], ...}"

用法:
    python localize_view_tooltips.py [--dry-run]
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
LANG_PATH = REPO_ROOT / 'src' / 'HmiEditor.App' / 'Resources' / 'Lang.cs'
ZH_PATH = REPO_ROOT / 'src' / 'HmiEditor.Core' / 'Resources' / 'Strings.zh-CN.json'
EN_PATH = REPO_ROOT / 'src' / 'HmiEditor.Core' / 'Resources' / 'Strings.en-US.json'

# ── 文件 → ToolTip 映射表 ──────────────────────────────────────
# 格式: (相对路径, [(中文文本, 键名, en-US翻译), ...])
FILE_TOOLTIPS: list[tuple[str, list[tuple[str, str, str]]]] = [
    # ═══ SimulatorView.axaml (32) ═══
    ('src/HmiEditor.App/Views/SimulatorView.axaml', [
        ('添加新的仿真变量',                    'sim.tt.add.var',         'Add a new simulated variable'),
        ('删除选中的仿真变量',                    'sim.tt.del.var',         'Delete the selected simulated variable'),
        ('选择信号生成模式',                      'sim.tt.signal.type',     'Select signal generation mode'),
        ('固定输出设定值',                       'sim.tt.const',           'Output a fixed set value'),
        ('sin(2πft) 正弦波形',                   'sim.tt.sine',            'sin(2πft) sine wave'),
        ('±幅值交替的方波信号',                   'sim.tt.square',          'Square wave alternating between ±amplitude'),
        ('线性上升后归零的锯齿波形',               'sim.tt.ramp',            'Sawtooth wave rising linearly then resetting'),
        ('在最小值和最大值之间循环递增',            'sim.tt.increment',       'Cyclically incrementing between min and max'),
        ('在范围内均匀分布的随机值',               'sim.tt.random',          'Uniformly distributed random value within range'),
        ('由用户手动设定值',                      'sim.tt.manual',          'Value set manually by user'),
        ('信号的幅值（峰值）',                    'sim.tt.amplitude',       'Signal amplitude (peak value)'),
        ('信号的直流偏移量',                      'sim.tt.offset',          'DC offset of the signal'),
        ('正弦波/方波/锯齿波的频率（秒）',          'sim.tt.freq',            'Frequency of sine/square/ramp wave (seconds)'),
        ('最小值',                              'sim.tt.min.val',          'Minimum value'),
        ('最大值',                              'sim.tt.max.val',          'Maximum value'),
        ('叠加的随机噪声幅度（0 为无噪声）',        'sim.tt.noise',           'Added random noise amplitude (0 = no noise)'),
        ('手动模式下的输出值',                    'sim.tt.manual.val',      'Output value in manual mode'),
        ('当前信号类型的波形特征描述',              'sim.tt.wave.desc',       'Waveform characteristic description of current signal type'),
        ('启动/停止仿真运行',                     'sim.tt.start.stop',      'Start/stop simulation'),
        ('仿真数据更新间隔',                      'sim.tt.tick.interval',   'Simulation data update interval'),
        ('重置仿真，清空所有数据',                 'sim.tt.reset',           'Reset simulation and clear all data'),
        ('断开虚拟 Modbus 从站连接',               'sim.tt.disconnect',      'Disconnect virtual Modbus slave'),
        ('重新连接虚拟 Modbus 从站',               'sim.tt.reconnect',       'Reconnect virtual Modbus slave'),
        ('设置写入响应模式',                      'sim.tt.write.mode',      'Set write response mode'),
        ('回显写入值',                           'sim.tt.echo',            'Echo the written value'),
        ('取反写入值',                           'sim.tt.invert',          'Invert the written value'),
        ('随机响应',                             'sim.tt.random.resp',     'Random response'),
        ('正常应答',                             'sim.tt.respond',         'Normal response'),
        ('寄存器地址（整数）',                    'sim.tt.reg.addr',        'Register address (integer)'),
        ('寄存器值（浮点数）',                    'sim.tt.reg.val',         'Register value (float)'),
        ('读取指定地址的寄存器值',                 'sim.tt.read.reg',        'Read register value at specified address'),
        ('向指定地址写入寄存器值',                 'sim.tt.write.reg',       'Write register value to specified address'),
    ]),

    # ═══ RecipeEditorView.axaml (24) ═══
    ('src/HmiEditor.App/Views/RecipeEditorView.axaml', [
        ('创建一个新的配方分组',                   'recipe.tt.add.group',    'Create a new recipe group'),
        ('删除选中分组及其所有配方',                'recipe.tt.del.group',    'Delete the selected group and all its recipes'),
        ('在当前分组中创建一个新配方',              'recipe.tt.new',          'Create a new recipe in the current group'),
        ('克隆选中的配方到一个新名称',              'recipe.tt.clone',        'Clone the selected recipe to a new name'),
        ('删除选中的配方（预置配方不可删除）',       'recipe.tt.del.recipe',   'Delete the selected recipe (preset recipes cannot be deleted)'),
        ('从 CSV 文件导入配方数据',                'recipe.tt.import.csv',   'Import recipe data from a CSV file'),
        ('将所有配方导出为 CSV 文件',              'recipe.tt.export.csv',   'Export all recipes to a CSV file'),
        ('给配方一个有意义的名称，如「高温模式」',   'recipe.tt.name',         'Give the recipe a meaningful name, e.g. "High-Temp Mode"'),
        ('配方所属的分组，与左侧选中分组联动',       'recipe.tt.group',        'Recipe group, linked to the selected group on the left'),
        ('描述这个配方的用途和适用场景',            'recipe.tt.desc',         'Describe the purpose and applicable scenarios of this recipe'),
        ('启用后该配方可在运行时被加载使用',         'recipe.tt.enabled',      'When enabled, this recipe can be loaded at runtime'),
        ('添加一个新的配方参数',                  'recipe.tt.add.param',    'Add a new recipe parameter'),
        ('删除选中的参数',                       'recipe.tt.del.param',    'Delete the selected parameter'),
        ('将选中参数上移一位',                    'recipe.tt.up.param',     'Move the selected parameter up by one'),
        ('将选中参数下移一位',                    'recipe.tt.down.param',   'Move the selected parameter down by one'),
        ('如「目标温度」',                      'recipe.tt.param.label',  'e.g. "Target Temperature"'),
        ('选择此参数绑定的项目变量',               'recipe.tt.param.var',    'Select the project variable this parameter binds to'),
        ('参数的默认预设值',                     'recipe.tt.param.default','Default preset value of the parameter'),
        ('参数的工程单位',                       'recipe.tt.param.unit',   'Engineering unit of the parameter'),
        ('参数允许的最小值',                     'recipe.tt.param.min',    'Minimum allowed value of the parameter'),
        ('参数允许的最大值',                     'recipe.tt.param.max',    'Maximum allowed value of the parameter'),
        ('参数的数据类型',                      'recipe.tt.param.type',   'Data type of the parameter'),
        ('参数在列表中的排序序号',                'recipe.tt.param.order',  'Sort order of the parameter in the list'),
        ('保存对当前配方的所有修改',              'recipe.tt.save',         'Save all changes to the current recipe'),
    ]),

    # ═══ AnimationEditorView.axaml (21) ═══
    ('src/HmiEditor.App/Views/AnimationEditorView.axaml', [
        ('创建一个新的动画效果',                  'anim.tt.add',            'Create a new animation effect'),
        ('按触发方式筛选动画列表',                'anim.tt.trigger.filter', 'Filter the animation list by trigger mode'),
        ('删除选中的动画效果（不可恢复）',          'anim.tt.del',            'Delete the selected animation (irreversible)'),
        ('给动画一个有辨识度的名称',              'anim.tt.name',           'Give the animation a recognizable name'),
        ('选择要应用动画的控件',                  'anim.tt.target',         'Select the control to apply the animation to'),
        ('选择要动画化的控件属性',                'anim.tt.prop',           'Select the control property to animate'),
        ('描述动画效果和用途',                    'anim.tt.desc',           'Describe the animation effect and its purpose'),
        ('启用后动画在运行时生效',                'anim.tt.enabled',       'When enabled, the animation takes effect at runtime'),
        ('选择动画的触发方式',                    'anim.tt.trigger',        'Select the trigger mode of the animation'),
        ('动画的起始值',                         'anim.tt.from.val',       'Start value of the animation'),
        ('动画的结束值',                         'anim.tt.to.val',         'End value of the animation'),
        ('动画持续时长（毫秒）',                  'anim.tt.duration',       'Duration of the animation (ms)'),
        ('动画启动前的延迟时间（毫秒）',           'anim.tt.delay',          'Delay before the animation starts (ms)'),
        ('选择动画的速度变化曲线',                'anim.tt.easing',         'Select the speed curve of the animation'),
        ('0=不重复，-1=无限循环，N=重复N次',       'anim.tt.repeat',         '0 = no repeat, -1 = infinite loop, N = repeat N times'),
        ('播放动画预览（拖拽关键帧可调整时间，双击轨道添加关键帧）',
                                                 'anim.tt.play.preview',   'Play animation preview (drag keyframes to adjust time, double-click the track to add a keyframe)'),
        ('在时间轴上添加一个关键帧',              'anim.tt.add.keyframe',   'Add a keyframe on the timeline'),
        ('删除选中的关键帧',                     'anim.tt.del.keyframe',   'Delete the selected keyframe'),
        ('关键帧在时间轴上的百分比位置（0-100）',    'anim.tt.kf.time',       'Percentage position of the keyframe on the timeline (0-100)'),
        ('关键帧处的目标属性值',                  'anim.tt.kf.val',         'Target property value at the keyframe'),
        ('此帧到下一帧的缓动函数（留空使用全局设置）', 'anim.tt.kf.easing',    'Easing function from this frame to the next (empty = use global setting)'),
    ]),

    # ═══ 其他 Views ToolTip 残留 (17) ═══
    ('src/HmiEditor.App/Views/TrendConfigView.axaml', [
        ('添加趋势',                              'trend.tt.add',           'Add a trend'),
        ('删除选中趋势',                          'trend.tt.del',           'Delete the selected trend'),
        ('添加曲线笔',                            'trend.tt.add.pen',       'Add a curve pen'),
        ('删除选中曲线笔',                         'trend.tt.del.pen',       'Delete the selected curve pen'),
    ]),
    ('src/HmiEditor.App/Views/FaceplateLibraryView.axaml', [
        ('AI 生成模板 (描述组件需求)',             'faceplate.tt.ai.gen',    'Generate template with AI (describe the component requirement)'),
        ('编辑选中模板',                          'faceplate.tt.edit',      'Edit the selected template'),
        ('删除选中模板',                          'faceplate.tt.del',       'Delete the selected template'),
        ('从选中控件创建模板',                     'faceplate.tt.create.from.ctrl', 'Create a template from the selected control'),
    ]),
    ('src/HmiEditor.App/Views/NavigationFlowView.axaml', [
        ('从当前项目重建导航图',                    'nav.flow.tt.rebuild',    'Rebuild the navigation graph from the current project'),
        ('保存导航图到 .nav.json 文件',            'nav.flow.tt.save',       'Save the navigation graph to a .nav.json file'),
        ('从 .nav.json 文件加载导航图',            'nav.flow.tt.load',       'Load the navigation graph from a .nav.json file'),
    ]),
    ('src/HmiEditor.App/Views/VariableImportView.axaml', [
        ('解析选中的 CSV 文件',                   'var.import.tt.parse',    'Parse the selected CSV file'),
        ('将解析的变量添加到当前项目',             'var.import.tt.add',      'Add the parsed variables to the current project'),
    ]),
    ('src/HmiEditor.App/Views/VariableBrowserView.axaml', [
    ]),
    ('src/HmiEditor.App/Views/ProjectExplorerView.axaml', [
        ('删除选中项',                            'project.tt.del',         'Delete the selected item'),
    ]),
    ('src/HmiEditor.App/Views/DesignCanvasView.axaml', [
        ('切换设计时数据预览模式 (绑定变量的控件显示模拟值)', 'canvas.tt.toggle.preview', 'Toggle design-time data preview mode (controls bound to variables show simulated values)'),
    ]),
    ('src/HmiEditor.App/Views/AssetManagerView.axaml', [
        ('用自然语言生成 SVG 图标, 自动导入资源库',   'asset.tt.ai.svg',        'Generate SVG icon with natural language and import into asset library'),
    ]),
]


def to_const_name(key: str) -> str:
    """sim.tt.add.var → SimTtAddVar"""
    parts = key.split('.')
    return ''.join(p[:1].upper() + p[1:] for p in parts)


def load_json(path: Path) -> dict:
    return json.loads(path.read_text(encoding='utf-8'))


def save_json(path: Path, data: dict) -> None:
    sorted_data = dict(sorted(data.items()))
    with path.open('w', encoding='utf-8') as f:
        json.dump(sorted_data, f, ensure_ascii=False, indent=2)
        f.write('\n')


def add_lang_constants(new_consts: list[tuple[str, str]]) -> int:
    """在 Lang.cs 末尾 } 前插入新常量。返回新增数量。"""
    content = LANG_PATH.read_text(encoding='utf-8')
    existing = set(re.findall(r'public\s+const\s+string\s+(\w+)', content))
    to_add = [(name, key) for name, key in new_consts if name not in existing]
    if not to_add:
        return 0
    max_len = max(len(n) for n, _ in to_add) + 3
    align = max(max_len, 30)
    lines = ['\n    // ── View ToolTip 同步（自动生成）──\n']
    for name, key in to_add:
        pad = ' ' * (align - len(name))
        lines.append(f'    public const string {name}{pad}= "{key}";\n')
    last_brace = content.rfind('}')
    if last_brace > 0:
        content = content[:last_brace] + ''.join(lines) + content[last_brace:]
        LANG_PATH.write_text(content, encoding='utf-8')
    return len(to_add)


def replace_tooltips_in_axaml(filepath: Path, mapping: dict[str, str]) -> int:
    """替换 axaml 中 ToolTip.Tip="中文" 为 ToolTip.Tip="{Binding [key], ...}"。"""
    content = filepath.read_text(encoding='utf-8')
    count = 0

    def replacer(m):
        nonlocal count
        zh = m.group(1)
        if zh in mapping:
            count += 1
            return f'ToolTip.Tip="{{Binding [{mapping[zh]}], Source={{x:Static res:AppLocalization.Service}}}}"'
        return m.group(0)

    # 匹配 ToolTip.Tip="中文"（不含已绑定的）
    new_content = re.sub(r'ToolTip\.Tip="([^"]*[\u4e00-\u9fff][^"]*)"', replacer, content)
    if count > 0:
        filepath.write_text(new_content, encoding='utf-8')
    return count


def main() -> int:
    dry_run = '--dry-run' in sys.argv
    zh = load_json(ZH_PATH)
    en = load_json(EN_PATH)

    total_json = 0
    total_lang = 0
    total_replaced = 0

    for rel_path, tooltips in FILE_TOOLTIPS:
        filepath = REPO_ROOT / rel_path
        mapping = {zh_text: key for zh_text, key, en_text in tooltips}

        # JSON
        for zh_text, key, en_text in tooltips:
            if key not in zh:
                zh[key] = zh_text
                total_json += 1
            if key not in en:
                en[key] = en_text

        # Lang.cs
        new_consts = [(to_const_name(key), key) for _, key, _ in tooltips]
        added = add_lang_constants(new_consts)
        total_lang += added

        # 替换 axaml
        if not dry_run:
            replaced = replace_tooltips_in_axaml(filepath, mapping)
            total_replaced += replaced
            print(f'  {filepath.name}: 替换 {replaced} 处 ToolTip')

    if not dry_run:
        save_json(ZH_PATH, zh)
        save_json(EN_PATH, en)

    print(f'\n总计: JSON +{total_json}, Lang.cs +{total_lang}, axaml 替换 {total_replaced} 处')
    print(f'zh-CN: {len(zh)} 键, en-US: {len(en)} 键')
    return 0


if __name__ == '__main__':
    sys.exit(main())
