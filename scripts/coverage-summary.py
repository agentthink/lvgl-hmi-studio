#!/usr/bin/env python3
"""汇总 coverlet 的 cobertura 报告。

为什么需要"合并"：每个测试项目各产一份 coverage.cobertura.xml，同一份产品代码会被
多个测试项目分别覆盖到。只看单份报告会把覆盖率**系统性低估**（例如 Core 被
Core.Tests 与 Simulator.Tests 都测到）。所以这里按「文件 + 行号」取并集。

用法：
    python3 scripts/coverage-summary.py <results-dir> [--top N]
"""
import pathlib
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

DEFAULT_TOP = 25
# 行数太少的文件不参与"最低覆盖率"排行：一个 3 行的文件里漏 1 行就是 33%，
# 排在最前面只会把真正该看的东西挤下去。
MIN_LINES_FOR_RANKING = 20

# 不算"产品代码"的路径片段。`obj/` 下会混进编译器/源生成器的产物
# （实测：`obj/Release/net10.0/System.Text.RegularExpressions.Generator/.../RegexGenerator.g.cs`
# 425 行 0% 覆盖）——那不是"我们该测却没测的行"，把它算进分母会同时
# ① 稀释覆盖率读数、② 让它顶在"最低覆盖率文件"排行第一名，纯噪音。
EXCLUDED_MARKERS = ("/obj/", "/bin/")


def normalize(path: str) -> str:
    """把绝对路径收敛成仓库内相对路径（找 src/ 起点）。"""
    p = path.replace("\\", "/")
    idx = p.find("/src/")
    return p[idx + 1:] if idx >= 0 else p


def load(results_dir: pathlib.Path):
    """返回 (lines, owner, 报告数, 各报告自称的覆盖行数之和)。

    最后一项**只用于自检**：合并结果若与它矛盾，说明解析口径错了（见 main）。
    """
    reports = sorted(results_dir.rglob("coverage.cobertura.xml"))
    if not reports:
        raise SystemExit(f"没找到 coverage.cobertura.xml（目录：{results_dir}）")

    lines: dict[str, dict[int, int]] = defaultdict(dict)
    owner: dict[str, str] = {}
    claimed_covered = 0

    for report in reports:
        root = ET.parse(report).getroot()
        claimed = int(root.get("lines-covered") or 0)
        claimed_covered += claimed

        # ── 逐报告自检 ────────────────────────────────────────────────
        # 根节点**自己**说覆盖了多少行；我们按同一份 XML 重新数一遍。
        # 两者必须同向：数出来是 0 而它自称 1244，只可能是解析口径错了
        # （本项目真发生过：属性名写 hit，读出全 0% 却毫无报错）。
        recount: set[tuple[str, int]] = set()
        seen_covered: set[tuple[str, int]] = set()

        for pkg in root.iter("package"):
            package = pkg.get("name") or "(unknown)"
            for cls in pkg.iter("class"):
                raw = cls.get("filename") or ""
                if not raw:
                    continue
                key = normalize(raw)
                owner.setdefault(key, package)
                bucket = lines[key]
                for line in cls.iter("line"):
                    # ⚠ 属性名是 hits（复数）。写成 hit 既不抛异常也不报错，只会让**每一行**
                    # 都算成未覆盖 —— 读数变成"全 0%"，看起来像"测试没起作用"，极易误判。
                    # 所以缺属性直接炸，不给静默降级的机会。
                    raw_hits = line.get("hits")
                    if raw_hits is None:
                        raise SystemExit("解析失败：<line> 缺少 hits 属性 —— cobertura 结构可能变了")
                    try:
                        number = int(line.get("number"))
                        hits = int(raw_hits)
                    except (TypeError, ValueError):
                        continue
                    if hits > bucket.get(number, -1):
                        bucket[number] = hits
                    if hits > 0:
                        recount.add((key, number))

        # 同一份报告里 (文件, 行号) 去重后只会更少，不会更多 ⇒ 只查两个方向：
        #   ① 它自称有、我们数出 0   ⇒ 解析口径错了（不是"代码没被覆盖"）
        #   ② 我们数出的比它自称的还多 ⇒ 报告结构被误解（重复计入）
        if claimed > 0 and not recount:
            raise SystemExit(
                f"自检失败：{report.name} 自称覆盖 {claimed} 行，我们按同一份 XML 数出 0 行。\n"
                "  这一定不是'代码没被覆盖'，而是解析口径错了（先查 <line> 的属性名）。"
            )
        seen_covered |= recount
        if len(seen_covered) > claimed:
            raise SystemExit(
                f"自检失败：{report.name} 自称覆盖 {claimed} 行，我们数出 {len(seen_covered)} 行。"
            )

    return lines, owner, len(reports), claimed_covered


