#!/usr/bin/env python3
"""把覆盖率读数里「整文件 0%」的部分变成可查证的事实。

模型（改这个脚本前先把三条读一遍 —— 前两条是踩出来的）：
1. 类型名**必须从文件内容里解析**，不能从文件名猜：
   - `X.axaml.cs` 的 `stem` 是 **`X.axaml`**（只剥最后一段后缀）；
   - `Wiring.Canvas.cs` 是**分部类文件**（`类名.区域.cs`），文件名里的点不是命名空间；
   - 一个文件可以声明多个类型（`OpcUaTypes.cs`）。
   ⇒ 做法：`.cs` 扫 `class|record|struct|interface|enum` 声明；`.axaml` 取 `x:Class=`。
2. **「引用」的定义**：在**别的**文件里出现、且那个文件**不声明**这个类型。
   否则分部类会互相"引用"（每个 `X.*.cs` 都声明 `X`，彼此的类名当然出现）——
   按"排除定义文件本身"做会把同族的其它分部文件全算成引用。
3. `x:Class="…"` 出现在自己的 `.axaml` 里是**定义**不是引用（`x:Class` 与代码后置声明同一个类）。

分桶：
- 无人引用（src=0, tests=0） ⇒ 死代码候选（删不删是产品决策）
- 活但零测试（src>0, tests=0） ⇒ 活代码、零测试；多是「要打开那个界面才走到」
- 测试引用了却没执行（tests>0） ⇒ 测试没真正执行它（被替身挡掉 / 走了别的分支）

用法：python3 tools/triage-zero-coverage.py <coverage-run-dir> [--min-lines N]
"""
import pathlib
import re
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict

SRC_ROOT = pathlib.Path("src")
TEST_ROOT = pathlib.Path("tests")

# 正控：这几个类型必须报出 src 引用 ≥ 1（它们只由代码 new 出来，没有任何测试引用）。
# 报 0 就说明类型解析或扫描口径又坏了 —— 那时整个分桶都是假的。
POSITIVE_CONTROLS = {
    "AnimationEditorView": 1,
    "RecipeEditorView": 1,
}

DECL_RE = re.compile(r"\b(?:class|record|struct|interface|enum)\s+([A-Za-z_]\w*)")
AXAML_CLASS_RE = re.compile(r'x:Class="(?:[\w.]+\.)?([A-Za-z_]\w*)"')


def declared_types(path: pathlib.Path) -> set[str]:
    """这个文件声明了哪些类型。"""
    try:
        text = path.read_text(errors="ignore")
    except OSError:
        return set()
    if path.suffix == ".axaml":
        return set(AXAML_CLASS_RE.findall(text))
    return set(DECL_RE.findall(text))


def normalize(path: str) -> str:
    p = path.replace("\\", "/")
    i = p.find("/src/")
    return p[i + 1:] if i >= 0 else p


def collect(run: pathlib.Path):
    lines: dict[str, dict[int, int]] = defaultdict(dict)
    owner: dict[str, str] = {}
    for rep in sorted(run.rglob("coverage.cobertura.xml")):
        for pkg in ET.parse(rep).getroot().iter("package"):
            pname = pkg.get("name") or "?"
            for cls in pkg.iter("class"):
                raw = cls.get("filename") or ""
                if not raw:
                    continue
                key = normalize(raw)
                if "/obj/" in key or "/bin/" in key:
                    continue
                owner.setdefault(key, pname)
                bucket = lines[key]
                for ln in cls.iter("line"):
                    n = int(ln.get("number"))
                    h = int(ln.get("hits") or 0)
                    if h > bucket.get(n, -1):
                        bucket[n] = h
    return lines, owner


