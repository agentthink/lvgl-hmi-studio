"""极简 .env 文件解析器（零依赖，标准库实现）。

支持格式：
  KEY=VALUE          普通键值
  KEY="value"        带引号的值（自动去除首尾引号）
  # 注释             # 开头的行忽略
  KEY=              空值

不依赖 python-dotenv，便于在任何 Python 环境直接运行。
"""

from __future__ import annotations

import os
from pathlib import Path
from typing import Dict, Optional


def parse_dotenv(text: str) -> Dict[str, str]:
    """解析 .env 文件文本为字典。"""
    result: Dict[str, str] = {}
    for raw_line in text.splitlines():
        line = raw_line.strip()
        # 跳过空行与注释
        if not line or line.startswith("#"):
            continue
        # 只取第一个 '=' 之前作为键名
        if "=" not in line:
            continue
        key, _, value = line.partition("=")
        key = key.strip()
        value = value.strip()
        # 去除成对引号
        if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
            value = value[1:-1]
        result[key] = value
    return result


def load_dotenv(path: Optional[str | os.PathLike] = None) -> Dict[str, str]:
    """加载 .env 文件；文件不存在时返回空字典。

    Args:
        path: .env 文件路径；默认从当前目录向上查找 .env。

    Returns:
        解析出的键值对字典。
    """
    if path is not None:
        p = Path(path)
    else:
        # 从当前工作目录逐级向上查找 .env
        p = _find_env_file(Path.cwd())
    if p is None or not p.exists():
        return {}
    return parse_dotenv(p.read_text(encoding="utf-8"))


def _find_env_file(start: Path) -> Optional[Path]:
    """向上逐级查找 .env 文件。"""
    for d in [start, *start.parents]:
        candidate = d / ".env"
        if candidate.exists():
            return candidate
    return None


def apply_to_env(values: Dict[str, str], overwrite: bool = False) -> None:
    """把解析出的键值写入 os.environ（不覆盖已有值，除非 overwrite=True）。"""
    for key, value in values.items():
        if overwrite or key not in os.environ:
            os.environ[key] = value
