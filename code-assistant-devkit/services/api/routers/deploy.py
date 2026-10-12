"""自动部署路由：POST /api/v1/deploy、GET /api/v1/deploy/{deploy_id}、POST 回滚、GET 历史。"""

from __future__ import annotations

from pathlib import Path

from fastapi import APIRouter, Depends

from services.api.auth import require_api_key
from services.api.errors import AppError, ErrorCode, success_response
from services.api.models import DeployRequest, RollbackRequest
from services.core import deployer, log_monitor

router = APIRouter(prefix="/deploy", tags=["自动部署"])


@router.post("")
def deploy(req: DeployRequest, _key: str = Depends(require_api_key)) -> dict:
    """部署构建产物到指定环境。

    - 请求体：{"artifact_path": "/path/demo-add-0.1.0.tar.gz", "env": "staging", "note": "..."}
    - 响应：{"code": "OK", "data": {deploy_id, version, release_dir, ...}}
    """
    artifact = Path(req.artifact_path)
    if not artifact.exists():
        raise AppError(ErrorCode.DEPLOY_ARTIFACT_MISSING, f"构建产物不存在: {artifact}", status_code=404)
    if artifact.suffix != ".gz" or not artifact.name.endswith(".tar.gz"):
        raise AppError(ErrorCode.DEPLOY_INVALID_FORMAT, "产物必须是 .tar.gz 格式")
    try:
        record = deployer.deploy(artifact, req.env, req.note)
    except (ValueError, RuntimeError) as exc:
        raise AppError(ErrorCode.DEPLOY_INVALID_ENV, str(exc)) from exc

    log_monitor.write_log(
        "deploy", "INFO",
        f"部署完成: {record['version']} -> {record['env']}",
        deploy_id=record["deploy_id"],
        release_dir=record["release_dir"],
    )
    return success_response(record)


@router.get("/{deploy_id}")
def get_deploy(deploy_id: str, env: str = "staging", _key: str = Depends(require_api_key)) -> dict:
    """按 deploy_id 查询部署记录。"""
    try:
        record = deployer.get_deploy(deploy_id, env)
    except ValueError as exc:
        raise AppError(ErrorCode.DEPLOY_INVALID_ENV, str(exc)) from exc
    if record is None:
        raise AppError(ErrorCode.DEPLOY_RECORD_MISSING, f"部署记录不存在: {deploy_id}", status_code=404)
    return success_response(record)


@router.post("/rollback")
def rollback(req: RollbackRequest, _key: str = Depends(require_api_key)) -> dict:
    """回滚：恢复到上一个版本或指定部署。

    - 请求体：{"env": "staging", "deploy_id": null}
    - 响应：{"code": "OK", "data": {回滚后的当前部署}}
    """
    try:
        current = deployer.rollback(req.env, req.deploy_id)
    except ValueError as exc:
        raise AppError(ErrorCode.DEPLOY_INVALID_ENV, str(exc)) from exc
    except (KeyError, RuntimeError) as exc:
        raise AppError(ErrorCode.DEPLOY_NO_HISTORY, str(exc)) from exc

    log_monitor.write_log("deploy", "WARN", f"回滚完成: 当前版本 -> {current['version']} ({req.env})")
    return success_response(current)


@router.get("/list/all")
def list_deploys(env: str = "staging", _key: str = Depends(require_api_key)) -> dict:
    """列出环境全部部署历史。"""
    try:
        history = deployer.list_deploys(env)
    except ValueError as exc:
        raise AppError(ErrorCode.DEPLOY_INVALID_ENV, str(exc)) from exc
    return success_response(history)
