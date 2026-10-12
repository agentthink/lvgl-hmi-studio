#!/usr/bin/env bash
# coverage-summary.py 的判据自检（正控 + 负控）。
#
# 为什么要有这个：汇总脚本里的三道自检**自己也会错**。本项目真踩过 ——
# 属性名写成 `hit`（实为 `hits`），既不抛异常也不报错，只是让**每一行**都算成
# 未覆盖，读数变成"全 0%"而看起来完全正常。所以每道自检都要能被**证明它在判**。
#
# 用法：bash tools/probe-coverage-summary.sh
# 退出码：0 = 全部符合预期；1 = 有不符合预期的（会逐条打印）。
#
# ⚠ fixture 建在**本次调用**的临时目录里。本沙箱 /tmp 跨 Bash 调用不共享，
#   也不要落到仓库树内（会被路径卫生检查读到）。

set -u
cd "$(dirname "$0")/.."

W=$(mktemp -d)
trap 'rm -rf "$W"' EXIT

python3 - "$W" <<'PY'
import pathlib, sys
base = pathlib.Path(sys.argv[1])

def mk(name, claimed, lines_xml):
    d = base / name
    d.mkdir(parents=True)
    (d / "coverage.cobertura.xml").write_text(
        '<?xml version="1.0" encoding="utf-8"?>\n'
        f'<coverage lines-covered="{claimed}" lines-valid="9">\n'
        '  <packages><package name="P"><classes>\n'
        '    <class name="Foo" filename="/repo/src/Foo.cs"><lines>\n'
        f'{lines_xml}'
        '    </lines></class>\n'
        '  </classes></package></packages>\n'
        '</coverage>\n')
    return d

def mk_multi(name, claimed, classes_xml):
    d = base / name
    d.mkdir(parents=True)
    (d / "coverage.cobertura.xml").write_text(
        '<?xml version="1.0" encoding="utf-8"?>\n'
        f'<coverage lines-covered="{claimed}" lines-valid="9">\n'
        '  <packages><package name="P"><classes>\n'
        f'{classes_xml}'
        '  </classes></package></packages>\n'
        '</coverage>\n')
    return d

L = lambda *xs: "".join(f'      <line number="{n}" hits="{h}" />\n' for n, h in xs)

# 正控：自称 2 行、实际 2 行命中 ⇒ 应当正常出结果
mk("ok", 2, L((1, 1), (2, 3), (3, 0)))
# 负控 1：自称 3 行，但 hits 全 0 ⇒「自称有、数出 0」必须炸（这正是 hit/hits 那个 bug 的形态）
mk("zero", 3, L((1, 0), (2, 0), (3, 0)))
# 负控 2：<line> 缺 hits 属性 ⇒ 必须炸
mk("noattr", 1, '      <line number="1" number2="1" />\n')
# 负控 3：数出的比自称的还多 ⇒ 必须炸
mk("over", 0, L((1, 1), (2, 1)))
# 正控 2：obj/ 下的生成产物要被排除、且**被报告**（分母只留 src/App/Real.cs 的 2 行）
mk_multi("objmix", 2,
         '    <class name="Gen" filename="/repo/src/App/obj/Release/net10.0/Gen.g.cs"><lines>\n'
         + L((1, 0), (2, 0)) +
         '    </lines></class>\n'
         '    <class name="Real" filename="/repo/src/App/Real.cs"><lines>\n'
         + L((1, 1), (2, 2)) +
         '    </lines></class>\n')
print("fixtures:", ", ".join(sorted(p.name for p in base.iterdir())))
PY

pass=0
fail=0

# expect <名称> <fixture> <期望退出码> <输出里必须出现的片段>
expect() {
    local name=$1 dir=$2 want_rc=$3 want_text=$4
    local out="$W/$name.out"
    python3 scripts/coverage-summary.py "$W/$dir" > "$out" 2>&1
    local rc=$?
    local why=""
    if [ "$rc" -ne "$want_rc" ]; then
        why="退出码 $rc ≠ 期望 $want_rc"
    elif ! grep -qF -- "$want_text" "$out"; then
        why="输出里没有：$want_text"
    fi
    if [ -z "$why" ]; then
        printf 'PASS  %-8s rc=%s\n' "$name" "$rc"
        pass=$((pass + 1))
    else
        printf 'FAIL  %-8s %s\n' "$name" "$why"
        sed 's/^/        | /' "$out" | head -5
        fail=$((fail + 1))
    fi
}

expect 正控1-正常汇总   ok      0 '| 覆盖行 / 总行 | 2 / 3 |'
expect 负控1-自称有数出0 zero    1 '自称覆盖 3 行，我们按同一份 XML 数出 0 行'
expect 负控2-缺hits属性 noattr  1 '缺少 hits 属性'
expect 负控3-数出多于自称 over    1 '我们数出 2 行'
expect 正控2-排除obj并报告 objmix  0 '已排除'

echo
echo "结果：$pass 通过 / $fail 失败"
[ "$fail" -eq 0 ] || exit 1
