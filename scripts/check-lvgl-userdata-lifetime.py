#!/usr/bin/env python3
"""守卫：交给 LVGL 的 event user_data 必须是**稳定存储**，不得指向栈上局部量/形参。

判据守的是这一族写法：

    lv_obj_add_event_cb(btn, cb, LV_EVENT_CLICKED, (void*)&act);      # 取栈上局部量的地址
    lv_obj_add_event_cb(btn, cb, LV_EVENT_CLICKED, (void*)action);    # 形参指针（指向调用者的栈）

**为什么它是缺陷**（阶段 120 实测，`soft_keyboard.c`）：
LVGL 把 user_data 原样存起来，**点击发生在函数返回之后**。上面两种写法在注册那一刻
指针就注定悬空 ⇒ 回调 `lv_event_get_user_data()` 解引用的是被复用的栈内容：

    · 每个按键的 user_data 都指向**同一个栈槽** ⇒ 按哪个键都可能执行"最后写入的那条动作"；
    · 属未定义行为（换个编译器/优化级别就变成崩溃或错字符）。

正确写法（`soft_keyboard.c` 修后的样子）：把动作**复制进一个静态池**，把池里那一份的
地址交出去（`kb_store_action()` → `s_key_actions[i]`），渲染前把游标归零。
其它合法形态：`NULL`、整数值（`(void*)(intptr_t)i`）、**指向静态/全局对象**的指针。

⚠ 覆盖范围（写清它看不见什么）：
    · 只认第 4 个实参的**字面形态**：`&<标识符>` 与"裸标识符且是当前函数形参"两档；
    · 看不见"先存进结构体再取地址"等跨语句的别名链 —— 那类要靠 review；
    · **不判** `&<file-scope static>`（合法）、整数转换、`NULL`、以及非形参的局部指针
      （例：`kb_action_t* slot = &s_key_actions[i]; … (void*)slot` —— 指向静态池，合法）。
"""

from __future__ import annotations

import re
import subprocess
import sys
import tempfile
from pathlib import Path

APP_DIR = Path("firmware/t113/app")
MIN_FILES = 15
MIN_CALLS = 4

CALL_RE = re.compile(r"lv_obj_add_event_cb\s*\(")
FUNC_RE = re.compile(r"(?m)^[A-Za-z_][\w \t\*]*?\b([A-Za-z_]\w*)\s*\(([^;{)]*)\)\s*\{")
STATIC_RE = re.compile(r"(?m)^\s*static\b[^;{}()]*?\b([A-Za-z_]\w*)\b\s*(?:\[[^\]]*\])?\s*(?:=|;)")
VOID_CAST_RE = re.compile(r"^\(\s*void\s*\*+\s*\)")
ADDR_RE = re.compile(r"^&\s*([A-Za-z_]\w*)")
BARE_ID_RE = re.compile(r"^([A-Za-z_]\w*)$")


def split_args(text: str, open_paren: int):
    """从 `(` 位置切出顶层实参列表；返回 (args, 右括号下标)。"""
    depth = 0
    args: list[str] = []
    start = open_paren + 1
    i = open_paren
    while i < len(text):
        c = text[i]
        if c == "(":
            depth += 1
        elif c == ")":
            depth -= 1
            if depth == 0:
                args.append(text[start:i])
                return [a.strip() for a in args], i
        elif c == "," and depth == 1:
            args.append(text[start:i])
            start = i + 1
        i += 1
    return [], -1


def find_issues(text: str, label: str):
    issues = []
    statics = set(STATIC_RE.findall(text))
    funcs = []
    for m in FUNC_RE.finditer(text):
        params = set()
        for p in m.group(2).split(","):
            p = re.sub(r"\[[^\]]*\]", "", p)      # 去掉数组后缀
            names = re.findall(r"[A-Za-z_]\w*", re.sub(r"\([^)]*\)", "", p))
            if names:
                params.add(names[-1])
        funcs.append((m.start(), params))

    def params_at(pos: int):
        best: set[str] = set()
        for start, params in funcs:
            if start <= pos:
                best = params
            else:
                break
        return best

    for call in CALL_RE.finditer(text):
        args, _ = split_args(text, call.end() - 1)
        if len(args) != 4:
            continue
        user_data = args[3]
        line = text.count("\n", 0, call.start()) + 1
        norm = VOID_CAST_RE.sub("", user_data).strip()
        norm = VOID_CAST_RE.sub("", norm).strip()   # 兼容 (void*)(void*)x 这类双写

        addr = ADDR_RE.match(norm)
        if addr:
            ident = addr.group(1)
            if ident not in statics:
                issues.append(
                    (label, line, user_data,
                     f"取 `&{ident}` 交给 LVGL —— 若是栈上局部量/形参，回调触发时指针已失效"))
            continue

        bare = BARE_ID_RE.match(norm)
        if bare and bare.group(1) in params_at(call.start()):
            issues.append(
                (label, line, user_data,
                 f"形参 `{bare.group(1)}` 直接当 event user_data —— 它指向调用者的栈，回调时已失效"))

    return issues


