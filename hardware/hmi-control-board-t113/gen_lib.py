# -*- coding: utf-8 -*-
"""Generate hmi.kicad_sym (v20211014 simple format, then kicad-cli upgrades to 20251024)."""
import re

SYMS = {}   # name -> dict(ref, bom, pins=[(num,name,etype,x,y,rot)], body=[str])


def rect(x1, y1, x2, y2):
    return (f'(rectangle (start {x1} {y1}) (end {x2} {y2}) '
            f'(stroke (width 0.254) (type default) (color 0 0 0 0)) (fill (type none)))')


def poly(pts):
    p = ' '.join(f'(xy {x} {y})' for x, y in pts)
    return f'(polyline (pts {p}) (stroke (width 0.254) (type default) (color 0 0 0 0)) (fill (type none)))'


def prop(key, val, id_, x, y, hide=False):
    h = ' (hide yes)' if hide else ''
    return (f'(property "{key}" "{val}" (id {id_}) (at {x} {y} 0) '
            f'(effects (font (size 1.27 1.27))){h})')


def pin(num, name, etype, x, y, rot=0, length=0.635):
    return f'(pin {etype} line (at {x} {y} {rot}) (length {length}) (name "{name}") (number "{num}"))'


def sym(name, ref, pins_, body_, bom=True, refpos=(0, 3.81), valpos=(0, -3.81), val=None):
    SYMS[name] = dict(ref=ref, bom=bom, pins=pins_, body=body_,
                      refpos=refpos, valpos=valpos, val=val if val is not None else ref)


# ---------- basic passives ----------
sym('R', 'R', [pin(1, '1', 'passive', -2.54, 0, 0, 1.905), pin(2, '2', 'passive', 2.54, 0, 180, 1.905)],
    [rect(-0.635, -1.27, 0.635, 1.27)])
sym('C', 'C', [pin(1, '1', 'passive', -2.54, 0, 0, 1.905), pin(2, '2', 'passive', 2.54, 0, 180, 1.905)],
    [poly([(-0.635, -1.27), (0.635, -1.27)]), poly([(-0.635, 1.27), (0.635, 1.27)])])
sym('CP', 'C', [pin(1, '+', 'passive', -2.54, 0, 0, 1.905), pin(2, '-', 'passive', 2.54, 0, 180, 1.905)],
    [poly([(-0.635, -1.27), (0.635, -1.27)]), poly([(-0.635, 1.27), (0.635, 1.27)]),
     poly([(-0.381, 0.762), (0.381, 0.762)])])
sym('L', 'L', [pin(1, '1', 'passive', -2.54, 0, 0, 1.905), pin(2, '2', 'passive', 2.54, 0, 180, 1.905)],
    [poly([(-0.9, 0), (-0.55, 0), (-0.3, 1.1), (-0.05, -1.1), (0.2, 1.1), (0.45, -1.1), (0.7, 0), (0.9, 0)])])
sym('LED', 'LED', [pin(1, 'A', 'passive', -2.54, 0, 0, 1.905), pin(2, 'K', 'passive', 2.54, 0, 180, 1.905)],
    [poly([(-0.635, -1.27), (-0.635, 1.27), (0.635, 1.27), (0.635, -1.27), (-0.635, -1.27)]),
     poly([(-0.635, 1.27), (0.635, -1.27)]),
     poly([(-0.635, -1.27), (-0.635, -0.2), (0.3, -1.27)]),
     poly([(0.635, -1.27), (0.635, -0.2), (-0.3, -1.27)])])
sym('D', 'D', [pin(1, 'A', 'passive', -2.54, 0, 0, 1.905), pin(2, 'K', 'passive', 2.54, 0, 180, 1.905)],
    [poly([(-0.635, -1.27), (-0.635, 1.27), (0.635, 0), (-0.635, -1.27)]), poly([(0.635, -1.27), (0.635, 1.27)])])
sym('TVS', 'D', [pin(1, '1', 'passive', -2.54, 0, 0, 1.905), pin(2, '2', 'passive', 2.54, 0, 180, 1.905)],
    [poly([(-0.635, -1.27), (-0.635, 1.27), (0.635, 0), (-0.635, -1.27)]),
     poly([(0.635, -1.27), (0.635, 1.27), (-0.635, 0), (0.635, -1.27)]),
     poly([(-0.635, -1.27), (-0.635, 1.27)]), poly([(0.635, -1.27), (0.635, 1.27)])])
