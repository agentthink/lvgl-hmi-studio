"""日志监控服务（log_monitor）：JSON Lines 日志写入与查询。

日志文件：{work_dir}/logs/app.log，每行一条 JSON 记录：
  {"ts": "2026-08-15T12:00:00+08:00", "service": "analysis", "level": "INFO", "message": "...", ...extra}

查询支持：level / service / keyword / since 过滤，limit 截断，按时间倒序返回。
说明：本实现为单进程追加写（带进程内锁）；多进程场景建议替换为集中式日志（如 Loki/ELK）。
"""

from __future__ import annotations

import json
import threading
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Dict, List, Optional

from devkit_common.settings import get_settings

_LOCK = threading.Lock()  # 单进程内保证追加写原子性

_LEVELS = {"DEBUG", "INFO", "WARN", "ERROR", "FATAL"}


def _log_file() -> Path:
    """日志文件路径（确保目录存在）。"""
    settings = get_settings()
    settings.log_dir.mkdir(parents=True, exist_ok=True)
    return settings.log_file


def write_log(service: str, level: str = "INFO", message: str = "", **extra: Any) -> None:
    """写入一条日志。

    Args:
        service: 来源服务名（analysis/build/deploy/logs/技能名）
        level: 级别 DEBUG/INFO/WARN/ERROR/FATAL
        message: 日志内容
        extra: 附加结构化字段
    """
    level = level.upper() if level.upper() in _LEVELS else "INFO"
    record: Dict[str, Any] = {
        "ts": datetime.now(timezone.utc).astimezone().isoformat(timespec="seconds"),
        "service": service,
        "level": level,
        "message": message,
    }
    record.update(extra)
    with _LOCK:
        with open(_log_file(), "a", encoding="utf-8") as f:
            f.write(json.dumps(record, ensure_ascii=False) + "\n")


def query_logs(
    level: str | None = None,
    service: str | None = None,
    keyword: str | None = None,
    since: str | None = None,
    limit: int = 50,
) -> Dict[str, Any]:
    """查询日志（按时间倒序）。

    Args:
        level: 按级别过滤
        service: 按服务名过滤
        keyword: 按关键词过滤（message 子串）
        since: ISO 时间，只返回该时间之后的日志
        limit: 最多返回条数

    Returns:
        {"total": 匹配总数, "returned": 返回条数, "logs": [...]}
    """
    path = _log_file()
    logs: List[Dict[str, Any]] = []
    if path.exists():
        for line in path.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            try:
                record = json.loads(line)
            except json.JSONDecodeError:
                continue  # 跳过损坏行
            if level and record.get("level") != level.upper():
                continue
            if service and record.get("service") != service:
                continue
            if keyword and keyword not in record.get("message", ""):
                continue
            if since and record.get("ts", "") < since:
                continue
            logs.append(record)

    logs.reverse()  # 最新在前
    total = len(logs)
    return {"total": total, "returned": min(total, limit), "logs": logs[:limit]}


def tail_logs(lines: int = 20) -> List[Dict[str, Any]]:
    """返回文件末尾若干条日志（不过滤）。"""
    return query_logs(limit=lines)["logs"]


def clear_logs() -> int:
    """清空日志文件，返回删除条数。"""
    path = _log_file()
    if not path.exists():
        return 0
    count = len(path.read_text(encoding="utf-8").splitlines())
    with _LOCK:
        path.write_text("", encoding="utf-8")
    return count
