"""服务入口：创建 FastAPI 应用并注册路由/异常处理。

启动方式：
  - 直接运行：python -m services.api.main
  - uvicorn：  uvicorn services.api.main:app --host 127.0.0.1 --port 8000
  - 一键脚本：./scripts/dev.sh
"""

from __future__ import annotations

from fastapi import FastAPI

from devkit_common.settings import get_settings
from .errors import register_exception_handlers, success_response
from .routers import analysis, build, deploy, logs


def create_app() -> FastAPI:
    """构建 FastAPI 应用实例（工厂模式，便于测试与部署）。"""
    settings = get_settings()
    app = FastAPI(
        title="代码助手自助服务体系（DevKit Services）",
        version=settings.version,
        description=(
            "为代码助手提供后端服务接口：代码静态分析 / 构建打包 / 自动部署 / 日志监控。\n\n"
            "鉴权：所有 /api/v1 接口需携带请求头 X-API-Key。\n"
            "统一响应：成功 {\"code\":\"OK\",\"data\":...}，失败 {\"code\":\"<错误码>\",\"message\":\"...\"}"
        ),
    )

    # 统一错误处理（必须在路由之前注册）
    register_exception_handlers(app)

    # 健康检查（免鉴权）
    @app.get("/health", tags=["系统"])
    def health() -> dict:
        """服务健康检查。"""
        return success_response(
            {
                "status": "ok",
                "version": settings.version,
                "services": ["analysis", "build", "deploy", "logs"],
            }
        )

    # 业务路由（统一挂载到 /api/v1，全部要求鉴权）
    for router in (analysis.router, build.router, deploy.router, logs.router):
        app.include_router(router, prefix="/api/v1", dependencies=[])

    return app


app = create_app()


def run() -> None:
    """以 uvicorn 启动服务（供 python -m services.api.main 调用）。"""
    import uvicorn

    settings = get_settings()
    uvicorn.run(
        "services.api.main:app",
        host=settings.host,
        port=settings.port,
        reload=False,
        log_level=settings.log_level.lower(),
    )


if __name__ == "__main__":
    run()
