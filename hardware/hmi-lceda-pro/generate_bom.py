#!/usr/bin/env python3
"""Build a review BOM from the LCEDA page matrix and footprint map."""
from __future__ import annotations
import csv
from collections import defaultdict
from pathlib import Path

BASE = Path(__file__).resolve().parent
MATRIX = BASE / "lceda-page-net-matrix.csv"
FOOTPRINTS = BASE / "footprints.csv"
OUT = BASE / "bom-review.csv"

# Matrix references are schematic refs; footprint map keys are part/library IDs.
# Keep this explicit so a refactor cannot silently turn all footprint matches into TBD.
REF_TO_LIB = {
    "U6": "T113_S3", "U7": "W25N01GV", "J3": "FPC50", "J4": "RTP_FPC",
    "U1": "MP1584EN", "U12": "TPS65100", "U13": "MT9284", "U11": "IP101GRI",
    "U9": "MAX3485", "U10": "MAX3232", "J7": "USB-C", "J8": "USB-A",
    "J9": "RJ45-MAG", "J2": "TF_SOCKET", "X1": "OSC24M", "X2": "X32K",
    "D1": "TVS", "Q1": "AOD4184", "J1": "DC_INPUT", "J10": "HDR_1x4",
    "J11": "HDR_2x12", "J4": "RTP_FPC", "J8": "USB-A", "J7": "USB-C",
}


def main() -> int:
    matrix = list(csv.DictReader(MATRIX.open(encoding="utf-8", newline="")))
    fp = {r["lib_id"]: r for r in csv.DictReader(FOOTPRINTS.open(encoding="utf-8", newline=""))}
    grouped = defaultdict(lambda: {"pages": set(), "signals": set(), "statuses": set()})
    for r in matrix:
        key = r["component"]
        item = grouped[key]
        item["pages"].add(r["page"])
        item["signals"].add(r["net"])
        item["statuses"].add(r["status"])
    with OUT.open("w", encoding="utf-8", newline="") as f:
        fields = ["reference", "pages", "nets", "status", "footprint", "footprint_status", "notes"]
        writer = csv.DictWriter(f, fieldnames=fields)
        writer.writeheader()
        for ref in sorted(grouped):
            item = grouped[ref]
            lib_id = REF_TO_LIB.get(ref, ref)
            match = fp.get(lib_id, {})
            writer.writerow({
                "reference": ref,
                "pages": ";".join(sorted(item["pages"])),
                "nets": ";".join(sorted(item["signals"])),
                "status": ";".join(sorted(item["statuses"])),
                "footprint": match.get("lc_eda_footprint", "TBD"),
                "footprint_status": match.get("status", "not mapped"),
                "notes": f"{lib_id}: " + match.get("notes", "Assign value and manufacturer part in LCEDA-Pro"),
            })
    print(f"wrote {OUT} ({len(grouped)} entries)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
