#!/usr/bin/env python3
"""Static connectivity checker for the generated schematics.

Builds a union-find over pin hotspots, wire segments and global-label anchors
per page, then reports any galvanically-connected group carrying MORE THAN ONE
distinct global-label name (an unintended net merge).
"""
import sys

sys.path.insert(0, "/home/chen/Git/zhishu-hmi/hardware/hmi-lceda-pro/kicad-export")
from generate import (load_t113_pins, build_symbols, build_page_P1, build_page_P2,
                      build_page_P3, build_page_P4, build_page_P5, build_page_P6,
                      build_page_P7, build_page_P8, build_page_P9,
                      PIN_DEFS, T113_PIN_LOCS)


class UF:
    def __init__(self):
        self.p = {}

    def find(self, a):
        self.p.setdefault(a, a)
        while self.p[a] != a:
            self.p[a] = self.p[self.p[a]]
            a = self.p[a]
        return a

    def union(self, a, b):
        ra, rb = self.find(a), self.find(b)
        if ra != rb:
            self.p[ra] = rb


def on_segment(px, py, x1, y1, x2, y2, eps=0.01):
    """Point lies on the segment (inclusive)."""
    if abs(x1 - x2) < eps:  # vertical
        return abs(px - x1) < eps and min(y1, y2) - eps <= py <= max(y1, y2) + eps
    if abs(y1 - y2) < eps:  # horizontal
        return abs(py - y1) < eps and min(x1, x2) - eps <= px <= max(x1, x2) + eps
    return False


def check_page(page, symbols):
    uf = UF()
    wires = []
    for w in page.wires:
        (pts, xy1, xy2) = w[1], w[1][1], w[1][2]
        wires.append((xy1[1], xy1[2], xy2[1], xy2[2]))
        uf.find(("wire", len(wires) - 1))
    # labels: (name, x, y)
    labels = []
    for lb in page.labels:
        name = lb[1]
        at = lb[3]
        labels.append((name, at[1], at[2]))
    # pin hotspots
    pins = []  # (ref, pinnum, x, y)
    for comp in page.components:
        if comp["lib_id"] == "hmi:T113_S3":
            for ball, (sx, sy, ang, nm) in T113_PIN_LOCS.items():
                pins.append((comp["ref"], ball, comp["x"] + sx, comp["y"] - sy))
        else:
            for p in PIN_DEFS.get(comp["lib_id"], []):
                num, px, py = p[1], p[4], p[5]
                pins.append((comp["ref"], num, comp["x"] + px, comp["y"] - py))
    # PWR_FLAG pseudo-components were appended to page.components in emit; here
    # we re-derive from page.power_flags
    for net, x, y, _a in page.power_flags:
        pins.append(("#FLG", net, x, y))
        labels.append((net, x, y - 2.54))
        wires.append((x, y, x, y - 2.54))

    # union rules
    for i, (x1, y1, x2, y2) in enumerate(wires):
        for j in range(i + 1, len(wires)):
            a = wires[j]
            # shared endpoint
            if any(abs(a[k] - b) < 0.01 for k, b in zip((0, 1), (x1, y1))) or \
               any(abs(a[k] - b) < 0.01 for k, b in zip((0, 1), (x2, y2))) or \
               any(abs(a[k] - b) < 0.01 for k, b in zip((2, 3), (x1, y1))) or \
               any(abs(a[k] - b) < 0.01 for k, b in zip((2, 3), (x2, y2))):
                # only endpoint-endpoint coincidence counts
                pass
        # simpler: endpoint equality
    for i in range(len(wires)):
        for j in range(i + 1, len(wires)):
            for ei in ((0, 1), (2, 3)):
                for ej in ((0, 1), (2, 3)):
                    if abs(wires[i][ei[0]] - wires[j][ej[0]]) < 0.01 and \
                       abs(wires[i][ei[1]] - wires[j][ej[1]]) < 0.01:
                        uf.union(("wire", i), ("wire", j))
    # label/pin onto wires (any point on segment)
    for idx, (name, lx, ly) in enumerate(labels):
        for wi, (x1, y1, x2, y2) in enumerate(wires):
            if on_segment(lx, ly, x1, y1, x2, y2):
                uf.union(("label", idx), ("wire", wi))
    for idx, (ref, num, px, py) in enumerate(pins):
        for wi, (x1, y1, x2, y2) in enumerate(wires):
            if on_segment(px, py, x1, y1, x2, y2):
                uf.union(("pin", idx), ("wire", wi))
    # coincident labels/pins
    for i in range(len(labels)):
        for j in range(i + 1, len(labels)):
            if abs(labels[i][1] - labels[j][1]) < 0.01 and abs(labels[i][2] - labels[j][2]) < 0.01:
                uf.union(("label", i), ("label", j))
    for i in range(len(pins)):
        for j in range(i + 1, len(pins)):
            if abs(pins[i][2] - pins[j][2]) < 0.01 and abs(pins[i][3] - pins[j][3]) < 0.01:
                uf.union(("pin", i), ("pin", j))

    # group labels by root
    groups = {}
    for idx, (name, lx, ly) in enumerate(labels):
        groups.setdefault(uf.find(("label", idx)), set()).add(name)
    merges = {r: names for r, names in groups.items() if len(names) > 1}
    if merges:
        print(f"[{page.name}] NET MERGES DETECTED:")
        for r, names in merges.items():
            pts = [(labels[i][0], labels[i][1], labels[i][2])
                   for i in range(len(labels)) if uf.find(("label", i)) == r]
            print(f"  merged net {sorted(names)}")
            for nm, x, y in pts:
                print(f"    label '{nm}' at ({x:.2f}, {y:.2f})")
            # find pins in this group
            for i, (ref, num, px, py) in enumerate(pins):
                if uf.find(("pin", i)) == r:
                    print(f"    pin {ref}/{num} at ({px:.2f}, {py:.2f})")
    else:
        print(f"[{page.name}] OK ({len(labels)} labels, {len(pins)} pins, {len(wires)} wires)")
    return not merges


def main():
    rows = load_t113_pins("/home/chen/Git/zhishu-hmi/hardware/hmi-lceda-pro/kicad-export/refs/t113-pins.csv")
    build_symbols(rows)
    pages = [build_page_P1(), build_page_P2(rows), build_page_P3(), build_page_P4(),
             build_page_P5(), build_page_P6(), build_page_P7(), build_page_P8(),
             build_page_P9()]
    ok = all([check_page(p, None) for p in pages])
    sys.exit(0 if ok else 1)


if __name__ == "__main__":
    main()