sym('F', 'F', [pin(1, '1', 'passive', -2.54, 0, 0, 1.905), pin(2, '2', 'passive', 2.54, 0, 180, 1.905)],
    [poly([(-1.1, 0), (1.1, 0)]), poly([(-0.75, 0), (-0.5, 0.5), (0.25, 0.5), (0.5, 0), (0.25, -0.5), (-0.75, -0.5), (-0.75, 0)])])
sym('FB', 'L', [pin(1, '1', 'passive', -2.54, 0, 0, 1.905), pin(2, '2', 'passive', 2.54, 0, 180, 1.905)],
    [poly([(-0.9, 0), (0.9, 0)])])
sym('X2', 'Y', [pin(1, '1', 'passive', -2.54, 0, 0, 1.905), pin(2, '2', 'passive', 2.54, 0, 180, 1.905)],
    [rect(-0.889, -1.016, 0.889, 1.016), poly([(-0.889, -0.508), (-0.508, -0.889)]),
     poly([(0.508, 0.508), (0.889, 0.889)])])
sym('SW', 'SW', [pin(1, '1', 'passive', -2.54, 0, 0, 1.905), pin(2, '2', 'passive', 2.54, 0, 180, 1.905)],
    [poly([(-2.54, 1.27), (2.54, 1.27)]), poly([(-1.27, 1.27), (1.27, 0)])])

# ---------- power symbols ----------
for rail in ['GND', 'VCC5V', 'VCC3V3', 'VCC1V8', 'VCC1V5', 'VDD0V9', 'VIN24']:
    SYMS[rail] = dict(ref='#PWR', bom=False,
                      pins=[pin(1, rail, 'power_in', 0, 0, 270, 0)],
                      body=[poly([(0, 0), (0, 1.27)]) if rail != 'GND' else poly([(-1.016, 0), (1.016, 0), (0, 1.016), (-1.016, 0)])],
                      refpos=(0, -3.175), valpos=(0, -6.35), val=rail, refval=('#PWR', rail))
    if rail == 'GND':
        SYMS[rail]['body'] = [poly([(-1.016, 0), (1.016, 0)]), poly([(-0.8, 0), (0.8, 0)])]

# ---------- generic 2-col IC builder ----------
def ic(name, ref, pins_):
    """pins_: list of (num, name, etype); left column = first half, right = second half, 2.54 pitch."""
    n = len(pins_)
    half = (n + 1) // 2
    body = []
    pins_out = []
    ytop = (half - 1) * 2.54 / 2 + 1.27
    for i, (num, pname, etype) in enumerate(pins_):
        if i < half:
            y = (half - 1 - i) * 2.54 - (half - 1) * 2.54 / 2
            pins_out.append(pin(num, pname, etype, -5.08, y, 0, 2.54))
        else:
            y = (half - 1 - (i - half)) * 2.54 - (half - 1) * 2.54 / 2
            pins_out.append(pin(num, pname, etype, 5.08, y, 180, 2.54))
    body.append(rect(-2.54, -ytop, 2.54, ytop))
    SYMS[name] = dict(ref=ref, bom=True, pins=pins_out, body=body,
                      refpos=(0, ytop + 2.54), valpos=(0, -ytop - 2.54), val=name)


