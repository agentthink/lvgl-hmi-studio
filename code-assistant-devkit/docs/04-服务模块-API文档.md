# 04 · 服务模块 API 文档

## 1. 概览

- Base URL：`http://<host>:<port>`（默认 `http://127.0.0.1:8000`）
- 自动生成文档：`GET /docs`（Swagger UI）
- 鉴权：除 `/health` 外，所有接口要求请求头 `X-API-Key: <key>`（密钥见 `DEVKIT_API_KEYS`）

## 2. 统一响应格式

**成功**：

```json
{ "code": "OK", "data": { ... } }
```

**失败**（统一错误信封，`code` 为错误码）：

```json
{ "code": "AUTH_INVALID_KEY", "message": "API Key 无效，请检查密钥或联系管理员" }
```

## 3. 统一错误码

| 错误码 | HTTP | 含义 |
| --- | --- | --- |
| `AUTH_MISSING_KEY` | 401 | 缺少 `X-API-Key` 请求头 |
| `AUTH_INVALID_KEY` | 401 | API Key 无效 |
| `NOT_FOUND` | 404 | 路由/资源不存在 |
| `VALIDATION_ERROR` | 422 | 请求体参数校验失败（FastAPI 自动） |
| `ANALYSIS_EMPTY_SOURCE` | 400 | source 与 file_path 都未提供 |
| `BUILD_INVALID_PROJECT` | 400 | 项目目录无效或缺少 pyproject.toml |
| `BUILD_NOT_FOUND` | 404 | 构建记录不存在 |
| `DEPLOY_ARTIFACT_NOT_FOUND` | 404 | 构建产物文件不存在 |
| `DEPLOY_INVALID_ARTIFACT_FORMAT` | 400 | 产物不是 .tar.gz |
| `DEPLOY_INVALID_ENV` | 400 | 非法环境名（仅 dev/staging/prod） |
| `DEPLOY_NO_HISTORY` | 400 | 部署历史不足，无法回滚 |
| `DEPLOY_RECORD_NOT_FOUND` | 404 | 部署记录不存在 |
| `INTERNAL_ERROR` | 500 | 未预期异常（已记录日志，不泄露细节） |

## 4. 接口定义

### 4.0 健康检查（免鉴权）

```
GET /health
```

```json
{ "code": "OK", "data": { "status": "ok", "version": "1.0.0", "services": ["analysis", "build", "deploy", "logs"] } }
```

### 4.1 静态分析

```
POST /api/v1/analysis
Authorization: X-API-Key: <key>
Content-Type: application/json

请求体（source 与 file_path 二选一）：
{
  "source": "def add(a, b)\n    return a + b",   // 或
  "file_path": "examples/demo_project/src/demo_add/__init__.py"
}
```

响应 `data`：

```json
{
  "filename": "...", "ok": true, "sloc": 3, "function_count": 1,
  "avg_function_lines": 2.0, "syntax_error": null,
  "issues": [ { "type": "undefined_name", "lineno": 2, "severity": "高", "message": "..." } ],
  "functions": [ { "name": "add", "lineno": 1, "lines": 2, "complexity": 1, "params": ["a", "b"] } ]
}
```

### 4.2 构建打包

```
POST /api/v1/build
{
  "project_dir": "examples/demo_project",   // 必填，须含 pyproject.toml
  "output_dir": null                        // 可选，产物输出目录
}
```

响应 `data`：`build_id / name / version / artifact_path / artifact_size / file_count / compile_errors / created_at`

```
GET /api/v1/build/{build_id}                // 查询构建记录
```

### 4.3 自动部署

```
POST /api/v1/deploy
{
  "artifact_path": ".runtime/builds/demo-add-0.1.0.tar.gz",  // 必填
  "env": "staging",         // dev / staging / prod
  "note": "发布说明（可选）"
}
```

响应 `data`：`deploy_id / env / version / release_dir / artifact / note / deployed_at / history_len`

```
GET  /api/v1/deploy/{deploy_id}?env=staging   // 查询部署记录
POST /api/v1/deploy/rollback                  // 回滚
     { "env": "staging", "deploy_id": null }  // deploy_id 缺省 = 回退到上一版本
GET  /api/v1/deploy/list/all?env=staging      // 部署历史
```

### 4.4 日志监控

```
GET /api/v1/logs?level=INFO&service=build&keyword=完成&since=2026-08-15T00:00:00&limit=10
GET /api/v1/logs/tail?lines=20
```

响应 `data`：`{ "total": 42, "returned": 10, "logs": [ { "ts", "service", "level", "message", ... } ] }`

## 5. 调用示例

Python（`examples/call_all_services.py` 完整版）：

```python
import httpx

BASE_URL, API_KEY = "http://127.0.0.1:8000", "dev-key-001"
headers = {"X-API-Key": API_KEY}

# 静态分析
resp = httpx.post(f"{BASE_URL}/api/v1/analysis",
                  json={"source": "def add(a, b)\n    return a + b"},
                  headers=headers)
print(resp.json())   # {"code": "OK", "data": {...}}
```

curl（`examples/curl_examples.md` 完整版）：

```bash
curl -s -X POST http://127.0.0.1:8000/api/v1/analysis \
  -H "X-API-Key: dev-key-001" -H "Content-Type: application/json" \
  -d '{"source": "def add(a, b)\n    return a + b"}'
```

## 6. 鉴权与安全

```python
# services/api/auth.py（核心逻辑）
def require_api_key(x_api_key: str | None = Header(default=None, alias="X-API-Key")) -> str:
    if not x_api_key:
        raise AppError(ErrorCode.AUTH_MISSING, "缺少鉴权头：请在请求头携带 X-API-Key", status_code=401)
    if x_api_key not in get_settings().api_keys:
        raise AppError(ErrorCode.AUTH_INVALID, "API Key 无效", status_code=401)
    return x_api_key
```

所有业务路由通过 `Depends(require_api_key)` 声明鉴权；扩展 JWT/mTLS 时只需替换该依赖，路由无需改动。
