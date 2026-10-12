#!/usr/bin/env python3
"""门禁读数摘要：从一跑 `scripts/ci-gate.sh` 的日志里抽出**收口表格要用的每一个数**。

为什么它该在仓里（而不是某个临时目录）：
    收口的每条读数都要能指回**日志正文**，而**量具自己是最容易读错的一环**。阶段 118 实测
    同一个采集脚本连错两处、**两处都不报错**：
      ① 数行数用 `len(text.split("\\n"))` —— 末尾换行会**多算一个空元素** ⇒ 打出「850 行」，
         真值是 **849**；
      ② 逐程序集读数的正则要求 `持续时间:` 后面直接是数字 ＋ ` - `，而真形态是
         `持续时间: 1 s - X.dll`（秒数后面还有单位 ` s`）⇒ **10 条一条都没匹配上**，
         脚本却照打「**合计 0**」—— 看起来像「没有数据」，而不是「我读错了」。
    ⇒ 本脚本把两条纪律固化下来：
      ① 行数一律 `splitlines()`；
      ② 关键项**匹配不到就报警并以非零退出** —— 绝不静默产出 `0` / 空。

⚠ 本脚本是**只读探针**：不判定、不写任何文件。门禁本身在 `scripts/ci-gate.sh`。

⚠ 「被并发挤行的固定标记行」：门禁有 9 次 `dotnet test` 调用（8 个项目 ＋ 1 次 git 专用段；
阶段 148 起 `DockPanelInvocationUiTests` 回到大队 ⇒ 少一次），各自打印一行
    `总共 1 个测试文件与指定模式相匹配。`；并发写同一管道时，其中两行会被**挤成一行**
    （另多一个空行）⇒ 非空行数在 `gate1` / `gate2` 上比 `gate3` / `gate4` 少 **1**。
    这是**结构性**的、不是内容差（阶段 115 立的规矩：先归一 ＋ 再比多重集）。

用法：
    python3 scripts/gate-readings.py <日志> [<日志> …]    # 逐份摘要
    python3 scripts/gate-readings.py --selftest            # 自检：3 档
"""
from __future__ import annotations

import io
import re
import sys
from pathlib import Path

MARKER = "总共 1 个测试文件与指定模式相匹配。"

# 取数一律用「限定词 + 数字」的完整形状，别用裸数字：
#   · 「0 个警告」不含 `: warning `、不会与别的 0 混淆；
#   · 逐程序集那条必须带 dll 名 —— 否则改成 `\d+` 就会把汇总行也吃进来。
PATTERNS = {
    "步数": re.compile(r"===== \[(\d)\]"),
    "GATE_EXIT": re.compile(r"GATE_EXIT=(\d+)"),
    "脚本数": re.compile(r"✅ (\d+) 个脚本语法正确"),
    "路径卫生文件数": re.compile(r"已检查 (\d+) 个文件"),
    "重复接线扫": re.compile(r"扫了 (\d+) 个有事件声明的 \.axaml"),
    "面板总线扫": re.compile(r"扫了 (\d+) 个 \.cs；识别 (\d+) 个视图成员"),
    "位置写入扫": re.compile(r"扫了 (\d+) 个 \.cs；接线 (\d+) 处、读取 (\d+) 处"),
    "策略通过": re.compile(r"通过:\s*(\d+)，已跳过:\s*(\d+)，总计:\s*(\d+)"),
    "警告数": re.compile(r"(\d+) 个警告"),
    "错误数": re.compile(r"(\d+) 个错误"),
}

# ⚠ `持续时间:[^-]*?-` 而不是 `\S+` —— 真形态是 `持续时间: 1 s - X.dll`（**中间有空格**），
#   写成 `\S+` 会让**全部**程序集行一条都匹配不上（阶段 118 真踩，10 条）。
LOADAVG = re.compile(r"负载 loadavg\([^)]*\):\s*(.+)")
ASM = re.compile(r"通过:\s*(\d+)，[^\n]*?持续时间:[^-]*?- ([\w.]+\.dll)")


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def summarize(text: str) -> dict:
    """抽读数。**不**在这里判「齐不齐」—— 那是 `missing()` 的事。"""
    out: dict = {}
    for label, pat in PATTERNS.items():
        hits = pat.findall(text)
        out[label] = hits
    out["行数"] = len(text.splitlines())
    out["非空行"] = sum(1 for x in text.splitlines() if x.strip())
    out["标记行独"] = sum(1 for x in text.splitlines() if x.strip() == MARKER)
    out["标记行双"] = sum(1 for x in text.splitlines() if x.strip() == MARKER * 2)
    out["程序集"] = [(int(n), d) for n, d in ASM.findall(text)]
    # 负载记账（阶段 223）：三处标记，按出现顺序取；老日志没有 ⇒ 空列表（**不作必检项**）
    out["负载"] = [m.group(1) for m in LOADAVG.finditer(text)]
    return out


