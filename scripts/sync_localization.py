#!/usr/bin/env python3
"""
sync_localization.py - 同步 Lang.cs 常量与 JSON 本地化资源

功能:
    1. 补全 JSON 中缺失的值（Lang.cs 有常量但 JSON 无对应键）
    2. 补全 Lang.cs 中缺失的常量定义（JSON 有键但 Lang.cs 无常量）

⚠ **默认只干跑，不写盘**（本仓纪律：批量改写类脚本的默认动作必须是"只看不改"）。
   落盘要显式给 `--write`；第 2 步还要**另外**给 `--add-consts`。

为什么第 2 步要单独开闸（登记 #52，2026-09-21 阶段 65 干跑量出）:
    `LangConstantUsageGuardTests.UnusedBaseline` 是**相等判定**，判的是
    「无代码引用的 Lang 常量个数」（当前 1125）。第 2 步会给「字典里有、Lang.cs 里没有」
    的键生成常量 —— 实测这样的键有 **360** 个 ⇒ 跑一次就把基线顶到 ≈1485 ⇒ 判据红 ⇒
    下一个人"顺手改大基线" ⇒ 这条 P0-5 防护就地失效。所以把它做成**显式动作**：
    真要选「常量集合与键集合同构」那个方向时，得先想清楚基线怎么办（见登记 #52 的 A/B）。

对于 JSON 缺失的值，优先从内置映射表获取合理翻译；
映射表中没有的键用键名作为占位值并标记 [TODO]。

用法:
    python3 scripts/sync_localization.py                      # 干跑：只打印会做什么
    python3 scripts/sync_localization.py --write              # 只补 JSON 缺失值
    python3 scripts/sync_localization.py --write --add-consts # 连常量一起生成（先读 #52）
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path


# ── 键名 → (中文值, 英文值) 映射表 ──────────────────────────────
# 这些键在 Lang.cs 中有常量定义，但从未在 InitializeDefaults() 中赋值
VALUE_MAP: dict[str, tuple[str, str]] = {
    # faceplate
    'faceplate.contains.controls': ('包含控件', 'Contains Controls'),

    # 项目设置 - 标题
    'win.project.settings': ('项目设置', 'Project Settings'),
    'project.settings.title': ('项目设置', 'Project Settings'),

    # 基本信息
    'project.settings.basic.author': ('作者', 'Author'),
    'project.settings.basic.company': ('公司', 'Company'),
    'project.settings.basic.created': ('创建时间', 'Created'),
    'project.settings.basic.desc': ('描述', 'Description'),
    'project.settings.basic.hw.target': ('硬件目标', 'Hardware Target'),
    'project.settings.basic.lvgl': ('LVGL 版本', 'LVGL Version'),
    'project.settings.basic.modified': ('修改时间', 'Modified'),
    'project.settings.basic.version': ('版本', 'Version'),

    # 设置分类
    'project.settings.category.basic': ('基本信息', 'Basic Info'),
    'project.settings.category.comm': ('通信设置', 'Communication'),
    'project.settings.category.display': ('显示设置', 'Display'),
    'project.settings.category.export': ('导出设置', 'Export'),
    'project.settings.category.runtime': ('运行时设置', 'Runtime'),
    'project.settings.category.security': ('安全设置', 'Security'),

    # 通信设置
    'project.settings.comm.recovery': ('恢复策略', 'Recovery'),
    'project.settings.comm.retry': ('重试次数', 'Retry Count'),
    'project.settings.comm.retry.backoff': ('重试退避', 'Retry Backoff'),
    'project.settings.comm.scan.interval': ('扫描周期', 'Scan Interval'),
    'project.settings.comm.timeout': ('超时', 'Timeout'),
    'project.settings.comm.write.coalesce': ('写入合并', 'Write Coalesce'),

    # 显示设置
    'project.settings.display.default.adaptive': ('自适应分辨率预览', 'Adaptive Resolution Preview'),
    'project.settings.display.default.bg.color': ('背景色', 'Background Color'),
    'project.settings.display.default.depth': ('颜色深度', 'Color Depth'),
    'project.settings.display.default.dpi': ('DPI', 'DPI'),
    'project.settings.display.default.height': ('默认高度', 'Default Height'),
    'project.settings.display.default.orientation': ('屏幕方向', 'Screen Orientation'),
    'project.settings.display.default.width': ('默认宽度', 'Default Width'),

    # 编辑器设置

    # 导出设置
    'project.settings.export.build.system': ('构建系统', 'Build System'),
    'project.settings.export.code.style': ('代码风格', 'Code Style'),
    'project.settings.export.comments': ('生成注释', 'Generate Comments'),
    'project.settings.export.debug.symbols': ('生成调试符号', 'Debug Symbols'),
    'project.settings.export.language': ('输出语言', 'Output Language'),
    'project.settings.export.license': ('许可证', 'License'),
    'project.settings.export.merge.resources': ('合并资源', 'Merge Resources'),
    'project.settings.export.naming': ('资源命名', 'Resource Naming'),
    'project.settings.export.optimization': ('优化级别', 'Optimization'),
    'project.settings.export.output': ('输出目录', 'Output Directory'),

    # 运行时设置
    'project.settings.runtime.boot.screen': ('启动画面', 'Boot Screen'),
    'project.settings.runtime.crash.dump': ('崩溃转储', 'Crash Dump'),
    'project.settings.runtime.crash.sector': ('崩溃转储扇区', 'Crash Dump Sector'),
    'project.settings.runtime.enable.reboot': ('允许重启', 'Enable Reboot'),
    'project.settings.runtime.heartbeat': ('心跳', 'Heartbeat'),
    'project.settings.runtime.history': ('历史路径', 'History Path'),
    'project.settings.runtime.log.level': ('日志级别', 'Log Level'),
    'project.settings.runtime.max.tasks': ('最大任务数量', 'Max Tasks'),
    'project.settings.runtime.scan': ('运行时扫描', 'Runtime Scan'),
    'project.settings.runtime.watchdog': ('看门狗', 'Watchdog'),

    # 安全设置
    'project.settings.security.audit': ('审计日志', 'Audit Log'),
    'project.settings.security.audit.path': ('审计路径', 'Audit Path'),
    'project.settings.security.auto.logout': ('自动登出', 'Auto Logout'),
    'project.settings.security.default.role': ('默认角色', 'Default Role'),
    'project.settings.security.lock.screen': ('锁定屏幕', 'Lock Screen'),
    'project.settings.security.lockout': ('锁定时长', 'Lockout Duration'),
    'project.settings.security.max.attempts': ('最大尝试次数', 'Max Attempts'),
    'project.settings.security.mode': ('操作模式', 'Operation Mode'),
    'project.settings.security.password.min.len': ('密码最小长度', 'Password Min Length'),
    'project.settings.security.tamper': ('防篡改', 'Tamper Detection'),
    'project.settings.security.timeout': ('会话超时', 'Session Timeout'),
}


# ── Lang.cs 解析 ────────────────────────────────────────────────

# 精确匹配: public const string Name = "key";
_CONST_RE = re.compile(r'public\s+const\s+string\s+(\w+)\s*=\s*"([^"]+)"')


def read_lang_constants(lang_path: Path) -> dict[str, str]:
    """读取 Lang.cs，返回 {const_name: key}。"""
    content = lang_path.read_text(encoding='utf-8')
    return {name: key for name, key in _CONST_RE.findall(content)}


def key_to_constant_name(key: str) -> str:
    """将键名转换为 C# 常量名（如 menu.file → MenuFile, canvas.emptyHint → CanvasEmptyHint）。"""
    parts = re.split(r'[.\-]', key)
    # 只首字母大写，保留其余字母大小写（避免 emptyHint → Emptyhint）
    name = ''.join((p[0].upper() + p[1:]) if p else '' for p in parts)
    if name and name[0].isdigit():
        name = '_' + name
    return name