def scan(root: Path):
    try:
        listed = subprocess.run(
            ["git", "ls-files", str(APP_DIR)], cwd=root,
            capture_output=True, text=True, check=True).stdout.splitlines()
        files = [root / p for p in listed if p.endswith(".c")]
    except Exception:
        files = sorted((root / APP_DIR).rglob("*.c"))
    files = [f for f in files if f.is_file()]

    issues, calls = [], 0
    for f in files:
        text = f.read_text(encoding="utf-8", errors="ignore")
        calls += len(CALL_RE.findall(text))
        rel = str(f.relative_to(root))
        issues += find_issues(text, rel)
    return files, calls, issues


def selftest() -> int:
    print("=== check-lvgl-userdata-lifetime.py --selftest ===")
    fixtures = [
        ("栈上局部量取地址（该红）", """\
static void kb_make(int accent) {
    kb_action_t act = {0};
    lv_obj_add_event_cb(btn, kb_key_cb, LV_EVENT_CLICKED, (void*)&act);
}
""", True),
        ("形参指针（该红）", """\
static lv_obj_t* kb_make_key(lv_obj_t* parent, const kb_action_t* action) {
    lv_obj_add_event_cb(btn, kb_key_cb, LV_EVENT_CLICKED, (void*)action);
    return btn;
}
""", True),
        ("非 void* 强转的形参（该红）", """\
static void build(kb_action_t* act) {
    lv_obj_add_event_cb(b, cb, E, act);
}
""", True),
        ("静态池里的地址（该绿）", """\
static kb_action_t s_key_actions[64];
static void kb_make(const kb_action_t* action) {
    kb_action_t* slot = &s_key_actions[0];
    *slot = *action;
    lv_obj_add_event_cb(btn, kb_key_cb, LV_EVENT_CLICKED, (void*)slot);
}
""", False),
        ("NULL（该绿）", """\
static void wire(lv_obj_t* ta) {
    lv_obj_add_event_cb(ta, ta_focus_cb, LV_EVENT_FOCUSED, NULL);
}
""", False),
        ("整数转换（该绿）", """\
static void tabs(int idx) {
    lv_obj_add_event_cb(b, tab_click_cb, LV_EVENT_CLICKED, (void*)(intptr_t)idx);
}
""", False),
        ("文件级 static 变量取地址（该绿）", """\
static int s_counter;
static void wire(void) {
    lv_obj_add_event_cb(b, cb, LV_EVENT_CLICKED, (void*)&s_counter);
}
""", False),
    ]
    failed = red = green = 0
    with tempfile.TemporaryDirectory() as tmp:
        for i, (title, code, want_red) in enumerate(fixtures):
            f = Path(tmp) / f"case{i}.c"
            f.write_text(code, encoding="utf-8")
            got = find_issues(code, f"case{i}.c")
            is_red = bool(got)
            ok = is_red == want_red
            if ok and want_red:
                red += 1
            if ok and not want_red:
                green += 1
            failed += 0 if ok else 1
            print(f"  {'✅' if ok else '❌'} {title:<34} 期望{'红' if want_red else '绿'}，"
                  f"实际{'红' if is_red else '绿'}")
            if ok and want_red:
                print(f"        └ {got[0][3]}")

    print(f"✅ 自检通过（{red} 档该红 / {green} 档该绿）" if failed == 0
          else f"❌ 自检失败 {failed} 档 —— 判据会误报或漏报（假门禁）")
    return 1 if failed else 0


def main() -> int:
    root = Path(__file__).resolve().parent.parent
    if "--selftest" in sys.argv:
        return selftest()

    if not (root / APP_DIR).is_dir():
        print(f"❌ 找不到 {APP_DIR}/ —— 扫描面失效，本判据会退化成永远通过")
        return 1

    files, calls, issues = scan(root)
    if len(files) < MIN_FILES:
        print(f"❌ 只扫到 {len(files)} 个 .c（下限 {MIN_FILES}）—— 扫描面塌了，"
              f"本判据会退化成永远通过")
        return 1
    if calls < MIN_CALLS:
        print(f"❌ 只扫到 {calls} 处 lv_obj_add_event_cb（下限 {MIN_CALLS}）—— "
              f"正则可能失效（那样本判据会恒绿）")
        return 1

    if issues:
        print(f"❌ 有 {len(issues)} 处 event user_data 指向栈上局部量/形参：")
        for label, line, ud, why in issues:
            print(f"    {label}:{line}  {ud}\n        └ {why}")
        return 1

    print(f"✅ LVGL event user_data 全部是稳定存储（扫 {len(files)} 个 .c / {calls} 处接线）")
    return 0


if __name__ == "__main__":
    sys.exit(main())