def missing(s: dict) -> list:
    """关键项自检 —— 这一条就是本脚本存在的理由。

    返回「不该缺却缺了」的项名列表。程序集那项为**空**是最阴的：它看起来像「没数据」。
    """
    bad = []
    if not s["步数"]:
        bad.append("步数（一条 `===== [N]` 都没匹配上）")
    if not s["GATE_EXIT"]:
        bad.append("GATE_EXIT（日志尾部没有它 ⇒ 那一跑可能踩了「半吊子 bash」）")
    if not s["脚本数"]:
        bad.append("脚本数")
    if not s["路径卫生文件数"]:
        bad.append("路径卫生文件数")
    if not s["程序集"]:
        bad.append("程序集（⚠ 大概率是我正则写错了，**不是**「没有数据」）")
    elif len(s["程序集"]) != 9:
        bad.append("程序集条数 %d ≠ 9（门禁有 8 ＋ 1 ＝ 9 次 `dotnet test` 调用 —— 阶段 148 起"
                   " `DockPanelInvocationUiTests` 回到大队，不再单独成段）"
                   % len(s["程序集"]))
    return bad


def report(name: str, s: dict) -> None:
    print("【%s】" % name)
    print("  行数 %s（非空 %s）；步数 %s；GATE_EXIT=%s"
          % (s["行数"], s["非空行"], len(s["步数"]),
             s["GATE_EXIT"][0] if s["GATE_EXIT"] else "?"))
    if s["负载"]:
        print("  负载 loadavg：开始 %s / 测试前 %s / 测试后 %s" % tuple(
            (s["负载"] + ["?", "?", "?"])[:3]))
    print("  脚本 %s 个；路径卫生 %s 个文件"
          % (s["脚本数"][0] if s["脚本数"] else "?",
             s["路径卫生文件数"][0] if s["路径卫生文件数"] else "?"))
    print("  标记行：独立 %s 次 / 双份 %s 次（合计标记 %s 个）"
          % (s["标记行独"], s["标记行双"], s["标记行独"] + 2 * s["标记行双"]))
    if s["程序集"]:
        print("  逐程序集：%s" % " / ".join(str(n) for n, _ in s["程序集"]))
        print("  合计 %s" % sum(n for n, _ in s["程序集"]))
    else:
        print("  ⚠ 逐程序集一条都没匹配上 —— 别把它当成「没有数据」")


def main(argv: list) -> int:
    logs = [a for a in argv if not a.startswith("--")]
    if not logs:
        print(__doc__)
        return 2
    rc = 0
    for i, p in enumerate(logs):
        path = Path(p)
        if not path.is_file():
            print("❌ 不是文件：%s" % p, file=sys.stderr)
            rc = 2
            continue
        if i:
            print()
        s = summarize(read(path))
        report(path.name, s)
        bad = missing(s)
        if bad:
            rc = 1
            print("  ❌ 关键项缺失（**不是**「没有数据」）：")
            for b in bad:
                print("     - %s" % b)
    return rc


# ────────────────────────── 自检（3 档） ──────────────────────────
def _good_log() -> str:
    lines = ["===== [1] bash -n 脚本语法检查 =====",
             "✅ 28 个脚本语法正确",
             "===== [2] 路径卫生（无硬编码开发机家目录） =====",
             "✅ 无硬编码开发机家目录路径（已检查 668 个文件）",
             "0 个警告", "0 个错误"]
    for n in (44, 44, 75, 87, 4, 315, 2128, 595, 8):
        lines.append("总共 1 个测试文件与指定模式相匹配。")
        lines.append("已通过! - 失败:     0，通过: %5d，已跳过:     0，总计: %5d，"
                     "持续时间: 1 s - X%02d.Tests.dll" % (n, n, n))
    lines.append("GATE_EXIT=0")
    return "\n".join(lines) + "\n"


def selftest() -> int:
    fails = []
    # 合成日志的行数 ＝ 6 头行 ＋ 9×2 程序集行（标记 ＋ 结果）＋ 1 退出行
    expect_lines = 6 + 18 + 1

    # 档 1：正常日志 ⇒ 读数对、退出 0
    s = summarize(_good_log())
    if (s["行数"], len(s["步数"]), s["程序集"][0][0], len(s["程序集"])) \
            != (expect_lines, 2, 44, 9):
        fails.append("档1 读数不对：行数 %s（应 %d）/ 步数 %s / 程序集 %s"
                     % (s["行数"], expect_lines, len(s["步数"]), len(s["程序集"])))
    if sum(n for n, _ in s["程序集"]) != 3300:
        fails.append("档1 合计不是 3300：%s" % sum(n for n, _ in s["程序集"]))
    if missing(s):
        fails.append("档1 不该报缺，却报了 %s" % missing(s))

    # 档 2：**行数口径** —— 末尾换行不许被多算一个（`split("\n")` 会多算）
    t = _good_log()
    if len(t.split("\n")) - 1 != len(t.splitlines()):
        fails.append("档2 自检前提不成立（两种口径本来就该差 1）")
    if summarize(t)["行数"] != expect_lines:
        fails.append("档2 行数多算了：%s（应 %d）" % (summarize(t)["行数"], expect_lines))

    # 档 3：**关键项缺失必须报警** —— 抽掉程序集行，绝不能静默产出 0
    broken = "\n".join(l for l in _good_log().splitlines()
                       if "持续时间:" not in l) + "\n"
    sb = summarize(broken)
    if sb["程序集"]:
        fails.append("档3 抽掉程序集行后仍有读数：%s" % sb["程序集"])
    if not any("程序集" in b for b in missing(sb)):
        fails.append("档3 程序集为空却没报警（这正是阶段 118 踩的那一脚）")

    for f in fails:
        print("❌ %s" % f)
    if fails:
        return 1
    print("✅ gate-readings 自检 3 档全过")
    return 0


if __name__ == "__main__":
    if "--selftest" in sys.argv:
        raise SystemExit(selftest())
    raise SystemExit(main(sys.argv[1:]))
