#!/usr/bin/env python3
"""两跑门禁日志的对拍：**先剥墙钟、再把「重排」和「真差异」分开、最后做结构性归一**。

为什么它该在仓里：收口时「这批改了读数没有」这个问题的答案，全靠这个工具。而它有三层
**都会给出看着合理的错答案**的口径，任何一层写错都不会报错：

    ① **不剥墙钟** ⇒ 报出来的全是时长差（`已用时间 00:01:29.70` / `持续时间: 1 s` /
       `AUDIT <ISO 时间戳>`）。阶段 113 实测：13 对 `+/-` 里**只有 1 对是内容差**。
    ② **只用 difflib 逐行比** ⇒ MSBuild 的并行构建清单与并行测试程序集的结束顺序**每次都不同**，
       「同一行换了位置」被报成一对 `+/-`。⇒ 用 `Counter` 做**多重集差**拿真差异，
       逐行位移单独报一个数（`> 真差异×2 ⇒ 有重排`）。
    ③ **多重集差仍剩一类伪差**：并发写同一管道会把固定标记行
       `总共 1 个测试文件与指定模式相匹配。` **挤成一行**，并另多一个空行 ⇒ 稳定报 **4 行**
       （−2 / +2）、且方向可反转（阶段 113 / 115 都撞过）。
       ⇒ 再做一层**结构性归一**：把双份标记行**拆回两行** ＋ 整体丢空行，然后比。

⚠ **归一是「拆结构 ＋ 丢空行」，不是「放宽对拍」**：真丢了内容，非空行照样对不上
    （`--selftest` 档 4 就钉这一条 —— 缺一行真内容时，归一后仍然是 2 行）。

用法：
    python3 scripts/gate-diff.py <旧日志> <新日志> [--all]
    python3 scripts/gate-diff.py --selftest          # 自检：4 档
"""
from __future__ import annotations

import difflib
import re
import sys
from collections import Counter
from pathlib import Path

# 固定标记行：门禁里 10 次 `dotnet test` 调用各打印一行
MARKER = "总共 1 个测试文件与指定模式相匹配。"

# 剥墙钟 —— 与 `scripts/gate-readings.py` 无关，但**必须**与历史上用过的口径一致，
# 否则「上一阶段报 0 行、这一阶段报 N 行」会被误读成回归。
STRIP = [
    (re.compile(r"已用时间\s+\d+:\d+:\d+\.\d+"), "已用时间 <墙钟>"),
    (re.compile(r"持续时间:\s*\d+\s*[a-z]+"), "持续时间 <墙钟>"),
    (re.compile(r"总时间:\s*[\d.]+ 秒"), "总时间 <墙钟>"),
    (re.compile(r"AUDIT\s+\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d"), "AUDIT <墙钟>"),
    (re.compile(r"\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d"), "<时间戳>"),
]


def strip_wall(text: str) -> list:
    out = []
    for line in text.splitlines():
        for pat, rep in STRIP:
            line = pat.sub(rep, line)
        out.append(line)
    return out


def normalize(lines: list) -> list:
    """结构性归一：拆回被挤成一行的标记行 ＋ 整体丢空行（**只拆结构**）。"""
    out = []
    for x in lines:
        n = x.strip().count(MARKER)
        if n > 1:
            out.extend([MARKER] * n)
        else:
            out.append(x)
    return [x for x in out if x.strip() != ""]


def compare(a_lines: list, b_lines: list) -> dict:
    only_a = Counter(a_lines) - Counter(b_lines)
    only_b = Counter(b_lines) - Counter(a_lines)
    na, nb = normalize(a_lines), normalize(b_lines)
    dn_a = Counter(na) - Counter(nb)
    dn_b = Counter(nb) - Counter(na)
    d = [x for x in difflib.unified_diff(a_lines, b_lines, lineterm="", n=0)
         if x[:1] in "+-" and x[:3] not in ("+++", "---")]
    return {
        "only_a": only_a, "only_b": only_b,
        "real": sum(only_a.values()) + sum(only_b.values()),
        "moved": len(d),
        "na": len(na), "nb": len(nb),
        "norm_real": sum(dn_a.values()) + sum(dn_b.values()),
        "norm_a": dn_a, "norm_b": dn_b,
    }


