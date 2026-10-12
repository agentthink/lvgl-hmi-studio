# 07 · Agent 集成指南（配合 AI 助手开发）

本指南说明如何把代码助手开发套件接入 AI 助手（Agent）的工作流，让 Agent 直接调用
静态分析 / 构建打包 / 自动部署 / 日志监控 / 技能编排能力，作为开发工具箱使用。

## 1. 两种接入方式

| 方式 | 入口 | 适用场景 | 依赖 |
| --- | --- | --- | --- |
| **CLI 工具** | `devkit-cli` | Agent / 脚本在本地直接调用，无需常驻进程 | 无额外依赖 |
| **MCP Server** | `devkit-mcp` | 支持 MCP 协议的 Agent 客户端（工具发现/调用） | `mcp>=1.0,<2.0` |

两种方式都直接调用套件核心（`services/core`），**不依赖 FastAPI 服务常驻**；
且都会写入统一运行日志（`{work_dir}/logs/app.log`），保证操作可追踪。

## 2. 安装

```bash
cd code-assistant-devkit
./scripts/setup.sh                                  # 基础环境（含 devkit-cli）
pip install -e '.[mcp]'                             # 额外安装 MCP 支持（devkit-mcp）
```

## 3. CLI 用法（推荐日常使用）

```bash
devkit-cli health                                   # 环境自检
devkit-cli analyze <文件>                           # 静态分析文件
devkit-cli analyze --source "..." --json            # 分析源码，JSON 输出
devkit-cli build <项目目录> [--output 目录]         # 构建打包（tar.gz）
devkit-cli deploy <产物.tar.gz> [--env staging] [--note "备注"]
devkit-cli rollback [--env staging] [--deploy-id ...]
devkit-cli logs [--level INFO] [--service build] [--keyword 关键词] [--limit 50]
devkit-cli logs --tail 20                           # 最近 20 条
devkit-cli skills-list                              # 能力清单
devkit-cli skills "诉求文本" [--context source=@文件路径] [--skill 技能名]
```

要点：

- 所有子命令支持 `--json` 输出原始 JSON（可放在子命令前或后），便于机器解析。
- `--context KEY=@path` 表示从文件读取内容（如 `source=@src/mod.py`）。
- 错误输出到 stderr 并返回非零退出码；成功返回 0。
- CLI 与 FastAPI 服务的操作都会写入统一日志，可用 `devkit-cli logs` 查询。

### 典型链路

```bash
# 体检 -> 构建 -> 部署 -> 查日志
devkit-cli analyze examples/demo_project/src/demo_add/__init__.py
devkit-cli build examples/demo_project
devkit-cli deploy .runtime/builds/demo-add-0.1.0.tar.gz --env staging --note "v0.1.0"
devkit-cli logs --service deploy
```

## 4. MCP 用法

### 4.1 stdio 传输（默认，推荐给 MCP 客户端）

```bash
devkit-mcp
```

在 MCP 客户端配置中注册：

```json
{
  "mcpServers": {
    "devkit": {
      "command": "/绝对路径/code-assistant-devkit/.venv/bin/devkit-mcp",
      "args": []
    }
  }
}
```

### 4.2 streamable-http 传输

```bash
devkit-mcp --transport http --port 9000
# 端点：http://127.0.0.1:9000/mcp
```

### 4.3 暴露的工具

| 工具 | 说明 | 主要参数 |
| --- | --- | --- |
| `analyze` | 静态分析（Python AST / C# 启发式） | `file_path` / `source`（`@path` 读文件）/ `filename` |
| `build` | 构建打包项目 | `project_dir` / `output_dir` |
| `deploy` | 部署构建产物 | `artifact_path` / `env` / `note` |
| `rollback` | 回滚部署 | `env` / `deploy_id` |
| `query_logs` | 查询运行日志 | `level` / `service` / `keyword` / `since` / `limit` |
| `tail_logs` | 最近 N 条日志 | `lines` |
| `list_skills` | 技能能力清单 | — |
| `run_skills` | 技能编排（意图→链路→执行） | `request` / `context`(JSON) / `explicit` |

