#!/usr/bin/env python3
"""Parse T113-S3 datasheet Table 4-2 (Pin Characteristics) from pdftotext output.

The table is a fixed-width layout from pdftotext -layout. Each data row has:
  Ball#  Pin Name  Type  ResetState  PullUp/Down  DefaultStrength(mA)  PowerSupply
Group header lines (e.g. "SDRAM", "GPIOB") separate signal groups.
"""
import re
import sys

SRC = "/home/chen/Git/zhishu-hmi/hardware/hmi-lceda-pro/kicad-export/refs/t113.txt"
OUT = "/home/chen/Git/zhishu-hmi/hardware/hmi-lceda-pro/kicad-export/refs/t113-pins.csv"

def main():
    lines = open(SRC, encoding="utf-8", errors="replace").read().splitlines()
    # Pin Characteristics section: between "4.2   Pin Characteristics" and "4.3   GPIO Multiplex Function"
    start = end = None
    for i, ln in enumerate(lines):
        if "4.2   Pin Characteristics" in ln:
            start = i
        if "4.3   GPIO Multiplex Function" in ln and start is not None:
            end = i
            break
    assert start is not None and end is not None, (start, end)
    body = lines[start:end]

    rows = []
    group = ""
    for ln in body:
        s = ln.strip()
        if not s:
            continue
        # group header lines: plain uppercase token(s) without leading ball number
        if re.fullmatch(r"[A-Z][A-Z0-9 _/&()-]*", s) and not re.match(r"^\d+\s", s):
            if any(k in s for k in ("SDRAM", "GPIO", "VCC", "VDD", "GND", "RESET", "OSC", "PLL", "EPAD", "DZQ", "TWI", "NC", "LDO", "USB", "MIC", "LINE", "FM", "HP", "TV", "TP", "GPADC", "REF")):
                group = s
            continue
        m = re.match(r"^(\d+)\s+(\S+)\s+(.+)$", s)
        if not m:
            continue
        ball, name, rest = m.groups()
        toks = rest.split()
        if len(toks) < 5:
            continue
        supply = toks[-1]
        # tail should be: NA NA NA <supply>  (reset_state, pull, strength)
        tail = toks[-4:]
        if tail[0] == "NA" and tail[1] == "NA" and tail[2] == "NA":
            ptype = " ".join(toks[:-4])
        else:
            # fallback: type = first token(s), ignore rest
            ptype = toks[0]
            supply = tail[-1]
        rows.append((ball, name, ptype, tail[0], tail[1], tail[2], supply, group))

    # EPAD row may be special ("129  EPAD  G ...")
    with open(OUT, "w", encoding="utf-8") as f:
        f.write("ball,pin_name,type,reset_state,pull,strength,power_supply,group\n")
        for r in rows:
            f.write(",".join(r) + "\n")
    print(f"parsed {len(rows)} pin rows -> {OUT}")
    # quick summary by type
    from collections import Counter
    c = Counter(r[2] for r in rows)
    print("types:", dict(c))
    # verify all expected balls 1..128 + 129 present
    balls = sorted(int(r[0]) for r in rows)
    print("ball range:", balls[0], "-", balls[-1], "count:", len(balls))
    missing = [b for b in range(1, 129) if b not in balls]
    print("missing balls 1-128:", missing)

if __name__ == "__main__":
    main()