def main() -> None:
    if len(sys.argv) < 2:
        raise SystemExit(__doc__)
    run = pathlib.Path(sys.argv[1])
    min_lines = 20
    if "--min-lines" in sys.argv:
        min_lines = int(sys.argv[sys.argv.index("--min-lines") + 1])

    lines, owner = collect(run)

    zero = []
    for f, b in lines.items():
        if not f.startswith("src/"):
            continue
        if sum(1 for h in b.values() if h > 0) == 0 and len(b) >= min_lines:
            zero.append((f, len(b), owner.get(f, "?")))

    total = sum(z[1] for z in zero)
    print(f"=== 整文件 0% 覆盖（行数 >= {min_lines}）：{len(zero)} 个文件 / {total} 行 ===\n")
    by: dict[str, list[int]] = defaultdict(lambda: [0, 0])
    for _, n, p in zero:
        by[p][0] += 1
        by[p][1] += n
    for p, (c, n) in sorted(by.items(), key=lambda kv: -kv[1][1]):
        print(f"  {p:<32} {c:2d} 个文件  {n:5d} 行")

    # ── 建索引：每个文件声明了哪些类型；每个类型被哪些文件声明 ──────────
    src_files = [p for p in SRC_ROOT.rglob("*") if p.suffix in (".cs", ".axaml")]
    test_files = [p for p in TEST_ROOT.rglob("*.cs")]
    decl_of: dict[pathlib.Path, set[str]] = {}
    for p in src_files + test_files:
        d = declared_types(p)
        if d:
            decl_of[p] = d
    declarers: dict[str, set[pathlib.Path]] = defaultdict(set)
    for p, names in decl_of.items():
        for n in names:
            declarers[n].add(p)

    def references(types: set[str], pool: list[pathlib.Path]) -> list[str]:
        hits = []
        for p in pool:
            # 声明这些类型的文件不算引用（分部类同族会被彼此算成引用）
            if decl_of.get(p, set()) & types:
                continue
            try:
                text = p.read_text(errors="ignore")
            except OSError:
                continue
            if any(re.search(r"\b" + re.escape(t) + r"\b", text) for t in types):
                hits.append(str(p))
        return hits

    buckets: dict[str, list] = {
        "无人引用（src=0, tests=0）": [],
        "活但零测试（src>0, tests=0）": [],
        "测试引用了却没执行（tests>0）": [],
    }
    refs_seen: dict[str, int] = {}
    unresolved: list[str] = []

    for f, n, _ in sorted(zero, key=lambda z: -z[1]):
        types = decl_of.get(pathlib.Path(f), set())
        if not types:
            unresolved.append(f)
            continue
        rs = references(types, src_files)
        rt = references(types, test_files)
        for t in types:
            refs_seen[t] = len(rs)
        if not rs and not rt:
            key = "无人引用（src=0, tests=0）"
        elif not rt:
            key = "活但零测试（src>0, tests=0）"
        else:
            key = "测试引用了却没执行（tests>0）"
        buckets[key].append((f, n, sorted(types), rs, rt))

    for key, items in buckets.items():
        print(f"\n########## {key} —— {len(items)} 个 / {sum(i[1] for i in items)} 行 ##########")
        for f, n, types, rs, rt in items:
            print(f"\n  {f}  （{n} 行；类型 {', '.join(types)}）")
            for h in rs[:4]:
                print(f"        src  <- {h}")
            if len(rs) > 4:
                print(f"        src  <- …共 {len(rs)} 处")
            for h in rt[:4]:
                print(f"        test <- {h}")

    # ── 自检 ────────────────────────────────────────────────────────
    print("\n=== 自检 ===")
    bad = 0
    for name, want in POSITIVE_CONTROLS.items():
        got = refs_seen.get(name)
        if got is None:
            print(f"  跳过 {name}（不在本轮 0% 清单里）")
            continue
        ok = got >= want
        bad += 0 if ok else 1
        print(f"  {'PASS' if ok else 'FAIL'}  {name}  src 引用 = {got}（期望 >= {want}）")
    if unresolved:
        bad += 1
        print(f"  FAIL  {len(unresolved)} 个文件解析不出任何类型声明（口径坏了或文件确实空）：")
        for f in unresolved[:5]:
            print(f"          {f}")
    if bad:
        raise SystemExit("自检失败：分桶结果不可信，先修解析口径再看结论。")


main()
