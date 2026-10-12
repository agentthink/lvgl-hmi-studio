"""日志监控路由：GET /api/v1/logs、GET /api/v1/logs/tail。"""

from __future__ import annotations

from typing import Optional

from fastapi import APIRouter, Depends, Query

from services.api.auth import require_api_key
from services.api.errors import success_response
from services.core import log_monitor

router = APIRouter(prefix="/logs", tags=["日志监控"])


@router.get("")
def query_logs(
    level: Optional[str] = Query(None, description="级别过滤 DEBUG/INFO/WARN/ERROR/FATAL"),
    service: Optional[str] = Query(None, description="服务名过滤"),
    keyword: Optional[str] = Query(None, description="关键词过滤（message 子串）"),
    since: Optional[str] = Query(None, description="起始时间（ISO 8601）"),
    limit: int = Query(50, ge=1, le=500, description="返回条数上限"),
    _key: str = Depends(require_api_key),
) -> dict:
    """按条件查询日志（倒序返回）。

    示例：GET /api/v1/logs?level=INFO&service=build&keyword=完成&limit=10
    """
    result = log_monitor.query_logs(
        level=level,
        service=service,
        keyword=keyword,
        since=since,
        limit=limit,
    )
    return success_response(result)


@router.get("/tail")
def tail_logs(
    lines: int = Query(20, ge=1, le=200, description="返回条数"),
    _key: str = Depends(require_api_key),
) -> dict:
    """返回最近 N 条日志（不过滤）。"""
    return success_response(log_monitor.tail_logs(lines))
