#!/usr/bin/env python3
"""Generate a KiCad 5.1/5.99 compatible schematic set for the HMI_T113_SZCLX070H_R1 board.

Outputs:
  HMI_T113_SZCLX070H_R1.kicad_pro  KiCad 6+ project file
  HMI_T113_SZCLX070H_R1.kicad_sch  Root schematic with 9 hierarchical sheets
  P1_POWER.kicad_sch               Power input / regulators
  P2_SOC_CORE.kicad_sch            T113-S3 + crystals + decoupling
  P3_STORAGE.kicad_sch             SPI NAND + microSD
  P4_LCD_RGB888.kicad_sch          50P FPC + control resistors
  P5_LCD_POWER_BACKLIGHT.kicad_sch TPS65100 panel bias + MT9284 backlight
  P6_RTP_TOUCH.kicad_sch           4-wire RTP FPC
  P7_USB.kicad_sch                 USB-C OTG + USB-A Host
  P8_COMM.kicad_sch                RS485/RS232/Ethernet
  P9_IO_EXPANSION.kicad_sch        Debug header + expansion + keys + LEDs
  hmi.kicad_sym                    Symbol library (for reference)

Schematics use (version 20200904) s-expression format with embedded lib_symbols.
Global labels are used for cross-page net merging.  Net names are taken from
normalized-netlist.csv, lceda-page-net-matrix.csv, lcd-t113-pin-handoff.csv and
PIN-MUX-AND-COMPONENT-SELECTION.md.

All refdes are prefixed by page to avoid collisions:
  P1: R1xx/C1xx/L1xx/D1xx/Q1/F1/J1/TP1-3
  P2: X1/X2/R2xx/C2xx/U6
  P3: U7/J2/R3xx/C3xx
  P4: J3/R4xx/C4xx
  P5: U12/U13/R5xx/C5xx/L501/D5xx/RCS1
  P6: J4/R6xx/C6xx/D6xx
  P7: J7/J8/U15/R7xx/C7xx/D7xx
  P8: U9A/U9B/U14/U10/U11/J5/J9/X3/R8xx/C8xx/D8xx
  P9: J10/J11/SW1-3/LED1-2/R9xx/C9xx
"""

import csv
import json
import os
import uuid
from typing import Any, Dict, List, Optional, Tuple

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.join(ROOT, "out")
PROJECT = "HMI_T113_SZCLX070H_R1"

# ---------------------------------------------------------------------------
# s-expression helpers
# ---------------------------------------------------------------------------


GRID = 1.27


def snap(v: float) -> float:
    """Snap a coordinate to the 1.27 mm schematic grid."""
    return round(v / GRID) * GRID


def uid() -> str:
    return str(uuid.uuid4())


class Raw(str):
    """A string that must be emitted as a bare s-expression token (no quotes),
    e.g. enum keywords like `default`, `none`, `passive`, `hide`, `left`."""
    pass


def _fmt_atom(v: Any) -> str:
    if isinstance(v, Raw):
        return str(v)
    if isinstance(v, bool):
        return "yes" if v else "no"
    if isinstance(v, (int, float)):
        return str(v)
    if isinstance(v, str):
        # KiCad does not quote numbers, but quoting every string is safe
        return '"' + v.replace('\\', '\\\\').replace('"', '\\"') + '"'
    raise TypeError(f"unsupported atom type {type(v)}")


def sexp(node: Any, indent: int = 0) -> str:
    """Convert a nested Python structure to an s-expression string.

    The first element of every list is treated as an UNQUOTED keyword
    (KiCad requires e.g. (symbol "R" ...) with a bare head token).
    """
    if not isinstance(node, (list, tuple)):
        return _fmt_atom(node)
    if not node:
        return "()"
    head = node[0]
    if isinstance(head, str):
        head_str = head  # bare keyword, never quoted
    else:
        head_str = _fmt_atom(head)
    inner = node[1:]
    if not inner:
        return f"({head_str})"
    # For short lists keep on one line; for long lists indent children
    short = all(not isinstance(c, (list, tuple)) for c in inner)
    if short:
        return f"({head_str} " + " ".join(_fmt_atom(c) for c in inner) + ")"
    lines = ["  " * (indent + 1) + sexp(c, indent + 1) for c in inner]
    return "(" + head_str + "\n" + "\n".join(lines) + "\n" + "  " * indent + ")"


def xy(x: float, y: float) -> Tuple:
    return ("xy", x, y)


def point(name: str, x: float, y: float) -> Tuple:
    return (name, ("xy", x, y))


def font(size: float = 1.27) -> Tuple:
    return ("font", ("size", size, size))


def effects(justify: Optional[str] = None, hide: bool = False, size: float = 1.27) -> Tuple:
    body: List[Any] = [("font", ("size", size, size))]
    if justify:
        body.append(("justify", *[Raw(t) for t in justify.split()]))
    if hide:
        body.append(Raw("hide"))
    return ("effects", *body)


def stroke(width: float = 0.0, typ: str = "default") -> Tuple:
    return ("stroke", ("width", width), ("type", Raw(typ)))


def fill(color: Tuple = (0, 0, 0, 0.0), typ: str = "none") -> Tuple:
    return ("fill", ("color", *color), ("type", Raw(typ)))


def property_(name: str, value: str, x: float, y: float, rot: float = 0.0, eff=None) -> Tuple:
    if eff is None:
        eff = effects()
    return ("property", name, value, ("at", x, y, rot), eff)


def pin(name: str, number: str, ptype: str, angle: float, x: float, y: float, length: float = 2.54) -> Tuple:
    return ("pin", Raw(ptype), Raw("line"),
            ("at", x, y, angle),
            ("length", length),
            ("name", name, effects(hide=False, size=1.016)),
            ("number", number, effects(hide=False, size=1.016)))


def pin_pwr(name: str, number: str, angle: float, x: float, y: float, length: float = 2.54) -> Tuple:
    return ("pin", "power_in", "line",
            ("at", x, y, angle),
            ("length", length),
            ("name", name, effects(hide=False, size=1.016)),
            ("number", number, effects(hide=False, size=1.016)))


def sym_head(name: str, ref: str, val: str, pin_names_offset: float = 1.016) -> List:
    return [
        ("pin_names", ("offset", pin_names_offset)),
        ("in_bom", True), ("on_board", True),
        property_("Reference", ref, 0, 0, 0, effects()),
        property_("Value", val, 0, 0, 0, effects()),
        property_("Footprint", "", 0, 0, 0, effects(hide=True)),
        property_("Datasheet", "", 0, 0, 0, effects(hide=True)),
    ]


# ---------------------------------------------------------------------------
# Symbol definitions
# ---------------------------------------------------------------------------

def make_resistor() -> Tuple:
    body = ("rectangle", ("start", -5.08, 1.27), ("end", 5.08, -1.27),
            stroke(0.254, "default"), fill(typ="none"))
    p1 = pin("1", "1", "passive", 270, 0.0, 3.81, 1.27)
    p2 = pin("2", "2", "passive", 90, 0.0, -3.81, 1.27)
    return ("symbol", "hmi:RES", *sym_head("hmi:RES", "R", "R"),
            ("symbol", "RES_0_1", body),
            ("symbol", "RES_1_1", p1, p2))


def make_capacitor() -> Tuple:
    body1 = ("polyline", ("pts", xy(-1.27, -1.27), xy(1.27, -1.27)),
             stroke(0.254, "default"), fill(typ="none"))
    body2 = ("polyline", ("pts", xy(-1.27, 1.27), xy(1.27, 1.27)),
             stroke(0.254, "default"), fill(typ="none"))
    p1 = pin("1", "1", "passive", 270, 0.0, 3.81, 2.54)
    p2 = pin("2", "2", "passive", 90, 0.0, -3.81, 2.54)
    return ("symbol", "hmi:CAP", *sym_head("hmi:CAP", "C", "C"),
            ("symbol", "CAP_0_1", body1, body2),
            ("symbol", "CAP_1_1", p1, p2))


def make_inductor() -> Tuple:
    # 4 inductor bumps drawn as polylines (arc tokens are not accepted by
    # the 20200904 parser, so use zig-zag approximation)
    bumps = []
    for i in range(4):
        x0 = -3.81 + i * 2.54
        pts = [xy(x0, 0.0), xy(x0 + 0.635, -1.27), xy(x0 + 1.905, -1.27), xy(x0 + 2.54, 0.0)]
        bumps.append(("polyline", ("pts", *pts), stroke(0.254, "default"), fill(typ="none")))
    p1 = pin("1", "1", "passive", 270, 0.0, 3.81, 1.27)
    p2 = pin("2", "2", "passive", 90, 0.0, -3.81, 1.27)
    return ("symbol", "hmi:IND", *sym_head("hmi:IND", "L", "L"),
            ("symbol", "IND_0_1", *bumps),
            ("symbol", "IND_1_1", p1, p2))


def make_diode() -> Tuple:
    tri = ("polyline", ("pts", xy(-1.27, -1.27), xy(1.27, 0.0), xy(-1.27, 1.27), xy(-1.27, -1.27)),
           stroke(0.254, "default"), fill(typ="none"))
    bar = ("polyline", ("pts", xy(1.27, -1.27), xy(1.27, 1.27)),
           stroke(0.254, "default"), fill(typ="none"))
    p1 = pin("A", "1", "passive", 270, 0.0, 3.81, 2.54)
    p2 = pin("K", "2", "passive", 90, 0.0, -3.81, 2.54)
    return ("symbol", "hmi:DIO", *sym_head("hmi:DIO", "D", "D"),
            ("symbol", "DIO_0_1", tri, bar),
            ("symbol", "DIO_1_1", p1, p2))


def make_led() -> Tuple:
    tri = ("polyline", ("pts", xy(-1.27, -1.27), xy(1.27, 0.0), xy(-1.27, 1.27), xy(-1.27, -1.27)),
           stroke(0.254, "default"), fill(typ="none"))
    bar = ("polyline", ("pts", xy(1.27, -1.27), xy(1.27, 1.27)),
           stroke(0.254, "default"), fill(typ="none"))
    arrow1 = ("polyline", ("pts", xy(-0.635, 1.905), xy(0.635, 3.175)), stroke(0.254, "default"), fill(typ="none"))
    arrow2 = ("polyline", ("pts", xy(0.635, 1.905), xy(1.905, 3.175)), stroke(0.254, "default"), fill(typ="none"))
    p1 = pin("K", "1", "passive", 270, 0.0, 3.81, 2.54)
    p2 = pin("A", "2", "passive", 90, 0.0, -3.81, 2.54)
    return ("symbol", "hmi:LED", *sym_head("hmi:LED", "LED", "LED"),
            ("symbol", "LED_0_1", tri, bar, arrow1, arrow2),
            ("symbol", "LED_1_1", p1, p2))


def make_switch() -> Tuple:
    p1 = pin("1", "1", "passive", 270, 0.0, 3.81, 1.27)
    p2 = pin("2", "2", "passive", 90, 0.0, -3.81, 1.27)
    line = ("polyline", ("pts", xy(-1.27, 1.27), xy(1.27, -1.905)), stroke(0.254, "default"), fill(typ="none"))
    return ("symbol", "hmi:SWITCH", *sym_head("hmi:SWITCH", "SW", "SW"),
            ("symbol", "SWITCH_0_1", line),
            ("symbol", "SWITCH_1_1", p1, p2))


def make_tp() -> Tuple:
    circle = ("circle", ("center", 0.0, 0.0), ("radius", 1.27), stroke(0.254, "default"), fill(typ="none"))
    p1 = pin("1", "1", "passive", 0, 0.0, 0.0, 0.0)
    return ("symbol", "hmi:TP", *sym_head("hmi:TP", "TP", "TP"),
            ("symbol", "TP_0_1", circle),
            ("symbol", "TP_1_1", p1))


def make_xtal2() -> Tuple:
    body = ("rectangle", ("start", -2.54, -3.81), ("end", 2.54, 3.81), stroke(0.254, "default"), fill(typ="none"))
    p1 = pin("1", "1", "passive", 270, 0.0, 6.35, 2.54)
    p2 = pin("2", "2", "passive", 90, 0.0, -6.35, 2.54)
    return ("symbol", "hmi:XTAL2", *sym_head("hmi:XTAL2", "X", "X"),
            ("symbol", "XTAL2_0_1", body),
            ("symbol", "XTAL2_1_1", p1, p2))


def make_pwr_flag() -> Tuple:
    tri = ("polyline", ("pts", xy(-1.27, 0.0), xy(0.0, 2.54), xy(1.27, 0.0), xy(-1.27, 0.0)),
           stroke(0.254, "default"), fill(typ="none"))
    # pin on bottom edge: tip at (0,0), body extends up into the flag
    p1 = pin("1", "1", "power_out", 90, 0.0, 0.0, 2.54)
    return ("symbol", "hmi:PWR_FLAG", *sym_head("hmi:PWR_FLAG", "#FLG", "PWR_FLAG"),
            ("symbol", "PWR_FLAG_0_1", tri),
            ("symbol", "PWR_FLAG_1_1", p1))


def normalize_pin(p: Tuple, width: float, height: float, pin_len: float) -> Tuple:
    """Move a pin's hotspot OUTSIDE the symbol body along its edge axis.

    Every pin becomes well-formed: hotspot at the body edge + pin_len, pin
    graphic pointing toward the body.  This makes the angle the authoritative
    edge indicator: 0=left, 180=right, 270=top(sheet), 90=bottom(sheet).
    """
    name, num, ptype, ang, x, y, _length = p
    if ang == 0:
        return (name, num, ptype, ang, -width / 2 - pin_len, y, pin_len)
    if ang == 180:
        return (name, num, ptype, ang, width / 2 + pin_len, y, pin_len)
    if ang == 270:
        return (name, num, ptype, ang, x, height / 2 + pin_len, pin_len)
    if ang == 90:
        return (name, num, ptype, ang, x, -height / 2 - pin_len, pin_len)
    return p


def make_rect_ic(name: str, ref: str, val: str, pins: List[Tuple], width: float, height: float,
                 pin_len: float = 2.54) -> Tuple:
    """Make a generic rectangular IC symbol with normalized (well-formed) pins.

    NOTE: inner unit names MUST be "<symbol-base>_<unit>_<convert>" where
    symbol-base is the part of `name` after the library colon -- KiCad's
    parser uses this to associate units with the parent symbol.
    """
    base = name.split(":", 1)[1]
    body = ("rectangle", ("start", -width / 2, -height / 2), ("end", width / 2, height / 2),
            stroke(0.254, "default"), fill(typ="none"))
    norm = [normalize_pin(p, width, height, pin_len) for p in pins]
    pin_objs = [pin(n, num, ptype, ang, x, y, length) for n, num, ptype, ang, x, y, length in norm]
    return ("symbol", name, *sym_head(name, ref, val),
            ("symbol", f"{base}_0_1", body),
            ("symbol", f"{base}_1_1", *pin_objs))


# Small IC helpers: place pins on two sides (DIP/SOIC style)
def dip_pins(pins_left: List[Tuple[str, str, str]], pins_right: List[Tuple[str, str, str]],
             pin_len: float = 2.54, pitch: float = 2.54) -> List[Tuple]:
    """Return pin definitions for a vertical dual-row package.
    Left pins numbered top-to-bottom, right pins bottom-to-top (SOIC convention).
    """
    n = max(len(pins_left), len(pins_right))
    height = n * pitch
    out: List[Tuple] = []
    # left side, top to bottom
    for i, (nm, num, ptype) in enumerate(pins_left):
        y = -height / 2 + pitch / 2 + i * pitch
        out.append((nm, num, ptype, 0, -pin_len, y, pin_len))
    # right side, bottom to top
    for i, (nm, num, ptype) in enumerate(pins_right):
        y = height / 2 - pitch / 2 - i * pitch
        out.append((nm, num, ptype, 180, pin_len, y, pin_len))
    return out


