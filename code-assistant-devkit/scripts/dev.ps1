# =============================================================
# 启动服务（Windows / PowerShell，开发模式）
# 用法：powershell -ExecutionPolicy Bypass -File scripts/dev.ps1
# =============================================================
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")

# 加载 .env（若存在）
if (Test-Path ".env") {
    Get-Content ".env" | ForEach-Object {
        if ($_ -match "^\s*([^#=]+)=(.*)$") {
            [Environment]::SetEnvironmentVariable($matches[1].Trim(), $matches[2].Trim(), "Process")
        }
    }
}

$HOST = if ($env:DEVKIT_HOST) { $env:DEVKIT_HOST } else { "127.0.0.1" }
$PORT = if ($env:DEVKIT_PORT) { $env:DEVKIT_PORT } else { "8000" }

$PYTHON = ".venv\Scripts\python.exe"
if (-not (Test-Path $PYTHON)) {
    Write-Host "!! 未找到虚拟环境，请先运行 .\scripts\setup.ps1"
    exit 1
}

Write-Host "==> 启动服务: http://$HOST`:$PORT  （文档: /docs）"
& $PYTHON -m uvicorn services.api.main:app --host $HOST --port $PORT
