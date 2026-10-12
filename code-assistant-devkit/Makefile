# =============================================================
# 代码助手开发套件 · Makefile 常用命令
# 用法（Linux/macOS）：make setup && make demo
# Windows 用户请直接使用 scripts/ 下的 .ps1 脚本
# =============================================================

.PHONY: setup dev test demo verify clean

# 一键初始化：创建虚拟环境 + 安装依赖 + 环境自检
setup:
	./scripts/setup.sh

# 启动服务（开发模式）
dev:
	./scripts/dev.sh

# 运行全部测试
test:
	.venv/bin/python -m pytest -q tests/

# 运行端到端演示（技能链 -> 分析 -> 构建 -> 部署 -> 日志）
demo:
	.venv/bin/python examples/run_demo_flow.py

# 环境自检：健康检查 + 能力清单
verify:
	./scripts/verify.sh

# 清理运行时数据与缓存（不删除虚拟环境）
clean:
	rm -rf .runtime .pytest_cache
	find . -type d -name __pycache__ -prune -exec rm -rf {} +
