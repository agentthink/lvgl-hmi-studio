# SDK 修复同步方案（sdk-fixes）团队推广指南

> 解决 `firmware/Tina-Linux/` 中被 git 忽略路径（`build/`、`out/` 等）的本地修复
> **无法入库、易丢失、难交接**的问题，让 SDK 修复像普通代码一样可提交、可评审、可追溯。

---

## 1. 背景与问题

Tina-Linux SDK 体积庞大且含大量构建产物，仓库通过 `firmware/.gitignore` 忽略了 `build/`、
`out/` 等目录。但**并非忽略目录下的修改都不重要**——例如：

| 修复文件 | 解决的问题 |
| --- | --- |
| `build/kernel-defaults.mk` | 内核 Kconfig 新增选项（NEW）导致 `syncconfig` 交互卡死，构建无法无人值守完成 |

这类修复曾经只存在于本地 SDK，带来三个隐患：

1. **无法入库**：`git status` 永远干净，修改不留痕，代码评审看不到
2. **易丢失**：清理工作区、切换机器、`git clean` 后修复静默消失
3. **难交接**：新人/新构建机搭环境时，不知道要补哪些修复，烧出问题的固件还不明原因

## 2. 方案原理

在仓库**可跟踪目录** `firmware/sdk-fixes/` 维护修复文件的副本 + 一个一键同步脚本：

```
firmware/sdk-fixes/
├── README.md                 本文件
├── kernel-defaults.mk        修复文件副本（内容 = SDK 中修复后的文件）
└── sync-sdk-fixes.sh         同步脚本（备份 + 复制 + 一致性校验）
```

**核心思想**：SDK 内所有本地修复都必须先在 `sdk-fixes/` 落一份副本并提交入库；
新环境/构建机通过脚本把副本批量还原到 SDK 对应路径。

```
   [仓库] sdk-fixes/ 副本（可入库）                    [本地] Tina-Linux/ 实际文件（被忽略）
   ┌──────────────────────────┐   sync-sdk-fixes.sh   ┌──────────────────────────┐
   │ kernel-defaults.mk       │ ────────────────────► │ build/kernel-defaults.mk │
   └──────────────────────────┘   备份→复制→校验      └──────────────────────────┘
```

脚本特性：

- **清单驱动**：`FILES` 数组 `"SDK相对路径|本目录文件名"`，新增修复追加一行即可
- **自动定位**：脚本通过自身相对路径推导 SDK 目录，无需传参
- **自动备份**：SDK 原文件与副本不一致时先备份 `*.sdk.bak.<时间戳>`
- **一致性校验**：同步后逐文件 `cmp` 验证，失败即非零退出

## 3. 快速开始（新环境接入）

```bash
# 1. 克隆仓库
git clone https://gitee.com/air_magic/zhishu-hmi.git
cd zhishu-hmi

# 2. 一键同步 SDK 修复（要求 firmware/Tina-Linux 已就位）
./firmware/sdk-fixes/sync-sdk-fixes.sh
```

预期输出：

```
SDK 目录: /home/xxx/zhishu-hmi/firmware/Tina-Linux
SYNC:   build/kernel-defaults.mk
OK:     build/kernel-defaults.mk
完成: 同步 1 个文件, 校验失败 0
```

## 4. 日常使用流程

### 4.1 修改了一个被忽略路径下的 SDK 文件

```bash
# 1. 修改 SDK 文件（例如 build/kernel-defaults.mk）
vim firmware/Tina-Linux/build/kernel-defaults.mk

# 2. 验证修改有效（编译/构建测试）

# 3. 把修复后的文件同步为仓库副本并提交推送
cp firmware/Tina-Linux/build/kernel-defaults.mk firmware/sdk-fixes/kernel-defaults.mk
git add firmware/sdk-fixes/kernel-defaults.mk
git commit -m "fix(firmware): kernel 构建修复说明"
git push origin main
```

**铁律：只要改了被忽略路径下的 SDK 文件，就必须同步副本 + 提交，禁止只改本地。**

### 4.2 新增一个被忽略路径下的修复文件

```bash
# 1. 把修复后的文件复制到 sdk-fixes/
cp firmware/Tina-Linux/build/xxx.mk firmware/sdk-fixes/xxx.mk

# 2. 在脚本 FILES 清单追加一行
#    vim firmware/sdk-fixes/sync-sdk-fixes.sh
#    FILES=(
#      "build/kernel-defaults.mk|kernel-defaults.mk"
#      "build/xxx.mk|xxx.mk"          # ← 新增
#    )

# 3. 提交推送
git add firmware/sdk-fixes/
git commit -m "feat(firmware): 新增 xxx 修复"
git push origin main
```

## 5. 团队推广规范

### 5.1 谁需要这套流程

- **固件开发成员**：所有 SDK 内修改的负责人，是副本同步的第一责任人
- **构建机/CI**：每次构建前执行一次同步脚本，确保构建环境包含全部修复
- **新人/新环境**：接入第一条命令就是同步脚本

### 5.2 提交流程

```
修改 SDK → 本地验证 → 同步副本 → git 提交（含修复说明）→ 推送 → 通知团队拉取
```

提交信息建议采用仓库既有风格 `类型(模块): 描述 — 细节`：

```text
fix(firmware): kernel-defaults.mk 构建收敛 — SetInitramfs 注入后 olddefconfig
```

### 5.3 评审要点

Code Review 时重点核对：

- [ ] 副本与 SDK 实际修改一致（`cmp` 无差异）
- [ ] 修复必要性有说明（为什么改、不改变砖/卡死的现象）
- [ ] 是否涉及 `build/`、`out/` 之外的其他忽略路径
- [ ] 同步脚本 `FILES` 清单是否同步更新

### 5.4 变更通知

每次推送 SDK 修复后，在团队群内 @固件成员：

> 已推送 SDK 修复：`build/kernel-defaults.mk`（内核构建收敛）。
> 请重新拉代码后运行 `./firmware/sdk-fixes/sync-sdk-fixes.sh`。

## 6. 故障排查

| 现象 | 原因 | 处理 |
| --- | --- | --- |
| `SKIP: 缺少修复源文件` | 副本文件缺失（可能误删/未同步提交） | 从 git 历史恢复副本，或补一份 |
| `FAIL: 与修复源不一致` | SDK 文件被本地改动覆盖 | 用脚本备份的 `.sdk.bak.*` 检查差异，重跑同步 |
| 同步后构建仍异常 | 修复被 SDK 升级/重建回退 | 确认 SDK 版本与修复基线一致；必要时重新提取副本 |
| 构建卡在 Kconfig 交互 | 旧内核 .config 残留 NEW 项 | 手动 `make olddefconfig` 收敛后重跑；长期靠 kernel-defaults.mk 修复 |

## 7. 最佳实践与后续演进

- **保持修复最小化**：只入库必要的 SDK 修改，避免把实验性改动带进副本
- **定期全量校验**：每次构建前运行脚本（幂等，重复执行无副作用）
- **CI 集成（可选）**：构建脚本开头加入 `bash firmware/sdk-fixes/sync-sdk-fixes.sh`
  并对 `FAIL` 非零退出，把"漏同步"变成构建错误而非烧砖事故
- **演进方向**：
  - 修复文件从"整文件副本"演进为"patch 形式"，便于合并 SDK 官方升级
  - 增加 `--verify` 只校验不同步、`--diff` 查看差异等子命令
