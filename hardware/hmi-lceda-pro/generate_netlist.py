#!/usr/bin/env python3
"""Expand the LCEDA page matrix into deterministic per-connection netlist CSV."""
from __future__ import annotations
import csv
from pathlib import Path

BASE = Path(__file__).resolve().parent
SRC = BASE / "lceda-page-net-matrix.csv"
OUT = BASE / "normalized-netlist.csv"


def expand(text: str) -> list[str]:
    if text == "LCD_B0..LCD_R7":
        return [f"LCD_{c}{i}" for c in "BGR" for i in range(8)]
    for color in "BGR":
        if text in {f"LCD_{color}7..LCD_{color}0", f"LCD_{color}0..LCD_{color}7"}:
            return [f"LCD_{color}{i}" for i in range(8)]
    if text == "TCH_XP/XN/YP/YN":
        return ["TCH_XP", "TCH_XN", "TCH_YP", "TCH_YN"]
    if "/" in text:
        return [x for x in text.split("/") if x.startswith(("LCD_", "VLED_", "TCH_"))]
    return [text]


def main() -> int:
    source = []
    with SRC.open(encoding="utf-8", newline="") as f:
        reader = csv.DictReader(f)
        for row in reader:
            extras = row.pop(None, []) or []
            if extras:
                row["value_or_note"] = ", ".join([row["value_or_note"], *extras[:-1]])
                row["status"] = extras[-1]
            source.append(row)
    with OUT.open("w", encoding="utf-8", newline="") as f:
        fields = ["page", "block", "component", "signal_or_pin", "net", "value_or_note", "status"]
        writer = csv.DictWriter(f, fieldnames=fields)
        writer.writeheader()
        count = 0
        for row in source:
            for net in expand(row["net"]):
                writer.writerow({**row, "net": net})
                count += 1
    print(f"wrote {OUT} ({count} normalized connections)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