def covered(bucket: dict[int, int]) -> tuple[int, int]:
    return sum(1 for h in bucket.values() if h > 0), len(bucket)


def pct(hit: int, total: int) -> str:
    return f"{100.0 * hit / total:5.1f}%" if total else "    -"


def main() -> None:
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)

    results_dir = pathlib.Path(sys.argv[1])
    top = DEFAULT_TOP
    if "--top" in sys.argv:
        top = int(sys.argv[sys.argv.index("--top") + 1])

    lines, owner, report_count, claimed_covered = load(results_dir)

    # 只统计产品代码：测试自身的覆盖率没有意义
    product = {f: v for f, v in lines.items() if f.startswith("src/") or "/src/" in f}
    if not product:
        raise SystemExit(f"报告里没有 src/ 下的文件（共 {len(lines)} 个文件）")

    # obj/ · bin/ 下的是生成产物，不算"产品代码"（但它必须被**报告**出来，
    # 否则"读数变小了"会被误读成"覆盖率掉了"）。
    excluded = {f: v for f, v in product.items() if any(m in f for m in EXCLUDED_MARKERS)}
    product = {f: v for f, v in product.items() if f not in excluded}
    ex_lines = sum(len(v) for v in excluded.values())
    ex_cov = sum(sum(1 for h in v.values() if h > 0) for v in excluded.values())

    print(f"# 代码覆盖率汇总\n")
    print(f"合并报告数：**{report_count}** 份；纳入统计的产品文件：**{len(product)}** 个\n")
    if excluded:
        print(
            f"（已排除 `obj/`·`bin/` 下的生成产物：**{len(excluded)}** 个文件 / "
            f"**{ex_lines}** 行，其中覆盖 {ex_cov} 行 —— 不是「该测却没测」的行）\n"
        )

    hit, total = 0, 0
    for bucket in product.values():
        h, t = covered(bucket)
        hit += h
        total += t

    # 汇总级自检：并集不可能大于各报告自称之和（只会更少，因为同一行被多份报告重复算）。
    # 这条挡的是"合并口径错了"；上一层的逐报告自检挡的是"解析口径错了"。两道都过，
    # 数字才可以拿去当基线。
    if hit > claimed_covered:
        raise SystemExit(
            f"自检失败：合并出 {hit} 覆盖行 > 各报告自称之和 {claimed_covered} 行 —— 口径不可能如此。"
        )
    if hit > 0 and claimed_covered == 0:
        raise SystemExit("自检失败：合并出覆盖行，但所有报告都自称 0 行 —— 解析口径错了。")

    print(f"## 总计\n")
    print(f"| 指标 | 值 |")
    print(f"|---|---|")
    print(f"| 覆盖行 / 总行 | {hit} / {total} |")
    print(f"| **行覆盖率** | **{pct(hit, total)}** |")
    print(f"| 自检：合并 / 各报告自称之和 | {hit} / {claimed_covered} |\n")

    # 按程序集
    by_pkg: dict[str, list[int]] = defaultdict(lambda: [0, 0])
    for filename, bucket in product.items():
        h, t = covered(bucket)
        agg = by_pkg[owner.get(filename, "(unknown)")]
        agg[0] += h
        agg[1] += t

    print(f"## 按程序集\n")
    print(f"| 程序集 | 覆盖 / 总行 | 行覆盖率 |")
    print(f"|---|---|---|")
    for name, (h, t) in sorted(by_pkg.items(), key=lambda kv: kv[1][0] / max(1, kv[1][1])):
        print(f"| `{name}` | {h} / {t} | {pct(h, t)} |")
    print()

    # 覆盖率最低的文件
    ranked = []
    for filename, bucket in product.items():
        h, t = covered(bucket)
        if t >= MIN_LINES_FOR_RANKING:
            ranked.append((h / t, filename, h, t))
    ranked.sort()

    print(f"## 覆盖率最低的 {top} 个文件（行数 ≥ {MIN_LINES_FOR_RANKING}）\n")
    print(f"| 文件 | 覆盖 / 总行 | 行覆盖率 |")
    print(f"|---|---|---|")
    for _, filename, h, t in ranked[:top]:
        print(f"| `{filename}` | {h} / {t} | {pct(h, t)} |")
    print()

    unranked = sum(1 for bucket in product.values() if len(bucket) < MIN_LINES_FOR_RANKING)
    print(f"（另有 {unranked} 个文件因行数 < {MIN_LINES_FOR_RANKING} 未参与排行）")


main()
