#!/usr/bin/env python3
"""Audit cross-page connectivity in the LCEDA-Pro capture source package."""
from __future__ import annotations
import csv
from collections import defaultdict
from pathlib import Path

BASE = Path(__file__).resolve().parent
MATRIX = BASE / "lceda-page-net-matrix.csv"
ASSIGN = BASE / "t113-lcd-net-assignment.csv"
OUT = BASE / "connectivity-audit.md"


def expand_net(text: str) -> set[str]:
    """Expand compact slash/range notation used in the matrix."""
    if text in {"TCH_XP/XN/YP/YN"}:
        return {"TCH_XP", "TCH_XN", "TCH_YP", "TCH_YN"}
    if text == "LCD_B0..LCD_R7":
        return {f"LCD_{c}{i}" for c in "BGR" for i in range(8)}
    if text in {"LCD_B7..LCD_B0", "LCD_B0..LCD_B7"}:
        return {f"LCD_B{i}" for i in range(8)}
    if text in {"LCD_G7..LCD_G0", "LCD_G0..LCD_G7"}:
        return {f"LCD_G{i}" for i in range(8)}
    if text in {"LCD_R7..LCD_R0", "LCD_R0..LCD_R7"}:
        return {f"LCD_R{i}" for i in range(8)}
    if "/" in text:
        return {part for part in text.split("/") if part.startswith(("LCD_", "VLED_", "TCH_"))}
    return {text}


def rows(path):
    with path.open(encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def main() -> int:
    matrix = rows(MATRIX)
    assign = rows(ASSIGN)
    occurrences: dict[str, set[str]] = defaultdict(set)
    for row in matrix:
        for net in expand_net(row["net"]):
            occurrences[net].add(row["page"])

    assignment_nets = {row["net"] for row in assign}
    lcd_nets = {f"LCD_{c}{i}" for c in "BGR" for i in range(8)}
    lcd_nets |= {"LCD_DCLK", "LCD_DE", "LCD_HSYNC", "LCD_VSYNC", "LCD_BL_PWM", "LCD_MODE", "LCD_RESET", "LCD_SHLR", "LCD_UPDN", "LCD_DITH"}
    expected_lcd_pages = {"LCD_RGB888": "P4", "SOC_CORE": "P2"}
    issues: list[str] = []

    for net in sorted(lcd_nets):
        if net not in assignment_nets:
            issues.append(f"missing SoC assignment: `{net}`")
        if net not in occurrences:
            issues.append(f"missing matrix occurrence: `{net}`")
        elif "P2" not in occurrences[net] and net not in {"LCD_MODE", "LCD_SHLR", "LCD_UPDN", "LCD_DITH"}:
            issues.append(f"`{net}` has no P2/SOC_CORE occurrence")
        if net not in occurrences or ("P4" not in occurrences[net] and net != "LCD_BL_PWM"):
            issues.append(f"`{net}` has no P4/LCD_RGB888 occurrence")

    required_pairs = {
        "VLED_A": {"P4", "P5"}, "VLED_K": {"P4", "P5"},
        "LCD_AVDD": {"P4", "P5"}, "LCD_VGH": {"P4", "P5"},
        "LCD_VGL": {"P4", "P5"}, "LCD_VCOM": {"P4", "P5"},
        "LCD_BL_PWM": {"P2", "P5"}, "TCH_XP": {"P2", "P6"},
        "TCH_XN": {"P2", "P6"}, "TCH_YP": {"P2", "P6"},
        "TCH_YN": {"P2", "P6"},
    }
    for net, pages in required_pairs.items():
        missing = pages - occurrences.get(net, set())
        if missing:
            issues.append(f"`{net}` missing expected page(s): {', '.join(sorted(missing))}")

    report = ["# Cross-page connectivity audit", "", f"- Matrix rows: {len(matrix)}", f"- Unique expanded nets: {len(occurrences)}", f"- LCD/T113 assignment nets: {len(assignment_nets)}", ""]
    if issues:
        report += ["## Issues", ""] + [f"- {issue}" for issue in issues]
    else:
        report += ["## Result", "", "No required LCD/RTP/panel-power cross-page connectivity gaps detected."]
    report += ["", "## Key page occurrences", ""]
    for net in sorted(required_pairs):
        report.append(f"- `{net}`: {', '.join(sorted(occurrences.get(net, set()))) or 'NONE'}")
    OUT.write_text("\n".join(report) + "\n", encoding="utf-8")
    print(f"connectivity audit: {'PASS' if not issues else 'FAIL'} ({len(issues)} issues)")
    return 0 if not issues else 1


if __name__ == "__main__":
    raise SystemExit(main())