# ── JSON 读写 ──────────────────────────────────────────────────

def load_json(path: Path) -> dict[str, str]:
    return json.loads(path.read_text(encoding='utf-8'))


def save_json(path: Path, data: dict[str, str]) -> int:
    sorted_data = dict(sorted(data.items()))
    with path.open('w', encoding='utf-8') as f:
        json.dump(sorted_data, f, ensure_ascii=False, indent=2)
        f.write('\n')
    return len(sorted_data)


# ── 主流程 ───────────────────────────────────────────────────────

def main() -> int:
    repo_root = Path(__file__).resolve().parent.parent
    lang_path = repo_root / 'src' / 'HmiEditor.App' / 'Resources' / 'Lang.cs'
    json_dir = repo_root / 'src' / 'HmiEditor.Core' / 'Resources'
    zh_path = json_dir / 'Strings.zh-CN.json'
    en_path = json_dir / 'Strings.en-US.json'

    # 默认**只看不改**（登记 #52）：批量改写类脚本不显式给 --write 就不落盘。
    # `--dry-run` 保留为显式写法，与默认行为等价（老命令行不会被改坏）。
    write = '--write' in sys.argv
    dry_run = not write
    # 第 2 步（生成常量）单独开闸：它会把 UnusedBaseline 顶穿（见文件头 / 登记 #52）。
    add_consts = '--add-consts' in sys.argv

    print('📖 读取 Lang.cs 常量 ...')
    lang_consts = read_lang_constants(lang_path)
    lang_keys = set(lang_consts.values())
    print(f'   {len(lang_consts)} 个常量')

    print('📖 读取 JSON ...')
    json_zh = load_json(zh_path)
    json_en = load_json(en_path)
    print(f'   zh-CN: {len(json_zh)} 个键, en-US: {len(json_en)} 个键')

    only_lang = sorted(lang_keys - set(json_zh.keys()))
    only_json = sorted(set(json_zh.keys()) - lang_keys)

    print(f'\n📊 差异分析:')
    print(f'   Lang.cs 有但 JSON 缺失: {len(only_lang)} 个')
    print(f'   JSON 有但 Lang.cs 无常量: {len(only_json)} 个')

    # ── 补全 JSON 缺失值 ──
    print(f'\n📝 补全 JSON 缺失值 ...')
    filled_mapped = 0
    filled_placeholder = 0
    for key in only_lang:
        if key in VALUE_MAP:
            zh_val, en_val = VALUE_MAP[key]
            json_zh[key] = zh_val
            json_en[key] = en_val
            filled_mapped += 1
        else:
            # 映射表中没有，用键名作为占位值
            json_zh[key] = f'[TODO] {key}'
            json_en[key] = f'[TODO] {key}'
            filled_placeholder += 1
            print(f'   ⚠️  无映射，用占位符: {key}')
    print(f'   映射表填充: {filled_mapped} 个, 占位符: {filled_placeholder} 个')

    # ── 生成 Lang.cs 常量定义（**必须显式 --add-consts**，见文件头 / 登记 #52）──
    new_consts: list[tuple[str, str]] = []
    if add_consts:
        print(f'\n📝 生成 Lang.cs 常量定义 ...')
        existing_names = set(lang_consts.keys())
        for key in only_json:
            name = key_to_constant_name(key)
            # 避免重名
            base_name = name
            suffix = 0
            while name in existing_names:
                suffix += 1
                name = f'{base_name}_{suffix}'
            existing_names.add(name)
            new_consts.append((name, key))
        print(f'   生成 {len(new_consts)} 个新常量')
    else:
        print('\n⏭️  跳过「生成 Lang.cs 常量定义」'
              f'（{len(only_json)} 个键在 Lang.cs 里没有常量）')
        print('   要补常量请显式加 --add-consts —— 它会把 UnusedBaseline 从 1125 顶到 '
              f'≈{1125 + len(only_json)}；先读登记 #52 的 A/B 再决定')

    if dry_run:
        print('\n[Dry Run 模式] 不写入任何文件（落盘要显式加 --write）\n')
        if new_consts:
            print('新增常量预览（前 10 个）:')
            for name, key in new_consts[:10]:
                print(f'   public const string {name:<28} = "{key}";')
            if len(new_consts) > 10:
                print(f'   ... 还有 {len(new_consts) - 10} 个')
        print('\nJSON 新增键预览（前 10 个）:')
        for key in only_lang[:10]:
            if key in VALUE_MAP:
                print(f'   {key}: {VALUE_MAP[key][0]} / {VALUE_MAP[key][1]}')
            else:
                print(f'   {key}: [TODO] / [TODO]')
        return 0

    # ── 写入 JSON ──
    print(f'\n💾 写入 JSON ...')
    zh_n = save_json(zh_path, json_zh)
    en_n = save_json(en_path, json_en)
    print(f'   zh-CN: {zh_n} 个键')
    print(f'   en-US: {en_n} 个键')

    # ── 写入 Lang.cs ──
    if new_consts:
        print(f'\n💾 更新 Lang.cs ...')
        content = lang_path.read_text(encoding='utf-8')

        # 计算对齐宽度
        max_name_len = max(len(name) for name, _ in new_consts)
        align_width = max(max_name_len + 3, 30)

        lines = ['\n    // ── 自动同步（JSON 有但 Lang.cs 原无常量）──\n']
        for name, key in new_consts:
            padding = ' ' * (align_width - len(name))
            lines.append(f'    public const string {name}{padding}= "{key}";\n')
        new_code = ''.join(lines)

        # 在最后一个 } 前插入
        last_brace = content.rfind('}')
        if last_brace > 0:
            content = content[:last_brace] + new_code + content[last_brace:]
            lang_path.write_text(content, encoding='utf-8')
            print(f'   已添加 {len(new_consts)} 个常量')

    # ── 一致性检查 ──
    print(f'\n✅ 同步完成')
    final_lang = read_lang_constants(lang_path)
    final_lang_keys = set(final_lang.values())
    final_json_keys = set(json_zh.keys())

    remaining_only_lang = sorted(final_lang_keys - final_json_keys)
    remaining_only_json = sorted(final_json_keys - final_lang_keys)

    print(f'   Lang.cs 常量: {len(final_lang)}')
    print(f'   JSON 键数: {zh_n}')
    if remaining_only_lang:
        print(f'   ⚠️  仍在 Lang.cs 中缺失 JSON 的键: {len(remaining_only_lang)}')
        for k in remaining_only_lang:
            print(f'      - {k}')
    if remaining_only_json:
        print(f'   ⚠️  仍在 JSON 中缺失常量的键: {len(remaining_only_json)}')
        for k in remaining_only_json:
            print(f'      - {k}')
    if not remaining_only_lang and not remaining_only_json:
        print(f'   ✓ Lang.cs 与 JSON 完全一致')

    # 检查 [TODO] 占位符
    todo_keys = [k for k, v in json_zh.items() if v.startswith('[TODO]')]
    if todo_keys:
        print(f'\n⚠️  有 {len(todo_keys)} 个键使用了 [TODO] 占位符，需要人工翻译:')
        for k in todo_keys:
            print(f'   - {k}')

    return 0


if __name__ == '__main__':
    sys.exit(main())
