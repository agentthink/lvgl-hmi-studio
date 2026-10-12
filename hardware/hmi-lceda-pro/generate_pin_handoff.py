#!/usr/bin/env python3
"""Generate exact LCD 50-pin ↔ T113/RTP/power handoff and check conflicts."""
from __future__ import annotations
import csv
from pathlib import Path

BASE = Path(__file__).resolve().parent
PINOUT = BASE / "lcd-50pin-pinout.csv"
ASSIGN = BASE / "t113-lcd-net-assignment.csv"
OUT = BASE / "lcd-t113-pin-handoff.csv"
REPORT = BASE / "lcd-t113-pin-handoff.md"


def read_rows(path: Path):
    rows = []
    with path.open(encoding="utf-8", newline="") as f:
        reader = csv.DictReader(f)
        for row in reader:
            extras = row.pop(None, []) or []
            if extras:
                row["remark"] = ", ".join([row.get("remark", ""), *extras]).strip(" ,")
            rows.append(row)
    return rows


def main() -> int:
    pinout = read_rows(PINOUT)
    assign = read_rows(ASSIGN)
    by_net = {r["net"]: r for r in assign}
    assignment_issues = []
    handoff = []
    for p in pinout:
        pin = int(p["pin"])
        signal = p["signal"]
        a = by_net.get(signal)
        if a:
            soc_pin, source = a["soc_pin"], "T113/TPADC"
            note = a.get("notes", "")
        elif signal in {"GND", "NC"}:
            soc_pin, source = ("GND", "panel ground") if signal == "GND" else ("NC", "no connect")
            note = p.get("remark", "")
        elif signal == "LCD_DVDD":
            soc_pin, source, note = "VCC3V3", "power rail", "3.3V typical"
        elif signal == "LCD_AVDD":
            soc_pin, source, note = "LCD_AVDD", "panel-bias rail", "9.0V typical"
        elif signal == "LCD_VGH":
            soc_pin, source, note = "LCD_VGH", "panel-bias rail", "18.0V typical"
        elif signal == "LCD_VGL":
            soc_pin, source, note = "LCD_VGL", "panel-bias rail", "-6.0V typical"
        elif signal == "LCD_VCOM":
            soc_pin, source, note = "LCD_VCOM", "panel-bias rail", "3.15V typical"
        elif signal in {"VLED_A", "VLED_K"}:
            soc_pin, source, note = signal, "backlight rail", "9.6V/180mA typical"
        else:
            assignment_issues.append(f"no assignment for panel signal {signal} (pin {pin})")
            soc_pin, source, note = "TBD", "unassigned", "requires T113/power review"
        handoff.append({"panel_pin": pin, "panel_signal": signal, "source": source, "soc_or_rail": soc_pin, "note": note})

    panel_pins = [r["panel_pin"] for r in handoff]
    if panel_pins != list(range(1, 51)):
        assignment_issues.append("panel pin handoff is not exactly 1..50")
    rgb = [r for r in assign if r["function"].startswith("LCD_") and r["function"][4] in "BGR" and r["function"][5:].isdigit()]
    soc_pins = [r["soc_pin"] for r in rgb]
    if len(soc_pins) != len(set(soc_pins)):
        assignment_issues.append("duplicate T113 pin in RGB assignment")
    panel_assigned = [int(r["panel_pin"]) for r in rgb]
    if len(panel_assigned) != len(set(panel_assigned)):
        assignment_issues.append("duplicate panel pin in RGB assignment")
    expected_rgb = set(range(12, 36))
    if set(panel_assigned) != expected_rgb:
        assignment_issues.append("RGB assignment does not cover panel pins 12..35 exactly")

    with OUT.open("w", encoding="utf-8", newline="") as f:
        fields = ["panel_pin", "panel_signal", "source", "soc_or_rail", "note"]
        writer = csv.DictWriter(f, fieldnames=fields); writer.writeheader(); writer.writerows(handoff)
    md = ["# LCD 50PIN ↔ T113/RTP/Power pin handoff", "", "| LCD pin | signal | source | T113/power net | note |", "|---:|---|---|---|---|"]
    md += [f"| {r['panel_pin']} | `{r['panel_signal']}` | {r['source']} | `{r['soc_or_rail']}` | {r['note']} |" for r in handoff]
    md += ["", "## Check result", ""]
    if assignment_issues:
        md += ["**FAIL**"] + [f"- {x}" for x in assignment_issues]
    else:
        md.append("**PASS — all 50 LCD pins represented; RGB pins 12..35 are unique and complete.**")
    REPORT.write_text("\n".join(md) + "\n", encoding="utf-8")
    print(f"pin handoff: {'PASS' if not assignment_issues else 'FAIL'} ({len(handoff)} panel pins, {len(assignment_issues)} issues)")
    return 0 if not assignment_issues else 1


if __name__ == "__main__":
    raise SystemExit(main())
