"""静态分析路由：POST /api/v1/analysis。"""

from __future__ import annotations

from fastapi import APIRouter, Depends

from services.api.auth import require_api_key
from services.api.errors import AppError, ErrorCode, success_response
from services.api.models import AnalysisRequest
from services.core import analyzer, log_monitor

router = APIRouter(prefix="/analysis", tags=["静态分析"])


@router.post("")
def analyze(req: AnalysisRequest, _key: str = Depends(require_api_key)) -> dict:
    """对源码或文件执行静态分析，返回结构化报告。

    - 请求体：{"source": "...", "file_path": null} 或 {"source": null, "file_path": "/path/x.py"}
    - 响应：{"code": "OK", "data": {报告}}
    """
    if req.source is None and req.file_path is None:
        raise AppError(ErrorCode.ANALYSIS_EMPTY, "source 与 file_path 至少提供一个")

    if req.file_path:
        if not req.source:
            report = analyzer.analyze_file(req.file_path, deep=req.deep)
        else:
            report = analyzer.analyze_source(req.source, filename=req.file_path)
    else:
        report = analyzer.analyze_source(req.source or "", filename="<内存源码>")

    log_monitor.write_log(
        "analysis", "INFO",
        f"分析完成: {report['filename']}（{report['sloc']} 行, {len(report['issues'])} 个问题）",
        ok=report["ok"],
        issue_count=len(report["issues"]),
    )
    return success_response(report)
