#!/usr/bin/env python3
"""
将 LocalizationService.cs 中 InitializeDefaults() 方法的内嵌字典
导出到 Strings.zh-CN.json 和 Strings.en-US.json。

用法:
    python export_localization.py [LocalizationService.cs 路径] [输出目录]

默认:
    LocalizationService.cs 路径 = src/HmiEditor.Core/Services/LocalizationService.cs
    输出目录 = src/HmiEditor.Core/Resources

特性:
    - 正则匹配 C# 字典初始化器 ["key"] = "value"
    - 正确处理 C# 转义序列 (\", \\, \n, \t, \r 等)
    - 通过大括号深度计数定位字典块边界（避免被字符串内的 { } 干扰）
    - 一致性检查：报告只在一边存在的键
    - 输出按键名字典序排序，保留 Unicode 字符（ensure_ascii=False）
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path


# ── C# 转义序列处理 ────────────────────────────────────────────────

_CSHARP_ESCAPES = {
    '"': '"',
    '\\': '\\',
    'n': '\n',
    't': '\t',
    'r': '\r',
    '0': '\0',
    'b': '\b',
    'f': '\f',
    'v': '\v',
    'a': '\a',
}


def unescape_csharp_string(s: str) -> str:
    """将 C# 字符串字面量中的转义序列还原为实际字符。"""
    result = []
    i = 0
    n = len(s)
    while i < n:
        ch = s[i]
        if ch == '\\' and i + 1 < n:
            nxt = s[i + 1]
            if nxt in _CSHARP_ESCAPES:
                result.append(_CSHARP_ESCAPES[nxt])
                i += 2
                continue
            # 未知转义：保留反斜杠 + 原字符
            result.append(ch)
            i += 1
        else:
            result.append(ch)
            i += 1
    return ''.join(result)


# ── 字典块定位（基于大括号深度，安全处理字符串内的 { } ）──────────

def _find_matching_brace(content: str, open_pos: int) -> int:
    """
    从 open_pos（指向 '{'）开始，向后查找匹配的 '}'。
    会跳过字符串字面量内的 { } 以避免误判。
    """
    depth = 0
    i = open_pos
    in_string = False
    escape = False
    n = len(content)

    while i < n:
        ch = content[i]
        if in_string:
            if escape:
                escape = False
            elif ch == '\\':
                escape = True
            elif ch == '"':
                in_string = False
        else:
            if ch == '"':
                in_string = True
            elif ch == '{':
                depth += 1
            elif ch == '}':
                depth -= 1
                if depth == 0:
                    return i
        i += 1

    raise ValueError("找不到匹配的 '}'，文件可能未闭合")


def extract_dict_block(content: str, var_name: str) -> str:
    """
    从文件内容中提取指定变量的字典初始化代码块（不含外层大括号）。

    匹配:
        var <var_name> = new Dictionary<string, string> { ... };
    """
    start_pattern = re.compile(
        rf'var\s+{re.escape(var_name)}\s*=\s*new\s+Dictionary\s*<\s*string\s*,\s*string\s*>\s*\{{',
        re.MULTILINE,
    )
    m = start_pattern.search(content)
    if not m:
        raise ValueError(f"找不到变量 {var_name} 的字典声明")

    open_pos = m.end() - 1  # 指向 '{'
    close_pos = _find_matching_brace(content, open_pos)
    return content[open_pos + 1:close_pos]


# ── 键值对解析 ────────────────────────────────────────────────────

# ["key"] = "value"
# value 可包含 \" 和 \\ 等转义（通过 (?:[^"\\]|\\.)* 匹配）
_ENTRY_RE = re.compile(
    r'\["([^"]+)"\]\s*=\s*"((?:[^"\\]|\\.)*)"',
    re.DOTALL,
)


def parse_dict_entries(block: str) -> dict[str, str]:
    """从字典初始化代码块中解析所有键值对。"""
    entries: dict[str, str] = {}
    for m in _ENTRY_RE.finditer(block):
        key = m.group(1)
        value = unescape_csharp_string(m.group(2))
        if key in entries:
            print(f"  ⚠️  重复键被覆盖: {key}")
        entries[key] = value
    return entries


# ── JSON 输出 ────────────────────────────────────────────────────

def write_json(entries: dict[str, str], output_path: Path) -> int:
    """按键名字典序排序后写入 JSON 文件，返回写入条数。"""
    sorted_entries = dict(sorted(entries.items()))
    with output_path.open('w', encoding='utf-8') as f:
        json.dump(sorted_entries, f, ensure_ascii=False, indent=2)
        f.write('\n')
    return len(sorted_entries)


# ── 主流程 ───────────────────────────────────────────────────────

def main() -> int:
    repo_root = Path(__file__).resolve().parent.parent
    default_cs = repo_root / "src" / "HmiEditor.Core" / "Services" / "LocalizationService.cs"
    default_out = repo_root / "src" / "HmiEditor.Core" / "Resources"

    cs_path = Path(sys.argv[1]).resolve() if len(sys.argv) > 1 else default_cs
    out_dir = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else default_out

    if not cs_path.is_file():
        print(f"✗ 找不到文件: {cs_path}")
        return 1
    if not out_dir.is_dir():
        print(f"✗ 输出目录不存在: {out_dir}")
        return 1

    print(f"📖 读取 {cs_path}")
    content = cs_path.read_text(encoding='utf-8')

    # 解析 zh-CN
    print("🔍 解析 zh-CN 字典 ...")
    zh_block = extract_dict_block(content, "zhCn")
    zh = parse_dict_entries(zh_block)
    print(f"   找到 {len(zh)} 个键")

    # 解析 en-US
    print("🔍 解析 en-US 字典 ...")
    en_block = extract_dict_block(content, "enUs")
    en = parse_dict_entries(en_block)
    print(f"   找到 {len(en)} 个键")

    # 一致性检查
    zh_keys, en_keys = set(zh), set(en)
    only_zh = sorted(zh_keys - en_keys)
    only_en = sorted(en_keys - zh_keys)
    if only_zh:
        print(f"\n⚠️  {len(only_zh)} 个键仅在 zh-CN 中存在:")
        for k in only_zh:
            print(f"   - {k}")
    if only_en:
        print(f"\n⚠️  {len(only_en)} 个键仅在 en-US 中存在:")
        for k in only_en:
            print(f"   - {k}")
    if not only_zh and not only_en:
        print("✓ 两边键集合完全一致")

    # 写入
    print("\n💾 写入 JSON 文件 ...")
    zh_path = out_dir / "Strings.zh-CN.json"
    en_path = out_dir / "Strings.en-US.json"
    zh_n = write_json(zh, zh_path)
    en_n = write_json(en, en_path)

    print(f"\n✅ 导出完成")
    print(f"   zh-CN: {zh_n} 条 → {zh_path}")
    print(f"   en-US: {en_n} 条 → {en_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