# T113-S3 eLQFP128, pin number + primary name (from t113-s3_datasheet_v1.2 Table 4-2)
T113 = [
    (1, 'PG6'), (2, 'PG7'), (3, 'PG8'), (4, 'PG9'), (5, 'PG10'), (6, 'PG11'),
    (7, 'PF0'), (8, 'PF1'), (9, 'PF2'), (10, 'PF3'), (11, 'PF4'), (12, 'PF5'),
    (13, 'PF6'), (14, 'PC7'), (15, 'PC6'), (16, 'PC5'), (17, 'PC4'), (18, 'PC3'),
    (19, 'PC2'), (20, 'VCC-PLL'), (21, 'REFCLK-OUT'), (22, 'DXOUT'), (23, 'DXIN'),
    (24, 'X32KOUT'), (25, 'X32KIN'), (26, 'VCC-RTC'), (27, 'RESET'), (28, 'LDOA-OUT'),
    (29, 'LDO-IN'), (30, 'LDOB-OUT'), (31, 'PE13'), (32, 'PE12'), (33, 'PE3'),
    (34, 'VCC-PE'), (35, 'PE2'), (36, 'PE11'), (37, 'PE10'), (38, 'PE9'), (39, 'PE8'),
    (40, 'PE7'), (41, 'PE6'), (42, 'PE5'), (43, 'PE4'), (44, 'PE0'), (45, 'PE1'),
    (46, 'VDD-SYS0'), (47, 'DZQ'), (48, 'VCC-DRAM0'), (49, 'VCC-DRAM1'), (50, 'VDD18-DRAM'),
    (51, 'VDD-SYS1'), (52, 'PD22'), (53, 'PD21'), (54, 'PD20'), (55, 'PD0'),
    (56, 'PD1'), (57, 'PD2'), (58, 'PD3'), (59, 'PD4'), (60, 'PD5'),
    (61, 'PD6'), (62, 'PD7'), (63, 'PD8'), (64, 'PD9'), (65, 'VCC-LVDS'),
    (66, 'VCC-PD'), (67, 'PD10'), (68, 'PD11'), (69, 'PD13'), (70, 'PD12'),
    (71, 'PD14'), (72, 'PD15'), (73, 'PD16'), (74, 'PD17'), (75, 'PD18'),
    (76, 'PD19'), (77, 'VCC-TVOUT'), (78, 'TVOUT0'), (79, 'PB7'), (80, 'PB6'),
    (81, 'VDD-SYS2'), (82, 'PB5'), (83, 'VCC-IO'), (84, 'PB4'), (85, 'PB3'),
    (86, 'PB2'), (87, 'MICIN3P'), (88, 'MICIN3N'), (89, 'AVCC'), (90, 'VRA2'),
    (91, 'AGND'), (92, 'VRA1'), (93, 'FMINR'), (94, 'FMINL'), (95, 'LINEINR'),
    (96, 'LINEINL'), (97, 'HPVCC'), (98, 'HPOUTR'), (99, 'HPOUTL'), (100, 'HPOUTFB'),
    (101, 'GPADC0'), (102, 'TP-X1'), (103, 'TP-X2'), (104, 'TP-Y1'), (105, 'TP-Y2'),
    (106, 'NC0'), (107, 'VCC-TVIN'), (108, 'TVIN0'), (109, 'TVIN1'), (110, 'TVIN-VRP'),
    (111, 'TVIN-VRN'), (112, 'USB1-DP'), (113, 'USB1-DM'), (114, 'USB0-DM'), (115, 'USB0-DP'),
    (116, 'VDD-CORE0'), (117, 'VDD-CORE1'), (118, 'PG1'), (119, 'PG2'), (120, 'PG0'),
    (121, 'PG3'), (122, 'PG5'), (123, 'PG4'), (124, 'PG12'), (125, 'PG13'),
    (126, 'PG14'), (127, 'PG15'), (128, 'VCC-PG'),
]
pt = []
for num, name in T113:
    t = 'power_in' if (name.startswith('VCC') or name.startswith('VDD') or name == 'LDO-IN') else 'passive'
    if name == 'AGND':
        t = 'power_in'
    if name == 'LDOA-OUT':
        t = 'power_out'
    pt.append((num, name, t))
# Keep the authoritative pin-number order in the library. The schematic-side
# slot geometry is reversed separately to match KiCad's right-side placement.
ic('T113_S3', 'U', pt)

# PWR_FLAG: drives a power net for ERC (used on every regulator output rail)
sym('PWR_FLAG', '#FLG', [pin(1, 'p', 'power_out', 0, 0, 270, 0)],
    [poly([(0, 0), (0, -1.27)]), poly([(-0.7, -1.27), (0.7, -1.27)])],
    bom=False, refpos=(0, -2.54), valpos=(0, -3.81), val='PWR_FLAG')

# W25N01GV SPI NAND (WSON8)
ic('W25N01GV', 'U', [(1, 'CS#', 'input'), (2, 'DO/IO1', 'bidirectional'), (3, 'WP#/IO2', 'bidirectional'),
                     (4, 'GND', 'power_in'), (5, 'DI/IO0', 'bidirectional'), (6, 'CLK', 'input'),
                     (7, 'HOLD#/IO3', 'bidirectional'), (8, 'VCC', 'power_in')])
