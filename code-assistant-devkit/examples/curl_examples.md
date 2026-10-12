# 服务接口 · curl 调用示例

> 前提：服务已启动（`./scripts/dev.sh` 或 `make dev`），默认地址 `http://127.0.0.1:8000`。

## 鉴权约定

所有 `/api/v1/*` 接口需要在请求头携带 API Key：

```bash
KEY="dev-key-001"
AUTH="X-API-Key: $KEY"
```

## 0. 健康检查（免鉴权）

```bash
curl -s http://127.0.0.1:8000/health | python3 -m json.tool
```

## 1. 静态分析

```bash
# 方式一：直接提交源码
curl -s -X POST http://127.0.0.1:8000/api/v1/analysis \
  -H "$AUTH" -H "Content-Type: application/json" \
  -d '{"source": "def add(a, b)\n    return a + b"}' | python3 -m json.tool

# 方式二：分析本地文件
curl -s -X POST http://127.0.0.1:8000/api/v1/analysis \
  -H "$AUTH" -H "Content-Type: application/json" \
  -d '{"file_path": "examples/demo_project/src/demo_add/__init__.py"}' | python3 -m json.tool
```

## 2. 构建打包

```bash
curl -s -X POST http://127.0.0.1:8000/api/v1/build \
  -H "$AUTH" -H "Content-Type: application/json" \
  -d '{"project_dir": "examples/demo_project"}' | python3 -m json.tool

# 按 build_id 查询构建记录（把 <BUILD_ID> 替换为返回的 build_id）
curl -s http://127.0.0.1:8000/api/v1/build/<BUILD_ID> -H "$AUTH" | python3 -m json.tool
```

## 3. 自动部署与回滚

```bash
# 部署（把 <ARTIFACT> 替换为构建返回的 artifact_path）
curl -s -X POST http://127.0.0.1:8000/api/v1/deploy \
  -H "$AUTH" -H "Content-Type: application/json" \
  -d '{"artifact_path": "<ARTIFACT>", "env": "staging", "note": "演示部署"}' | python3 -m json.tool

# 回滚到上一版本
curl -s -X POST http://127.0.0.1:8000/api/v1/deploy/rollback \
  -H "$AUTH" -H "Content-Type: application/json" \
  -d '{"env": "staging"}' | python3 -m json.tool

# 查看部署历史
curl -s "http://127.0.0.1:8000/api/v1/deploy/list/all?env=staging" -H "$AUTH" | python3 -m json.tool
```

## 4. 日志监控

```bash
# 按条件过滤查询
curl -s "http://127.0.0.1:8000/api/v1/logs?level=INFO&service=deploy&limit=10" -H "$AUTH" | python3 -m json.tool

# 查看最近 20 条
curl -s "http://127.0.0.1:8000/api/v1/logs/tail?lines=20" -H "$AUTH" | python3 -m json.tool
```

## 5. 错误场景演示

```bash
# 缺少鉴权头 -> 401 AUTH_MISSING_KEY
curl -s http://127.0.0.1:8000/api/v1/analysis -X POST -d '{"source": "x"}'

# 无效密钥 -> 401 AUTH_INVALID_KEY
curl -s http://127.0.0.1:8000/api/v1/analysis -X POST -H "X-API-Key: wrong" -d '{"source": "x"}'

# 资源不存在 -> 404 NOT_FOUND（统一错误格式）
curl -s http://127.0.0.1:8000/api/v1/nothing -H "X-API-Key: dev-key-001"
```
