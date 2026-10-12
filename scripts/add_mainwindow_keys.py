#!/usr/bin/env python3
"""
add_mainwindow_keys.py - 为 MainWindow 补全缺失的 47 个本地化键

将 MainWindow.axaml 和 MainWindow.axaml.cs 中硬编码的中文文本
对应的键值对添加到 Lang.cs 和 JSON 资源文件中。

用法:
    python add_mainwindow_keys.py [--dry-run]
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path


# ── 47 个键的完整定义 ──────────────────────────────────────────
# 格式: (键名, 常量名, 中文值, 英文值, 来源描述)
NEW_KEYS: list[tuple[str, str, str, str, str]] = [
    # ═══ 菜单栏 ═══
    ('menu.validate.code',       'MenuValidateCode',
     '验证生成代码(_V)...',       'Validate Generated Code (_V)...',
     'MainWindow.axaml L24'),
    ('menu.export.simulator',    'MenuExportSimulator',
     '导出模拟器工程(_M)...',     'Export Simulator Project (_M)...',
     'MainWindow.axaml L25'),
    ('menu.create.template',     'MenuCreateTemplate',
     '从选中控件创建模板(_T)',     'Create Template from Selection (_T)',
     'MainWindow.axaml L36'),
    ('menu.panel',               'MenuPanel',
     '面板(_P)',                  'Panels (_P)',
     'MainWindow.axaml L48'),
    ('menu.reload.panels',       'MenuReloadPanels',
     '重新加载面板(_L)',           'Reload Panels (_L)',
     'MainWindow.axaml L62'),
    ('menu.tools',               'MenuTools',
     '工具(_T)',                  'Tools (_T)',
     'MainWindow.axaml L64'),
    ('menu.git.init',            'MenuGitInit',
     '初始化 Git 仓库(_G)...',    'Initialize Git Repo (_G)...',
     'MainWindow.axaml L84'),
    ('menu.git.commit',          'MenuGitCommit',
     'Git 提交(_C)...',           'Git Commit (_C)...',
     'MainWindow.axaml L85'),
    ('menu.git.history',         'MenuGitHistory',
     'Git 历史(_H)...',           'Git History (_H)...',
     'MainWindow.axaml L86'),
    ('menu.git.refresh',         'MenuGitRefresh',
     '刷新 Git 状态(_R)',         'Refresh Git Status (_R)',
     'MainWindow.axaml L87'),
    ('menu.lang.zhCN',           'MenuLangZhCN',
     '中文',                     '中文',
     'MainWindow.axaml L90'),

    # ═══ 分辨率 ComboBox ═══
    ('cb.resolution.off',        'CbResolutionOff',
     '分辨率预览: 关',             'Resolution Preview: Off',
     'MainWindow.axaml L172'),
    ('cb.resolution.800x480',    'CbResolution800x480',
     '800x480 (设计)',            '800x480 (Design)',
     'MainWindow.axaml L173'),
    ('cb.resolution.1024x600',   'CbResolution1024x600',
     '1024x600',                 '1024x600',
     'MainWindow.axaml L174'),
    ('cb.resolution.1280x800',   'CbResolution1280x800',
     '1280x800',                 '1280x800',
     'MainWindow.axaml L175'),
    ('cb.resolution.480x800',    'CbResolution480x800',
     '480x800 (竖屏)',            '480x800 (Portrait)',
     'MainWindow.axaml L176'),

    # ═══ 工具栏按钮 Label ═══
    ('toolbar.reloadPanels',     'ToolbarReloadPanels',
     '重载面板',                  'Reload Panels',
     'MainWindow.axaml L189 Label'),
    ('toolbar.reloadPanels.tt', 'ToolbarReloadPanelsTt',
     '重新加载所有面板内容 (F5)',   'Reload all panel contents (F5)',
     'MainWindow.axaml L189 ToolTip'),
    ('toolbar.alignLeft',        'ToolbarAlignLeft',
     '左对齐',                    'Align Left',
     'MainWindow.axaml L198 Label+ToolTip'),
    ('toolbar.alignRight',       'ToolbarAlignRight',
     '右对齐',                    'Align Right',
     'MainWindow.axaml L203 Label+ToolTip'),
    ('toolbar.alignTop',         'ToolbarAlignTop',
     '顶对齐',                    'Align Top',
     'MainWindow.axaml L208 Label'),
    ('toolbar.alignTop.tt',     'ToolbarAlignTopTt',
     '顶部对齐',                  'Align Top',
     'MainWindow.axaml L208 ToolTip'),
    ('toolbar.alignBottom',      'ToolbarAlignBottom',
     '底对齐',                    'Align Bottom',
     'MainWindow.axaml L213 Label'),
    ('toolbar.alignBottom.tt',  'ToolbarAlignBottomTt',
     '底部对齐',                  'Align Bottom',
     'MainWindow.axaml L213 ToolTip'),
    ('toolbar.alignCenterH',     'ToolbarAlignCenterH',
     '水平居中',                  'Center Horizontally',
     'MainWindow.axaml L218 Label+ToolTip'),
    ('toolbar.alignCenterV',     'ToolbarAlignCenterV',
     '垂直居中',                  'Center Vertically',
     'MainWindow.axaml L223 Label+ToolTip'),

    # ═══ 状态栏 ═══
    ('status.controls',          'StatusControls',
     '控件: 0',                   'Controls: 0',
     'MainWindow.axaml L298'),
    ('status.target',            'StatusTarget',
     '目标: generic',             'Target: generic',
     'MainWindow.axaml L325'),
    ('status.git.not.ready',     'StatusGitNotReady',
     'Git: 未安装或未打开项目',     'Git: Not installed or no project opened',
     'MainWindow.axaml L330'),

    # ═══ MainWindow.axaml.cs 状态文本 ═══
    ('status.reloading.panels',  'StatusReloadingPanels',
     '正在重载面板...',             'Reloading panels...',
     'MainWindow.axaml.cs L303'),
    ('status.panels.reloaded',   'StatusPanelsReloaded',
     '面板重载完成',               'Panels reloaded',
     'MainWindow.axaml.cs L312'),
    ('status.reload.failed',     'StatusReloadFailed',
     '重载失败: {0}',              'Reload failed: {0}',
     'MainWindow.axaml.cs L318'),
    ('status.asset.info',        'StatusAssetInfo',
     '资源: {0} ({1}) — {2}',       'Asset: {0} ({1}) — {2}',
     'MainWindow.axaml.cs L541'),
    ('status.save.first',        'StatusSaveFirst',
     '请先保存项目 (Ctrl+S) 再生成图标', 'Please save the project first (Ctrl+S) before generating icons',
     'MainWindow.axaml.cs L562'),
    ('status.icons.generated',   'StatusIconsGenerated',
     'AI 已生成 {0} 个图标, 双击资源可应用到 SVG 控件',
     'AI generated {0} icons. Double-click a resource to apply to SVG control',
     'MainWindow.axaml.cs L578'),
    ('status.select.svg.first',  'StatusSelectSvgFirst',
     '请先在画布上选中一个 SVG 控件，再双击 SVG 资源',
     'Please select an SVG control on the canvas first, then double-click the SVG resource',
     'MainWindow.axaml.cs L589'),
    ('status.svg.applied',       'StatusSvgApplied',
     '已应用 SVG 资源: {0}（{1} 个形状）',
     'SVG resource applied: {0} ({1} shapes)',
     'MainWindow.axaml.cs L608'),
    ('status.svg.apply.failed',  'StatusSvgApplyFailed',
     '应用 SVG 失败: {0}',
     'Failed to apply SVG: {0}',
     'MainWindow.axaml.cs L612'),
    ('app.slogan',               'AppSlogan',
     '为工业 HMI 开发倾心打造 ❤',
     'Crafted with ❤ for Industrial HMI Development',
     'MainWindow.axaml.cs L715'),
    ('status.preview.no.action', 'StatusPreviewNoAction',
     '预览: 点击 {0} 无可用动作',
     'Preview: Click {0} has no action',
     'MainWindow.axaml.cs L880'),
    ('status.logged.in',         'StatusLoggedIn',
     '已登录: {0}',
     'Logged in: {0}',
     'MainWindow.axaml.cs L892'),
    ('status.unknown.user',     'StatusUnknownUser',
     '未知',
     'Unknown',
     'MainWindow.axaml.cs L892b'),
    ('confirm.discard.changes',  'ConfirmDiscardChanges',
     '当前项目 "{0}" 有未保存的更改，是否放弃并新建工程？',
     'Current project "{0}" has unsaved changes. Discard and create a new project?',
     'MainWindow.axaml.cs L1110'),
]

# ── Lang.cs 解析 ────────────────────────────────────────────────

_CONST_RE = re.compile(r'public\s+const\s+string\s+(\w+)\s*=\s*"([^"]+)"')


def read_lang_constants(lang_path: Path) -> dict[str, str]:
    content = lang_path.read_text(encoding='utf-8')
    return {name: key for name, key in _CONST_RE.findall(content)}


def insert_constants(lang_path: Path, new_consts: list[tuple[str, str]]) -> None:
    """在 Lang.cs 末尾的 } 之前插入新常量。"""
    content = lang_path.read_text(encoding='utf-8')
    existing_names = set(_CONST_RE.findall(content))

    to_insert: list[tuple[str, str]] = []
    for name, key in new_consts:
        if name not in existing_names:
            to_insert.append((name, key))

    if not to_insert:
        return

    max_name_len = max(len(n) for n, _ in to_insert)
    align_width = max(max_name_len + 3, 30)

    lines = ['\n    // ── MainWindow 补全（自动生成）──\n']
    for name, key in to_insert:
        padding = ' ' * (align_width - len(name))
        lines.append(f'    public const string {name}{padding}= "{key}";\n')
    new_code = ''.join(lines)

    last_brace = content.rfind('}')
    if last_brace > 0:
        content = content[:last_brace] + new_code + content[last_brace:]
        lang_path.write_text(content, encoding='utf-8')


# ── JSON 读写 ──────────────────────────────────────────────────

def load_json(path: Path) -> dict[str, str]:
    return json.loads(path.read_text(encoding='utf-8'))


def save_json(path: Path, data: dict[str, str]) -> int:
    sorted_data = dict(sorted(data.items()))
    with path.open('w', encoding='utf-8') as f:
        json.dump(sorted_data, f, ensure_ascii=False, indent=2)
        f.write('\n')
    return len(sorted_data)


def fill_json(json_data: dict[str, str], keys: list[tuple[str, str, str, str, str]], lang: str) -> int:
    """向 JSON 字典填充缺失的键值。lang='zh' 用中文，否则用英文。"""
    count = 0
    for key, _name, zh_val, en_val, _src in keys:
        if key not in json_data:
            json_data[key] = zh_val if lang == 'zh' else en_val
            count += 1
    return count


# ── 主流程 ───────────────────────────────────────────────────────

def main() -> int:
    repo_root = Path(__file__).resolve().parent.parent
    lang_path = repo_root / 'src' / 'HmiEditor.App' / 'Resources' / 'Lang.cs'
    json_dir = repo_root / 'src' / 'HmiEditor.Core' / 'Resources'
    zh_path = json_dir / 'Strings.zh-CN.json'
    en_path = json_dir / 'Strings.en-US.json'

    dry_run = '--dry-run' in sys.argv

    print('📖 读取现有数据 ...')
    lang_consts = read_lang_constants(lang_path)
    lang_keys = set(lang_consts.values())
    json_zh = load_json(zh_path)
    json_en = load_json(en_path)
    json_keys = set(json_zh.keys())

    existing_const_names = set(lang_consts.keys())

    # 检查已有
    already_in_lang = [(n, k) for k, n, *_ in NEW_KEYS if k in lang_keys]
    already_in_json = [k for k, *_ in NEW_KEYS if k in json_keys]
    missing_lang = [(k, n) for k, n, *_ in NEW_KEYS if k not in lang_keys]
    missing_json = [k for k, *_ in NEW_KEYS if k not in json_keys]

    print(f'   Lang.cs 现有: {len(lang_consts)} 个常量')
    print(f'   JSON zh-CN 现有: {len(json_zh)} 个键')
    print(f'   本次新增: {len(NEW_KEYS)} 个键')
    print(f'   已存在于 Lang.cs: {len(already_in_lang)} 个')
    print(f'   已存在于 JSON: {len(already_in_json)} 个')
    print(f'   需新增到 Lang.cs: {len(missing_lang)} 个')
    print(f'   需新增到 JSON: {len(missing_json)} 个')

    if dry_run:
        print('\n[Dry Run] 将写入以下内容：\n')
        print('Lang.cs 新增常量:')
        for key, name in missing_lang[:10]:
            zh = next(zh for k, n, zh, en, s in NEW_KEYS if k == key)
            print(f'  public const string {name} = "{key}";')
        if len(missing_lang) > 10:
            print(f'  ... 还有 {len(missing_lang) - 10} 个')

        print('\nJSON zh-CN 新增:')
        for key, *_ in NEW_KEYS:
            if key not in json_keys:
                zh = next(zh for k, n, zh, en, s in NEW_KEYS if k == key)
                print(f'  "{key}": "{zh}"')
        return 0

    # ── 更新 JSON ──
    print('\n💾 更新 JSON ...')
    zh_added = fill_json(json_zh, NEW_KEYS, 'zh')
    en_added = fill_json(json_en, NEW_KEYS, 'en')
    zh_n = save_json(zh_path, json_zh)
    en_n = save_json(en_path, json_en)
    print(f'   zh-CN: +{zh_added} 个 → {zh_n} 个键')
    print(f'   en-US: +{en_added} 个 → {en_n} 个键')

    # ── 更新 Lang.cs ──
    print('\n💾 更新 Lang.cs ...')
    new_consts = [(n, k) for k, n, *_ in NEW_KEYS if k not in lang_keys and n not in existing_const_names]
    insert_constants(lang_path, new_consts)
    print(f'   新增 {len(new_consts)} 个常量')

    # ── 验证一致性 ──
    print('\n🔍 验证一致性 ...')
    final_lang = read_lang_constants(lang_path)
    final_lang_keys = set(final_lang.values())
    final_json_keys = set(json_zh.keys())

    remaining_lang = sorted(final_lang_keys - final_json_keys)
    remaining_json = sorted(final_json_keys - final_lang_keys)

    if remaining_lang:
        print(f'   ⚠️  Lang.cs 有但 JSON 缺失: {len(remaining_lang)} 个')
        for k in remaining_lang:
            print(f'      - {k}')
    if remaining_json:
        print(f'   ⚠️  JSON 有但 Lang.cs 缺失: {len(remaining_json)} 个')
        for k in remaining_json:
            print(f'      - {k}')
    if not remaining_lang and not remaining_json:
        print(f'   ✓ Lang.cs ({len(final_lang)}) 与 JSON ({zh_n}) 完全一致')

    print('\n✅ 完成')
    return 0


if __name__ == '__main__':
    sys.exit(main())
