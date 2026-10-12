# =============================================================
# 一键初始化环境（Windows / PowerShell）
# 功能：创建虚拟环境 -> 安装依赖 -> 生成 .env -> 环境自检
# 用法：powershell -ExecutionPolicy Bypass -File scripts/setup.ps1
# =============================================================
$ErrorActionPreference = "Stop"

# 切换到项目根目录
Set-Location (Join-Path $PSScriptRoot "..")
$ROOT = Get-Location
Write-Host "==> 项目根目录: $ROOT"

# 1) 选择 Python 解释器
$PYTHON = if ($env:PYTHON) { $env:PYTHON } else { "python" }
try {
    & $PYTHON --version
} catch {
    Write-Host "!! 未找到 python，请先安装 Python 3.10+ 并加入 PATH"
    exit 1
}

# 2) 创建虚拟环境
if (-not (Test-Path ".venv")) {
    Write-Host "==> 创建虚拟环境 .venv ..."
    & $PYTHON -m venv .venv
}
& ".venv\Scripts\Activate.ps1"

# 3) 安装依赖
Write-Host "==> 安装依赖（requirements.txt + 本项目 editable）..."
python -m pip install --upgrade pip -q
python -m pip install -r requirements.txt -q
python -m pip install -e . -q

# 4) 生成本地 .env
if (-not (Test-Path ".env")) {
    Copy-Item ".env.example" ".env"
    Write-Host "==> 已从模板生成 .env（含默认开发密钥）"
}

# 5) 环境自检
Write-Host "==> 运行环境自检..."
python -c "import skills, services, devkit_common; print('导入 OK')"

Write-Host ""
Write-Host "✔ 初始化完成！下一步："
Write-Host "   启动服务:   .\scripts\dev.ps1"
Write-Host "   跑全量测试:  python -m pytest -q tests\"
Write-Host "   端到端演示:  python examples\run_demo_flow.py"
