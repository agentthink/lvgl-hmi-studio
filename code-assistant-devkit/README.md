# 代码助手自助式开发环境与服务体系（Code Assistant DevKit）

为开发代码助手（Agent）自身构建的一整套**可运行**的自助开发环境与服务体系，覆盖三大核心模块：

| 模块 | 说明 | 目录 |
| --- | --- | --- |
| **技能模块** | 能力清单（代码生成/调试修复/单元测试/文档编写/性能优化），含适用场景、输入输出契约、触发条件、调用关系，与智能编排器 | `skills/` |
| **环境模块** | 可复现的开发运行环境：依赖管理、环境变量、配置模板、跨平台（Windows/macOS/Linux）一键初始化与启动脚本 | `pyproject.toml`、`scripts/`、`config/` |
| **服务模块** | 后端服务接口：代码静态分析 / 构建打包 / 自动部署 / 日志监控，含 API 文档、统一错误处理、API Key 鉴权与示例调用 | `services/` |

> 三模块协同演示：运行 `make demo`（或 `python examples/run_demo_flow.py`）即可看到
> "修复缺陷 → 回归测试 → 生成文档 → 性能优化 → 静态分析 → 构建 → 部署 → 日志监控 → 回滚" 全链路真实执行。

## 快速开始

```bash
# 1. 一键初始化（创建虚拟环境 + 安装依赖 + 生成 .env）
./scripts/setup.sh            # Linux/macOS
powershell -ExecutionPolicy Bypass -File scripts/setup.ps1   # Windows

# 2. 启动服务（文档见 http://127.0.0.1:8000/docs）
./scripts/dev.sh              # 或 make dev

# 3. 跑测试 / 端到端演示 / 环境自检
make test                     # 全量测试
make demo                     # 三模块协同演示（生成 docs/outputs/demo-report.md）
make verify                   # 环境自检
```

## Agent 集成（CLI 与 MCP）

套件提供两种供 AI 助手 / 脚本直接调用的入口，无需 HTTP 服务常驻：

**命令行工具 `devkit-cli`**（安装后即用）：

```bash
devkit-cli health                                    # 配置与状态
devkit-cli analyze src/mod.py                        # 静态分析文件
devkit-cli analyze src/Mod.cs --deep                 # C# 深度检查（dotnet build 语义诊断）
devkit-cli analyze --source "def add(a,b): return a+b" --json   # 分析源码(JSON)
devkit-cli build examples/demo_project               # 构建打包
devkit-cli deploy .runtime/builds/demo-add-0.1.0.tar.gz --env staging --note "发布"
devkit-cli rollback --env staging                    # 回滚
devkit-cli logs --service build --limit 10           # 查询日志
devkit-cli skills-list                               # 技能能力清单
devkit-cli skills "修复这个报错" --context source=@src/mod.py   # 技能编排
```

**MCP Server `devkit-mcp`**（供支持 MCP 协议的客户端调用）：

```bash
devkit-mcp                                   # stdio 传输（默认，适合 MCP 客户端直接拉起）
devkit-mcp --transport http --port 9000      # streamable-http 传输（HTTP 端点 /mcp）
```

MCP 工具：`analyze` / `build` / `deploy` / `rollback` / `query_logs` / `tail_logs` /
`list_skills` / `run_skills`。需先安装可选依赖：`pip install -e '.[mcp]'`
（注意：mcp 固定 `>=1.0,<2.0`，2.x API 不兼容）。

> 详细说明见 [07-Agent集成指南](docs/07-Agent集成指南.md)。

**批量体检整个仓库**（含 C# 语义检查）：

```bash
python examples/scan_csharp.py                 # 启发式 + Roslyn 语法（默认扫 src/ 与 firmware/）
python examples/scan_csharp.py --deep          # + 项目级 dotnet build 语义检查（按项目分组构建）
```

CI：仓库根 `.workflow/devkit-check.yml` 提供 Gitee Go 流水线（push 自动跑测试 + 体检）。

## 目录结构

