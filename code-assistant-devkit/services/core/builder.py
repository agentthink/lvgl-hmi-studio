"""构建打包服务（builder）：对项目做语法校验并产出 sdist 风格 tar.gz 产物。

支持的项目类型：
  - Python：须含 pyproject.toml（tomllib 读取 name/version，compileall 语法编译）
  - C# / 其他：无 pyproject.toml 时自动降级——优先解析 *.csproj 的
    PackageId/AssemblyName 与 Version；否则用目录名 + "0.0.0"。
    语法校验仅对 *.py 文件执行（compileall），C# 文件不做编译检查。

流程：
  1. 校验项目结构并读取元数据（name/version）
  2. 对 *.py 文件做语法编译校验，收集错误
  3. 打包：pyproject.toml / *.csproj / README* / 源码目录（src、包目录）→ tar.gz
  4. 记录构建档案到 builds/builds.json（供查询与追踪）

产物命名：{name}-{version}.tar.gz；构建记录：build_id（UUID 短码）。
"""

from __future__ import annotations

import compileall
import io
import json
import re
import tarfile
import tomllib
import uuid
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, List

from devkit_common.settings import get_settings

# 打包时排除的目录/文件
_EXCLUDED_DIRS = {".git", ".venv", "__pycache__", ".runtime", "build", "dist", ".pytest_cache", "node_modules"}
_EXCLUDED_SUFFIXES = {".pyc", ".pyo", ".log", ".tmp"}

# .csproj 元数据正则
_CS_NAME_RE = re.compile(r"<PackageId>\s*([^<]+)\s*</PackageId>|<AssemblyName>\s*([^<]+)\s*</AssemblyName>")
_CS_VERSION_RE = re.compile(r"<Version>\s*([^<]+)\s*</Version>")


def _read_metadata(project_dir: Path) -> Dict[str, str]:
    """读取项目元数据：优先 pyproject.toml，降级 .csproj，最后目录名。

    返回 {"name", "version"}；目录无效（两者皆无）时 name 用目录名。
    """
    pyproject = project_dir / "pyproject.toml"
    if pyproject.exists():
        data = tomllib.loads(pyproject.read_text(encoding="utf-8"))
        project = data.get("project", {})
        name = project.get("name", project_dir.name)
        version = project.get("version", "0.0.0")
        return {"name": name, "version": str(version)}

    # 降级：.csproj（C# 项目）
    csproj_files = sorted(project_dir.glob("*.csproj"))
    if csproj_files:
        text = csproj_files[0].read_text(encoding="utf-8")
        name_m = _CS_NAME_RE.search(text)
        version_m = _CS_VERSION_RE.search(text)
        name = (name_m.group(1) or name_m.group(2) or csproj_files[0].stem).strip()
        version = version_m.group(1).strip() if version_m else "0.0.0"
        return {"name": name, "version": version}

    # 兜底：目录名
    return {"name": project_dir.name, "version": "0.0.0"}


def _collect_files(project_dir: Path) -> List[Path]:
    """收集待打包文件（排除虚拟环境/缓存等）。"""
    files: List[Path] = []
    for p in sorted(project_dir.rglob("*")):
        if not p.is_file():
            continue
        rel = p.relative_to(project_dir)
        parts = rel.parts
        if any(part in _EXCLUDED_DIRS for part in parts):
            continue
        if p.suffix.lower() in _EXCLUDED_SUFFIXES:
            continue
        files.append(p)
    return files


def build_project(project_dir: str | Path, output_dir: str | Path | None = None) -> Dict[str, Any]:
    """构建项目并产出 tar.gz 产物。

    Args:
        project_dir: 项目根目录（须含 pyproject.toml 或 *.csproj，二者皆无时用目录名）
        output_dir: 产物输出目录（默认取配置 build_dir）

    Returns:
        {
          "build_id", "name", "version", "artifact_path", "artifact_size",
          "file_count", "compile_errors": [...], "created_at"
        }
    """
    settings = get_settings()
    pdir = Path(project_dir).resolve()
    out_dir = Path(output_dir) if output_dir else settings.build_dir
    out_dir.mkdir(parents=True, exist_ok=True)

    # 1) 结构校验 + 元数据
    meta = _read_metadata(pdir)
    name, version = meta["name"], meta["version"]

    # 2) 语法编译校验（compileall 捕获全部语法错误）
    compile_errors: List[str] = []
    compileall.compile_dir(str(pdir), force=True, quiet=2)
    # compileall 返回 False 说明存在语法错误；再用递归扫描精确收集
    for py in pdir.rglob("*.py"):
        if any(part in _EXCLUDED_DIRS for part in py.relative_to(pdir).parts):
            continue
        try:
            compile(py.read_text(encoding="utf-8"), str(py), "exec")
        except SyntaxError as exc:
            compile_errors.append(f"{py.name}:{exc.lineno} {exc.msg}")

    # 3) 打包为 tar.gz（sdist 风格：顶层目录 {name}-{version}/）
    files = _collect_files(pdir)
    artifact_name = f"{name}-{version}.tar.gz"
    artifact_path = out_dir / artifact_name
    top = f"{name}-{version}"
    with tarfile.open(artifact_path, "w:gz") as tar:
        for f in files:
            tar.add(f, arcname=f"{top}/{f.relative_to(pdir).as_posix()}")
        # 写入一份构建元信息
        info = {
            "build_id": "",
            "name": name,
            "version": version,
            "built_at": datetime.now().isoformat(),
        }
        info_bytes = json.dumps(info, ensure_ascii=False, indent=2).encode("utf-8")
        tarinfo = tarfile.TarInfo(f"{top}/BUILD_INFO.json")
        tarinfo.size = len(info_bytes)
        tar.addfile(tarinfo, io.BytesIO(info_bytes))

    # 4) 记录构建档案
    build_id = uuid.uuid4().hex[:12]
    record = {
        "build_id": build_id,
        "name": name,
        "version": version,
        "artifact_path": str(artifact_path),
        "artifact_size": artifact_path.stat().st_size,
        "file_count": len(files),
        "compile_errors": compile_errors,
        "created_at": datetime.now().isoformat(),
    }
    _append_build_record(record)
    return record


def _build_records_file() -> Path:
    """构建档案存储文件。"""
    return get_settings().build_dir / "builds.json"


def _append_build_record(record: Dict[str, Any]) -> None:
    """把构建记录追加到 builds.json。"""
    path = _build_records_file()
    records = []
    if path.exists():
        records = json.loads(path.read_text(encoding="utf-8"))
    records.append(record)
    path.write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding="utf-8")


def get_build(build_id: str) -> Dict[str, Any] | None:
    """按 build_id 查询构建记录。"""
    path = _build_records_file()
    if not path.exists():
        return None
    for record in json.loads(path.read_text(encoding="utf-8")):
        if record["build_id"] == build_id:
            return record
    return None
