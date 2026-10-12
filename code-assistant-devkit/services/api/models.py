"""API 请求/响应模型（Pydantic v2）。"""

from __future__ import annotations

from typing import Any, Dict, List, Optional

from pydantic import BaseModel, Field


class AnalysisRequest(BaseModel):
    """静态分析请求：源码与文件路径二选一。"""

    source: Optional[str] = Field(None, description="源码内容（与 file_path 二选一）")
    file_path: Optional[str] = Field(None, description="待分析文件路径（与 source 二选一）")
    options: Dict[str, Any] = Field(default_factory=dict, description="扩展选项（预留）")
    deep: bool = Field(False, description="C# 文件是否执行项目级 dotnet build 语义检查")


class BuildRequest(BaseModel):
    """构建打包请求。"""

    project_dir: str = Field(..., description="项目根目录（须含 pyproject.toml）")
    output_dir: Optional[str] = Field(None, description="产物输出目录（默认取配置）")


class DeployRequest(BaseModel):
    """自动部署请求。"""

    artifact_path: str = Field(..., description="构建产物 .tar.gz 路径")
    env: str = Field("staging", description="目标环境：dev/staging/prod")
    note: str = Field("", description="部署备注")


class RollbackRequest(BaseModel):
    """回滚请求。"""

    env: str = Field("staging", description="目标环境")
    deploy_id: Optional[str] = Field(None, description="回滚到的部署记录 id（缺省回退上一版本）")
