"""构建打包路由：POST /api/v1/build、GET /api/v1/build/{build_id}。"""

from __future__ import annotations

from pathlib import Path

from fastapi import APIRouter, Depends

from services.api.auth import require_api_key
from services.api.errors import AppError, ErrorCode, success_response
from services.api.models import BuildRequest
from services.core import builder, log_monitor

router = APIRouter(prefix="/build", tags=["构建打包"])


@router.post("")
def build(req: BuildRequest, _key: str = Depends(require_api_key)) -> dict:
    """构建并打包项目，产出 tar.gz 产物。

    - 请求体：{"project_dir": "/path/to/project", "output_dir": null}
    - 响应：{"code": "OK", "data": {build_id, artifact_path, ...}}
    - 项目须含 pyproject.toml 或 *.csproj（无则自动用目录名构建）
    """
    project_dir = Path(req.project_dir)
    has_manifest = (project_dir / "pyproject.toml").exists() or bool(list(project_dir.glob("*.csproj")))
    if not project_dir.exists() or not has_manifest:
        raise AppError(
            ErrorCode.BUILD_INVALID_PROJECT,
            f"项目目录无效或缺少 pyproject.toml/*.csproj: {project_dir}",
        )
    try:
        record = builder.build_project(project_dir, req.output_dir)
    except FileNotFoundError as exc:
        raise AppError(ErrorCode.BUILD_INVALID_PROJECT, str(exc)) from exc

    log_monitor.write_log(
        "build", "INFO",
        f"构建完成: {record['name']}-{record['version']}（{record['file_count']} 个文件）",
        build_id=record["build_id"],
        artifact=record["artifact_path"],
    )
    return success_response(record)


@router.get("/{build_id}")
def get_build(build_id: str, _key: str = Depends(require_api_key)) -> dict:
    """按 build_id 查询构建记录。"""
    record = builder.get_build(build_id)
    if record is None:
        raise AppError(ErrorCode.BUILD_NOT_FOUND, f"构建记录不存在: {build_id}", status_code=404)
    return success_response(record)
