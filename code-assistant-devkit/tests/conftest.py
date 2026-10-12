"""pytest 公共配置：统一运行时目录，保证测试可重复执行。"""

from __future__ import annotations

import os
from pathlib import Path

# 让全部测试使用仓库内 .runtime 作为运行时目录（该目录已被 .gitignore 忽略）
# 必须在任何 services 模块导入前设置（get_settings 首次调用时读取）
os.environ.setdefault("DEVKIT_WORK_DIR", str(Path(__file__).resolve().parents[1] / ".runtime"))