所有工具返回 JSON 字符串。

### 4.4 工具调用示例（HTTP）

```bash
# 1) initialize（获取 Mcp-Session-Id）
curl -si -X POST http://127.0.0.1:9000/mcp \
  -H "Accept: application/json, text/event-stream" -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"cli","version":"1.0"}}}'

# 2) 携带 Mcp-Session-Id 调用工具
curl -s -X POST http://127.0.0.1:9000/mcp \
  -H "Accept: application/json, text/event-stream" -H "Content-Type: application/json" \
  -H "Mcp-Session-Id: <上一步返回的会话ID>" \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"analyze","arguments":{"source":"def add(a,b): return a+b"}}}'
```

## 5. 与 FastAPI 服务的关系

```
                    ┌─────────────────────────────────┐
                    │        services/core/*          │ 业务核心（唯一实现）
                    └──────────┬──────────────────────┘
          ┌────────────────────┼──────────────────────┐
          ▼                    ▼                      ▼
   FastAPI 接口层      devkit-cli（CLI）      devkit-mcp（MCP Server）
   /api/v1/*           bash 直接调用            MCP 客户端调用
   (HTTP + API Key)    (进程内调用)              (stdio / streamable-http)
```

三层共享同一核心与统一日志，行为一致；按场景选择接入方式。

## 6. 配合 AI 助手开发的建议

- **代码体检**：Agent 修改代码后，用 `devkit-cli analyze <file>` 快速检查语法/未定义名称，
  比人工 review 更快的基线；`--json` 便于结构化消费。
- **构建发布**：`build -> deploy -> rollback` 形成最小发布闭环，Agent 可在测试通过后自动执行。
- **可追踪**：所有 CLI/MCP 操作写入统一日志，Agent 可用 `logs` 复盘自己或他人的操作。
- **技能编排**：`skills` / `run_skills` 演示了"意图→链路→执行"模式；对复杂任务，
  Agent 可借鉴其"后置校验"（如修复后必须回归）思想组织自己的工具链。

> 语言支持：`analyze` 自动检测 python / csharp（按扩展名或内容特征）；`build` 支持
> pyproject.toml（Python）与 *.csproj（C#，提取 PackageId/AssemblyName 与 Version，
> 无则用目录名）；`codegen` 支持生成 python / csharp 代码（`--context language=csharp`）。
>
> **C# 深度检查（Roslyn）**：`analyze <文件>` 对 .cs 文件自动叠加 Roslyn 真实语法检查
> （`tools/roslyn_check/`，C# 单文件编译诊断，需 dotnet SDK）。纯语法诊断
> （SyntaxTree 级）可靠，作为"高"严重度问题计入报告；编译诊断（含语义/引用）
> 可能因缺项目引用误报，仅放入 `compile` 字段供参考。dotnet 不可用时自动跳过。
>
> **批量体检**：`python examples/scan_csharp.py [目录...]`（默认扫 src/ 与 firmware/），
> 启发式 + Roslyn 语法；加 `--deep` 时按所属项目分组执行 `dotnet build`，
> 每个项目只构建一次，检出编译器判定的语义错误与警告。
>
> **深度语义检查（--deep）**：`analyze <C#文件> --deep` 对文件所属项目执行
> `dotnet build`（含完整项目引用上下文），编译器判定的语义错误（类型不匹配/
> 签名错误等，如 CS0029）计入"高"严重度，结果放入 `deep` 字段。需 dotnet SDK
> 与可构建的项目；未找到项目或构建失败时返回原因并跳过。
>
> **CI 集成**：仓库根 `.workflow/devkit-check.yml`（Gitee Go 流水线）在 push 到 main
> 时自动执行 devkit 测试与 C# 全仓库体检；CI 环境无 dotnet 时 Roslyn/深度检查
> 自动跳过（启发式仍执行）。在 Gitee 仓库「流水线」页面启用后生效。