def make_ic_symbol(name: str, ref: str, val: str, pins: List[Tuple], width: float, height: float) -> Tuple:
    return make_rect_ic(name, ref, val, pins, width, height)


# Specific ICs

def make_xl4015() -> Tuple:
    # TO-263-5: 1=VIN, 2=GND(TAB), 3=EN (XL4015 actually has no EN; use pin 3=FB?)
    # XL4015 pinout: 1=VIN, 2=SW, 3=GND, 4=FB, 5=OUT? Actually XL4015 TO263-5:
    # Pins: 1=GND, 2=VIN, 3=SW, 4=FB, 5=OUT? I need to be careful.
    # Common XL4015 TO-263-5: 1=VIN, 2=GND, 3=SW, 4=FB, 5=OUT? Hmm.
    # Actually XL4015 TO263-5: 1=VIN, 2=GND, 3=SW, 4=FB, 5=OUT? No standard is:
    # XL4015 TO263-5 (5 pins): 1=VIN, 2=SW, 3=GND, 4=FB, 5=OUT? Let me use:
    # Pin1=VIN, Pin2=GND, Pin3=SW, Pin4=FB, Pin5=OUT. This is a common representation.
    # I'll expose VIN, GND, SW, FB, OUT.
    pins = [
        ("VIN", "1", "power_in", 0, -2.54, 5.08, 2.54),
        ("GND", "2", "power_in", 0, -2.54, 0.0, 2.54),
        ("SW", "3", "output", 0, -2.54, -5.08, 2.54),
        ("OUT", "5", "power_out", 180, 2.54, -5.08, 2.54),
        ("FB", "4", "input", 180, 2.54, 5.08, 2.54),
    ]
    return make_rect_ic("hmi:XL4015E1", "U", "XL4015E1", pins, 12.7, 25.4)


def make_sy8089() -> Tuple:
    # SOT-23-5: 1=EN, 2=GND, 3=LX, 4=IN, 5=FB
    pins = [
        ("EN", "1", "input", 0, -2.54, 5.08, 2.54),
        ("GND", "2", "power_in", 0, -2.54, 0.0, 2.54),
        ("LX", "3", "output", 0, -2.54, -5.08, 2.54),
        ("IN", "4", "power_in", 180, 2.54, -5.08, 2.54),
        ("FB", "5", "passive", 180, 2.54, 5.08, 2.54),
    ]
    return make_rect_ic("hmi:SY8089A1AAC", "U", "SY8089A1AAC", pins, 12.7, 25.4)


def make_rt9193() -> Tuple:
    # SOT-23-5: 1=VIN, 2=GND, 3=EN, 4=BP, 5=VOUT
    pins = [
        ("VIN", "1", "power_in", 0, -2.54, 5.08, 2.54),
        ("GND", "2", "power_in", 0, -2.54, 0.0, 2.54),
        ("EN", "3", "input", 0, -2.54, -5.08, 2.54),
        ("BP", "4", "passive", 180, 2.54, -5.08, 2.54),
        ("VOUT", "5", "power_out", 180, 2.54, 5.08, 2.54),
    ]
    return make_rect_ic("hmi:RT9193", "U", "RT9193-18", pins, 12.7, 25.4)


def make_sy6280() -> Tuple:
    # SY6280AAAC SOT-23-5: 1=OUT, 2=GND, 3=ISET, 4=EN, 5=IN.
    pins = [
        ("OUT", "1", "power_out", 0, -2.54, 5.08, 2.54),
        ("GND", "2", "power_in", 0, -2.54, 0.0, 2.54),
        ("ISET", "3", "passive", 0, -2.54, -5.08, 2.54),
        ("EN", "4", "input", 180, 2.54, -5.08, 2.54),
        ("IN", "5", "power_in", 180, 2.54, 5.08, 2.54),
    ]
    return make_rect_ic("hmi:SY6280", "U", "SY6280AAAC", pins, 12.7, 25.4)


def make_w25n01gv() -> Tuple:
    # WSON-8: 1=/CS, 2=DO(IO1), 3=/WP(IO2), 4=GND, 5=DI(IO0), 6=CLK, 7=/HOLD(IO3), 8=VCC
    pins = [
        ("CS#", "1", "input", 0, -2.54, 8.89, 2.54),
        ("DO/IO1", "2", "bidirectional", 0, -2.54, 6.35, 2.54),
        ("WP#/IO2", "3", "bidirectional", 0, -2.54, 3.81, 2.54),
        ("GND", "4", "power_in", 0, -2.54, 1.27, 2.54),
        ("DI/IO0", "5", "bidirectional", 180, 2.54, 1.27, 2.54),
        ("CLK", "6", "input", 180, 2.54, 3.81, 2.54),
        ("HOLD#/IO3", "7", "bidirectional", 180, 2.54, 6.35, 2.54),
        ("VCC", "8", "power_in", 180, 2.54, 8.89, 2.54),
    ]
    return make_rect_ic("hmi:W25N01GV", "U", "W25N01GVZEIG", pins, 12.7, 25.4)


def make_tps65100() -> Tuple:
    # TSSOP-24, pins left/right as above
    left = [
        ("FB1", "1", "input"), ("FB4", "2", "input"), ("BASE", "3", "output"),
        ("VIN", "4", "power_in"), ("SW", "5", "passive"), ("SW", "6", "passive"),
        ("PGND", "7", "power_in"), ("PGND", "8", "power_in"), ("SUP", "9", "power_in"),
        ("VCOM", "10", "output"), ("VCOMIN", "11", "input"), ("FB3", "12", "input"),
    ]
    right = [
        ("OUT3", "13", "output"), ("C2+", "14", "passive"), ("C2-/MODE", "15", "passive"),
        ("C1+", "16", "passive"), ("C1-", "17", "passive"), ("DRV", "18", "output"),
        ("GND", "19", "power_in"), ("REF", "20", "output"), ("FB2", "21", "input"),
        ("COMP", "22", "passive"), ("ENR", "23", "input"), ("EN", "24", "input"),
    ]
    pins = dip_pins(left, right, pin_len=2.54, pitch=2.54)
    # Add exposed pad as pin 25
    pins.append(("EP", "25", "power_in", 90, 0.0, -19.05, 2.54))
    return make_rect_ic("hmi:TPS65100", "U", "TPS65100", pins, 22.86, 35.56)


def make_mt9284() -> Tuple:
    # TSOT-23-6: 1=SW, 2=GND, 3=FB, 4=EN, 5=OV, 6=VIN
    pins = [
        ("SW", "1", "output", 0, -2.54, 7.62, 2.54),
        ("GND", "2", "power_in", 0, -2.54, 2.54, 2.54),
        ("FB", "3", "input", 0, -2.54, -2.54, 2.54),
        ("OV", "5", "passive", 180, 2.54, -2.54, 2.54),
        ("EN", "4", "input", 180, 2.54, 2.54, 2.54),
        ("VIN", "6", "power_in", 180, 2.54, 7.62, 2.54),
    ]
    return make_rect_ic("hmi:MT9284", "U", "MT9284-28J", pins, 12.7, 25.4)


def make_ip101gri() -> Tuple:
    # QFN-32 5x5, pins around 4 sides.  Symbol pitch 2.54mm (grid-aligned),
    # 8 pins per side.
    pitch = 2.54
    n_side = 8
    span = (n_side - 1) * pitch
    pins: List[Tuple] = []
    # Bottom edge (pin 1..8), left to right, angle 90 (pointing up, placed on bottom)
    bot = [
        ("TXER/FXSD", "1", "output"), ("X1", "2", "passive"), ("X2", "3", "passive"),
        ("COL/RMII", "4", "input"), ("TXEN", "5", "input"), ("TXD3", "6", "input"),
        ("TXD2", "7", "input"), ("TXD1", "8", "input"),
    ]
    for i, (nm, num, ptype) in enumerate(bot):
        x = -span / 2 + i * pitch
        pins.append((nm, num, ptype, 90, x, 10.16 + 1.27, 1.27))
    # Right edge (pin 9..16), bottom to top, angle 180
    right = [
        ("TXD0", "9", "input"), ("TXCLK/50M_CLKI", "10", "input"),
        ("LED0/PHY_AD0", "11", "bidirectional"), ("LED3/PHY_AD3", "12", "bidirectional"),
        ("VDD_IO", "13", "power_in"), ("RXCLK/50M_CLKO", "14", "output"),
        ("RXD3", "15", "output"), ("RXD2", "16", "output"),
    ]
    for i, (nm, num, ptype) in enumerate(right):
        y = span / 2 - i * pitch
        pins.append((nm, num, ptype, 180, 10.16 + 1.27, y, 1.27))
    # Top edge (pin 17..24), right to left, angle 270
    top = [
        ("RXD1", "17", "output"), ("RXD0", "18", "output"),
        ("RXDV/CRS_DV", "19", "output"), ("CRS/LEDMOD", "20", "input"),
        ("RXER/INTR", "21", "output"), ("MDC", "22", "input"),
        ("MDIO", "23", "bidirectional"), ("TEST_ON", "24", "input"),
    ]
    for i, (nm, num, ptype) in enumerate(top):
        x = span / 2 - i * pitch
        pins.append((nm, num, ptype, 270, x, -10.16 - 1.27, 1.27))
    # Left edge (pin 25..32), top to bottom, angle 0
    left = [
        ("ISET", "25", "passive"), ("MDI_RN", "26", "passive"), ("MDI_RP", "27", "passive"),
        ("REGOUT", "28", "output"), ("MDI_TN", "29", "passive"), ("MDI_TP", "30", "passive"),
        ("AVDD33", "31", "power_in"), ("RESET_N", "32", "input"),
    ]
    for i, (nm, num, ptype) in enumerate(left):
        y = -span / 2 + i * pitch
        pins.append((nm, num, ptype, 0, -10.16 - 1.27, y, 1.27))
    # EP pad
    pins.append(("EP/GND", "33", "power_in", 90, 0.0, 0.0, 0.0))
    return make_rect_ic("hmi:IP101GRI", "U", "IP101GRI", pins, 22.86, 22.86)


def make_adum1201() -> Tuple:
    # SOIC-8: 1=VDD1, 2=GND1, 3=VIA, 4=VIB, 5=VOB, 6=VOA, 7=GND2, 8=VDD2
    left = [("VDD1", "1", "power_in"), ("GND1", "2", "power_in"),
            ("VIA", "3", "input"), ("VIB", "4", "input")]
    right = [("VOB", "5", "output"), ("VOA", "6", "output"),
             ("GND2", "7", "power_in"), ("VDD2", "8", "power_in")]
    pins = dip_pins(left, right)
    return make_rect_ic("hmi:ADUM1201", "U", "ADUM1201", pins, 12.7, 17.78)


def make_max3485() -> Tuple:
    # SOIC-8: 1=RO, 2=RE#, 3=DE, 4=DI, 5=GND, 6=A, 7=B, 8=VCC
    left = [("RO", "1", "output"), ("RE#", "2", "input"), ("DE", "3", "input"), ("DI", "4", "input")]
    right = [("GND", "5", "power_in"), ("A", "6", "bidirectional"),
             ("B", "7", "bidirectional"), ("VCC", "8", "power_in")]
    pins = dip_pins(left, right)
    return make_rect_ic("hmi:MAX3485", "U", "MAX3485", pins, 12.7, 17.78)


def make_b0505s() -> Tuple:
    # DIP-4 / SMD-4: 1=VIN, 2=GND, 3=0V, 4=+VO
    pins = [
        ("VIN", "1", "power_in", 0, -2.54, 2.54, 2.54),
        ("GND", "2", "power_in", 0, -2.54, -2.54, 2.54),
        ("0V", "3", "power_out", 180, 2.54, -2.54, 2.54),
        ("+VO", "4", "power_out", 180, 2.54, 2.54, 2.54),
    ]
    return make_rect_ic("hmi:B0505S", "U", "B0505S-1W", pins, 10.16, 12.7)


def make_max3232() -> Tuple:
    # SOIC-16
    left = [
        ("C1+", "1", "passive"), ("V+", "2", "power_out"), ("C1-", "3", "passive"),
        ("C2+", "4", "passive"), ("C2-", "5", "passive"), ("V-", "6", "power_out"),
        ("T2OUT", "7", "output"), ("R2IN", "8", "input"),
    ]
    right = [
        ("R2OUT", "9", "output"), ("T2IN", "10", "input"),
        ("T1IN", "11", "input"), ("R1OUT", "12", "output"),
        ("R1IN", "13", "input"), ("T1OUT", "14", "output"),
        ("GND", "15", "power_in"), ("VCC", "16", "power_in"),
    ]
    pins = dip_pins(left, right, pitch=2.54)
    return make_rect_ic("hmi:MAX3232", "U", "MAX3232", pins, 12.7, 35.56)


def make_usblc6() -> Tuple:
    # SOT-23-6: 1=IO1, 2=GND, 3=IO2, 4=VBUS, 5=GND, 6=GND
    pins = [
        ("IO1", "1", "passive", 0, -2.54, 5.08, 2.54),
        ("GND", "2", "power_in", 0, -2.54, 0.0, 2.54),
        ("IO2", "3", "passive", 0, -2.54, -5.08, 2.54),
        ("GND", "5", "power_in", 180, 2.54, -5.08, 2.54),
        ("VBUS", "4", "passive", 180, 2.54, 0.0, 2.54),
        ("GND", "6", "power_in", 180, 2.54, 5.08, 2.54),
    ]
    return make_rect_ic("hmi:USBLC6", "U", "USBLC6-2SC6", pins, 12.7, 25.4)


def make_pmos_so8() -> Tuple:
    # AOD4184 SO-8: simplified 3-pin symbol: G,D,S plus tab=D
    pins = [
        ("G", "1", "input", 0, -2.54, 0.0, 2.54),
        ("D", "2", "passive", 180, 2.54, 2.54, 2.54),
        ("S", "3", "passive", 180, 2.54, -2.54, 2.54),
    ]
    return make_rect_ic("hmi:PMOS_SO8", "Q", "AOD4184", pins, 12.7, 12.7)


def make_fuse() -> Tuple:
    # 2-pin fuse
    body = ("rectangle", ("start", -3.81, -1.27), ("end", 3.81, 1.27), stroke(0.254, "default"), fill(typ="none"))
    p1 = pin("1", "1", "passive", 270, 0.0, 3.81, 2.54)
    p2 = pin("2", "2", "passive", 90, 0.0, -3.81, 2.54)
    return ("symbol", "hmi:FUSE", *sym_head("hmi:FUSE", "F", "F"),
            ("symbol", "FUSE_0_1", body), ("symbol", "FUSE_1_1", p1, p2))


def make_tvs() -> Tuple:
    # 2-pin TVS, bidirectional
    body = ("polyline", ("pts", xy(-1.27, -1.27), xy(0.0, 0.0), xy(1.27, -1.27), xy(0.0, 0.0), xy(0.0, 1.27)),
            stroke(0.254, "default"), fill(typ="none"))
    p1 = pin("1", "1", "passive", 270, 0.0, 3.81, 2.54)
    p2 = pin("2", "2", "passive", 90, 0.0, -3.81, 2.54)
    return ("symbol", "hmi:TVS", *sym_head("hmi:TVS", "D", "TVS"),
            ("symbol", "TVS_0_1", body), ("symbol", "TVS_1_1", p1, p2))


def make_term2p() -> Tuple:
    # 2-pin terminal block
    body = ("rectangle", ("start", -3.81, -2.54), ("end", 3.81, 2.54), stroke(0.254, "default"), fill(typ="none"))
    p1 = pin("1", "1", "passive", 270, -2.54, 5.08, 2.54)
    p2 = pin("2", "2", "passive", 90, 2.54, -5.08, 2.54)
    return ("symbol", "hmi:TERM2P", *sym_head("hmi:TERM2P", "J", "TERM"),
            ("symbol", "TERM2P_0_1", body), ("symbol", "TERM2P_1_1", p1, p2))