# CH340E (MSOP10)
ic('CH340E', 'U', [(1, 'UD+', 'bidirectional'), (2, 'UD-', 'bidirectional'), (3, 'GND', 'power_in'),
                   (4, 'RTS#', 'output'), (5, 'CTS#', 'input'), (6, 'TNOW', 'output'),
                   (7, 'VCC', 'power_in'), (8, 'TXD', 'output'), (9, 'RXD', 'input'), (10, 'V3', 'passive')])
# GL850G (SSOP28) — pins 4-11,14-16,18,20-22,24,25 confirmed from WavePi-S6 reference
ic('GL850G', 'U', [(1, 'VD33', 'power_in'), (2, 'GANG#', 'input'), (3, 'OVCUR#', 'input'),
                   (4, 'DM4', 'bidirectional'), (5, 'DP4', 'bidirectional'), (6, 'DM3', 'bidirectional'),
                   (7, 'DP3', 'bidirectional'), (8, 'DM2', 'bidirectional'), (9, 'DP2', 'bidirectional'),
                   (10, 'DM1', 'bidirectional'), (11, 'DP1', 'bidirectional'), (12, 'PGANG', 'output'),
                   (13, 'OVCUR2#', 'input'), (14, 'REXT', 'passive'), (15, 'DMU', 'bidirectional'),
                   (16, 'DPU', 'bidirectional'), (17, 'VDD5', 'power_in'), (18, 'VBUSM', 'input'),
                   (19, 'VSS', 'power_in'), (20, 'VDD5', 'power_in'), (21, 'VD33_0', 'power_in'),
                   (22, 'DRV', 'passive'), (23, 'VSS', 'power_in'), (24, 'VD33', 'power_in'),
                   (25, 'PWRJ', 'output'), (26, 'TEST', 'input'), (27, 'TEST2', 'input'), (28, 'VD33', 'power_in')])
# RTL8201F-VB-CG (QFN32+EPAD): pins 9-16, 25-33 confirmed from reference; 1-8/17-24 per datasheet (verify)
ic('RTL8201F', 'U', [(1, 'TXER/FXSD', 'input'), (2, 'COL/RMII', 'input'), (3, 'X1', 'input'),
                     (4, 'X2', 'output'), (5, 'VDD33', 'power_in'), (6, 'GND', 'power_in'),
                     (7, 'LED2/PHY_AD2', 'output'), (8, 'LED1/PHY_AD1', 'output'),
                     (9, 'TXD0', 'input'), (10, 'TXCLK/50M_CLKI', 'output'), (11, 'RXD1', 'output'),
                     (12, 'LED3/PHY_AD3', 'output'), (13, 'RXD0/VDO_IO', 'output'), (14, 'RXDV/CRS_DV/FX_HEN', 'output'),
                     (15, 'RXD3', 'output'), (16, 'RXD2', 'output'), (17, 'CRS/LEDMOP', 'output'),
                     (18, 'RXER/INTR_32', 'output'), (19, 'MDIO', 'bidirectional'), (20, 'MDC', 'input'),
                     (21, 'TXEN', 'input'), (22, 'TXD1', 'input'), (23, 'DVDD33', 'power_in'),
                     (24, 'TEST_ON', 'input'), (25, 'ISET', 'passive'), (26, 'MDI_RN', 'bidirectional'),
                     (27, 'MDI_RP', 'bidirectional'), (28, 'REGOUT', 'passive'), (29, 'MDI_TN', 'bidirectional'),
                     (30, 'MDI_TP', 'bidirectional'), (31, 'AVDD33', 'power_in'), (32, 'RESET_N', 'input'),
                     (33, 'EPAD', 'power_in')])
# SP3485E (SOIC8)
ic('SP3485', 'U', [(1, 'RO', 'output'), (2, 'RE#', 'input'), (3, 'DE', 'input'), (4, 'DI', 'input'),
                   (5, 'GND', 'power_in'), (6, 'A', 'bidirectional'), (7, 'B', 'bidirectional'), (8, 'VCC', 'power_in')])
# SP3232E (SOIC16)
ic('SP3232', 'U', [(1, 'C1+', 'passive'), (2, 'V+', 'passive'), (3, 'C1-', 'passive'), (4, 'C2+', 'passive'),
                   (5, 'C2-', 'passive'), (6, 'V-', 'passive'), (7, 'T2OUT', 'output'), (8, 'R2IN', 'input'),
                   (9, 'R2OUT', 'output'), (10, 'T2IN', 'input'), (11, 'T1IN', 'input'), (12, 'R1OUT', 'output'),
                   (13, 'R1IN', 'input'), (14, 'T1OUT', 'output'), (15, 'GND', 'power_in'), (16, 'VCC', 'power_in')])
