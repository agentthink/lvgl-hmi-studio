#!/usr/bin/env python3
"""Validate the new T113-S3 LCD/RTP assignment before LCEDA-Pro generation.

This is intentionally a gate, not a fake final schematic generator.  The screen PDF
has no guaranteed DVDD/AVDD/VGH/VGL/VCOM/backlight operating values, and the RTP FPC
pin order is not supplied.  Generation of power/connector symbols is refused until
those values are supplied explicitly in a design-parameters.json file.
"""
from __future__ import annotations

import csv
import json
from pathlib import Path

BASE = Path(__file__).resolve().parent
PINOUT = BASE / "lcd-50pin-pinout.csv"
ASSIGN = BASE / "t113-lcd-net-assignment.csv"
PARAMS = BASE / "design-parameters.json"
REQUIRED = {
    "dvdd_v",
    "avdd_v",
    "vgh_v",
    "vgl_v",
    "vcom_v",
    "backlight_voltage_v",
    "backlight_current_a",
}
OPTIONAL_TBD = {"rtp_fpc_pinout"}


def read_csv(path: Path):
    with path.open(newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def validate_pinout(rows):
    pins = [int(r["pin"]) for r in rows]
    if pins != list(range(1, 51)):
        raise SystemExit("lcd-50pin-pinout.csv must contain pins 1..50 exactly once")
    expected = {
        1: "VLED_A", 2: "VLED_A", 3: "VLED_K", 4: "VLED_K",
        5: "GND", 6: "LCD_VCOM", 7: "LCD_DVDD", 8: "LCD_MODE",
        9: "LCD_DE", 10: "LCD_VSYNC", 11: "LCD_HSYNC", 37: "LCD_DCLK",
        39: "LCD_SHLR", 40: "LCD_UPDN", 41: "LCD_VGH", 42: "LCD_VGL",
        43: "LCD_AVDD", 44: "LCD_RESET", 47: "LCD_DITH",
        45: "NC", 49: "NC", 50: "NC",
    }
    by_pin = {int(r["pin"]): r["signal"] for r in rows}
    for pin, signal in expected.items():
        if by_pin[pin] != signal:
            raise SystemExit(f"panel pin {pin}: expected {signal}, got {by_pin[pin]}")
    for start, prefix in ((12, "LCD_B"), (20, "LCD_G"), (28, "LCD_R")):
        for offset in range(8):
            pin = start + offset
            expected_signal = f"{prefix}{7-offset}"
            if by_pin[pin] != expected_signal:
                raise SystemExit(f"panel pin {pin}: expected {expected_signal}, got {by_pin[pin]}")


def validate_assignment(rows):
    required = {f"LCD_{c}{i}" for c in "BGR" for i in range(8)}
    required |= {"LCD_DCLK", "LCD_DE", "LCD_HSYNC", "LCD_VSYNC", "LCD_BL_PWM"}
    nets = {r["net"] for r in rows}
    missing = sorted(required - nets)
    if missing:
        raise SystemExit("missing T113 LCD nets: " + ", ".join(missing))
    mapping = {r["net"]: r["soc_pin"] for r in rows}
    if mapping["LCD_BL_PWM"] != "PD22":
        raise SystemExit("LCD_BL_PWM must be PWM7/PD22")
    if mapping["LCD_DCLK"] != "PD18" or mapping["LCD_DE"] != "PD19":
        raise SystemExit("LCD control mapping does not match the RGB24 firmware mapping")


def load_params():
    if not PARAMS.exists():
        return {}, [f"missing {PARAMS.name}"]
    data = json.loads(PARAMS.read_text(encoding="utf-8"))
    missing = sorted(REQUIRED - data.keys())
    return data, missing


def main():
    validate_pinout(read_csv(PINOUT))
    validate_assignment(read_csv(ASSIGN))
    params, missing = load_params()
    print("LCD 50PIN: validated")
    print("T113 RGB888/RTP assignment: validated")
    if missing:
        print("BLOCKED: final LCEDA-Pro schematic generation requires:")
        for item in missing:
            print(f"  - {item}")
        print("Do not generate panel-bias/backlight power values from assumptions.")
        return 2
    unresolved = [
        key for key, value in params.items()
        if value in (None, "", "TBD")
        or (isinstance(value, str) and value.startswith("TBD"))
    ]
    required_unresolved = sorted(set(unresolved) & REQUIRED)
    optional_unresolved = sorted(set(unresolved) & OPTIONAL_TBD)
    if required_unresolved:
        print("BLOCKED: required design parameters still contain TBD/empty values:")
        for item in required_unresolved:
            print(f"  - {item}")
        return 2
    if optional_unresolved:
        print("WARNING: optional items remain vendor-defined:")
        for item in optional_unresolved:
            print(f"  - {item}")
    print("LCD power/interface baseline validated; LCEDA-Pro capture may proceed with RTP electrical pinout fixed and mechanical connector TBD.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