```
code-assistant-devkit/
├── README.md                     # 本文件
├── pyproject.toml                # 依赖管理与打包（pip install -e .）
├── requirements.txt              # 运行时依赖清单
├── .env.example                  # 环境变量模板（复制为 .env）
├── Makefile                      # 常用命令入口
├── config/
│   └── config.yaml.example       # 配置文件模板（复制为 config.yaml）
├── scripts/                      # 跨平台脚本（Linux/macOS 用 .sh，Windows 用 .ps1）
│   ├── setup.sh / setup.ps1      # 一键初始化
│   ├── dev.sh   / dev.ps1        # 启动服务
│   └── verify.sh                 # 环境自检
├── devkit_common/                # 公共层：三层配置加载（默认值/YAML/.env+环境变量）
│   ├── settings.py               #   配置定义与加载（DEVKIT_ 前缀）
│   ├── envfile.py                #   极简 .env 解析器（零依赖）
│   └── cli.py                    #   ★ devkit-cli 命令行入口（Agent 工具箱）
├── skills/                       # ★ 技能模块
│   ├── registry.json             #   能力清单：场景/输入输出/触发条件/调用关系
│   ├── core/
│   │   ├── skill_base.py         #   技能基类（SkillMeta/SkillResult/BaseSkill）
│   │   ├── registry.py           #   注册表：清单加载、触发条件匹配
│   │   └── orchestrator.py       #   编排器：意图检测 -> 链路规划 -> 顺序执行
│   └── {codegen,debug_fix,unit_test,doc_writing,perf_opt}/
│       └── skill_*.py            #   五个技能实现（纯标准库，零第三方依赖）
├── services/                     # ★ 服务模块
│   ├── core/                     #   业务核心（无框架依赖）
│   │   ├── analyzer.py           #   静态分析（Python AST / C# 启发式 + Roslyn 深度检查）
│   │   ├── builder.py            #   构建打包（sdist 风格 tar.gz）
│   │   ├── deployer.py           #   自动部署（多环境 + 回滚）
│   │   └── log_monitor.py        #   日志监控（JSON Lines 写入/查询）
│   ├── api/                      #   FastAPI 接口层
│   │   ├── main.py               #   应用工厂 + 入口
│   │   ├── auth.py               #   API Key 鉴权依赖
│   │   ├── errors.py             #   统一错误码与全局异常处理
│   │   ├── models.py             #   Pydantic 请求模型
│   │   └── routers/              #   analysis / build / deploy / logs 路由
│   └── mcp_server.py             #   ★ devkit-mcp：MCP Server（stdio / http 传输）
├── examples/                     # ★ 示例与演示
│   ├── call_all_services.py      #   服务调用示例（含错误场景）
│   ├── curl_examples.md          #   curl 调用示例
│   ├── run_demo_flow.py          #   三模块协同端到端演示
│   └── demo_project/             #   演示项目（含缺陷，供技能链处理）
├── tests/                        # pytest 测试（技能/服务/端到端）
└── docs/                         # 完整文档（见下）
```

## 文档导航

| 文档 | 内容 |
| --- | --- |
| [01-架构总览](docs/01-架构总览.md) | 三模块架构、数据流、目录说明 |
| [02-技能模块](docs/02-技能模块.md) | 能力清单表、技能契约、触发条件、调用关系图 |
| [03-环境模块](docs/03-环境模块.md) | 依赖管理、环境变量说明、配置优先级、跨平台脚本 |
| [04-服务模块-API文档](docs/04-服务模块-API文档.md) | 四个接口的请求/响应/错误码，鉴权与示例 |
| [05-配置与部署指南](docs/05-配置与部署指南.md) | 生产部署（systemd/Docker）、安全加固、运维 |
| [06-演示示例流程](docs/06-演示示例流程.md) | 三模块协同的完整示例流程（含真实运行输出） |
| [07-Agent集成指南](docs/07-Agent集成指南.md) | devkit-cli / devkit-mcp 接入 AI 助手、工具清单与调用示例 |

## 设计要点

- **技能即契约**：每个技能用 `SkillMeta` 声明能力、输入输出与调用关系，注册表（`registry.json`）即"能力清单"，新增技能 = 新增一个类 + 注册一行。
- **编排即流水线**：`SkillOrchestrator` 把自然语言诉求映射为技能链（含"修复后必须回归"这类**后置校验**约束），前一技能输出自动成为下游输入，失败即停。
- **服务即 API**：统一响应信封 `{"code":"OK","data":...}` / `{"code":"<错误码>","message":"..."}`，统一鉴权（`X-API-Key`），统一异常处理，接口与核心逻辑分层解耦。
- **配置即三层覆盖**：默认值 < `config/config.yaml` < `.env` + 环境变量（`DEVKIT_` 前缀），开箱即用、可复现。