# XC6206 (SOT23-3): 1=GND 2=OUT 3=IN
ic('XC6206', 'U', [(1, 'GND', 'power_in'), (2, 'OUT', 'power_out'), (3, 'IN', 'power_in')])
# SY8089AAC (SOT23-5)
ic('SY8089', 'U', [(1, 'EN', 'input'), (2, 'GND', 'power_in'), (3, 'SW', 'passive'), (4, 'IN', 'power_in'),
                   (5, 'FB', 'input')])
# TPS5430 (SOIC8): 1=BOOT 2=NC 3=NC 4=VSENSE 5=ENA 6=GND 7=VIN 8=PH
ic('TPS5430', 'U', [(1, 'BOOT', 'passive'), (2, 'NC', 'no_connect'), (3, 'NC', 'no_connect'),
                    (4, 'VSENSE', 'input'), (5, 'ENA', 'input'), (6, 'GND', 'power_in'),
                    (7, 'VIN', 'power_in'), (8, 'PH', 'passive')])
# MSK4010 (SOT23-6): 1=SW 2=GND 3=FB 4=EN 5=OV 6=VIN
ic('MSK4010', 'U', [(1, 'SW', 'passive'), (2, 'GND', 'power_in'), (3, 'FB', 'input'),
                    (4, 'EN', 'input'), (5, 'OV', 'passive'), (6, 'VIN', 'power_in')])
# 24MHz oscillator (4-pin): 1=EN 2=GND 3=OUT 4=VCC
ic('OSC24M', 'U', [(1, 'EN', 'input'), (2, 'GND', 'power_in'), (3, 'OUT', 'output'), (4, 'VCC', 'power_in')])


# ---------- connectors ----------
def conn(name, ref, pins_):
    """single-row connector, pins on left, tip at -2.54"""
    n = len(pins_)
    pins_out = []
    ytop = (n - 1) * 2.54 / 2
    body = []
    body.append(poly([(-2.54, ytop + 1.27), (2.54, ytop + 1.27), (2.54, -ytop - 1.27), (-2.54, -ytop - 1.27)]))
    for i, (num, pname) in enumerate(pins_):
        y = ytop - i * 2.54
        pins_out.append(pin(num, pname, 'passive', -2.54, y, 0, 1.905))
    SYMS[name] = dict(ref=ref, bom=True, pins=pins_out, body=body,
                      refpos=(0, ytop + 3.81), valpos=(0, -ytop - 3.81), val=name)


conn('CONN_2', 'J', [(1, '+'), (2, '-')])
conn('HDR_1x2', 'J', [(1, 'A'), (2, 'B')])
conn('HDR_1x4', 'J', [(1, '1'), (2, '2'), (3, '3'), (4, '4')])
conn('USB_A', 'J', [(1, 'VBUS'), (2, 'D-'), (3, 'D+'), (4, 'GND')])
conn('USB_HDR', 'J', [(1, 'VBUS'), (2, 'D-'), (3, 'D+'), (4, 'GND')])
# USB-C: A/B rows (16 signal pins + 2 shield)
conn('USB_C', 'J', [(1, 'A1/GND'), (2, 'A4/VBUS'), (3, 'A5/CC1'), (4, 'A6/D+'), (5, 'A7/D-'),
                    (6, 'A9/VBUS'), (7, 'A12/GND'), (8, 'B1/GND'), (9, 'B4/VBUS'), (10, 'B5/CC2'),
                    (11, 'B6/D+'), (12, 'B7/D-'), (13, 'B9/VBUS'), (14, 'B12/GND'), (15, 'S1'), (16, 'S2')])
conn('DB9', 'J', [(1, 'DCD'), (2, 'RXD'), (3, 'TXD'), (4, 'DTR'), (5, 'GND'),
                  (6, 'DSR'), (7, 'RTS'), (8, 'CTS'), (9, 'RI')])
conn('RJ45', 'J', [(1, 'TXP'), (2, 'TXN'), (3, 'RXP'), (4, 'TXCT'), (5, 'TXCT'), (6, 'RXN'),
                   (7, 'RXCT'), (8, 'RXCT'), (9, 'GLED+'), (10, 'GLED-'), (11, 'YLED+'), (12, 'YLED-'),
                   (13, 'SH1'), (14, 'SH2')])