def make_sm712() -> Tuple:
    # SOT-23: 1=A, 2=GND, 3=B
    pins = [
        ("A", "1", "passive", 0, -2.54, 2.54, 2.54),
        ("GND", "2", "power_in", 0, -2.54, 0.0, 2.54),
        ("B", "3", "passive", 0, -2.54, -2.54, 2.54),
    ]
    return make_rect_ic("hmi:SM712", "D", "SM712", pins, 10.16, 12.7)


def make_fpc50() -> Tuple:
    # 50-pin FPC, 2 rows of 25 pins each, left/right
    pins: List[Tuple] = []
    left = []
    right = []
    for i in range(1, 51):
        if i <= 25:
            left.append((f"P{i}", str(i), "passive"))
        else:
            right.append((f"P{i}", str(i), "passive"))
    pins = dip_pins(left, right, pin_len=2.54, pitch=2.54)
    return make_rect_ic("hmi:FPC50", "J", "FPC50", pins, 20.32, 76.2)


def make_fpc4() -> Tuple:
    pins = [
        ("1", "1", "passive", 0, -2.54, 3.81, 2.54),
        ("2", "2", "passive", 0, -2.54, 1.27, 2.54),
        ("3", "3", "passive", 180, 2.54, 1.27, 2.54),
        ("4", "4", "passive", 180, 2.54, 3.81, 2.54),
    ]
    return make_rect_ic("hmi:FPC4", "J", "FPC4", pins, 12.7, 12.7)


def make_usbc16() -> Tuple:
    # Simplified 16P USB-C: left row = pins 1-8 (A side), right row = pins 9-16 (B side)
    pins: List[Tuple] = []
    left = [(f"A{i}", str(i), "passive") for i in range(1, 9)]
    right = [(f"B{i}", str(i + 8), "passive") for i in range(1, 9)]
    pins = dip_pins(left, right, pin_len=2.54, pitch=2.54)
    return make_rect_ic("hmi:USBC16", "J", "USBC16", pins, 17.78, 25.4)


def make_usba() -> Tuple:
    pins = [
        ("VBUS", "1", "passive", 0, -2.54, 2.54, 2.54),
        ("D-", "2", "passive", 0, -2.54, 0.0, 2.54),
        ("D+", "3", "passive", 0, -2.54, -2.54, 2.54),
        ("GND", "4", "power_in", 180, 2.54, -2.54, 2.54),
        ("SH", "5", "passive", 180, 2.54, 2.54, 2.54),
    ]
    return make_rect_ic("hmi:USBA", "J", "USBA", pins, 12.7, 12.7)


def make_hr911105a() -> Tuple:
    # 16-pin RJ45 magjack: left 8 = P1-P8, right 8 = LED/NC
    left = [(f"P{i}", str(i), "passive") for i in range(1, 9)]
    right = [(f"P{i}", str(i), "passive") for i in range(9, 17)]
    pins = dip_pins(left, right, pin_len=2.54, pitch=2.54)
    return make_rect_ic("hmi:HR911105A", "J", "HR911105A", pins, 17.78, 25.4)


def make_microsd() -> Tuple:
    # microSD pins: 1=DAT2, 2=CD/DAT3, 3=CMD, 4=VDD, 5=CLK, 6=VSS, 7=DAT0, 8=DAT1, 9=CD, 10-13=shield GND
    pins = [
        ("DAT2", "1", "bidirectional", 0, -2.54, 10.16, 2.54),
        ("CD/DAT3", "2", "bidirectional", 0, -2.54, 7.62, 2.54),
        ("CMD", "3", "bidirectional", 0, -2.54, 5.08, 2.54),
        ("VDD", "4", "power_in", 0, -2.54, 2.54, 2.54),
        ("CLK", "5", "input", 0, -2.54, 0.0, 2.54),
        ("VSS", "6", "power_in", 0, -2.54, -2.54, 2.54),
        ("DAT0", "7", "bidirectional", 180, 2.54, -2.54, 2.54),
        ("DAT1", "8", "bidirectional", 180, 2.54, 0.0, 2.54),
        ("CD", "9", "input", 180, 2.54, 2.54, 2.54),
        ("SH_GND", "10", "power_in", 180, 2.54, 5.08, 2.54),
    ]
    return make_rect_ic("hmi:MICROSD", "J", "MICROSD", pins, 15.24, 30.48)


def make_hdr1x4() -> Tuple:
    pins = [(f"P{i}", str(i), "passive", 0 if i <= 2 else 180, -2.54 if i <= 2 else 2.54,
             (2.5 - i) * 2.54, 2.54) for i in range(1, 5)]
    return make_rect_ic("hmi:HDR1X4", "J", "HDR1X4", pins, 12.7, 15.24)


def make_hdr2x12() -> Tuple:
    left = [(f"P{i}A", str(i), "passive") for i in range(1, 13)]
    right = [(f"P{i}B", str(i + 12), "passive") for i in range(1, 13)]
    pins = dip_pins(left, right, pin_len=2.54, pitch=2.54)
    return make_rect_ic("hmi:HDR2X12", "J", "HDR2X12", pins, 15.24, 38.1)


def make_db9f() -> Tuple:
    # DB9 female (board-side) signals: 2=RX, 3=TX, 5=GND (DTE pinout)
    pins = [
        ("1", "1", "passive", 0, -2.54, 10.16, 2.54),
        ("RX", "2", "passive", 0, -2.54, 7.62, 2.54),
        ("TX", "3", "passive", 0, -2.54, 5.08, 2.54),
        ("4", "4", "passive", 0, -2.54, 2.54, 2.54),
        ("GND", "5", "power_in", 0, -2.54, 0.0, 2.54),
        ("6", "6", "passive", 180, 2.54, 0.0, 2.54),
        ("7", "7", "passive", 180, 2.54, 2.54, 2.54),
        ("8", "8", "passive", 180, 2.54, 5.08, 2.54),
        ("9", "9", "passive", 180, 2.54, 7.62, 2.54),
    ]
    return make_rect_ic("hmi:DB9F", "J", "DB9F", pins, 15.24, 30.48)


# ---------------------------------------------------------------------------
# T113-S3 symbol: data-driven from CSV
# ---------------------------------------------------------------------------

