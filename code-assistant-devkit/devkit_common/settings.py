"""统一配置加载器：三层合并，优先级从低到高。

  1. 内置默认值（Settings 构造默认参数）
  2. config/config.yaml（若存在，仅覆盖已声明字段）
  3. .env 文件（自动向上查找）
  4. 进程环境变量（DEVKIT_ 前缀，优先级最高）

所有环境变量命名约定：DEVKIT_<字段名大写>，例如 DEVKIT_PORT、DEVKIT_API_KEYS。
"""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from functools import lru_cache
from pathlib import Path
from typing import List

from .envfile import load_dotenv

# 项目根目录（devkit_common/ 的上一级）
PROJECT_ROOT = Path(__file__).resolve().parents[1]

# .env 中的布尔/列表类型标记，用于字符串到类型的转换
_TRUE_VALUES = {"1", "true", "yes", "on"}


@dataclass(frozen=True)
class Settings:
    """全局配置。字段均为只读，保证运行时不被意外修改。"""

    host: str = "127.0.0.1"          # 服务监听地址（部署到服务器时改为 0.0.0.0）
    port: int = 8000                 # 服务端口
    api_keys: List[str] = field(default_factory=lambda: ["dev-key-001", "dev-key-002"])
    # 鉴权密钥列表，逗号分隔；生产环境务必替换为强随机值
    work_dir: str = ".runtime"       # 运行时数据根目录（构建产物/日志/部署记录）
    log_level: str = "INFO"          # 日志级别
    version: str = "1.0.0"           # 套件版本号

    # ---- 派生目录（相对 work_dir，按需创建） ----
    @property
    def build_dir(self) -> Path:
        """构建产物目录（tar.gz 归档存放处）。"""
        return Path(self.work_dir) / "builds"

    @property
    def releases_dir(self) -> Path:
        """部署发布目录（按环境分子目录）。"""
        return Path(self.work_dir) / "releases"

    @property
    def log_dir(self) -> Path:
        """运行日志目录（JSON Lines 格式）。"""
        return Path(self.work_dir) / "logs"

    @property
    def tmp_dir(self) -> Path:
        """临时目录（单元测试生成的临时文件等）。"""
        return Path(self.work_dir) / "tmp"

    def ensure_dirs(self) -> None:
        """确保所有运行时目录存在。"""
        for d in (self.build_dir, self.releases_dir, self.log_dir, self.tmp_dir):
            d.mkdir(parents=True, exist_ok=True)

    @property
    def log_file(self) -> Path:
        """统一日志文件（服务调用与技能运行都会写入）。"""
        return self.log_dir / "app.log"


def _parse_api_keys(raw: str) -> List[str]:
    """把逗号分隔的密钥字符串解析为列表，去掉空项与空白。"""
    return [k.strip() for k in raw.split(",") if k.strip()]


def _coerce(name: str, raw: str, current):
    """根据默认值的类型把字符串环境变量转换为对应类型。"""
    if isinstance(current, bool):
        return raw.lower() in _TRUE_VALUES
    if isinstance(current, int):
        return int(raw)
    return raw


def _load_yaml_config() -> dict:
    """读取 config/config.yaml（若存在）。使用标准库无法解析 YAML 时优雅降级。"""
    yaml_path = PROJECT_ROOT / "config" / "config.yaml"
    if not yaml_path.exists():
        return {}
    try:
        # 延迟导入：pyyaml 是可选依赖，未安装时仅提示忽略
        import yaml  # type: ignore
        with open(yaml_path, encoding="utf-8") as f:
            data = yaml.safe_load(f) or {}
        return data if isinstance(data, dict) else {}
    except Exception:  # noqa: BLE001 —— 配置缺失不应阻断启动
        return {}


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    """获取全局唯一配置实例（进程内缓存）。

    优先级：默认值 < config/config.yaml < .env 文件 < 环境变量。
    """
    # 1) 先应用 .env 到 os.environ（不覆盖已存在的真实环境变量）
    load_dotenv()
    env = os.environ

    # 2) 从默认值出发，依次叠加 YAML 与 DEVKIT_ 环境变量
    defaults = Settings()
    merged = {
        "host": defaults.host,
        "port": defaults.port,
        "api_keys": list(defaults.api_keys),
        "work_dir": defaults.work_dir,
        "log_level": defaults.log_level,
        "version": defaults.version,
    }

    # 2a) YAML 配置（仅覆盖已知字段）
    for key, value in _load_yaml_config().items():
        if key in merged and value is not None:
            merged[key] = value

    # 2b) DEVKIT_ 环境变量（最高优先级，含 .env 注入的）
    for key in list(merged.keys()):
        env_key = f"DEVKIT_{key.upper()}"
        if env_key in env:
            merged[key] = _coerce(key, env[env_key], merged[key])

    # api_keys 若是字符串（来自 YAML/环境变量），统一转为列表
    if isinstance(merged["api_keys"], str):
        merged["api_keys"] = _parse_api_keys(merged["api_keys"])

    settings = Settings(**merged)
    settings.ensure_dirs()
    return settings
