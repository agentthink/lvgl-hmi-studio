"""统一错误处理：错误码体系 + 全局异常处理器。

约定：
  - 所有错误响应格式统一为 {"code": "...", "message": "...", "details": ...}
  - 业务错误抛 AppError（可携带 HTTP 状态码、错误码、附加信息）
  - 未知异常兜底为 500（并记录日志），绝不把堆栈泄露给调用方
"""

from __future__ import annotations

from typing import Any, Optional

from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse
from starlette.exceptions import HTTPException as StarletteHTTPException


class ErrorCode:
    """统一错误码（前缀按服务域划分）。"""

    # 通用
    INTERNAL = "INTERNAL_ERROR"            # 未预期异常
    VALIDATION = "VALIDATION_ERROR"        # 参数校验失败
    NOT_FOUND = "NOT_FOUND"                # 资源不存在
    # 鉴权
    AUTH_MISSING = "AUTH_MISSING_KEY"      # 缺少 API Key
    AUTH_INVALID = "AUTH_INVALID_KEY"      # API Key 无效
    # 静态分析
    ANALYSIS_EMPTY = "ANALYSIS_EMPTY_SOURCE"
    ANALYSIS_FILE_MISSING = "ANALYSIS_FILE_NOT_FOUND"
    # 构建
    BUILD_INVALID_PROJECT = "BUILD_INVALID_PROJECT"   # 缺少 pyproject.toml
    BUILD_NOT_FOUND = "BUILD_NOT_FOUND"
    # 部署
    DEPLOY_ARTIFACT_MISSING = "DEPLOY_ARTIFACT_NOT_FOUND"
    DEPLOY_INVALID_FORMAT = "DEPLOY_INVALID_ARTIFACT_FORMAT"
    DEPLOY_INVALID_ENV = "DEPLOY_INVALID_ENV"
    DEPLOY_NO_HISTORY = "DEPLOY_NO_HISTORY"
    DEPLOY_RECORD_MISSING = "DEPLOY_RECORD_NOT_FOUND"


class AppError(Exception):
    """业务异常：统一错误响应。"""

    def __init__(
        self,
        code: str,
        message: str,
        status_code: int = 400,
        details: Optional[Any] = None,
    ) -> None:
        super().__init__(message)
        self.code = code
        self.message = message
        self.status_code = status_code
        self.details = details


def error_response(code: str, message: str, details: Any = None) -> dict:
    """统一的错误响应体。"""
    body: dict = {"code": code, "message": message}
    if details is not None:
        body["details"] = details
    return body


def success_response(data: Any = None) -> dict:
    """统一的成功响应体。"""
    return {"code": "OK", "data": data}


def register_exception_handlers(app: FastAPI) -> None:
    """向 FastAPI 应用注册全部异常处理器。"""
    from services.core import log_monitor  # 延迟导入避免循环

    @app.exception_handler(AppError)
    async def handle_app_error(request: Request, exc: AppError) -> JSONResponse:
        """业务错误：按错误码返回对应状态码。"""
        log_monitor.write_log("api", "WARN", f"{request.method} {request.url.path} -> {exc.code}: {exc.message}")
        return JSONResponse(
            status_code=exc.status_code,
            content=error_response(exc.code, exc.message, exc.details),
        )

    @app.exception_handler(StarletteHTTPException)
    async def handle_http_error(request: Request, exc: StarletteHTTPException) -> JSONResponse:
        """框架 HTTP 异常（404/405 等）统一格式。"""
        return JSONResponse(
            status_code=exc.status_code,
            content=error_response(
                "NOT_FOUND" if exc.status_code == 404 else "HTTP_ERROR",
                str(exc.detail),
            ),
        )

    @app.exception_handler(Exception)
    async def handle_unexpected(request: Request, exc: Exception) -> JSONResponse:
        """兜底异常：记录日志，返回 500，不泄露内部细节。"""
        log_monitor.write_log(
            "api", "ERROR",
            f"{request.method} {request.url.path} 未预期异常: {exc!r}",
        )
        return JSONResponse(
            status_code=500,
            content=error_response(ErrorCode.INTERNAL, "服务内部错误，请查看服务日志"),
        )