def main(argv: list) -> int:
    args = [a for a in argv if not a.startswith("--")]
    if len(args) != 2:
        print(__doc__)
        return 2
    pa, pb = Path(args[0]), Path(args[1])
    for p in (pa, pb):
        if not p.is_file():
            print("❌ 不是文件：%s" % p, file=sys.stderr)
            return 2
    la = strip_wall(pa.read_text(encoding="utf-8", errors="replace"))
    lb = strip_wall(pb.read_text(encoding="utf-8", errors="replace"))
    r = compare(la, lb)

    print("%s → %s" % (pa.name, pb.name))
    print("  剥墙钟后原样多重集差：**%d 行**（−%d / +%d）"
          % (r["real"], sum(r["only_a"].values()), sum(r["only_b"].values())))
    for line in sorted(r["only_a"].elements()):
        print("     -  %s" % line)
    for line in sorted(r["only_b"].elements()):
        print("     +  %s" % line)
    print("  逐行 diff 位移 %d 行%s"
          % (r["moved"],
             "" if r["moved"] == 2 * r["real"] else "（≠ 真差异×2 ⇒ 有重排，不影响结论）"))
    print("  结构性归一后：行数 %d 对 %d；**真差异 %d 行**"
          % (r["na"], r["nb"], r["norm_real"]))
    for line in sorted(r["norm_a"].elements()):
        print("     ⚠ -  %s" % line)
    for line in sorted(r["norm_b"].elements()):
        print("     ⚠ +  %s" % line)
    if r["real"] and r["norm_real"] == 0:
        print("  ⇒ 那 %d 行**全部**是结构性伪差（被挤成一行的固定标记行 ＋ 多出的空行）" % r["real"])
    return 1 if r["norm_real"] else 0


# ────────────────────────── 自检（4 档） ──────────────────────────
HEAD = ["===== [1] x =====", "✅ 28 个脚本语法正确", "", ""]


def _log(markers: int, extra_content: list) -> str:
    lines = list(HEAD) + [MARKER] * markers + list(extra_content)
    lines.append("GATE_EXIT=0")
    return "\n".join(lines) + "\n"


def _log_merged(markers: int = 2, content=None) -> str:
    """生成「标记行被并发挤成一行（另多一个空行）」的形态。

    这是**结构**上的挤行：N 个 `MARKER` 被写进**同一行**，而不是 N 个独立行。
    """
    lines = list(HEAD) + [MARKER * markers, ""]
    if content:
        lines.append(content)
    lines.append("GATE_EXIT=0")
    return "\n".join(lines) + "\n"


def selftest() -> int:
    fails = []

    # 档 1：两份只在墙钟上不同的日志 ⇒ 真差异 0（证明「剥墙钟」在干活）
    a = _log(2, ["已用时间 00:00:02.33", "持续时间: 1 s - X.dll"])
    b = _log(2, ["已用时间 00:00:09.99", "持续时间: 12 s - X.dll"])
    r = compare(strip_wall(a), strip_wall(b))
    if r["real"] != 0:
        fails.append("档1 剥墙钟后仍有真差异 %d：%s" % (r["real"], dict(r["only_a"])))

    # 档 2：一侧多一行真内容 ⇒ 真差异 1（**只在单侧出现 ⇒ 只算 1，不是 2**），归一后仍非 0
    c = _log(2, ["已用时间 00:00:02.33"])
    d = _log(2, ["已用时间 00:00:02.33", "只在这一侧出现的一行"])
    r2 = compare(strip_wall(c), strip_wall(d))
    if r2["real"] != 1:
        fails.append("档2 真差异应为 1（单侧多一行），实为 %d" % r2["real"])
    if r2["norm_real"] != 1:
        fails.append("档2 归一后应仍为 1（**归一不是放宽**！），实为 %d" % r2["norm_real"])

    # 档 3：挤行形态 ⇒ 原样 −2/+2（合计 4）、**归一后 0 行**（§115.11 记的那条形态）
    e = _log(2, ["已用时间 00:00:02.33"])                  # 2 个独立标记行
    f = _log_merged(content="已用时间 00:00:02.33")         # 1 个双份标记行 ＋ 多一个空行
    r3 = compare(strip_wall(e), strip_wall(f))
    if (sum(r3["only_a"].values()), sum(r3["only_b"].values())) != (2, 2):
        fails.append("档3 原样应为 −2/+2，实为 −%d/+%d"
                     % (sum(r3["only_a"].values()), sum(r3["only_b"].values())))
    if r3["norm_real"] != 0:
        fails.append("档3 归一后应为 0，实为 %d" % r3["norm_real"])
    if r3["na"] != r3["nb"]:
        fails.append("档3 归一行数应相等，实为 %d 对 %d" % (r3["na"], r3["nb"]))

    # 档 4：归一**不许**把「真内容差」也吃掉 —— 挤行 ＋ 真缺一行 ⇒ 归一后仍报 1
    g = _log(2, ["已用时间 00:00:02.33"])
    h = _log_merged(content=None)
    r4 = compare(strip_wall(g), strip_wall(h))
    if r4["norm_real"] != 1:
        fails.append("档4 归一后应为 1（挤行吃掉那 4 行伪差，只剩真缺的 1 行），实为 %d"
                     % r4["norm_real"])
    if not r4["real"] > r4["norm_real"]:
        fails.append("档4 原样差异应大于归一后（否则说明归一什么也没吃掉）：%d vs %d"
                     % (r4["real"], r4["norm_real"]))

    for x in fails:
        print("❌ %s" % x)
    if fails:
        return 1
    print("✅ gate-diff 自检 4 档全过")
    return 0


if __name__ == "__main__":
    if "--selftest" in sys.argv:
        raise SystemExit(selftest())
    raise SystemExit(main(sys.argv[1:]))