def load_t113_pins(path: str) -> List[Dict[str, str]]:
    rows = []
    with open(path, newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            rows.append(row)
    return rows


T113_NETS: Dict[str, Optional[str]] = {
    # PB
    "PB2": "LCD_B0", "PB3": "LCD_B1", "PB4": "LCD_G0", "PB5": "LCD_G1",
    "PB6": "LCD_R0", "PB7": "LCD_R1",
    # PC
    "PC2": "NAND_CLK", "PC3": "NAND_CS", "PC4": "NAND_DI", "PC5": "NAND_DO",
    "PC6": "NAND_WP", "PC7": "NAND_HOLD",
    # PD
    "PD0": "LCD_B2", "PD1": "LCD_B3", "PD2": "LCD_B4", "PD3": "LCD_B5",
    "PD4": "LCD_B6", "PD5": "LCD_B7", "PD6": "LCD_G2", "PD7": "LCD_G3",
    "PD8": "LCD_G4", "PD9": "LCD_G5", "PD10": "LCD_G6", "PD11": "LCD_G7",
    "PD12": "LCD_R2", "PD13": "LCD_R3", "PD14": "LCD_R4", "PD15": "LCD_R5",
    "PD16": "LCD_R6", "PD17": "LCD_R7", "PD18": "LCD_DCLK", "PD19": "LCD_DE",
    "PD20": "LCD_HSYNC", "PD21": "LCD_VSYNC", "PD22": "LCD_BL_PWM",
    # PE
    "PE0": "STATUS_GREEN", "PE1": "STATUS_RED",
    "PE2": "UART2_TXD", "PE3": "UART2_RXD",
    "PE4": "UART4_TXD", "PE5": "UART4_RXD",
    "PE6": "UART5_TXD", "PE7": "UART5_RXD",
    "PE8": "PE8",
    "PE9": "LCD_RESET",
    "PE10": "PE10_GPIO",
    "PE11": "PE11_GPIO",
    "PE12": "TWI2_SCL",
    "PE13": "TWI2_SDA",
    # PE14-PE17 do not exist on the T113-S3 eLQFP-128 package
    # PF
    "PF0": "SD_D1", "PF1": "SD_D0", "PF2": "SD_CLK", "PF3": "SD_CMD",
    "PF4": "SD_D3", "PF5": "SD_D2", "PF6": "SD_DET",
    # PG
    "PG0": "ETH_CRS_DV", "PG1": "ETH_RXD0", "PG2": "ETH_RXD1",
    "PG3": "ETH_REFCLK", "PG4": "ETH_TXD0", "PG5": "ETH_TXD1",
    "PG6": "UART1_TXD", "PG7": "UART1_RXD",
    "PG8": "UART3_TXD", "PG9": "UART3_RXD",
    "PG10": "PWM3_VDD_CPU",
    "PG11": "ETH_RST",
    "PG12": "ETH_TXEN", "PG13": "ETH_RXER",
    "PG14": "ETH_MDC", "PG15": "ETH_MDIO",
    # TP
    "TP-X1": "TCH_XP", "TP-X2": "TCH_XN", "TP-Y1": "TCH_YP", "TP-Y2": "TCH_YN",
    # USB
    "USB0-DM": "USB0_DM", "USB0-DP": "USB0_DP",
    "USB1-DM": "USB1_DM", "USB1-DP": "USB1_DP",
    # TV / audio (unused)
    "TVOUT0": None, "TVIN0": None, "TVIN1": None,
    "MICIN3P": None, "MICIN3N": None, "FMINR": None, "FMINL": None,
    "LINEINR": None, "LINEINL": None, "HPOUTR": None, "HPOUTL": None, "HPOUTFB": None,
    "VRA1": None, "VRA2": None,
    # ADC
    "GPADC0": None,
    # Osc / RTC
    "X32KIN": "X32K_IN", "X32KOUT": "X32K_OUT",
    "DXIN": "X24M_IN", "DXOUT": "X24M_OUT",
    "REFCLK-OUT": None,
    # Reset
    "RESET": "RESET_N",
    # SDRAM calibration
    "DZQ": "DZQ",
    # Power rails
    "VCC-IO": "VCC3V3", "VCC-PD": "VCC3V3", "VCC-PE": "VCC3V3", "VCC-PG": "VCC3V3",
    "VCC-LVDS": "VCC3V3", "VCC-RTC": "VCC3V3", "VCC-PLL": "VCC3V3",
    "VDD-SYS0": "VDD0V9", "VDD-SYS1": "VDD0V9", "VDD-SYS2": "VDD0V9",
    "VDD-CORE0": "VDD0V9", "VDD-CORE1": "VDD0V9",
    "VCC-DRAM0": "VCC1V5", "VCC-DRAM1": "VCC1V5", "VDD18-DRAM": "VCC1V8",
    "LDO-IN": "VCC3V3", "LDOA-OUT": "AVCC", "LDOB-OUT": "HPVCC",
    "VCC-TVOUT": "VCC3V3", "VCC-TVIN": "VCC3V3",
    "TVIN-VRP": None, "TVIN-VRN": None,
    "HPVCC": "HPVCC", "AVCC": "AVCC", "AGND": "GND",
    "NC0": None,
}


def assign_pin_sides(rows: List[Dict[str, str]]) -> Dict[str, Dict[str, Any]]:
    """Assign each T113 pin to a side of the symbol and determine net/type."""
    # Group pins per the layout plan
    west_groups = {"GPIOB", "GPIOF"}
    east_groups = {"GPIOD", "GPIOE"}
    north_groups = {"SDRAM", "RTC & PLL", "GPADC", "TPADC", "GPIOG"}  # GPIOG goes west? Let's put GPIOG west
    # Rebalance: west = GPIOB, GPIOC, GPIOF, GPIOG; east = GPIOD, GPIOE, USB signal pins
    # South = all power pins
    west_groups = {"GPIOB", "GPIOC", "GPIOF", "GPIOG"}
    east_groups = {"GPIOD", "GPIOE", "USB"}  # USB signals only
    north_groups = {"SDRAM", "RTC & PLL", "GPADC", "TPADC"}
    south_groups = set()  # all power determined by type/name

    info: Dict[str, Dict[str, Any]] = {}
    for r in rows:
        name = r["pin_name"]
        group = r["group"]
        ptype = r["type"]
        supply = r["power_supply"]
        net = T113_NETS.get(name)

        side = None
        if ptype in ("P", "G") or name.startswith("VCC") or name.startswith("VDD") or name.startswith("LDO") or name in ("AVCC", "HPVCC", "AGND", "TVIN-VRP", "TVIN-VRN"):
            side = "south"
        elif name == "RESET":
            side = "north"
        elif group in west_groups:
            side = "west"
        elif group in east_groups:
            side = "east"
        elif group in north_groups:
            side = "north"
        else:
            side = "south"  # default power-ish

        # Determine KiCad pin type
        if ptype == "P" or name.startswith("VCC") or name.startswith("VDD") or name in ("AVCC", "HPVCC", "LDO-IN", "LDOA-OUT", "LDOB-OUT"):
            ktype = "power_in"
        elif ptype == "G":
            ktype = "power_in"
        elif name in ("RESET",):
            ktype = "input"
        elif ptype in ("AO",) or name in ("VRA1", "VRA2"):
            ktype = "output"
        elif ptype in ("AI", "A I/O"):
            ktype = "passive"
        elif ptype == "I":
            ktype = "input"
        elif ptype == "I/O":
            ktype = "bidirectional"
        else:
            ktype = "passive"

        info[name] = {"row": r, "net": net, "side": side, "type": ktype}
    return info


def make_t113_symbol(rows: List[Dict[str, str]]) -> Tuple:
    info = assign_pin_sides(rows)
    sides = {"west": [], "east": [], "north": [], "south": []}
    for name, d in info.items():
        sides[d["side"]].append((name, d))

    # Sort each side for a sensible order
    def sort_key(item):
        name = item[0]
        # numeric suffix first
        import re
        m = re.search(r"(\d+)$", name)
        return (re.sub(r"\d+$", "", name), int(m.group(1)) if m else 0, name)

    for s in sides:
        sides[s].sort(key=sort_key)

    # body dimensions must be multiples of 2.54 so that all pin tips land on
    # the 1.27 mm schematic grid (half-dim multiples of 1.27)
    body_w = 71.12   # 56 x 1.27
    body_h = 142.24  # 112 x 1.27
    pin_len = 5.08
    pitch = 2.54
    pins: List[Tuple] = []

    def place(side: str, items: List[Tuple[str, Dict[str, Any]]]):
        n = len(items)
        if side == "west":
            x = -body_w / 2 - pin_len
            span = (n - 1) * pitch
            start_y = span / 2
            for i, (name, d) in enumerate(items):
                y = start_y - i * pitch
                pins.append((name, d["row"]["ball"], d["type"], 0.0, x, y, pin_len))
        elif side == "east":
            x = body_w / 2 + pin_len
            span = (n - 1) * pitch
            start_y = span / 2
            for i, (name, d) in enumerate(items):
                y = start_y - i * pitch
                pins.append((name, d["row"]["ball"], d["type"], 180.0, x, y, pin_len))
        elif side == "north":
            y = -body_h / 2 - pin_len
            span = (n - 1) * pitch
            start_x = -span / 2
            for i, (name, d) in enumerate(items):
                x = start_x + i * pitch
                pins.append((name, d["row"]["ball"], d["type"], 270.0, x, y, pin_len))
        elif side == "south":
            y = body_h / 2 + pin_len
            span = (n - 1) * pitch
            start_x = -span / 2
            for i, (name, d) in enumerate(items):
                x = start_x + i * pitch
                pins.append((name, d["row"]["ball"], d["type"], 90.0, x, y, pin_len))

    place("west", sides["west"])
    place("east", sides["east"])
    place("north", sides["north"])
    place("south", sides["south"])

    # Normalize pins (hotspots outside body) BEFORE recording their positions
    pins = [normalize_pin(p, body_w, body_h, pin_len) for p in pins]

    # Build lookup from ball -> (x, y, angle) for wiring on P2
    global T113_PIN_LOCS
    T113_PIN_LOCS = {}
    for p in pins:
        name, ball, ptype, ang, x, y, length = p
        T113_PIN_LOCS[ball] = (x, y, ang, name)

    return make_rect_ic("hmi:T113_S3", "U", "T113-S3", pins, body_w, body_h, pin_len)


T113_PIN_LOCS: Dict[str, Tuple[float, float, float, str]] = {}


# ---------------------------------------------------------------------------
# Component instance placement helpers
# ---------------------------------------------------------------------------

class Page:
    def __init__(self, name: str, title: str):
        self.name = name
        self.title = title
        self.components: List[Dict] = []
        self.wires: List[Tuple] = []
        self.labels: List[Tuple] = []
        self.no_connects: List[Tuple] = []
        self.texts: List[Tuple] = []
        self.power_flags: List[Tuple] = []
        self.used_lib_ids: set = set()

    def add_component(self, ref: str, lib_id: str, value: str, footprint: str,
                      x: float, y: float, angle: float = 0.0,
                      pin_nets: Optional[Dict[str, str]] = None,
                      fields: Optional[Dict[str, str]] = None,
                      dnp: bool = False):
        self.used_lib_ids.add(lib_id)
        comp = {
            "ref": ref, "lib_id": lib_id, "value": value, "footprint": footprint,
            "x": x, "y": y, "angle": angle, "uuid": uid(),
            "pin_nets": pin_nets or {}, "fields": fields or {}, "dnp": dnp,
        }
        self.components.append(comp)
        return comp

    def add_text(self, txt: str, x: float, y: float, size: float = 1.27):
        self.texts.append(("text", txt, ("at", snap(x), snap(y), 0.0),
                           effects(justify="left bottom", size=size)))

    def add_global_label(self, net: str, x: float, y: float, angle: float = 0.0):
        self.labels.append(("global_label", net,
                            ("shape", Raw("bidirectional")),
                            ("at", snap(x), snap(y), angle),
                            effects(size=1.27),
                            ("uuid", uid())))

    def add_wire(self, x1: float, y1: float, x2: float, y2: float):
        self.wires.append(("wire", ("pts", xy(snap(x1), snap(y1)), xy(snap(x2), snap(y2))),
                           stroke(0.0, "default"), ("uuid", uid())))

    def add_no_connect(self, x: float, y: float):
        self.no_connects.append(("no_connect", ("at", snap(x), snap(y)), ("uuid", uid())))

    def add_pwr_flag(self, net: str, x: float, y: float, angle: float = 0.0):
        self.power_flags.append((net, x, y, angle))
        self.used_lib_ids.add("hmi:PWR_FLAG")


# ---------------------------------------------------------------------------
# Pin-coordinate helpers per symbol
# ---------------------------------------------------------------------------

# For each symbol we need a function to compute pin tip (connection) coords in
# sheet space.  These are derived from the symbol definitions above.
# Generic rectangular IC: pin tip = (x_inst + pin_x*cos(angle) - pin_y*sin(angle),
#                                    y_inst + pin_x*sin(angle) + pin_y*cos(angle))
# where (pin_x, pin_y, angle) are the symbol-local values from the pin defs.

PIN_DEFS: Dict[str, List[Tuple]] = {}


def register_pins(lib_id: str, pins: List[Tuple]):
    """pins as returned by make_rect_ic: (name, number, type, angle, x, y, length)"""
    PIN_DEFS[lib_id] = pins


def pin_tip(lib_id: str, pin_num: str, ix: float, iy: float, inst_angle: float = 0.0) -> Tuple[float, float]:
    """Sheet-space position of a pin's electrical hotspot.

    KiCad library symbol coordinates use a Y-UP convention while schematic
    sheet coordinates are Y-DOWN; instantiating a symbol flips the local Y
    axis.  Verified against the netlist: lib pin (px, py) lands at
    (ix + px, iy - py) for an unrotated instance.
    """
    if inst_angle != 0.0:
        raise NotImplementedError("rotated symbol instances are not used by this generator")
    ix, iy = snap(ix), snap(iy)
    for p in PIN_DEFS[lib_id]:
        name, num, ptype, ang, px, py, length = p
        if num == str(pin_num):
            return ix + px, iy - py
    raise KeyError(f"pin {pin_num} not found in {lib_id}")


import math  # noqa: E402


def connect_pin(page: Page, comp: Dict, pin_num: str, net: Optional[str],
                wire_len: float = 5.08):
    """Draw a wire from a component pin tip AWAY from the symbol body and put
    a global label (or no_connect) at the wire endpoint.

    KiCad pin angle semantics (well-formed symbols, hotspot outside body):
      angle 0 = pin on LEFT edge, graphic points right toward body
      angle 180 = pin on RIGHT edge
      angle 270 = pin on TOP edge (sheet), graphic points down toward body
      angle 90 = pin on BOTTOM edge (sheet), graphic points up toward body
    Wire extends AWAY from the body: sheet direction (-cos(a), +sin(a)).
    """
    lib_id = comp["lib_id"]
    x, y = pin_tip(lib_id, pin_num, comp["x"], comp["y"], comp["angle"])
    ang = None
    for p in PIN_DEFS[lib_id]:
        if p[1] == str(pin_num):
            ang = p[3]
            break
    if ang is None:
        raise KeyError(f"pin {pin_num} not found in {lib_id}")
    # NC pin: place the no-connect marker ON the pin hotspot, draw NO wire.
    # Drawing a wire here is dangerous -- the stub can cross another net's
    # label and short the "unconnected" pin to it.
    if net is None:
        page.add_no_connect(x, y)
        return
    # away vector (opposite of pin graphic direction, in sheet coords)
    if ang == 0.0:
        ax, ay = -1.0, 0.0
    elif ang == 180.0:
        ax, ay = 1.0, 0.0
    elif ang == 270.0:
        ax, ay = 0.0, -1.0    # up (top-edge pin)
    elif ang == 90.0:
        ax, ay = 0.0, 1.0     # down (bottom-edge pin)
    else:
        ax, ay = -1.0, 0.0
    wx = x + wire_len * ax
    wy = y + wire_len * ay
    page.add_wire(x, y, wx, wy)
    # label angle points along the wire direction away from the pin
    if (ax, ay) == (-1.0, 0.0):
        langle = 180.0
    elif (ax, ay) == (1.0, 0.0):
        langle = 0.0
    elif (ax, ay) == (0.0, 1.0):
        langle = 90.0
    else:
        langle = 270.0
    page.add_global_label(net, wx, wy, langle)


def connect_passive(page: Page, comp: Dict, pin1_net: str, pin2_net: str,
                    pin1_offset: Tuple[float, float], pin2_offset: Tuple[float, float]):
    """Connect a 2-pin passive to two nets using short wires and labels.

    Vertical offset components are CLAMPED to point AWAY from the symbol body
    (pin 1 is the top pin in sheet coords, pin 2 the bottom pin): otherwise
    the two wires overlap and merge the two nets galvanically.
    """
    lib_id = comp["lib_id"]
    x1, y1 = pin_tip(lib_id, "1", comp["x"], comp["y"], comp["angle"])
    x2, y2 = pin_tip(lib_id, "2", comp["x"], comp["y"], comp["angle"])
    # clamp vertical offsets away from the body
    dy1 = -abs(pin1_offset[1]) if pin1_offset[1] else 0.0   # pin1 -> up
    dy2 = abs(pin2_offset[1]) if pin2_offset[1] else 0.0    # pin2 -> down
    lx1, ly1 = x1 + pin1_offset[0], y1 + dy1
    lx2, ly2 = x2 + pin2_offset[0], y2 + dy2
    page.add_wire(x1, y1, lx1, ly1)
    page.add_wire(x2, y2, lx2, ly2)
    page.add_global_label(pin1_net, lx1, ly1)
    page.add_global_label(pin2_net, lx2, ly2)


def passive_between(page: Page, ref: str, lib_id: str, value: str, footprint: str,
                    x: float, y: float, net_a: str, net_b: str,
                    a_offset: Tuple[float, float], b_offset: Tuple[float, float],
                    angle: float = 0.0):
    comp = page.add_component(ref, lib_id, value, footprint, x, y, angle)
    connect_passive(page, comp, net_a, net_b, a_offset, b_offset)
    return comp


# ---------------------------------------------------------------------------
# Build all symbol definitions and register pin coordinates
# ---------------------------------------------------------------------------

def build_symbols(rows: List[Dict[str, str]]) -> Dict[str, Tuple]:
    syms = {}
    syms["hmi:RES"] = make_resistor()
    syms["hmi:CAP"] = make_capacitor()
    syms["hmi:IND"] = make_inductor()
    syms["hmi:DIO"] = make_diode()
    syms["hmi:LED"] = make_led()
    syms["hmi:SWITCH"] = make_switch()
    syms["hmi:TP"] = make_tp()
    syms["hmi:XTAL2"] = make_xtal2()
    syms["hmi:PWR_FLAG"] = make_pwr_flag()
    syms["hmi:XL4015E1"] = make_xl4015()
    syms["hmi:SY8089A1AAC"] = make_sy8089()
    syms["hmi:RT9193"] = make_rt9193()
    syms["hmi:SY6280"] = make_sy6280()
    syms["hmi:W25N01GV"] = make_w25n01gv()
    syms["hmi:TPS65100"] = make_tps65100()
    syms["hmi:MT9284"] = make_mt9284()
    syms["hmi:IP101GRI"] = make_ip101gri()
    syms["hmi:ADUM1201"] = make_adum1201()
    syms["hmi:MAX3485"] = make_max3485()
    syms["hmi:B0505S"] = make_b0505s()
    syms["hmi:MAX3232"] = make_max3232()
    syms["hmi:USBLC6"] = make_usblc6()
    syms["hmi:PMOS_SO8"] = make_pmos_so8()
    syms["hmi:FUSE"] = make_fuse()
    syms["hmi:TVS"] = make_tvs()
    syms["hmi:TERM2P"] = make_term2p()
    syms["hmi:SM712"] = make_sm712()
    syms["hmi:FPC50"] = make_fpc50()
    syms["hmi:FPC4"] = make_fpc4()
    syms["hmi:USBC16"] = make_usbc16()
    syms["hmi:USBA"] = make_usba()
    syms["hmi:HR911105A"] = make_hr911105a()
    syms["hmi:MICROSD"] = make_microsd()
    syms["hmi:HDR1X4"] = make_hdr1x4()
    syms["hmi:HDR2X12"] = make_hdr2x12()
    syms["hmi:DB9F"] = make_db9f()
    syms["hmi:T113_S3"] = make_t113_symbol(rows)

    # Register pin coords for all non-T113 symbols
    for lib_id, sym in syms.items():
        if lib_id == "hmi:T113_S3":
            continue  # T113 special handling via T113_PIN_LOCS
        # Extract pin list from symbol body
        pins = []
        # sym structure: ("symbol", name, ..., ("symbol", "REF_1_1", pin1, pin2, ...))
        for sub in sym:
            if isinstance(sub, (list, tuple)) and len(sub) >= 2 and sub[0] == "symbol":
                for item in sub[2:]:
                    if isinstance(item, (list, tuple)) and item and item[0] == "pin":
                        # item = ("pin", ptype, "line", ("at",x,y,angle), ("length",len),
                        #         ("name", nm, effects), ("number", num, effects))
                        at = item[3]
                        px, py, pang = at[1], at[2], at[3]
                        length = item[4][1]
                        nm = str(item[5][1])
                        num = str(item[6][1])
                        pins.append((nm, num, item[1], pang, px, py, length))
        register_pins(lib_id, pins)
    return syms


# ---------------------------------------------------------------------------
# Page builders
# ---------------------------------------------------------------------------

def build_page_P1() -> Page:
    page = Page("P1_POWER", "POWER_INPUT")
    page.add_text("HMI_T113 电源输入与电源树", 20.0, 20.0, 2.54)
    page.add_text("12-24V DC -> VCC5V -> VCC3V3 -> VCC1V8 -> VDD0V9 -> VCC1V5", 20.0, 25.0, 1.27)

    # ---- 输入保护链 (top row, left -> right) ----
    # J1 接线端子
    j1 = page.add_component("J1", "hmi:TERM2P", "DC_INPUT", "TERM-2P-5.08",
                            30.0, 45.0, 0.0,
                            pin_nets={"1": "VIN24_RAW", "2": "GND"})
    connect_pin(page, j1, "1", "VIN24_RAW")
    connect_pin(page, j1, "2", "GND")

    # F1 自恢复保险丝 (VIN24_RAW -> VIN24_F)
    passive_between(page, "F1", "hmi:FUSE", "3A", "FUSE-SCD-3A",
                    62.0, 45.0, "VIN24_RAW", "VIN24_F",
                    (5.0, 0.0), (-5.0, 0.0))

    # D1 TVS 钳位 (VIN24_F -> GND)
    passive_between(page, "D1", "hmi:TVS", "SMCJ24A", "SMCJ24A-SMC",
                    94.0, 45.0, "VIN24_F", "GND",
                    (5.0, 0.0), (0.0, -5.0))

    # Q1 P-MOS 反接保护 (source-drain path)
    q1 = page.add_component("Q1", "hmi:PMOS_SO8", "AOD4184", "SO-8",
                            126.0, 45.0, 0.0,
                            pin_nets={"1": "VIN24_F", "2": "VIN24_F", "3": "VIN24_RAW"})
    connect_pin(page, q1, "3", "VIN24_RAW")
    connect_pin(page, q1, "2", "VIN24_F")
    connect_pin(page, q1, "1", None)

    # 输入侧滤波 C105
    passive_between(page, "C105", "hmi:CAP", "10u", "0805",
                    94.0, 70.0, "VIN24_F", "GND",
                    (5.0, 0.0), (0.0, -5.0))
    page.add_pwr_flag("VIN24_F", 158.0, 42.0)

    # U1 XL4015: VIN24_F -> VCC5V
    u1 = page.add_component("U1", "hmi:XL4015E1", "XL4015E1", "TO-263-5",
                            200.0, 45.0, 0.0,
                            pin_nets={"1": "VIN24_F", "2": "GND", "3": "SW", "4": "FB", "5": "VCC5V"})
    connect_pin(page, u1, "1", "VIN24_F")
    connect_pin(page, u1, "2", "GND")
    connect_pin(page, u1, "3", None)
    connect_pin(page, u1, "4", "VCC5V")
    connect_pin(page, u1, "5", "VCC5V")

    # 5V 输出侧滤波 C106
    passive_between(page, "C106", "hmi:CAP", "10u", "0805",
                    246.0, 70.0, "VCC5V", "GND",
                    (5.0, 0.0), (0.0, -5.0))
    page.add_text("XL4015: SW/FB/OUT 需外部电感/分压", 190.0, 82.0, 1.016)

    # ---- 测试点 (y=100) ----
    tp1 = page.add_component("TP1", "hmi:TP", "VIN24_F", "TP-1.0",
                             30.0, 100.0, 0.0, pin_nets={"1": "VIN24_F"})
    connect_pin(page, tp1, "1", "VIN24_F")
    tp2 = page.add_component("TP2", "hmi:TP", "VCC5V", "TP-1.0",
                             62.0, 100.0, 0.0, pin_nets={"1": "VCC5V"})
    connect_pin(page, tp2, "1", "VCC5V")
    tp3 = page.add_component("TP3", "hmi:TP", "VCC3V3", "TP-1.0",
                             94.0, 100.0, 0.0, pin_nets={"1": "VCC3V3"})
    connect_pin(page, tp3, "1", "VCC3V3")

    # ---- U2: SY8089 5V -> 3V3 (左列上方) ----
    u2 = page.add_component("U2", "hmi:SY8089A1AAC", "SY8089A1AAC", "TSOT-23-5",
                            140.0, 140.0, 0.0,
                            pin_nets={"1": "EN_3V3", "2": "GND", "3": "LX", "4": "VCC5V", "5": "FB"})
    connect_pin(page, u2, "4", "VCC5V")
    connect_pin(page, u2, "2", "GND")
    connect_pin(page, u2, "3", None)
    connect_pin(page, u2, "5", "VCC3V3")
    passive_between(page, "R101", "hmi:RES", "100k", "0603",
                    110.0, 125.0, "VCC5V", "EN_3V3", (5.0, 0.0), (-5.0, 0.0))
    passive_between(page, "C101", "hmi:CAP", "10n", "0603",
                    110.0, 140.0, "EN_3V3", "GND", (5.0, 0.0), (0.0, -5.0))
    connect_pin(page, u2, "1", "EN_3V3")
    passive_between(page, "C107", "hmi:CAP", "10u", "0805",
                    186.0, 155.0, "VCC3V3", "GND", (5.0, 0.0), (0.0, -5.0))
    page.add_pwr_flag("VCC3V3", 186.0, 125.0)

    # ---- U3: RT9193 3V3 -> 1V8 (右列上方) ----
    u3 = page.add_component("U3", "hmi:RT9193", "RT9193-18", "SOT-23-5",
                            260.0, 140.0, 0.0,
                            pin_nets={"1": "VCC3V3", "2": "GND", "3": "VCC3V3", "4": "BP", "5": "VCC1V8"})
    connect_pin(page, u3, "1", "VCC3V3")
    connect_pin(page, u3, "2", "GND")
    connect_pin(page, u3, "3", "VCC3V3")
    connect_pin(page, u3, "4", None)
    connect_pin(page, u3, "5", "VCC1V8")
    passive_between(page, "C108", "hmi:CAP", "10u", "0805",
                    306.0, 155.0, "VCC1V8", "GND", (5.0, 0.0), (0.0, -5.0))
    page.add_text("RT9193 BP pin: add 22nF to GND", 250.0, 175.0, 1.016)
    page.add_pwr_flag("GND", 30.0, 30.0)

    # ---- U5: SY8089 5V -> 1V5 (右列下方) ----
    u5 = page.add_component("U5", "hmi:SY8089A1AAC", "SY8089A1AAC", "TSOT-23-5",
                            260.0, 215.0, 0.0,
                            pin_nets={"1": "EN_1V5", "2": "GND", "3": "LX", "4": "VCC5V", "5": "FB"})
    connect_pin(page, u5, "4", "VCC5V")
    connect_pin(page, u5, "2", "GND")
    connect_pin(page, u5, "3", None)
    connect_pin(page, u5, "5", "VCC1V5")
    passive_between(page, "R106", "hmi:RES", "100k", "0603",
                    230.0, 200.0, "VDD0V9", "EN_1V5", (5.0, 0.0), (-5.0, 0.0))
    passive_between(page, "C104", "hmi:CAP", "10n", "0603",
                    230.0, 215.0, "EN_1V5", "GND", (5.0, 0.0), (0.0, -5.0))
    connect_pin(page, u5, "1", "EN_1V5")
    passive_between(page, "C110", "hmi:CAP", "10u", "0805",
                    306.0, 245.0, "VCC1V5", "GND", (5.0, 0.0), (0.0, -5.0))
    page.add_pwr_flag("VCC1V5", 306.0, 195.0)

    # ---- U4: SY8089 5V -> 0V9 (DVFS via PWM3, 左列下方单独展开) ----
    u4 = page.add_component("U4", "hmi:SY8089A1AAC", "SY8089A1AAC", "TSOT-23-5",
                            140.0, 215.0, 0.0,
                            pin_nets={"1": "EN_0V9", "2": "GND", "3": "LX", "4": "VCC5V", "5": "FB"})
    connect_pin(page, u4, "4", "VCC5V")
    connect_pin(page, u4, "2", "GND")
    connect_pin(page, u4, "3", None)
    connect_pin(page, u4, "5", "VDD0V9")
    passive_between(page, "R102", "hmi:RES", "100k", "0603",
                    110.0, 200.0, "VCC3V3", "EN_0V9", (5.0, 0.0), (-5.0, 0.0))
    passive_between(page, "C102", "hmi:CAP", "10n", "0603",
                    110.0, 215.0, "EN_0V9", "GND", (5.0, 0.0), (0.0, -5.0))
    connect_pin(page, u4, "1", "EN_0V9")
    passive_between(page, "R103", "hmi:RES", "10k", "0603",
                    172.0, 215.0, "PWM3_VDD_CPU", "FB_0V9", (5.0, 0.0), (-5.0, 0.0))
    passive_between(page, "C103", "hmi:CAP", "10n", "0603",
                    172.0, 230.0, "FB_0V9", "GND", (5.0, 0.0), (0.0, -5.0))
    passive_between(page, "R104", "hmi:RES", "TBD", "0603",
                    206.0, 215.0, "VDD0V9", "FB_0V9", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "R105", "hmi:RES", "TBD", "0603",
                    224.0, 215.0, "FB_0V9", "GND", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "C109", "hmi:CAP", "10u", "0805",
                    186.0, 245.0, "VDD0V9", "GND", (5.0, 0.0), (0.0, -5.0))
    page.add_pwr_flag("VDD0V9", 240.0, 195.0)

    page.add_text("NOTE: 输入/输出电容、电感、续流二极管、反馈电阻值按最终电源设计确认", 20.0, 275.0, 1.27)
    return page


def build_page_P2(rows: List[Dict[str, str]]) -> Page:
    page = Page("P2_SOC_CORE", "SOC_CORE")
    page.add_text("T113-S3 SoC 核心 + 晶振 + 去耦", 20.0, 20.0, 2.54)
    page.add_text("128-ball eLQFP, internal 128MB DDR3", 20.0, 25.0, 1.27)

    # Place T113 symbol in the middle of the sheet
    u6 = page.add_component("U6", "hmi:T113_S3", "T113-S3", "LQFP-128_L14.0-W14.0-P0.4",
                            120.0, 160.0, 0.0)
    # Wire every pin to its net using T113_PIN_LOCS (symbol-local coords)
    for r in rows:
        ball = r["ball"]
        name = r["pin_name"]
        net = T113_NETS.get(name)
        sx, sy, ang, _ = T113_PIN_LOCS[ball]
        # lib coords (Y-up) -> sheet coords (Y-down): flip Y (grid-snapped)
        px = snap(u6["x"]) + sx
        py = snap(u6["y"]) - sy
        # NC pin: no-connect marker ON the hotspot, no wire stub
        if net is None:
            page.add_no_connect(px, py)
            continue
        # away vector opposite to pin graphic direction (sheet coords)
        if ang == 0.0:
            ax, ay = -1.0, 0.0
        elif ang == 180.0:
            ax, ay = 1.0, 0.0
        elif ang == 270.0:
            ax, ay = 0.0, -1.0    # top-edge pin -> wire up
        else:
            ax, ay = 0.0, 1.0     # bottom-edge pin -> wire down
        wx = px + 5.08 * ax
        wy = py + 5.08 * ay
        page.add_wire(px, py, wx, wy)
        if (ax, ay) == (-1.0, 0.0):
            langle = 180.0
        elif (ax, ay) == (1.0, 0.0):
            langle = 0.0
        elif (ax, ay) == (0.0, 1.0):
            langle = 90.0
        else:
            langle = 270.0
        page.add_global_label(net, wx, wy, langle)

    # Power flags: single flag per net globally (P1 covers the main rails);
    # AVCC/HPVCC are generated here by the T113 internal LDOs
    for net, x, y in [("AVCC", 60.0, 140.0), ("HPVCC", 60.0, 160.0)]:
        page.add_pwr_flag(net, x, y)

    # Decoupling caps near the symbol (place around it)
    # For each unique power net on T113, place a 100nF cap to GND.
    power_nets = ["VCC3V3", "VDD0V9", "VCC1V5", "VCC1V8", "AVCC", "HPVCC"]
    cx, cy = 200.0, 80.0
    for i, net in enumerate(power_nets):
        x = cx + (i % 3) * 20.0
        y = cy + (i // 3) * 15.0
        passive_between(page, f"C{201 + i}", "hmi:CAP", "100n", "0603",
                        x, y, net, "GND", (5.0, 0.0), (0.0, -5.0))

    # DZQ calibration resistor 240R
    passive_between(page, "R201", "hmi:RES", "240R 1%", "0603",
                    200.0, 120.0, "DZQ", "GND", (5.0, 0.0), (0.0, -5.0))

    # 24MHz crystal X1 on DXIN/DXOUT
    x1 = page.add_component("X1", "hmi:XTAL2", "24MHz", "XTAL-SMD-3225",
                            60.0, 262.0, 0.0,
                            pin_nets={"1": "X24M_IN", "2": "X24M_OUT"})
    connect_pin(page, x1, "1", "X24M_IN")
    connect_pin(page, x1, "2", "X24M_OUT")
    passive_between(page, "C211", "hmi:CAP", "12p", "0603", 45.0, 262.0, "X24M_IN", "GND", (-5.0, 0.0), (0.0, 5.0))
    passive_between(page, "C212", "hmi:CAP", "12p", "0603", 75.0, 262.0, "X24M_OUT", "GND", (5.0, 0.0), (0.0, 5.0))

    # 32.768kHz crystal X2 on X32KIN/X32KOUT
    x2 = page.add_component("X2", "hmi:XTAL2", "32.768kHz", "XTAL-SMD-3215",
                            120.0, 262.0, 0.0,
                            pin_nets={"1": "X32K_IN", "2": "X32K_OUT"})
    connect_pin(page, x2, "1", "X32K_IN")
    connect_pin(page, x2, "2", "X32K_OUT")
    passive_between(page, "C213", "hmi:CAP", "12p", "0603", 105.0, 262.0, "X32K_IN", "GND", (-5.0, 0.0), (0.0, 5.0))
    passive_between(page, "C214", "hmi:CAP", "12p", "0603", 135.0, 262.0, "X32K_OUT", "GND", (5.0, 0.0), (0.0, 5.0))

    page.add_text("NOTE: 所有未用模拟/音频/TV 引脚标记为 NC；VCC-TVOUT/TVIN 接 VCC3V3 供电", 20.0, 280.0, 1.27)
    return page


def build_page_P3() -> Page:
    page = Page("P3_STORAGE", "STORAGE")
    page.add_text("SPI NAND 启动 + microSD 卡", 20.0, 20.0, 2.54)

    # W25N01GV U7
    u7 = page.add_component("U7", "hmi:W25N01GV", "W25N01GVZEIG", "WSON-8_L6.0-W5.0-P1.27",
                            60.0, 80.0, 0.0,
                            pin_nets={"1": "NAND_CS", "2": "NAND_DO", "3": "NAND_WP",
                                      "4": "GND", "5": "NAND_DI", "6": "NAND_CLK",
                                      "7": "NAND_HOLD", "8": "VCC3V3"})
    connect_pin(page, u7, "1", "NAND_CS")
    connect_pin(page, u7, "2", "NAND_DO")
    connect_pin(page, u7, "3", "NAND_WP")
    connect_pin(page, u7, "4", "GND")
    connect_pin(page, u7, "5", "NAND_DI")
    connect_pin(page, u7, "6", "NAND_CLK")
    connect_pin(page, u7, "7", "NAND_HOLD")
    connect_pin(page, u7, "8", "VCC3V3")

    # Pull-ups for WP/HOLD/CS
    passive_between(page, "R301", "hmi:RES", "10k", "0603", 30.0, 88.0, "VCC3V3", "NAND_WP", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "R302", "hmi:RES", "10k", "0603", 30.0, 108.0, "VCC3V3", "NAND_HOLD", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "R303", "hmi:RES", "10k", "0603", 30.0, 128.0, "VCC3V3", "NAND_CS", (0.0, 5.0), (0.0, -5.0))

    # Decoupling
    passive_between(page, "C301", "hmi:CAP", "100n", "0603", 90.0, 90.0, "VCC3V3", "GND", (5.0, 0.0), (0.0, -5.0))
    passive_between(page, "C302", "hmi:CAP", "10u", "0805", 90.0, 104.0, "VCC3V3", "GND", (5.0, 0.0), (0.0, -5.0))

    # microSD J2
    j2 = page.add_component("J2", "hmi:MICROSD", "MICROSD", "MICROSD-SMD",
                            60.0, 160.0, 0.0,
                            pin_nets={"1": "SD_D2", "2": "SD_D3", "3": "SD_CMD",
                                      "4": "VCC3V3", "5": "SD_CLK", "6": "GND",
                                      "7": "SD_D0", "8": "SD_D1", "9": "SD_DET", "10": "GND"})
    connect_pin(page, j2, "1", "SD_D2")
    connect_pin(page, j2, "2", "SD_D3")
    connect_pin(page, j2, "3", "SD_CMD")
    connect_pin(page, j2, "4", "VCC3V3")
    connect_pin(page, j2, "5", "SD_CLK")
    connect_pin(page, j2, "6", "GND")
    connect_pin(page, j2, "7", "SD_D0")
    connect_pin(page, j2, "8", "SD_D1")
    connect_pin(page, j2, "9", "SD_DET")
    connect_pin(page, j2, "10", "GND")

    passive_between(page, "R304", "hmi:RES", "10k", "0603", 30.0, 170.0, "VCC3V3", "SD_DET", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "C303", "hmi:CAP", "100n", "0603", 90.0, 170.0, "VCC3V3", "GND", (5.0, 0.0), (0.0, -5.0))

    return page


def build_page_P4() -> Page:
    page = Page("P4_LCD_RGB888", "LCD_RGB888")
    page.add_text("SZCLX070H 7\" LCD RGB888 50P FPC", 20.0, 20.0, 2.54)

    j3 = page.add_component("J3", "hmi:FPC50", "FPC-SMD-50P-0.5", "FPC-SMD-50P-0.5",
                            100.0, 160.0, 0.0)

    # J3 pin map from lcd-50pin-pinout.csv and lcd-t113-pin-handoff.csv
    # Rows: (pin, net, note)
    j3_pins = [
        (1, "VLED_A"), (2, "VLED_A"), (3, "VLED_K"), (4, "VLED_K"),
        (5, "GND"), (6, "LCD_VCOM"), (7, "VCC3V3"), (8, "LCD_MODE"),
        (9, "LCD_DE"), (10, "LCD_VSYNC"), (11, "LCD_HSYNC"),
        (12, "LCD_B7"), (13, "LCD_B6"), (14, "LCD_B5"), (15, "LCD_B4"),
        (16, "LCD_B3"), (17, "LCD_B2"), (18, "LCD_B1"), (19, "LCD_B0"),
        (20, "LCD_G7"), (21, "LCD_G6"), (22, "LCD_G5"), (23, "LCD_G4"),
        (24, "LCD_G3"), (25, "LCD_G2"), (26, "LCD_G1"), (27, "LCD_G0"),
        (28, "LCD_R7"), (29, "LCD_R6"), (30, "LCD_R5"), (31, "LCD_R4"),
        (32, "LCD_R3"), (33, "LCD_R2"), (34, "LCD_R1"), (35, "LCD_R0"),
        (36, "GND"), (37, "LCD_DCLK"), (38, "GND"), (39, "LCD_SHLR"),
        (40, "LCD_UPDN"), (41, "LCD_VGH"), (42, "LCD_VGL"), (43, "LCD_AVDD"),
        (44, "LCD_RESET"), (45, None), (46, "LCD_VCOM"), (47, "LCD_DITH"),
        (48, "GND"), (49, None), (50, None),
    ]
    for pin_num, net in j3_pins:
        connect_pin(page, j3, str(pin_num), net)

    # Control resistors
    passive_between(page, "R401", "hmi:RES", "10k", "0603", 40.0, 60.0, "VCC3V3", "LCD_MODE", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "R402", "hmi:RES", "10k", "0603", 55.0, 60.0, "LCD_DITH", "GND", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "R403", "hmi:RES", "10k", "0603", 70.0, 60.0, "VCC3V3", "LCD_RESET", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "C401", "hmi:CAP", "100n", "0603", 85.0, 60.0, "LCD_RESET", "GND", (5.0, 0.0), (0.0, -5.0))
    passive_between(page, "R404", "hmi:RES", "0R", "0603", 100.0, 60.0, "LCD_SHLR", "GND", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "R405", "hmi:RES", "0R", "0603", 115.0, 60.0, "LCD_UPDN", "GND", (0.0, 5.0), (0.0, -5.0))

    page.add_text("NOTE: R401-R424 22R series resistors are OPTIONAL/DNP; place near T113 or FPC per SI review", 20.0, 280.0, 1.27)
    return page


def build_page_P5() -> Page:
    page = Page("P5_LCD_POWER_BACKLIGHT", "LCD_POWER_BACKLIGHT")
    page.add_text("LCD 偏压 (TPS65100) + 背光升压 (MT9284)", 20.0, 20.0, 2.54)

    # TPS65100 U12
    u12 = page.add_component("U12", "hmi:TPS65100", "TPS65100", "TSSOP-24",
                             80.0, 140.0, 0.0,
                             pin_nets={"1": "FB1", "2": "FB4", "3": "BASE", "4": "VCC3V3",
                                       "5": "SW", "6": "SW", "7": "GND", "8": "GND",
                                       "9": "VCC3V3", "10": "LCD_VCOM", "11": "VCOMIN",
                                       "12": "FB3", "13": "LCD_AVDD", "14": "C2+",
                                       "15": "C2-/MODE", "16": "C1+", "17": "C1-",
                                       "18": "DRV", "19": "GND", "20": "REF",
                                       "21": "FB2", "22": "COMP", "23": "VCC3V3", "24": "VCC3V3", "25": "GND"})
    connect_pin(page, u12, "4", "VCC3V3")
    connect_pin(page, u12, "9", "VCC3V3")
    connect_pin(page, u12, "23", "VCC3V3")  # ENR -> VCC3V3
    connect_pin(page, u12, "24", "VCC3V3")  # EN -> VCC3V3
    connect_pin(page, u12, "7", "GND")
    connect_pin(page, u12, "8", "GND")
    connect_pin(page, u12, "19", "GND")
    connect_pin(page, u12, "25", "GND")
    connect_pin(page, u12, "10", "LCD_VCOM")
    connect_pin(page, u12, "13", "LCD_AVDD")
    connect_pin(page, u12, "1", "FB1")
    connect_pin(page, u12, "21", "FB2")
    connect_pin(page, u12, "12", "FB3")
    connect_pin(page, u12, "22", "COMP")
    connect_pin(page, u12, "14", "C2+")
    connect_pin(page, u12, "15", "C2-/MODE")
    connect_pin(page, u12, "16", "C1+")
    connect_pin(page, u12, "17", "C1-")
    connect_pin(page, u12, "20", "REF")
    connect_pin(page, u12, "11", "VCOMIN")
    connect_pin(page, u12, "5", "SW_BIAS")
    connect_pin(page, u12, "6", "SW_BIAS")
    connect_pin(page, u12, "2", None)   # FB4 (VCOM buffer) TBD
    connect_pin(page, u12, "3", None)   # BASE (external VCOM transistor) TBD
    connect_pin(page, u12, "18", None)  # DRV TBD
    # main boost inductor VCC3V3 -> SW
    passive_between(page, "L502", "hmi:IND", "10uH", "SMD", 25.0, 140.0, "VCC3V3", "SW_BIAS", (0.0, 5.0), (0.0, -5.0))

    # Charge pump / feedback passives (placeholders, values from TPS65100 typical app)
    passive_between(page, "C501", "hmi:CAP", "1u", "0603", 40.0, 100.0, "C1+", "C1-", (5.0, 0.0), (-5.0, 0.0))
    passive_between(page, "C502", "hmi:CAP", "1u", "0603", 40.0, 114.0, "C2+", "C2-/MODE", (5.0, 0.0), (-5.0, 0.0))
    passive_between(page, "C503", "hmi:CAP", "2.2u", "0603", 40.0, 128.0, "LCD_AVDD", "GND", (5.0, 0.0), (0.0, -5.0))
    passive_between(page, "C504", "hmi:CAP", "1u", "0603", 40.0, 142.0, "LCD_VCOM", "GND", (5.0, 0.0), (0.0, -5.0))
    passive_between(page, "R501", "hmi:RES", "TBD", "0603", 40.0, 156.0, "LCD_AVDD", "FB3", (5.0, 0.0), (-5.0, 0.0))
    passive_between(page, "R502", "hmi:RES", "TBD", "0603", 40.0, 170.0, "FB3", "GND", (5.0, 0.0), (0.0, -5.0))
    passive_between(page, "R503", "hmi:RES", "TBD", "0603", 40.0, 184.0, "VCOMIN", "REF", (5.0, 0.0), (-5.0, 0.0))
    # Boost compensation (value TBD per TPS65100 datasheet)
    passive_between(page, "C515", "hmi:CAP", "TBD", "0603", 118.0, 210.0, "COMP", "GND", (5.0, 0.0), (0.0, -5.0))
    # VGH / VGL feedback dividers (values TBD per TPS65100 datasheet)
    passive_between(page, "R506", "hmi:RES", "TBD", "0603", 60.0, 210.0, "LCD_VGH", "FB2", (5.0, 0.0), (-5.0, 0.0))
    passive_between(page, "R507", "hmi:RES", "TBD", "0603", 75.0, 210.0, "FB2", "GND", (5.0, 0.0), (-5.0, 0.0))
    passive_between(page, "R508", "hmi:RES", "TBD", "0603", 90.0, 210.0, "LCD_VGL", "FB1", (5.0, 0.0), (-5.0, 0.0))
    passive_between(page, "R509", "hmi:RES", "TBD", "0603", 105.0, 210.0, "FB1", "GND", (5.0, 0.0), (-5.0, 0.0))

    # MT9284 U13 backlight boost
    u13 = page.add_component("U13", "hmi:MT9284", "MT9284-28J", "TSOT-23-6",
                             140.0, 140.0, 0.0,
                             pin_nets={"1": "SW", "2": "GND", "3": "LCD_BL_FB",
                                       "4": "LCD_BL_PWM", "5": "OV", "6": "VCC5V"})
    connect_pin(page, u13, "6", "VCC5V")
    connect_pin(page, u13, "2", "GND")
    connect_pin(page, u13, "4", "LCD_BL_PWM")
    connect_pin(page, u13, "3", "VLED_K")
    connect_pin(page, u13, "5", None)  # OVP divider
    connect_pin(page, u13, "1", "SW_BL")

    # Backlight passives
    passive_between(page, "L501", "hmi:IND", "22uH", "SMD", 170.0, 130.0, "VCC5V", "SW_BL", (5.0, 0.0), (-5.0, 0.0))
    passive_between(page, "D501", "hmi:DIO", "SS34", "SMA", 160.0, 120.0, "SW_BL", "VLED_A", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "C505", "hmi:CAP", "10u", "0805", 178.0, 110.0, "VCC5V", "GND", (5.0, 0.0), (0.0, -5.0))
    passive_between(page, "C506", "hmi:CAP", "22u", "0805", 160.0, 100.0, "VLED_A", "GND", (5.0, 0.0), (0.0, -5.0))
    passive_between(page, "R510", "hmi:RES", "0.56R 1%", "0805", 170.0, 150.0, "VLED_K", "GND", (5.0, 0.0), (0.0, -5.0))
    # Pull-up on PWM line (harmless)
    passive_between(page, "R504", "hmi:RES", "100k", "0603", 140.0, 170.0, "VCC5V", "LCD_BL_PWM", (0.0, 5.0), (0.0, -5.0))
    # Spare enable net (pulled up, not wired to U13; PWM drives EN directly)
    passive_between(page, "R505", "hmi:RES", "100k", "0603", 120.0, 180.0, "VCC5V", "LCD_BL_EN", (-5.0, 0.0), (5.0, 0.0))

    page.add_text("NOTE: TPS65100 反馈/VCOM/电荷泵阻容按目标电压计算；MT9284 RCS1=0.56R (VFB=104mV/180mA)", 20.0, 260.0, 1.27)
    return page


def build_page_P6() -> Page:
    page = Page("P6_RTP_TOUCH", "RTP_TOUCH")
    page.add_text("4-wire 电阻触摸屏 FPC", 20.0, 20.0, 2.54)
    page.add_text("电气序固定: 1=Y- 2=X+ 3=Y+ 4=X- (SoC TP-X1..TP-Y2)", 20.0, 25.0, 1.27)

    j4 = page.add_component("J4", "hmi:FPC4", "RTP_FPC", "CONN-FPC-4P",
                            80.0, 80.0, 0.0,
                            pin_nets={"1": "TCH_YN_F", "2": "TCH_XP_F",
                                      "3": "TCH_YP_F", "4": "TCH_XN_F"})
    connect_pin(page, j4, "1", "TCH_YN_F")
    connect_pin(page, j4, "2", "TCH_XP_F")
    connect_pin(page, j4, "3", "TCH_YP_F")
    connect_pin(page, j4, "4", "TCH_XN_F")

    # 0R debug links between SoC TP nets and FPC nets (renamed from R401-R404
    # to R601-R604 because R401-R424 on P4 are the LCD series resistors)
    passive_between(page, "R601", "hmi:RES", "0R", "0603", 50.0, 60.0, "TCH_XP", "TCH_XP_F", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "R602", "hmi:RES", "0R", "0603", 65.0, 60.0, "TCH_XN", "TCH_XN_F", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "R603", "hmi:RES", "0R", "0603", 80.0, 60.0, "TCH_YP", "TCH_YP_F", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "R604", "hmi:RES", "0R", "0603", 95.0, 60.0, "TCH_YN", "TCH_YN_F", (0.0, 5.0), (0.0, -5.0))

    page.add_text("D401 ESDA6V8 (SOT-23-6) 选配/DNP，通道数与封装待屏厂 FPC 确认", 20.0, 120.0, 1.27)
    page.add_text("NOTE: 原 R401-R404 重命名为 R601-R604，避免与 P4 LCD 串联电阻 R401-R424 重号", 20.0, 130.0, 1.27)
    return page


def build_page_P7() -> Page:
    page = Page("P7_USB", "USB")
    page.add_text("USB Type-C (USB0 OTG) + USB-A Host (USB1)", 20.0, 20.0, 2.54)

    # USB-C J7 (16P simplified: A1..A8, B1..B8)
    # Standard USB-C pin arrangement for UFP, data on A6/A7 + B6/B7
    j7 = page.add_component("J7", "hmi:USBC16", "USB-C-SMD-16P", "USB-C-SMD-16P",
                            80.0, 100.0, 0.0,
                            pin_nets={"1": "GND", "2": "USB0_VBUS", "3": "USB0_DP",
                                      "4": "USB0_DM", "5": "CC1", "6": "CC2",
                                      "7": "SBU1", "8": "GND",
                                      "9": "GND", "10": "USB0_VBUS", "11": "USB0_DP",
                                      "12": "USB0_DM", "13": "CC1", "14": "CC2",
                                      "15": "SBU2", "16": "GND"})
    # Map by common USB-C pinout: A1=GND A4=VBUS A5=CC1 A6=DP A7=DN A8=SBU1 A9=VBUS A12=GND
    # B1=GND B4=VBUS B5=CC2 B6=DP B7=DN B8=SBU2 B9=VBUS B12=GND
    # For the simplified 2x8 symbol we assign pins 1-8 = A row, 9-16 = B row.
    connect_pin(page, j7, "1", "GND")          # A1
    connect_pin(page, j7, "2", "USB0_VBUS")    # A4
    connect_pin(page, j7, "3", "USB0_DP")      # A6
    connect_pin(page, j7, "4", "USB0_DM")      # A7
    connect_pin(page, j7, "5", "CC1")          # A5
    connect_pin(page, j7, "6", None)           # A8 SBU1 unused
    connect_pin(page, j7, "7", "GND")          # A9
    connect_pin(page, j7, "8", "GND")          # A12
    connect_pin(page, j7, "9", "GND")          # B1
    connect_pin(page, j7, "10", "USB0_VBUS")   # B4
    connect_pin(page, j7, "11", "USB0_DP")     # B6
    connect_pin(page, j7, "12", "USB0_DM")     # B7
    connect_pin(page, j7, "13", "CC2")         # B5
    connect_pin(page, j7, "14", None)          # B8 SBU2 unused
    connect_pin(page, j7, "15", "GND")         # B9
    connect_pin(page, j7, "16", "GND")         # B12

    # CC pull-downs (UFP)
    passive_between(page, "R701", "hmi:RES", "5.1k", "0603", 50.0, 130.0, "CC1", "GND", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "R702", "hmi:RES", "5.1k", "0603", 65.0, 130.0, "CC2", "GND", (0.0, 5.0), (0.0, -5.0))

    # USB0 ESD
    d_esd0 = page.add_component("D701", "hmi:USBLC6", "USBLC6-2SC6", "SOT-23-6",
                                120.0, 100.0, 0.0,
                                pin_nets={"1": "USB0_DP", "3": "USB0_DM",
                                          "2": "GND", "4": "USB0_VBUS", "5": "GND", "6": "GND"})
    connect_pin(page, d_esd0, "1", "USB0_DP")
    connect_pin(page, d_esd0, "3", "USB0_DM")
    connect_pin(page, d_esd0, "2", "GND")
    connect_pin(page, d_esd0, "4", "USB0_VBUS")
    connect_pin(page, d_esd0, "5", "GND")
    connect_pin(page, d_esd0, "6", "GND")

    # USB0 VBUS via schottky to VCC5V
    passive_between(page, "D702", "hmi:DIO", "SS34", "SMA", 140.0, 90.0, "USB0_VBUS", "VCC5V", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "C701", "hmi:CAP", "10u", "0805", 155.0, 90.0, "USB0_VBUS", "GND", (5.0, 0.0), (0.0, -5.0))

    # USB-A J8 (USB1 host)
    j8 = page.add_component("J8", "hmi:USBA", "USB-A-SMD", "USB-A-SMD",
                            80.0, 180.0, 0.0,
                            pin_nets={"1": "USB1_VBUS", "2": "USB1_DM", "3": "USB1_DP",
                                      "4": "GND", "5": "GND"})
    connect_pin(page, j8, "1", "USB1_VBUS")
    connect_pin(page, j8, "2", "USB1_DM")
    connect_pin(page, j8, "3", "USB1_DP")
    connect_pin(page, j8, "4", "GND")
    connect_pin(page, j8, "5", "GND")

    # USB1 ESD
    d_esd1 = page.add_component("D703", "hmi:USBLC6", "USBLC6-2SC6", "SOT-23-6",
                                120.0, 180.0, 0.0,
                                pin_nets={"1": "USB1_DP", "3": "USB1_DM",
                                          "2": "GND", "4": "USB1_VBUS", "5": "GND", "6": "GND"})
    connect_pin(page, d_esd1, "1", "USB1_DP")
    connect_pin(page, d_esd1, "3", "USB1_DM")
    connect_pin(page, d_esd1, "2", "GND")
    connect_pin(page, d_esd1, "4", "USB1_VBUS")
    connect_pin(page, d_esd1, "5", "GND")
    connect_pin(page, d_esd1, "6", "GND")

    # U15 SY6280AAAC current-limited host power switch (LCSC C207620)
    u15 = page.add_component("U15", "hmi:SY6280", "SY6280AAAC", "SOT-23-5",
                             160.0, 180.0, 0.0,
                             pin_nets={"1": "USB1_VBUS", "2": "GND", "3": "ILIM",
                                       "4": "USB1_VBUS_EN", "5": "VCC5V"})
    connect_pin(page, u15, "5", "VCC5V")
    connect_pin(page, u15, "4", "USB1_VBUS_EN")
    connect_pin(page, u15, "1", "USB1_VBUS")
    connect_pin(page, u15, "2", "GND")
    connect_pin(page, u15, "3", "ILIM")
    passive_between(page, "R703", "hmi:RES", "6.8k", "0603", 160.0, 200.0, "ILIM", "GND", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "C702", "hmi:CAP", "100u", "CASE-B", 175.0, 180.0, "USB1_VBUS", "GND", (5.0, 0.0), (0.0, -5.0))

    page.add_text("USB1_VBUS_EN = PB10 GPIO (T113 USB1-VBUS-EN)", 140.0, 160.0, 1.27)
    # PB10 is not bonded on T113-S3 eLQFP-128: default pull-up = always-on host power
    passive_between(page, "R705", "hmi:RES", "10k", "0603", 200.0, 160.0, "VCC5V", "USB1_VBUS_EN", (0.0, 5.0), (0.0, -5.0))
    page.add_text("PB10 不在 eLQFP-128 封装: 默认上拉常开; 如需 GPIO 控制改接 PE10 (J11 Pin3)", 140.0, 170.0, 1.016)
    page.add_text("U15=SY6280AAAC (C207620) 负载开关; ISET: Ilim=6800/Rset, R703=6.8k -> 1A", 20.0, 230.0, 1.27)
    return page


def build_page_P8() -> Page:
    page = Page("P8_COMM", "COMM")
    page.add_text("隔离 RS485 (UART3) + RS232 (UART2) + Ethernet RMII (IP101GRI)", 20.0, 20.0, 2.54)

    # ---- Isolated RS485 ----
    # B0505S isolation power
    u9p = page.add_component("U14", "hmi:B0505S", "B0505S-1W", "DIP-4/SMD-4",
                             50.0, 60.0, 0.0,
                             pin_nets={"1": "VCC5V", "2": "GND", "3": "ISO_GND", "4": "ISO_5V"})
    connect_pin(page, u9p, "1", "VCC5V")
    connect_pin(page, u9p, "2", "GND")
    connect_pin(page, u9p, "3", "ISO_GND")
    connect_pin(page, u9p, "4", "ISO_5V")
    passive_between(page, "C801", "hmi:CAP", "100n", "0603", 70.0, 60.0, "VCC5V", "GND", (5.0, 0.0), (0.0, -5.0))
    passive_between(page, "C802", "hmi:CAP", "100n", "0603", 80.0, 60.0, "ISO_5V", "ISO_GND", (5.0, 0.0), (0.0, -5.0))

    # ADUM1201 digital isolator
    u9a = page.add_component("U9", "hmi:ADUM1201", "ADUM1201", "SOIC-8",
                             50.0, 100.0, 0.0,
                             pin_nets={"1": "VCC3V3", "2": "GND", "3": "UART3_TXD",
                                       "4": "RS485_RXD", "5": "RS485_RXD_ISO",
                                       "6": "RS485_TXD_ISO", "7": "ISO_GND", "8": "ISO_5V"})
    # pins: 1=VDD1 2=GND1 3=VIA 4=VIB 5=VOB 6=VOA 7=GND2 8=VDD2
    # channel A->B: VIA(3) -> VOA(6);  channel B->A: VIB(4) -> VOB(5)
    connect_pin(page, u9a, "1", "VCC3V3")
    connect_pin(page, u9a, "2", "GND")
    connect_pin(page, u9a, "3", "UART3_TXD")
    connect_pin(page, u9a, "4", "RS485_RXD")
    connect_pin(page, u9a, "5", "UART3_RXD")
    connect_pin(page, u9a, "6", "RS485_TXD")
    connect_pin(page, u9a, "7", "ISO_GND")
    connect_pin(page, u9a, "8", "ISO_5V")

    # MAX3485 isolated-side transceiver
    u9b = page.add_component("U16", "hmi:MAX3485", "MAX3485", "SOIC-8",
                             50.0, 150.0, 0.0,
                             pin_nets={"1": "RS485_RXD", "2": "RS485_DE",
                                       "3": "RS485_DE", "4": "RS485_TXD",
                                       "5": "ISO_GND", "6": "RS485_A",
                                       "7": "RS485_B", "8": "ISO_5V"})
    connect_pin(page, u9b, "1", "RS485_RXD")
    connect_pin(page, u9b, "2", "RS485_DE")
    connect_pin(page, u9b, "3", "RS485_DE")
    connect_pin(page, u9b, "4", "RS485_TXD")
    connect_pin(page, u9b, "5", "ISO_GND")
    connect_pin(page, u9b, "6", "RS485_A")
    connect_pin(page, u9b, "7", "RS485_B")
    connect_pin(page, u9b, "8", "ISO_5V")

    # RS485 direction control placeholder (pulled down = receiver on)
    passive_between(page, "R801", "hmi:RES", "10k", "0603", 30.0, 150.0, "RS485_DE", "ISO_GND", (0.0, 5.0), (0.0, -5.0))
    # Termination (optional)
    passive_between(page, "R802", "hmi:RES", "120R", "0603", 80.0, 150.0, "RS485_A", "RS485_B", (0.0, 5.0), (0.0, -5.0))
    # SM712 protection
    d801 = page.add_component("D801", "hmi:SM712", "SM712", "SOT-23",
                              100.0, 150.0, 0.0,
                              pin_nets={"1": "RS485_A", "2": "ISO_GND", "3": "RS485_B"})
    connect_pin(page, d801, "1", "RS485_A")
    connect_pin(page, d801, "2", "ISO_GND")
    connect_pin(page, d801, "3", "RS485_B")

    page.add_text("RS485_DE 方向控制: 默认下拉=接收; 待改 GPIO/RTS 驱动", 30.0, 180.0, 1.016)

    # ---- RS232 (UART2, MAX3232) ----
    u10 = page.add_component("U10", "hmi:MAX3232", "MAX3232", "SOIC-16",
                             160.0, 60.0, 0.0,
                             pin_nets={"1": "C1P", "2": "V+", "3": "C1N",
                                       "4": "C2P", "5": "C2N", "6": "V-",
                                       "7": "NC_T2OUT", "8": "NC_R2IN",
                                       "9": "NC_R2OUT", "10": "GND_T2IN",
                                       "11": "UART2_TXD", "12": "UART2_RXD",
                                       "13": "RS232_RX", "14": "RS232_TX",
                                       "15": "GND", "16": "VCC3V3"})
    connect_pin(page, u10, "1", "C1P")
    connect_pin(page, u10, "2", "V232_P")
    connect_pin(page, u10, "3", "C1N")
    connect_pin(page, u10, "4", "C2P")
    connect_pin(page, u10, "5", "C2N")
    connect_pin(page, u10, "6", "V232_N")
    connect_pin(page, u10, "7", None)
    connect_pin(page, u10, "8", None)
    connect_pin(page, u10, "9", None)
    connect_pin(page, u10, "10", "GND")
    connect_pin(page, u10, "11", "UART2_TXD")
    connect_pin(page, u10, "12", "UART2_RXD")
    connect_pin(page, u10, "13", "RS232_RX")
    connect_pin(page, u10, "14", "RS232_TX")
    connect_pin(page, u10, "15", "GND")
    connect_pin(page, u10, "16", "VCC3V3")

    passive_between(page, "C803", "hmi:CAP", "1u", "0603", 140.0, 45.0, "C1P", "C1N", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "C804", "hmi:CAP", "1u", "0603", 150.0, 45.0, "C2P", "C2N", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "C805", "hmi:CAP", "1u", "0603", 160.0, 45.0, "V232_P", "GND", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "C806", "hmi:CAP", "1u", "0603", 170.0, 45.0, "V232_N", "GND", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "C807", "hmi:CAP", "100n", "0603", 180.0, 45.0, "VCC3V3", "GND", (0.0, 5.0), (0.0, -5.0))

    # DB9 connector J5 (board edge)
    j5 = page.add_component("J5", "hmi:DB9F", "DB9F", "DB9",
                            220.0, 60.0, 0.0,
                            pin_nets={"2": "RS232_RX", "3": "RS232_TX", "5": "GND"})
    connect_pin(page, j5, "1", None)
    connect_pin(page, j5, "2", "RS232_RX")
    connect_pin(page, j5, "3", "RS232_TX")
    connect_pin(page, j5, "4", None)
    connect_pin(page, j5, "5", "GND")
    connect_pin(page, j5, "6", None)
    connect_pin(page, j5, "7", None)
    connect_pin(page, j5, "8", None)
    connect_pin(page, j5, "9", None)

    # ---- Ethernet PHY IP101GRI ----
    u11 = page.add_component("U11", "hmi:IP101GRI", "IP101GRI", "QFN-32_L5.0-W5.0-P0.5",
                             160.0, 150.0, 0.0)
    # RMII and management connections
    eth_nets = {
        "1": None,             # TXER/FXSD unused -> NC
        "2": "X_ETH_IN",       # X1
        "3": "X_ETH_OUT",      # X2
        "4": None,             # COL/RMII unused
        "5": "ETH_TXEN",       # TXEN
        "6": None,             # TXD3 unused (RMII)
        "7": None,             # TXD2 unused
        "8": "ETH_TXD1",
        "9": "ETH_TXD0",
        "10": None,            # TXCLK/50M_CLKI unused (PHY supplies clock)
        "11": "PHY_AD0",       # LED0/PHY_AD0 strap
        "12": "PHY_AD3",       # LED3/PHY_AD3 strap
        "13": "VCC3V3",        # VDD_IO
        "14": "ETH_REFCLK",    # RXCLK/50M_CLKO -> MAC
        "15": None,            # RXD3 unused
        "16": None,            # RXD2 unused
        "17": "ETH_RXD1",
        "18": "ETH_RXD0",
        "19": "ETH_CRS_DV",
        "20": None,            # CRS/LEDMOD unused
        "21": "ETH_RXER",
        "22": "ETH_MDC",
        "23": "ETH_MDIO",
        "24": "GND",           # TEST_ON -> GND
        "25": "ISET",
        "26": "ETH_RXN",       # MDI_RN
        "27": "ETH_RXP",       # MDI_RP
        "28": "REGOUT",
        "29": "ETH_TXN",       # MDI_TN
        "30": "ETH_TXP",       # MDI_TP
        "31": "VCC3V3",        # AVDD33
        "32": "ETH_RST",       # RESET_N
        "33": "GND",           # EP
    }
    for pin_num, net in eth_nets.items():
        connect_pin(page, u11, pin_num, net)

    # PHY strapping / support
    passive_between(page, "R803", "hmi:RES", "10k", "0603", 130.0, 175.0, "PHY_AD0", "GND", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "R804", "hmi:RES", "10k", "0603", 140.0, 175.0, "PHY_AD3", "GND", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "R805", "hmi:RES", "2.49k 1%", "0603", 150.0, 175.0, "ISET", "GND", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "C808", "hmi:CAP", "10u", "0805", 190.0, 175.0, "REGOUT", "GND", (5.0, 0.0), (0.0, -5.0))
    passive_between(page, "C809", "hmi:CAP", "100n", "0603", 208.0, 185.0, "VCC3V3", "GND", (5.0, 0.0), (0.0, -5.0))

    # 25MHz crystal X3
    x3 = page.add_component("X3", "hmi:XTAL2", "25MHz", "XTAL-SMD-3225",
                            120.0, 120.0, 0.0,
                            pin_nets={"1": "X_ETH_IN", "2": "X_ETH_OUT"})
    connect_pin(page, x3, "1", "X_ETH_IN")
    connect_pin(page, x3, "2", "X_ETH_OUT")
    passive_between(page, "C810", "hmi:CAP", "18p", "0603", 105.0, 120.0, "X_ETH_IN", "GND", (-5.0, 0.0), (0.0, 5.0))
    passive_between(page, "C811", "hmi:CAP", "18p", "0603", 135.0, 120.0, "X_ETH_OUT", "GND", (5.0, 0.0), (0.0, 5.0))

    # RJ45 with magnetics J9
    j9 = page.add_component("J9", "hmi:HR911105A", "HR911105A", "HR911105A",
                            230.0, 150.0, 0.0)
    # Pins: 1-8 = magnetics side P1-P8; 9-16 = LEDs
    # P1=TD+ P2=TD- P3=RD+ P4=RD- P5=RD-CT P6=RD+CT P7=NC P8=CHS_GND
    j9_nets = {
        "1": "ETH_TXP", "2": "ETH_TXN",
        "3": "ETH_RXP", "4": "ETH_RXN",
        "5": "ETH_RCT", "6": "ETH_TCT",
        "7": None, "8": "CHS_GND",
        "9": None, "10": None, "11": None, "12": None,
        "13": None, "14": None, "15": None, "16": None,
    }
    for pin_num, net in j9_nets.items():
        connect_pin(page, j9, pin_num, net)
    # Center taps decoupled to GND
    passive_between(page, "C812", "hmi:CAP", "100n", "0603", 210.0, 130.0, "ETH_RCT", "GND", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "C813", "hmi:CAP", "100n", "0603", 220.0, 130.0, "ETH_TCT", "GND", (0.0, 5.0), (0.0, -5.0))
    # Chassis ground via Y-cap
    passive_between(page, "C814", "hmi:CAP", "1n 2kV", "1206", 250.0, 130.0, "CHS_GND", "GND", (0.0, 5.0), (0.0, -5.0))

    page.add_text("IP101GRI: PHY 地址 0x00 (AD0/AD3 下拉); REF_CLK 由 PHY 输出给 MAC (use_ephy25m=0)", 20.0, 260.0, 1.27)
    page.add_text("LCSC 勘误(2026-09 复核): IP101GRI=C910373 而非 C50326; HR911105A=C12074 而非 C7633; ADUM1201=C9669 而非 C25226; MAX3232=C15846 而非 C5757; MAX3485=C18148 而非 C7593; B0505S=C512032 而非 C83467; 详见 out/README.md", 20.0, 270.0, 1.27)
    return page


def build_page_P9() -> Page:
    page = Page("P9_IO_EXPANSION", "IO_EXPANSION")
    page.add_text("调试串口 + 扩展排针 + 按键 + LED", 20.0, 20.0, 2.54)

    # J10 debug UART1 header
    j10 = page.add_component("J10", "hmi:HDR1X4", "HDR-1x4-2.54", "HDR-1x4-2.54",
                             60.0, 60.0, 0.0,
                             pin_nets={"1": "UART1_TXD", "2": "UART1_RXD",
                                       "3": "VCC3V3", "4": "GND"})
    connect_pin(page, j10, "1", "UART1_TXD")
    connect_pin(page, j10, "2", "UART1_RXD")
    connect_pin(page, j10, "3", "VCC3V3")
    connect_pin(page, j10, "4", "GND")

    # J11 expansion 2x12
    j11 = page.add_component("J11", "hmi:HDR2X12", "HDR-2x12-2.54", "HDR-2x12-2.54",
                             60.0, 140.0, 0.0)
    j11_nets = {
        "1": "VCC3V3", "2": "GND",
        "3": "PE10_GPIO", "4": "PE11_GPIO",
        "5": "TWI2_SCL", "6": "TWI2_SDA",
        "7": None, "8": None,
        "9": "UART4_TXD", "10": "UART4_RXD",
        "11": "UART5_TXD", "12": "UART5_RXD",
        "13": None, "14": None, "15": None, "16": None, "17": None, "18": None,
        "19": "GND", "20": "VCC3V3",
        "21": None, "22": None, "23": None, "24": None,
    }
    for pin_num, net in j11_nets.items():
        connect_pin(page, j11, pin_num, net)
    page.add_text("J11 Pin7/8: PE14/PE15 不在 T113-S3 eLQFP-128 封装上, NC", 100.0, 140.0, 1.016)
    page.add_text("J11 Pin13-18: 预留 PB2-PB7 (RGB24 模式被 LCD 占用, RGB18 模式可作 CAN0/1)", 100.0, 150.0, 1.016)

    # Keys SW1/2/3
    sw1 = page.add_component("SW1", "hmi:SWITCH", "RESET_KEY", "TACT-6x6",
                             160.0, 60.0, 0.0, pin_nets={"1": "RESET_N", "2": "GND"})
    connect_pin(page, sw1, "1", "RESET_N")
    connect_pin(page, sw1, "2", "GND")
    passive_between(page, "R901", "hmi:RES", "10k", "0603", 140.0, 60.0, "VCC3V3", "RESET_N", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "C901", "hmi:CAP", "100n", "0603", 180.0, 60.0, "RESET_N", "GND", (0.0, 5.0), (0.0, -5.0))

    sw2 = page.add_component("SW2", "hmi:SWITCH", "FEL_KEY", "TACT-6x6",
                             160.0, 90.0, 0.0, pin_nets={"1": "FEL_N", "2": "GND"})
    connect_pin(page, sw2, "1", "FEL_N")
    connect_pin(page, sw2, "2", "GND")
    passive_between(page, "R902", "hmi:RES", "10k", "0603", 140.0, 90.0, "VCC3V3", "FEL_N", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "C902", "hmi:CAP", "100n", "0603", 180.0, 90.0, "FEL_N", "GND", (0.0, 5.0), (0.0, -5.0))

    sw3 = page.add_component("SW3", "hmi:SWITCH", "USER_KEY", "TACT-6x6",
                             160.0, 120.0, 0.0, pin_nets={"1": "USER_KEY", "2": "GND"})
    connect_pin(page, sw3, "1", "USER_KEY")
    connect_pin(page, sw3, "2", "GND")
    passive_between(page, "R903", "hmi:RES", "10k", "0603", 140.0, 120.0, "VCC3V3", "USER_KEY", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "C903", "hmi:CAP", "100n", "0603", 180.0, 120.0, "USER_KEY", "GND", (0.0, 5.0), (0.0, -5.0))
    # USER_KEY to PE8 via 0R
    passive_between(page, "R904", "hmi:RES", "0R", "0603", 200.0, 120.0, "USER_KEY", "PE8", (5.0, 0.0), (5.0, 0.0))

    # LEDs
    led1 = page.add_component("LED1", "hmi:LED", "RED", "0603",
                              160.0, 190.0, 0.0, pin_nets={"1": "N_LED1", "2": "P_LED1"})
    connect_pin(page, led1, "1", "N_LED1")
    connect_pin(page, led1, "2", "P_LED1")
    passive_between(page, "R905", "hmi:RES", "1k", "0603", 140.0, 190.0, "STATUS_RED", "N_LED1", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "R906", "hmi:RES", "0R", "0603", 180.0, 190.0, "P_LED1", "GND", (0.0, 5.0), (0.0, -5.0))

    led2 = page.add_component("LED2", "hmi:LED", "GREEN", "0603",
                              160.0, 220.0, 0.0, pin_nets={"1": "N_LED2", "2": "P_LED2"})
    connect_pin(page, led2, "1", "N_LED2")
    connect_pin(page, led2, "2", "P_LED2")
    passive_between(page, "R907", "hmi:RES", "1k", "0603", 140.0, 220.0, "STATUS_GREEN", "N_LED2", (0.0, 5.0), (0.0, -5.0))
    passive_between(page, "R908", "hmi:RES", "0R", "0603", 180.0, 220.0, "P_LED2", "GND", (0.0, 5.0), (0.0, -5.0))

    page.add_text("FEL_N: T113 无专用 FEL 引脚, 需按量产方案接入启动检测 GPIO (当前悬空)", 20.0, 260.0, 1.27)
    page.add_text("USER_KEY 经 R904 0R 接 PE8 (备用 GPIO)", 20.0, 270.0, 1.27)
    return page


# ---------------------------------------------------------------------------
# Emitters
# ---------------------------------------------------------------------------

_FLG_COUNTER = 0


def symbol_pin_numbers(lib_id: str) -> List[str]:
    if lib_id == "hmi:T113_S3":
        return [ball for ball in T113_PIN_LOCS]
    return [p[1] for p in PIN_DEFS[lib_id]]


# LCSC/JLC component identifiers carried into the generated schematic.
# Connector and passive codes that depend on the exact LCEDA library variant are
# intentionally omitted until mechanically/electrically verified in LCEDA.
LCSC_BY_REF = {
    "U1": "C51661", "U2": "C479074", "U3": "C7528821",
    "U4": "C479074", "U5": "C479074", "U7": "C907680",
    "U9": "C9669", "U10": "C15846", "U11": "C910373",
    "U12": "C181650", "U13": "C181780", "U14": "C512032",
    "U15": "C207620", "U16": "C18148", "J9": "C12074",
    "X1": "C7424728", "X3": "C5155479",
    "J7": "C165948", "J8": "C3034183",
    "D701": "C7519", "D703": "C7519",
    "SW1": "C480267", "SW2": "C480267", "SW3": "C480267",
    "U6": "C5197687", "D1": "C19077604", "D501": "C8678",
    "D701": "C7519", "D703": "C7519", "D801": "C3647085",
    "D501": "C8678", "D702": "C8678", "D704": "C8678",
    "F1": "C262070", "J1": "C475092", "J2": "C41426932", "J3": "C190666",
    "J7": "C165948", "J8": "C3034183", "J10": "C492403", "J11": "C2883562",
    "X2": "C32346", "RCS1": "C2174717",
}


def emit_component(comp: Dict) -> Tuple:
    pins = symbol_pin_numbers(comp["lib_id"])
    lcsc = LCSC_BY_REF.get(comp["ref"])
    properties = [
        ("property", "Reference", comp["ref"], ("at", snap(comp["x"]), snap(comp["y"] - 2.54), 0), effects(justify="left")),
        ("property", "Value", comp["value"], ("at", snap(comp["x"]), snap(comp["y"] + 2.54), 0), effects(justify="left")),
        ("property", "Footprint", comp["footprint"], ("at", snap(comp["x"]), snap(comp["y"]), 0), effects(hide=True)),
        ("property", "Datasheet", "", ("at", snap(comp["x"]), snap(comp["y"]), 0), effects(hide=True)),
    ]
    if lcsc:
        properties.append(("property", "LCSC", lcsc, ("at", snap(comp["x"]), snap(comp["y"]), 0), effects(hide=True)))
    return ("symbol",
            ("lib_id", comp["lib_id"]),
            ("at", snap(comp["x"]), snap(comp["y"]), comp["angle"]),
            ("unit", 1),
            ("exclude_from_sim", False),
            ("in_bom", True),
            ("on_board", True),
            ("dnp", bool(comp["dnp"])),
            ("uuid", comp["uuid"]),
            *properties,
            *[("pin", p) for p in pins])


def emit_page(page: Page, symbols: Dict[str, Tuple], page_uuid: str) -> str:
    parts: List[Any] = [
        "kicad_sch",
        ("version", 20200904),
        ("generator", "eeschema"),
        ("generator_version", "5.1.9"),
        ("uuid", page_uuid),
        ("paper", "A3"),
        ("title_block", ("title", page.title), ("company", "zhishu-hmi")),
    ]
    # lib_symbols
    lib_syms = [symbols[lid] for lid in sorted(page.used_lib_ids)]
    parts.append(("lib_symbols", *lib_syms))

    # PWR_FLAG instances -> convert to components + wires + labels
    global _FLG_COUNTER
    for net, x, y, _ang in page.power_flags:
        _FLG_COUNTER += 1
        comp = {
            "ref": f"#FLG0{_FLG_COUNTER:02d}", "lib_id": "hmi:PWR_FLAG", "value": net,
            "footprint": "", "x": x, "y": y, "angle": 0.0, "uuid": uid(),
            "pin_nets": {}, "fields": {}, "dnp": False,
        }
        page.components.append(comp)
        # pin tip at (x, y); pin angle 90 (body up) -> away = down
        page.add_wire(snap(x), snap(y), snap(x), snap(y - 2.54))
        page.add_global_label(net, snap(x), snap(y - 2.54), 270.0)

    # components
    for comp in page.components:
        parts.append(emit_component(comp))
    # wires
    for w in page.wires:
        parts.append(w)
    # labels
    for lb in page.labels:
        parts.append(lb)
    # no-connects
    for nc in page.no_connects:
        parts.append(nc)
    # texts
    for tx in page.texts:
        parts.append(tx)
    # sheet_instances
    parts.append(("sheet_instances", ("path", f"/{page_uuid}", ("page", "1"))))
    return sexp(parts) + "\n"


def emit_root(pages: List[Page], page_uuids: Dict[str, str], root_uuid: str) -> str:
    parts: List[Any] = [
        "kicad_sch",
        ("version", 20200904),
        ("generator", "eeschema"),
        ("generator_version", "5.1.9"),
        ("uuid", root_uuid),
        ("paper", "A1"),
        ("title_block", ("title", PROJECT), ("company", "zhishu-hmi")),
        ("lib_symbols",),
    ]
    cols, rows_n = 3, 3
    for i, page in enumerate(pages):
        col = i % cols
        row = i // cols
        x = 20.0 + col * 220.0
        y = 20.0 + row * 140.0
        parts.append(("sheet", ("at", x, y), ("size", 200.0, 120.0), ("fields_autoplaced",),
                      stroke(0.1524, "solid"),
                      fill((0, 0, 0, 0.0), "none"),
                      ("uuid", page_uuids[page.name]),
                      ("property", "Sheetname", page.name, ("at", x, y - 1.27, 0), effects(justify="left top")),
                      ("property", "Sheetfile", f"{page.name}.kicad_sch", ("at", x, y + 121.27, 0), effects(justify="left top")),
                      ("instances", ("project", PROJECT,
                                     ("path", f"/{root_uuid}", ("page", str(i + 2)))))))
    parts.append(("sheet_instances", ("path", "/", ("page", "1"))))
    return sexp(parts) + "\n"


def emit_kicad_pro(page_uuids: Dict[str, str], root_uuid: str) -> str:
    # KiCad 8/9/10 project format (meta.version=3, schematic.top_level_sheets).
    # The board settings are defaults; KiCad fills the rest on open.
    pro = {
        "board": {
            "3dviewports": [],
            "design_settings": {
                "defaults": {
                    "board_outline_line_width": 0.05, "copper_line_width": 0.2,
                    "copper_text_size_h": 1.5, "copper_text_size_v": 1.5,
                    "copper_text_thickness": 0.3, "silk_line_width": 0.1,
                    "silk_text_size_h": 1.0, "silk_text_size_v": 1.0,
                    "silk_text_thickness": 0.1, "fab_line_width": 0.1,
                    "other_line_width": 0.1,
                },
                "diff_pair_dimensions": [], "drc_exclusions": [],
                "meta": {"version": 2}, "rule_severities": {}, "rules": {},
                "teardrop_options": [], "teardrop_parameters": [],
                "track_widths": [], "tuning_pattern_settings": {},
                "via_dimensions": [], "zones_allow_external_fillets": False,
            },
            "layer_presets": [], "viewports": [],
        },
        "boards": [],
        "component_class_settings": {
            "assignments": [], "meta": {"version": 0},
            "sheet_component_classes": {"enabled": False},
        },
        "cvpcb": {"equivalence_files": []},
        "libraries": {"pinned_footprint_libs": [], "pinned_symbol_libs": []},
        "meta": {"filename": f"{PROJECT}.kicad_pro", "version": 3},
        "net_settings": {
            "classes": [{
                "bus_width": 12, "clearance": 0.2, "diff_pair_gap": 0.25,
                "diff_pair_via_gap": 0.25, "diff_pair_width": 0.2, "line_style": 0,
                "microvia_diameter": 0.3, "microvia_drill": 0.1, "name": "Default",
                "pcb_color": "rgba(0, 0, 0, 0.000)", "priority": 2147483647,
                "schematic_color": "rgba(0, 0, 0, 0.000)", "track_width": 0.2,
                "tuning_profile": "", "via_diameter": 0.6, "via_drill": 0.3,
                "wire_width": 6,
            }],
            "meta": {"version": 5},
            "net_colors": None, "netclass_assignments": None, "netclass_patterns": [],
        },
        "pcbnew": {
            "last_paths": {"idf": "", "netlist": "", "plot": "",
                           "specctra_dsn": "", "step": "", "vrml": ""},
            "page_layout_descr_file": "",
        },
        "schematic": {
            "bus_aliases": {},
            "drawing": {"label_size_ratio": 0.25, "pin_symbol_size": 0,
                        "text_offset_ratio": 0.08},
            "legacy_lib_dir": "", "legacy_lib_list": [],
            "top_level_sheets": [{
                "filename": f"{PROJECT}.kicad_sch", "name": PROJECT,
                "uuid": root_uuid,
            }],
        },
        "sheets": [],
        "text_variables": {},
        "tuning_profiles": {"meta": {"version": 0},
                            "tuning_profiles_impedance_geometric": []},
    }
    return json.dumps(pro, indent=2, ensure_ascii=False) + "\n"


def emit_kicad_sym(symbols: Dict[str, Tuple]) -> str:
    """Emit a KiCad 6+ symbol library from the same symbol definitions."""
    parts: List[Any] = ["kicad_symbol_lib", ("version", 20220914),
                        ("generator", "kicad_symbol_editor")]
    for lib_id, sym in symbols.items():
        if lib_id == "hmi:PWR_FLAG":
            continue  # power flags belong to the schematic only
        name = lib_id.split(":", 1)[1]
        # clone the symbol tuple, renaming the first atom
        sym_clone = list(sym)
        sym_clone[1] = name
        parts.append(tuple(sym_clone))
    return sexp(parts) + "\n"


# ---------------------------------------------------------------------------
# main
# ---------------------------------------------------------------------------

def main():
    src_dir = os.path.join(ROOT, "..")
    rows = load_t113_pins(os.path.join(ROOT, "refs", "t113-pins.csv"))
    assert len(rows) == 128, f"expected 128 T113 pins, got {len(rows)}"
    missing = [n for n in T113_NETS if n not in {r["pin_name"] for r in rows}]
    if missing:
        raise SystemExit(f"T113_NETS references unknown pins: {missing}")

    symbols = build_symbols(rows)
    os.makedirs(OUT_DIR, exist_ok=True)

    pages = [
        build_page_P1(),
        build_page_P2(rows),
        build_page_P3(),
        build_page_P4(),
        build_page_P5(),
        build_page_P6(),
        build_page_P7(),
        build_page_P8(),
        build_page_P9(),
    ]

    page_uuids = {p.name: uid() for p in pages}
    root_uuid = uid()

    for page in pages:
        content = emit_page(page, symbols, page_uuids[page.name])
        path = os.path.join(OUT_DIR, f"{page.name}.kicad_sch")
        with open(path, "w", encoding="utf-8") as f:
            f.write(content)
        n_comps = len(page.components) + len(page.power_flags)
        print(f"  {page.name}.kicad_sch: {n_comps} components, "
              f"{len(page.wires)} wires, {len(page.labels)} labels, "
              f"{len(page.no_connects)} NC")

    root_path = os.path.join(OUT_DIR, f"{PROJECT}.kicad_sch")
    with open(root_path, "w", encoding="utf-8") as f:
        f.write(emit_root(pages, page_uuids, root_uuid))
    print(f"  {PROJECT}.kicad_sch (root, {len(pages)} sheets)")

    with open(os.path.join(OUT_DIR, f"{PROJECT}.kicad_pro"), "w", encoding="utf-8") as f:
        f.write(emit_kicad_pro(page_uuids, root_uuid))
    print(f"  {PROJECT}.kicad_pro")

    with open(os.path.join(OUT_DIR, "hmi.kicad_sym"), "w", encoding="utf-8") as f:
        f.write(emit_kicad_sym(symbols))
    print(f"  hmi.kicad_sym ({len(symbols) - 1} symbols)")

    # Duplicate-reference check
    all_refs = []
    for p in pages:
        all_refs += [c["ref"] for c in p.components]
    dupes = sorted({r for r in all_refs if all_refs.count(r) > 1})
    if dupes:
        print(f"  WARNING: duplicate references: {dupes}")
    else:
        print(f"  refdes check OK ({len(all_refs)} unique references)")

    print("Done ->", OUT_DIR)


if __name__ == "__main__":
    main()


