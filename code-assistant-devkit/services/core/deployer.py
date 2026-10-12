"""自动部署服务（deployer）：把构建产物解压部署到目标环境，支持回滚。

部署模型：
  - releases/{env}/ 下按版本保存解压后的产物目录
  - releases/{env}/state.json 记录部署历史（current + history）
  - 部署 = 解压产物到 releases/{env}/{version}/，更新 state
  - 回滚 = 从历史中取上一个版本，更新 current（目录已存在，零拷贝切换）

说明：本实现为"本地/单机模拟部署"；对接真实环境时，
     将 extract 步骤替换为 scp/rsync/容器镜像推送即可，接口保持不变。
"""

from __future__ import annotations

import json
import shutil
import tarfile
import uuid
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List, Optional

from devkit_common.settings import get_settings

# 部署环境白名单
_VALID_ENVS = {"dev", "staging", "prod"}


def _state_path(env: str) -> Path:
    """环境状态文件：releases/{env}/state.json。"""
    return get_settings().releases_dir / env / "state.json"


def _read_state(env: str) -> Dict[str, Any]:
    """读取环境部署状态（无历史时返回空结构）。"""
    path = _state_path(env)
    if not path.exists():
        return {"env": env, "current": None, "history": []}
    return json.loads(path.read_text(encoding="utf-8"))


def _write_state(env: str, state: Dict[str, Any]) -> None:
    """写回部署状态。"""
    _state_path(env).parent.mkdir(parents=True, exist_ok=True)
    _state_path(env).write_text(
        json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8"
    )


def _validate_env(env: str) -> str:
    """校验环境名。"""
    if env not in _VALID_ENVS:
        raise ValueError(f"非法环境 {env!r}，可选: {', '.join(_VALID_ENVS)}")
    return env


def deploy(
    artifact_path: str | Path,
    target_env: str = "staging",
    note: str = "",
) -> Dict[str, Any]:
    """部署一个构建产物（tar.gz）到指定环境。

    Args:
        artifact_path: 构建产物路径
        target_env: 目标环境 dev/staging/prod
        note: 部署备注

    Returns:
        {"deploy_id", "env", "version", "release_dir", "note", "deployed_at", "history_len"}
    """
    _validate_env(target_env)
    artifact = Path(artifact_path)
    if not artifact.exists():
        raise FileNotFoundError(f"构建产物不存在: {artifact}")
    if artifact.suffix != ".gz" or not artifact.name.endswith(".tar.gz"):
        raise ValueError(f"产物必须是 .tar.gz 格式: {artifact.name}")

    # 版本号 = 文件名 {name}-{version}.tar.gz 中的 version 段
    stem = artifact.name[: -len(".tar.gz")]          # e.g. demo-add-0.1.0
    version = stem.rsplit("-", 1)[-1]

    # 解压到 releases/{env}/{version}/
    env_dir = get_settings().releases_dir / target_env
    release_dir = env_dir / version
    if release_dir.exists():
        shutil.rmtree(release_dir)                    # 同版本重复部署则覆盖
    release_dir.mkdir(parents=True, exist_ok=True)
    with tarfile.open(artifact, "r:gz") as tar:
        try:
            tar.extractall(release_dir, filter="data")   # filter="data"：安全解压，拒绝路径穿越
        except TypeError:                                # Python 3.10 及以下无 filter 参数
            tar.extractall(release_dir)

    # 更新状态
    state = _read_state(target_env)
    deploy_id = uuid.uuid4().hex[:12]
    record = {
        "deploy_id": deploy_id,
        "env": target_env,
        "version": version,
        "release_dir": str(release_dir),
        "artifact": artifact.name,
        "note": note,
        "deployed_at": datetime.now().isoformat(),
    }
    state["current"] = record
    state["history"].append(record)
    _write_state(target_env, state)

    return {**record, "history_len": len(state["history"])}


def get_deploy(deploy_id: str, target_env: str = "staging") -> Optional[Dict[str, Any]]:
    """按 deploy_id 查询部署记录。"""
    state = _read_state(_validate_env(target_env))
    for record in state.get("history", []):
        if record["deploy_id"] == deploy_id:
            return record
    return None


def rollback(target_env: str = "staging", deploy_id: str | None = None) -> Dict[str, Any]:
    """回滚到指定部署（默认回滚到上一个版本）。

    Args:
        target_env: 环境
        deploy_id: 目标部署记录 id；不传则回退到当前记录的前一条

    Returns:
        回滚后的 current 记录。
    """
    _validate_env(target_env)
    state = _read_state(target_env)
    history = state.get("history", [])
    if len(history) < 2:
        raise RuntimeError(f"环境 {target_env} 的历史不足，无法回滚（至少需要 2 次部署）")

    if deploy_id:
        target = next((r for r in history if r["deploy_id"] == deploy_id), None)
        if target is None:
            raise KeyError(f"部署记录不存在: {deploy_id}")
    else:
        # 默认回退：当前记录的前一条
        current_id = (state.get("current") or {}).get("deploy_id")
        idx = next((i for i, r in enumerate(history) if r["deploy_id"] == current_id), len(history) - 1)
        target = history[max(0, idx - 1)]

    state["current"] = target
    state["history"].append(
        {
            "deploy_id": uuid.uuid4().hex[:12],
            "env": target_env,
            "version": target["version"],
            "release_dir": target["release_dir"],
            "artifact": target.get("artifact", ""),
            "note": f"回滚至 {target['version']}",
            "deployed_at": datetime.now().isoformat(),
        }
    )
    _write_state(target_env, state)
    return state["current"]


def list_deploys(target_env: str = "staging") -> List[Dict[str, Any]]:
    """列出环境全部部署历史。"""
    return _read_state(_validate_env(target_env)).get("history", [])
