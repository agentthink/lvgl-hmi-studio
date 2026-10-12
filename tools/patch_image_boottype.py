#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
patch_image_boottype.py — 把 Tina pack 镜像的 Boot0/Boot1 条目类型字段
**归一到 openixcli(0.2.x) 实际查找的 (maintype, subtype)**。

## openixcli 到底找什么（读源码得出，非猜测）

`OpenixCLI/src/flash/fes_handler/boot_download.rs`：

    Boot1 (PKG 模式):
        SPI-NOR            → ("12345678", "BOOTPKG-NOR00000")
        其余(NAND/eMMC/SD)  → ("12345678", "BOOTPKG-00000000")
    Boot0 (PKG/NORMAL 模式):
        NAND/SPINAND       → ("BOOT", "BOOT0_0000000000")
        其它                → ("12345678", "1234567890BOOT_0")

按 **(maintype, subtype) 二元组**查找；找不到就报
「Boot1 not found in firmware」/「Boot0 not found in firmware」。

## 关键结论：原始镜像**不需要**打补丁

Tina `pack` 的产物本来就满足上面两个键。2026-10-10 实测：
未打补丁的 `tina_t113-bingpi_m2_uart3.img` 一次烧录成功
（5 个分区 + Boot0/Boot1 全部写入并校验，openixcli 退出码 0）。

⚠ **历史教训：2026-08-17 的旧版脚本是错的。** 旧版把 `boot_package.fex` 的
maintype 改成 `BOOTPKG`、把 `boot0_nand.fex` 改成 `BOOT0` —— 这**恰好把两个
查找都改坏**（openixcli 要的是 `12345678` / `BOOT`），于是烧录必然在分区烧完后
卡在「Boot1 not found in firmware」。同日对照复测：
**打过旧补丁的镜像失败、原始镜像成功**。

## 本工具现在的语义：归一（幂等）

    - 正常镜像            → 无改动
    - 被旧补丁改坏的镜像   → 修回 "12345678" / "BOOT"
    - subtype 缺失/不符    → 补齐

用法:
    python3 patch_image_boottype.py --check <镜像>          # 只检查，不写文件
    python3 patch_image_boottype.py <原始镜像> <输出镜像>     # 归一化输出到新文件
"""
import shutil
import sys

IMG_FHDR_LEN = 1024          # 每条目长度
NAME_POS_IN_ENTRY = 36       # filename 字段在条目内的偏移(4+4+8+16+4)
MAINTYPE_POS = 8             # maintype@entry+8, 8 字节
SUBTYPE_POS = 16             # subtype@entry+16, 16 字节

# openixcli 0.2.x 在 PKG 模式 + NAND/eMMC 下的查找键（本板：SPI NAND 启动）
EXPECTED = {
    b"boot_package.fex": (b"12345678", b"BOOTPKG-00000000"),
    b"boot0_nand.fex":   (b"BOOT",     b"BOOT0_0000000000"),
}


def pad(s: bytes, n: int) -> bytes:
    return s + b"\0" * (n - len(s))


def find_entry(data: bytes, name: bytes):
    """条目起点 = 文件名字段位置 - 36；找不到返回 None。"""
    pos = data.find(name)
    if pos < 0:
        return None
    return pos - NAME_POS_IN_ENTRY


def read_str(data, off: int, n: int) -> str:
    return data[off:off + n].split(b"\0")[0].decode("latin1")


def same(cur: str, want: bytes) -> bool:
    """openixcli 的字段比较容忍尾部填充：Tina pack 出的 boot0_nand.fex maintype
    是 'BOOT' + 4 个空格（inspect 显示为 'BOOT'，实机也烧得通），
    所以按**去除尾部空格/NUL** 后的值比较，避免把本来正确的镜像无谓改写。"""
    return cur.rstrip(" \0") == want.decode()


def check_image(src: str) -> int:
    data = open(src, "rb").read()
    bad = 0
    for name, (mt, st) in EXPECTED.items():
        e = find_entry(data, name)
        if e is None:
            print(f"!! {name.decode()}: 镜像中未找到该条目")
            bad += 1
            continue
        cur_mt = read_str(data, e + MAINTYPE_POS, 8)
        cur_st = read_str(data, e + SUBTYPE_POS, 16)
        ok = same(cur_mt, mt) and same(cur_st, st)
        print(f"{name.decode():18s} maintype={cur_mt!r:12s} subtype={cur_st!r:22s}"
              f" -> {'OK (openixcli 能命中)' if ok else 'MISMATCH (openixcli 会报 not found)'}")
        if not ok:
            bad += 1
    if bad == 0:
        print("结论: 镜像可直接用 openixcli 烧录")
    else:
        print("结论: 镜像需归一化 -> python3 patch_image_boottype.py <in> <out>")
    return 0 if bad == 0 else 1


def main():
    args = sys.argv[1:]
    if "--check" in args:
        rest = [a for a in args if a != "--check"]
        if len(rest) != 1:
            print(__doc__)
            sys.exit(2)
        sys.exit(check_image(rest[0]))

    if len(args) != 2:
        print(__doc__)
        sys.exit(1)

    src, dst = args
    shutil.copy(src, dst)
    d = bytearray(open(dst, "rb").read())

    changed = []
    for name, (mt, st) in EXPECTED.items():
        e = find_entry(bytes(d), name)
        if e is None:
            print(f"!! {name.decode()} 未在镜像中找到")
            continue
        old_mt = read_str(d, e + MAINTYPE_POS, 8)
        old_st = read_str(d, e + SUBTYPE_POS, 16)
        if not (same(old_mt, mt) and same(old_st, st)):
            d[e + MAINTYPE_POS:e + MAINTYPE_POS + 8] = pad(mt, 8)
            d[e + SUBTYPE_POS:e + SUBTYPE_POS + 16] = pad(st, 16)
            changed.append((name.decode(), old_mt, old_st, mt.decode(), st.decode()))
        else:
            print(f"  {name.decode()}: 已是 openixcli 期望的 {mt.decode()}/{st.decode()}，跳过")

    open(dst, "wb").write(bytes(d))
    if changed:
        for name, om, os_, nm, ns in changed:
            print(f"  归一 {name}: {om!r}/{os_!r} -> {nm!r}/{ns!r}")
        print(f"已归一 -> {dst}，可直接用 openixcli 烧录")
    else:
        print(f"无需修改，输出 -> {dst}")


if __name__ == "__main__":
    main()
