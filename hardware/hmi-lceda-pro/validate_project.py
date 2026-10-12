#!/usr/bin/env python3
"""Cross-check the LCEDA-Pro capture source package."""
from __future__ import annotations
import csv, json
from pathlib import Path

BASE = Path(__file__).resolve().parent

def csv_rows(path):
    with path.open(encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))

def main():
    project = json.loads((BASE / "project.json").read_text(encoding="utf-8"))
    matrix = csv_rows(BASE / "lceda-page-net-matrix.csv")
    normalized = csv_rows(BASE / "normalized-netlist.csv")
    handoff = csv_rows(BASE / "lcd-t113-pin-handoff.csv")
    pinout = csv_rows(BASE / "lcd-50pin-pinout.csv")
    assign = csv_rows(BASE / "t113-lcd-net-assignment.csv")
    assert [int(r["pin"]) for r in pinout] == list(range(1, 51))
    assert len(normalized) > len(matrix)
    assert len(handoff) == 50
    assert [int(r["panel_pin"]) for r in handoff] == list(range(1, 51))
    assert all(r["net"] and r["page"] and r["component"] for r in normalized)
    assert {r["page"] for r in matrix} == {f"P{i}" for i in range(1, 10)}
    assert len(project["pages"]) == 9
    assert {p["id"] for p in project["pages"]} == {f"P{i}" for i in range(1, 10)}
    nets = {r["net"] for r in assign}
    for net in [f"LCD_{c}{i}" for c in "BGR" for i in range(8)]:
        assert net in nets, net
    assert dict((r["net"], r["soc_pin"]) for r in assign)["LCD_BL_PWM"] == "PD22"
    rtp = project["confirmed_design"]["rtp_pinout"]
    assert rtp == {"1": "TCH_YN", "2": "TCH_XP", "3": "TCH_YP", "4": "TCH_XN"}
    for page in project["pages"]:
        assert (BASE / page["source"]).exists(), page["source"]
    print(f"project validated: {len(matrix)} matrix rows, {len(normalized)} normalized connections, {len(handoff)} pin handoff rows, {len(pinout)} LCD pins, {len(assign)} SoC assignments, 9 pages")
    print("RTP electrical pinout validated; connector mechanical footprint remains pending")

if __name__ == "__main__":
    main()
