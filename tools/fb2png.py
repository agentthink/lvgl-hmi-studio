#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
fb0.raw -> PNG 渲染器（无 PIL 依赖）
用法: python3 fb2png.py <raw> <w> <h> <outprefix>
32bpp 双缓冲：第 0/1 页各渲染 BGRA 与 RGBA 两种字节序
"""
import struct
import sys
import zlib


def write_png(path, w, h, rgb):
    def chunk(typ, data):
        c = struct.pack(">I", len(data)) + typ + data
        c += struct.pack(">I", zlib.crc32(typ + data) & 0xFFFFFFFF)
        return c
    stride = w * 3
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        raw += rgb[y * stride:(y + 1) * stride]
    ihdr = struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)
    png = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) +
           chunk(b"IDAT", zlib.compress(bytes(raw), 6)) + chunk(b"IEND", b""))
    with open(path, "wb") as f:
        f.write(png)
    print("wrote %s (%dx%d)" % (path, w, h))


def main():
    src, w, h, out = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), sys.argv[4]
    data = open(src, "rb").read()
    page_bytes = w * h * 4
    n_pages = len(data) // page_bytes
    for pg in range(n_pages):
        base = pg * page_bytes
        for label, order in (("bgra", (2, 1, 0)), ("rgba", (0, 1, 2))):
            rgb = bytearray(w * h * 3)
            for i in range(w * h):
                o = base + i * 4
                b0, b1, b2 = data[o + order[0]], data[o + order[1]], data[o + order[2]]
                rgb[i * 3] = b0
                rgb[i * 3 + 1] = b1
                rgb[i * 3 + 2] = b2
            write_png("%s_p%d_%s.png" % (out, pg, label), w, h, bytes(rgb))


if __name__ == "__main__":
    main()