conn('TF', 'J', [(1, 'DAT2'), (2, 'DAT3'), (3, 'CMD'), (4, 'VDD'), (5, 'CLK'),
                 (6, 'GND'), (7, 'DAT0'), (8, 'DAT1'), (9, 'CD'), (10, 'GND')])

# LCD FPC 50-pin (7" 1024x600 RGB666 50P convention, verify vs panel datasheet docx)
FPC = [(1, 'LEDK'), (2, 'LEDA'), (3, 'LEDA'), (4, 'LEDK'),
       (5, 'R0'), (6, 'R1'), (7, 'R2'), (8, 'R3'), (9, 'R4'), (10, 'R5'), (11, 'R6'), (12, 'R7'),
       (13, 'G0'), (14, 'G1'), (15, 'G2'), (16, 'G3'), (17, 'G4'), (18, 'G5'), (19, 'G6'), (20, 'G7'),
       (21, 'B0'), (22, 'B1'), (23, 'B2'), (24, 'B3'), (25, 'B4'), (26, 'B5'), (27, 'B6'), (28, 'B7'),
       (29, 'VCC'), (30, 'DCLK'), (31, 'NC/MODE'), (32, 'HSYNC'), (33, 'VSYNC'), (34, 'DE'),
       (35, 'NC'), (36, 'NC'), (37, 'NC'), (38, 'NC'), (39, 'NC'), (40, 'NC'),
       (41, 'NC'), (42, 'NC'), (43, 'NC'), (44, 'NC'), (45, 'GND'),
       (46, 'X-'), (47, 'Y-'), (48, 'X+'), (49, 'Y+'), (50, 'GND')]
conn('FPC50', 'J', FPC)

# 2x10 expansion header: row1 pins 1-10, row2 pins 11-20
EXP = [(1, '5V'), (2, '3V3'), (3, 'GND'), (4, 'GND'), (5, 'I2C2_SCK'), (6, 'I2C2_SDA'),
       (7, 'UART0_TXD'), (8, 'UART0_RXD'), (9, 'CAN1_TX'), (10, 'CAN1_RX'),
       (11, 'PE0'), (12, 'PE1'), (13, 'PE4'), (14, 'PE5'), (15, 'PE6'), (16, 'PE7'),
       (17, 'PE10'), (18, 'PE11'), (19, 'PG8'), (20, 'VIN_SENSE')]
conn('HDR_2x10', 'J', EXP)
conn('HDR_2x12', 'J', EXP + [(21, 'GPIO_PG6'), (22, 'GPIO_PG7'), (23, 'GPIO_PG8'), (24, 'GND')])

# DC input connector pin1 is a power source
SYMS['CONN_2']['pins'][0] = pin(1, '+', 'power_out', -2.54, 1.27, 0, 1.905)


# ---------- emission ----------
def emit_lib(path):
    out = ['(kicad_symbol_lib (version 20211014) (generator "gen_lib")']
    for name in SYMS:
        s = SYMS[name]
        ref = s['ref']
        val = s.get('refval', (ref, s.get('val', name)))
        rp = s.get('refpos', (0, 3.81))
        vp = s.get('valpos', (0, -3.81))
        out.append(f'  (symbol "{name}" (pin_names (offset 0.762)) (in_bom {"yes" if s["bom"] else "no"}) (on_board {"yes" if s["bom"] else "no"})')
        out.append(f'    {prop("Reference", val[0], 0, rp[0], rp[1])}')
        out.append(f'    {prop("Value", val[1], 1, vp[0], vp[1])}')
        out.append(f'    {prop("Footprint", "", 2, 0, 0, hide=True)}')
        out.append(f'    {prop("Datasheet", "", 3, 0, 0, hide=True)}')
        for b in s['body']:
            out.append('    ' + b)
        for p in s['pins']:
            out.append('    ' + p)
        out.append('  )')
    out.append(')')
    open(path, 'w').write('\n'.join(out))
    print(f'wrote {path} with {len(SYMS)} symbols')


if __name__ == '__main__':
    emit_lib('/home/chen/Git/zhishu-hmi/hardware/hmi-control-board-t113/hmi.kicad_sym')
