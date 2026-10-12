"""鉴权方案：基于 API Key 的简单有效鉴权。

实现：
  - 客户端在请求头携带 X-API-Key: <key>
  - 密钥列表来自配置 SERVICE_API_KEYS / DEVKIT_API_KEYS（逗号分隔）
  - 未携带 -> 401 AUTH_MISSING_KEY；无效 -> 401 AUTH_INVALID_KEY

安全建议（详见 docs/05-配置与部署指南.md）：
  - 生产环境使用强随机密钥（openssl rand -hex 32）
  - 需要更高安全级别时，可在此处扩展为 JWT / mTLS，接口签名不变
"""

from __future__ import annotations

from typing import Optional

from fastapi import Header

from devkit_common.settings import get_settings
from .errors import AppError, ErrorCode


def require_api_key(x_api_key: Optional[str] = Header(default=None, alias="X-API-Key")) -> str:
    """FastAPI 依赖：校验请求头 API Key，返回通过校验的密钥。"""
    if not x_api_key:
        raise AppError(
            ErrorCode.AUTH_MISSING,
            "缺少鉴权头：请在请求头携带 X-API-Key",
            status_code=401,
        )
    if x_api_key not in get_settings().api_keys:
        raise AppError(
            ErrorCode.AUTH_INVALID,
            "API Key 无效，请检查密钥或联系管理员",
            status_code=401,
        )
    return x_api_key
