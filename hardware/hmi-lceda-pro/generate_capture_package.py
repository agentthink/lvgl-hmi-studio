#!/usr/bin/env python3
"""Generate reviewable LCEDA-Pro capture sheets from the approved net matrix.

LCEDA-Pro's native project format is proprietary, so this produces deterministic
page manifests and connection tables for manual capture/import. It does not claim
to be a native LCEDA project.
"""
from __future__ import annotations
import csv
from collections import defaultdict
from pathlib import Path

BASE = Path(__file__).resolve().parent
MATRIX = BASE / "lceda-page-net-matrix.csv"
OUT = BASE / "capture-package"


def main() -> int:
    rows = []
    with MATRIX.open(encoding="utf-8", newline="") as f:
        reader = csv.DictReader(f)
        for raw in reader:
            extras = raw.pop(None, []) or []
            if extras:
                raw["value_or_note"] = ", ".join([raw["value_or_note"], *extras[:-1]])
                raw["status"] = extras[-1]
            rows.append(raw)
    pages = defaultdict(list)
    for row in rows:
        pages[row["page"]].append(row)
    OUT.mkdir(exist_ok=True)
    index = [
        "# LCEDA-Pro HMI capture package",
        "",
        "> Generated from `lceda-page-net-matrix.csv`. Capture these pages in LCEDA-Pro; this is not a native LCEDA project file.",
        "",
    ]
    for page in sorted(pages, key=lambda x: int(x[1:])):
        page_rows = pages[page]
        blocks = sorted({r["block"] for r in page_rows})
        md = [f"# {page} — {', '.join(blocks)}", "", "| Component | Pin/signal | Net | Value/note | Status |", "|---|---|---|---|---|"]
        for r in page_rows:
            md.append(f"| `{r['component']}` | `{r['signal_or_pin']}` | `{r['net']}` | {r['value_or_note']} | **{r['status']}** |")
        md += ["", "## Capture rules", "", "- Use exact net names; do not merge ISO_GND with GND.", "- Add ERC power flags on each generated rail.", "- Mark TBD components/footprints as pending rather than selecting a guessed part.", ""]
        (OUT / f"{page}.md").write_text("\n".join(md), encoding="utf-8")
        with (OUT / f"{page}.csv").open("w", encoding="utf-8", newline="") as f:
            writer = csv.DictWriter(f, fieldnames=rows[0].keys())
            writer.writeheader(); writer.writerows(page_rows)
        index.append(f"- [{page}]({page}.md) — {', '.join(blocks)}")
    (OUT / "README.md").write_text("\n".join(index) + "\n", encoding="utf-8")
    print(f"generated {len(pages)} capture pages in {OUT}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
