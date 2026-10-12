# -*- coding: utf-8 -*-
"""Generate HMI control board KiCad 10 schematic (multi-sheet, global labels, embedded lib cache).

Connection model: every pin gets a 2.54mm stub wire ending in a global_label (net) or a
power symbol (rail). Same-named global labels connect across all sheets.
"""
import re, os

BASE = os.path.dirname(os.path.abspath(__file__))
LIB = os.path.join(BASE, 'hmi_lib.kicad_sym')
PROJ = 'hmi-board'

_uuid_ctr = [0]
GRID = 1.27


def S(v):
    return round(v / GRID) * GRID


def uuid():
    _uuid_ctr[0] += 1
    return 'hmi%08x-0000-0000-0000-%012x' % (_uuid_ctr[0], _uuid_ctr[0])


def load_cache():
    text = open(LIB).read()
    cache = {}
    for m in re.finditer(r'\(symbol "([^"]+)"\n', text):
        name = m.group(1)
        start = m.start()
        depth, i = 0, start
        while i < len(text):
            c = text[i]
            if c == '(':
                depth += 1
            elif c == ')':
                depth -= 1
                if depth == 0:
                    break
            i += 1
        body = text[start:i + 1]
        body = re.sub(r'\(symbol "([^"]+)"', r'(symbol "hmi:\1"', body, count=1)
        cache[name] = body
    return cache


CACHE = load_cache()


_GREF = {}
_GPWR = {}

PREFIX = {'R': 'R', 'C': 'C', 'CP': 'C', 'L': 'L', 'LED': 'D', 'D': 'D', 'TVS': 'D', 'F': 'F',
          'FB': 'L', 'X2': 'Y', 'SW': 'SW', 'T113_S3': 'U', 'W25N01GV': 'U', 'CH340E': 'U',
          'GL850G': 'U', 'RTL8201F': 'U', 'SP3485': 'U', 'SP3232': 'U', 'XC6206': 'U',
          'SY8089': 'U', 'TPS5430': 'U', 'MSK4010': 'U', 'OSC24M': 'U', 'CONN_2': 'J',
          'HDR_1x2': 'J', 'HDR_1x4': 'J', 'USB_A': 'J', 'USB_HDR': 'J', 'USB_C': 'J',
          'DB9': 'J', 'RJ45': 'J', 'TF': 'J', 'FPC50': 'J', 'HDR_2x10': 'J', 'HDR_2x12': 'J', 'PWR_FLAG': '#FLG'}


class Sheet:
    def __init__(self, fname, title, paper='A3'):
        self.fname = fname
        self.title = title
        self.paper = paper
        self.items = []
        self.used = set()

    def nxt(self, prefix):
        _GREF[prefix] = _GREF.get(prefix, 0) + 1
        return '%s%d' % (prefix, _GREF[prefix])

    def use(self, libname):
        self.used.add(libname)

    def add_sym(self, libname, ref, value, x, y):
        x, y = S(x), S(y)
        self.use(libname)
        ref = self.nxt(PREFIX[libname])
        self.items.append(
            '  (symbol (lib_id "hmi:%s") (at %.2f %.2f 0) (unit 1) (in_bom yes) (on_board yes)\n'
            '    (instances (project "%s" (path "/%s" (reference "%s") (value "%s") (footprint ""))))\n'
            '    (uuid "%s"))' % (libname, x, y, PROJ, ref, ref, value, uuid()))

    def add_pwr(self, rail, x, y):
        x, y = S(x), S(y)
        self.use(rail)
        _GPWR[rail] = _GPWR.get(rail, 0) + 1
        ref = '#PWR%s%02d' % (rail, _GPWR[rail])
        self.items.append(
            '  (symbol (lib_id "hmi:%s") (at %.2f %.2f 0) (unit 1) (in_bom no) (on_board no)\n'
            '    (instances (project "%s" (path "/%s" (reference "%s") (value "%s") (footprint ""))))\n'
            '    (uuid "%s"))' % (rail, x, y, PROJ, ref, ref, rail, uuid()))

    def wire(self, x1, y1, x2, y2):
        x1, y1, x2, y2 = S(x1), S(y1), S(x2), S(y2)
        self.items.append('  (wire (pts (xy %.2f %.2f) (xy %.2f %.2f)) (stroke (width 0) (type default) (color 0 0 0 0)) (uuid "%s"))'
                          % (x1, y1, x2, y2, uuid()))

    def gnet(self, name, x, y):
        x, y = S(x), S(y)
        self.items.append('  (global_label "%s" (shape passive) (at %.2f %.2f 0) (fields_autoplaced)\n    (effects (font (size 1.27 1.27))) (uuid "%s"))'
                          % (name, x, y, uuid()))

    def nc(self, x, y):
        x, y = S(x), S(y)
        self.items.append('  (no_connect (at %.2f %.2f) (uuid "%s"))' % (x, y, uuid()))

    def add_flag(self, x, y):
        x, y = S(x), S(y)
        self.use('PWR_FLAG')
        _GPWR['FLG'] = _GPWR.get('FLG', 0) + 1
        ref = '#FLG%02d' % _GPWR['FLG']
        self.items.append(
            '  (symbol (lib_id "hmi:PWR_FLAG") (at %.2f %.2f 0) (unit 1) (in_bom no) (on_board no)\n'
            '    (instances (project "%s" (path "/%s" (reference "%s") (value "PWR_FLAG") (footprint ""))))\n'
            '    (uuid "%s"))' % (x, y, PROJ, ref, ref, uuid()))

    def note(self, text, x, y, size=2.0):
        self.items.append('  (text "%s" (exclude_from_sim no) (at %.2f %.2f 0)\n    (effects (font (size %.2f %.2f)) (justify left bottom)) (uuid "%s"))'
                          % (text, x, y, size, size, uuid()))

    def emit(self):
        out = ['(kicad_sch (version 20241122) (generator "gen_sch")']
        out.append('  (uuid "%s")' % uuid())
        out.append('  (paper "%s")' % self.paper)
        out.append('  (title_block (title "%s") (date "2026-08-19") (rev "1.0") (company "zhishu-hmi"))' % self.title)
        out.append('  (lib_symbols')
        for name in sorted(self.used):
            out.append(CACHE[name])
        out.append('  )')
        out.extend(self.items)
        out.append(')')
        path = os.path.join(BASE, self.fname)
        open(path, 'w').write('\n'.join(out))
        print('wrote %s (%d items, %d symbols)' % (self.fname, len(self.items), len(self.used)))


# ---------- geometry ----------
def ic_geom(n):
    half = (n + 1) // 2
    g = {}
    for i in range(n):
        if i < half:
            y = (half - 1 - i) * 2.54 - (half - 1) * 1.27
            g[str(i + 1)] = (-1, y)
        else:
            y = (half - 1 - (i - half)) * 2.54 - (half - 1) * 1.27
            g[str(i + 1)] = (+1, y)
    return g


def conn_geom(n):
    ytop = (n - 1) * 1.27
    return {str(i + 1): (-1, ytop - i * 2.54) for i in range(n)}


_t113_base = ic_geom(128)
# KiCad assigns the 64 right-hand physical slots to pin numbers 128..65.
# Map each authoritative pin number to its actual right-hand slot.
T113_GEOM = dict(_t113_base)
for _pin in range(1, 129):
    _slot = (65 - _pin) if _pin <= 64 else (193 - _pin)
    T113_GEOM[str(_pin)] = _t113_base[str(_slot)]
NAND_GEOM = ic_geom(8)
CH340_GEOM = ic_geom(10)
GL850_GEOM = ic_geom(28)
PHY_GEOM = ic_geom(33)
SP3485_GEOM = ic_geom(8)
SP3232_GEOM = ic_geom(16)
XC6206_GEOM = ic_geom(3)
SY8089_GEOM = ic_geom(5)
TPS5430_GEOM = ic_geom(8)
MSK4010_GEOM = ic_geom(6)
OSC24M_GEOM = ic_geom(4)
CONN2_GEOM = conn_geom(2)
HDR2_GEOM = conn_geom(2)
HDR4_GEOM = conn_geom(4)
USBA_GEOM = conn_geom(4)
USBC_GEOM = conn_geom(16)
DB9_GEOM = conn_geom(9)
RJ45_GEOM = conn_geom(14)
TF_GEOM = conn_geom(10)
FPC_GEOM = conn_geom(50)
EXP_GEOM = conn_geom(24)
R_GEOM = {'1': (-1, 0), '2': (+1, 0)}


def place_ic(s, libname, ref, value, x, y, geom, pinmap, pitch=5.08):
    x, y = S(x), S(y)
    s.add_sym(libname, ref, value, x, y)
    for num, (side, dy) in geom.items():
        mapnum = str(len(geom) + 1 - int(num)) if pitch == 2.54 else num
        if mapnum not in pinmap:
            continue
        tipx, tipy = x + side * pitch, y + dy
        kind = pinmap[mapnum]
        if kind[0] == 'nc':
            s.nc(tipx, tipy)
        else:
            ex = tipx + side * 2.54
            s.wire(tipx, tipy, ex, tipy)
            if kind[0] == 'net':
                s.gnet(kind[1], ex, tipy)
            else:
                s.add_pwr(kind[1], ex, tipy)


def place2(s, libname, ref, value, x, y, n1, n2):
    x, y = S(x), S(y)
    s.add_sym(libname, ref, value, x, y)
    for num, side in [('1', -1), ('2', +1)]:
        tipx, tipy = x + side * 2.54, y
        k = n1 if num == '1' else n2
        if k[0] == 'nc':
            s.nc(tipx, tipy)
        else:
            ex = tipx + side * 2.54
            s.wire(tipx, tipy, ex, tipy)
            if k[0] == 'net':
                s.gnet(k[1], ex, tipy)
            else:
                s.add_pwr(k[1], ex, tipy)


def r(s, ref, val, x, y, n1, n2):
    place2(s, 'R', ref, val, x, y, n1, n2)


def c(s, ref, val, x, y, n1, n2):
    place2(s, 'C', ref, val, x, y, n1, n2)


def cp(s, ref, val, x, y, n1, n2):
    place2(s, 'CP', ref, val, x, y, n1, n2)


def decap(s, ref, rail, x, y):
    c(s, ref, '100nF', x, y, ('pwr', rail), ('pwr', 'GND'))


# ================= POWER =================
def gen_power():
    s = Sheet('power.kicad_sch', 'Power 24V->5V/3V3/1V5/0V9/1V8')
    s.note('HMI Control Board - Power Tree', 20, 150)
    s.note('DC24V -> TPS5430 (5V) -> SY8089 x3 (3V3/1V5/0V9) + XC6206 (1V8)', 20, 146)
    s.note('SY8089 Vout=0.6*(1+Rup/Rdn): 3V3=150K/33K, 1V5=150K/100K, 0V9=100K/200K', 20, 142)
    # 24V input + protection
    place_ic(s, 'CONN_2', 'J1', 'DC24V_IN', 42.54, 138, CONN2_GEOM, pitch=2.54, pinmap={'1': ('net', 'VIN24'), '2': ('pwr', 'GND')})
    place2(s, 'F', 'F1', 'Fuse_3A', 65, 138, ('net', 'VIN24'), ('net', 'VIN24_F'))
    place2(s, 'TVS', 'D1', 'SMBJ26A', 85, 138, ('pwr', 'GND'), ('net', 'VIN24_F'))
    cp(s, 'C1', '100uF/50V', 105, 138, ('net', 'VIN24_F'), ('pwr', 'GND'))
    c(s, 'C2', '100nF', 115, 138, ('net', 'VIN24_F'), ('pwr', 'GND'))
    # TPS5430 24V->5V
    place_ic(s, 'TPS5430', 'U1', 'TPS5430', 145, 138, TPS5430_GEOM, {
        '1': ('net', 'TPS_BOOT'), '2': ('nc',), '3': ('nc',), '4': ('net', 'V5_FB'),
        '5': ('net', 'TPS_ENA'), '6': ('pwr', 'GND'), '7': ('net', 'VIN24_F'), '8': ('net', 'TPS_PH')})
    c(s, 'C3', '100nF', 150, 150, ('net', 'TPS_BOOT'), ('net', 'TPS_PH'))
    r(s, 'R1', '100K', 158, 152, ('net', 'VIN24_F'), ('net', 'TPS_ENA'))
    r(s, 'R2', '15K', 158, 146, ('net', 'TPS_ENA'), ('pwr', 'GND'))
    place2(s, 'D', 'D2', 'SS34', 168, 128, ('pwr', 'GND'), ('net', 'TPS_PH'))
    place2(s, 'L', 'L1', '22uH', 185, 138, ('net', 'TPS_PH'), ('pwr', 'VCC5V'))
    cp(s, 'C4', '100uF/10V', 205, 138, ('pwr', 'VCC5V'), ('pwr', 'GND'))
    c(s, 'C5', '100nF', 215, 138, ('pwr', 'VCC5V'), ('pwr', 'GND'))
    r(s, 'R3', '10K', 205, 148, ('pwr', 'VCC5V'), ('net', 'V5_FB'))
    r(s, 'R4', '3.24K', 205, 154, ('net', 'V5_FB'), ('pwr', 'GND'))
    # SY8089 3V3
    place_ic(s, 'SY8089', 'U2', 'SY8089', 40, 118, SY8089_GEOM, {
        '1': ('net', 'V3V3_EN'), '2': ('pwr', 'GND'), '3': ('net', 'V3V3_SW'), '4': ('pwr', 'VCC5V'), '5': ('net', 'V3V3_FB')})
    c(s, 'C6', '10uF', 40, 126, ('pwr', 'VCC5V'), ('pwr', 'GND'))
    r(s, 'R5', '100K', 40, 111, ('pwr', 'VCC5V'), ('net', 'V3V3_EN'))
    place2(s, 'L', 'L2', '2.2uH', 60, 118, ('net', 'V3V3_SW'), ('net', 'VCC3V3'))
    c(s, 'C7', '22uF', 82, 118, ('net', 'VCC3V3'), ('pwr', 'GND'))
    r(s, 'R6', '150K', 82, 127, ('net', 'VCC3V3'), ('net', 'V3V3_FB'))
    r(s, 'R7', '33K', 82, 133, ('net', 'V3V3_FB'), ('pwr', 'GND'))
    # SY8089 1V5
    place_ic(s, 'SY8089', 'U3', 'SY8089', 40, 96, SY8089_GEOM, {
        '1': ('net', 'V1V5_EN'), '2': ('pwr', 'GND'), '3': ('net', 'V1V5_SW'), '4': ('pwr', 'VCC5V'), '5': ('net', 'V1V5_FB')})
    c(s, 'C8', '10uF', 40, 104, ('pwr', 'VCC5V'), ('pwr', 'GND'))
    r(s, 'R8', '100K', 40, 89, ('pwr', 'VCC5V'), ('net', 'V1V5_EN'))
    place2(s, 'L', 'L3', '2.2uH', 60, 96, ('net', 'V1V5_SW'), ('net', 'VCC1V5'))
    c(s, 'C9', '22uF', 82, 96, ('net', 'VCC1V5'), ('pwr', 'GND'))
    r(s, 'R9', '150K', 82, 105, ('net', 'VCC1V5'), ('net', 'V1V5_FB'))
    r(s, 'R10', '100K', 82, 111, ('net', 'V1V5_FB'), ('pwr', 'GND'))
    # SY8089 0V9
    place_ic(s, 'SY8089', 'U4', 'SY8089', 40, 74, SY8089_GEOM, {
        '1': ('net', 'V0V9_EN'), '2': ('pwr', 'GND'), '3': ('net', 'V0V9_SW'), '4': ('pwr', 'VCC5V'), '5': ('net', 'V0V9_FB')})
    c(s, 'C10', '10uF', 40, 82, ('pwr', 'VCC5V'), ('pwr', 'GND'))
    r(s, 'R11', '100K', 40, 67, ('pwr', 'VCC5V'), ('net', 'V0V9_EN'))
    place2(s, 'L', 'L4', '2.2uH', 60, 74, ('net', 'V0V9_SW'), ('net', 'VDD0V9'))
    c(s, 'C11', '22uF', 82, 74, ('net', 'VDD0V9'), ('pwr', 'GND'))
    r(s, 'R12', '100K', 82, 83, ('net', 'VDD0V9'), ('net', 'V0V9_FB'))
    r(s, 'R13', '200K', 82, 89, ('net', 'V0V9_FB'), ('pwr', 'GND'))
    # XC6206 1V8
    place_ic(s, 'XC6206', 'U5', 'XC6206P182MR', 140, 74, XC6206_GEOM, {
        '1': ('pwr', 'GND'), '2': ('pwr', 'VCC1V8'), '3': ('pwr', 'VCC3V3')})
    c(s, 'C12', '1uF', 140, 84, ('pwr', 'VCC3V3'), ('pwr', 'GND'))
    c(s, 'C13', '2.2uF', 148, 74, ('pwr', 'VCC1V8'), ('pwr', 'GND'))
    # power leds
    place2(s, 'R', 'R14', '1K', 180, 60, ('pwr', 'VCC5V'), ('net', 'PWR_LED'))
    place2(s, 'LED', 'D3', 'LED_GREEN', 195, 60, ('net', 'PWR_LED'), ('pwr', 'GND'))
    # PWR_FLAG on every regulator-output rail (ERC drivers)
    for i, rail in enumerate(['VIN24', 'VCC5V', 'VCC3V3', 'VCC1V5', 'VDD0V9', 'VCC1V8']):
        x, y = 240, 40 - i * 7.62
        s.add_pwr(rail, x, y)
        s.wire(x, y, x + 2.54, y)
        s.add_flag(x + 2.54, y)
    return s


# ================= CORE =================
def gen_core():
    s = Sheet('core.kicad_sch', 'Core T113-S3')
    s.note('T113-S3 eLQFP128: power, clocks, reset, boot straps, SPI NAND, microSD', 20, 320)
    s.note('VDD-SYS0/1/2+VDD-CORE0/1=VDD0V9 | VCC-DRAM0/1=VCC1V5 | VDD18-DRAM/VCC-PLL/VCC-RTC=VCC1V8 | IO/VCC-PE/PD/PG=VCC3V3', 20, 316)
    x, y = 130, 165
    t113_nets = {
        '1': ('net', 'GPIO_PG6'), '2': ('net', 'GPIO_PG7'), '3': ('net', 'GPIO_PG8'),
        '4': ('net', 'RS485_DE'), '5': ('net', 'GPIO_PG10'), '6': ('net', 'CLK_25M'),
        '7': ('net', 'SD_D1'), '8': ('net', 'SD_D0'), '9': ('net', 'SD_CLK'), '10': ('net', 'SD_CMD'),
        '11': ('net', 'SD_D3'), '12': ('net', 'SD_D2'), '13': ('net', 'SD_DET'),
        '14': ('net', 'NAND_HOLD'), '15': ('net', 'NAND_WP'), '16': ('net', 'NAND_DO'),
        '17': ('net', 'NAND_DI'), '18': ('net', 'NAND_CS'), '19': ('net', 'NAND_CLK'),
        '20': ('pwr', 'VCC1V8'), '21': ('nc',), '22': ('nc',), '23': ('net', 'X24M_IN'),
        '24': ('net', 'X32K_OUT'), '25': ('net', 'X32K_IN'), '26': ('pwr', 'VCC1V8'),
        '27': ('net', 'SYS_RSTn'), '28': ('net', 'AVCC'), '29': ('pwr', 'VCC3V3'), '30': ('net', 'LDOB_OUT'),
        '31': ('net', 'I2C2_SDA'), '32': ('net', 'I2C2_SCK'), '33': ('net', 'UART0_RXD'),
        '34': ('pwr', 'VCC3V3'), '35': ('net', 'UART0_TXD'), '36': ('net', 'GPIO_PE11'),
        '37': ('net', 'GPIO_PE10'), '38': ('net', 'MDIO'), '39': ('net', 'MDC'),
        '40': ('net', 'GPIO_PE7'), '41': ('net', 'GPIO_PE6'), '42': ('net', 'GPIO_PE5'),
        '43': ('net', 'GPIO_PE4'), '44': ('net', 'GPIO_PE0'), '45': ('net', 'GPIO_PE1'),
        '46': ('pwr', 'VDD0V9'), '47': ('net', 'DZQ'), '48': ('pwr', 'VCC1V5'), '49': ('pwr', 'VCC1V5'),
        '50': ('pwr', 'VCC1V8'), '51': ('pwr', 'VDD0V9'), '52': ('net', 'LCD_BL_PWM'),
        '53': ('net', 'LCD_VSYNC'), '54': ('net', 'LCD_HSYNC'),
        '55': ('net', 'LCD_B0'), '56': ('net', 'LCD_B1'), '57': ('net', 'LCD_B2'), '58': ('net', 'LCD_B3'),
        '59': ('net', 'LCD_B4'), '60': ('net', 'LCD_B5'), '61': ('net', 'LCD_G0'), '62': ('net', 'LCD_G1'),
        '63': ('net', 'LCD_G2'), '64': ('net', 'LCD_G3'), '65': ('pwr', 'VCC3V3'),
        '66': ('pwr', 'VCC3V3'), '67': ('net', 'LCD_G4'), '68': ('net', 'LCD_G5'),
        '69': ('net', 'LCD_R1'), '70': ('net', 'LCD_R0'), '71': ('net', 'LCD_R2'), '72': ('net', 'LCD_R3'),
        '73': ('net', 'LCD_R4'), '74': ('net', 'LCD_R5'), '75': ('net', 'LCD_CLK'), '76': ('net', 'LCD_DE'),
        '77': ('pwr', 'VCC3V3'), '78': ('nc',), '79': ('net', 'UART3_RXD'), '80': ('net', 'UART3_TXD'),
        '81': ('pwr', 'VDD0V9'), '82': ('net', 'CAN1_RX'), '83': ('pwr', 'VCC3V3'), '84': ('net', 'CAN1_TX'),
        '85': ('net', 'UART4_RXD'), '86': ('net', 'UART4_TXD'),
        '87': ('nc',), '88': ('nc',), '89': ('net', 'AVCC'), '90': ('net', 'VRA2'), '91': ('pwr', 'GND'),
        '92': ('net', 'VRA1'), '93': ('nc',), '94': ('nc',), '95': ('nc',), '96': ('nc',),
        '97': ('pwr', 'VCC1V8'), '98': ('nc',), '99': ('nc',), '100': ('nc',),
        '101': ('net', 'VIN_SENSE'), '102': ('net', 'TCH_XP'), '103': ('net', 'TCH_XN'),
        '104': ('net', 'TCH_YP'), '105': ('net', 'TCH_YN'), '106': ('nc',),
        '107': ('pwr', 'VCC3V3'), '108': ('nc',), '109': ('nc',), '110': ('net', 'TVIN_VRP'),
        '111': ('net', 'TVIN_VRN'), '112': ('net', 'HUB_DP'), '113': ('net', 'HUB_DM'),
        '114': ('net', 'USB0_DM'), '115': ('net', 'USB0_DP'), '116': ('pwr', 'VDD0V9'), '117': ('pwr', 'VDD0V9'),
        '118': ('net', 'RMII_RXD0'), '119': ('net', 'RMII_RXD1'), '120': ('net', 'RMII_CRS_DV'),
        '121': ('net', 'RMII_REFCLK'), '122': ('net', 'RMII_TXD1'), '123': ('net', 'RMII_TXD0'),
        '124': ('net', 'RMII_TXEN'), '125': ('net', 'RMII_RXER'), '126': ('net', 'KEY_USER'),
        '127': ('net', 'LED_USER'), '128': ('pwr', 'VCC3V3'),
    }
    # KiCad reports the right-side unit slots in reverse pin-number order;
    # remap only the pinmap keys so netlist pin numbers remain authoritative.
    place_ic(s, 'T113_S3', 'U1', 'T113-S3', x, y, T113_GEOM, t113_nets)
    # decoupling
    for i, rail in enumerate(['VCC3V3', 'VCC3V3', 'VCC3V3', 'VCC1V8', 'VCC1V8', 'VDD0V9', 'VDD0V9', 'VCC1V5']):
        decap(s, 'C%d' % (100 + i), rail, 62 + i * 8, 40)
    # 24MHz oscillator -> DXIN
    place_ic(s, 'OSC24M', 'U2', '24MHz_OSC', 45, 255, OSC24M_GEOM, {
        '1': ('pwr', 'VCC3V3'), '2': ('pwr', 'GND'), '3': ('net', 'OSC24M_OUT'), '4': ('pwr', 'VCC3V3')})
    c(s, 'C108', '100nF', 45, 262, ('pwr', 'VCC3V3'), ('pwr', 'GND'))
    c(s, 'C109', '1uF', 60, 253.73, ('net', 'OSC24M_OUT'), ('net', 'X24M_IN'))
    # 32.768kHz crystal
    place2(s, 'X2', 'Y1', '32.768kHz', 45, 235, ('net', 'X32K_OUT'), ('net', 'X32K_IN'))
    c(s, 'C110', '22pF', 38, 235, ('net', 'X32K_OUT'), ('pwr', 'GND'))
    c(s, 'C111', '22pF', 52, 235, ('net', 'X32K_IN'), ('pwr', 'GND'))
    # reset (pullup to VCC1V8: RESET buffer supply = VCC-RTC)
    r(s, 'R15', '10K', 45, 215, ('pwr', 'VCC1V8'), ('net', 'SYS_RSTn'))
    place2(s, 'SW', 'SW1', 'RESET', 60, 215, ('net', 'SYS_RSTn'), ('pwr', 'GND'))
    c(s, 'C112', '100nF', 70, 215, ('net', 'SYS_RSTn'), ('pwr', 'GND'))
    # DZQ
    r(s, 'R16', '240R', 45, 205, ('net', 'DZQ'), ('pwr', 'GND'))
    # boot straps: SEL0=PC4(pulldown), SEL1=PC5(pullup) -> SMHC0->SPI NAND->other
    r(s, 'R17', '10K', 45, 195, ('net', 'NAND_DI'), ('pwr', 'GND'))
    r(s, 'R18', '10K', 45, 187, ('net', 'NAND_DO'), ('pwr', 'VCC3V3'))
    # W25N01GV
    place_ic(s, 'W25N01GV', 'U3', 'W25N01GVZEIG', 45, 160, NAND_GEOM, {
        '1': ('net', 'NAND_CS'), '2': ('net', 'NAND_DO'), '3': ('net', 'NAND_WP'),
        '4': ('pwr', 'GND'), '5': ('net', 'NAND_DI'), '6': ('net', 'NAND_CLK'),
        '7': ('net', 'NAND_HOLD'), '8': ('pwr', 'VCC3V3')})
    c(s, 'C113', '100nF', 50.08, 154, ('pwr', 'VCC3V3'), ('pwr', 'GND'))
    r(s, 'R19', '10K', 50.08, 146, ('pwr', 'VCC3V3'), ('net', 'NAND_WP'))
    r(s, 'R20', '10K', 50.08, 138, ('pwr', 'VCC3V3'), ('net', 'NAND_HOLD'))
    # microSD
    place_ic(s, 'TF', 'J1', 'microSD', 245, 250, TF_GEOM, pitch=2.54, pinmap={
        '1': ('net', 'SD_D2'), '2': ('net', 'SD_D3'), '3': ('net', 'SD_CMD'), '4': ('pwr', 'VCC3V3'),
        '5': ('net', 'SD_CLK'), '6': ('pwr', 'GND'), '7': ('net', 'SD_D0'), '8': ('net', 'SD_D1'),
        '9': ('net', 'SD_DET'), '10': ('pwr', 'GND')})
    r(s, 'R21', '10K', 255, 235, ('pwr', 'VCC3V3'), ('net', 'SD_CMD'))
    r(s, 'R22', '10K', 255, 228, ('pwr', 'VCC3V3'), ('net', 'SD_DET'))
    cp(s, 'C114', '10uF', 255, 221, ('pwr', 'VCC3V3'), ('pwr', 'GND'))
    # analog
    cp(s, 'C115', '2.2uF', 62, 60, ('net', 'AVCC'), ('pwr', 'GND'))
    c(s, 'C116', '100nF', 70, 60, ('net', 'AVCC'), ('pwr', 'GND'))
    c(s, 'C117', '470nF', 78, 60, ('net', 'VRA1'), ('pwr', 'GND'))
    c(s, 'C118', '470nF', 86, 60, ('net', 'VRA2'), ('pwr', 'GND'))
    c(s, 'C119', '100nF', 94, 60, ('net', 'TVIN_VRP'), ('pwr', 'GND'))
    c(s, 'C120', '100nF', 102, 60, ('net', 'TVIN_VRN'), ('pwr', 'GND'))
    cp(s, 'C121', '2.2uF', 110, 60, ('net', 'LDOB_OUT'), ('pwr', 'GND'))
    # GPIO expansion pullups (boot-safe: PG6/7/8 pulled down on some boards)
    return s



# ================= DISPLAY =================
def gen_display():
    s = Sheet('display.kicad_sch', 'Display LCD50P + Backlight + Touch')
    s.note('7inch 1024x600 RGB666 50P FPC (SZCLX070H-500-IPS-G-RTP, VERIFY PINOUT VS PANEL DATASHEET)', 20, 320)
    s.note('RGB666 high-aligned: R5-R0=D23-D18=PD17..PD12, G5-G0=D15-D10=PD11..PD6, B5-B0=D7-D2=PD5..PD0', 20, 316)
    s.note('R6/R7, G0/G1, B0/B1 tied low; 4-wire resistive touch -> T113 TPADC (X+ X- Y+ Y-)', 20, 312)
    fpc = {
        '1': ('net', 'VLED_K'), '2': ('net', 'VLED_A'), '3': ('net', 'VLED_A'), '4': ('net', 'VLED_K'),
        '5': ('net', 'LCD_R0'), '6': ('net', 'LCD_R1'), '7': ('net', 'LCD_R2'), '8': ('net', 'LCD_R3'),
        '9': ('net', 'LCD_R4'), '10': ('net', 'LCD_R5'), '11': ('pwr', 'GND'), '12': ('pwr', 'GND'),
        '13': ('net', 'LCD_G0'), '14': ('net', 'LCD_G1'), '15': ('net', 'LCD_G2'), '16': ('net', 'LCD_G3'),
        '17': ('net', 'LCD_G4'), '18': ('net', 'LCD_G5'), '19': ('pwr', 'GND'), '20': ('pwr', 'GND'),
        '21': ('net', 'LCD_B0'), '22': ('net', 'LCD_B1'), '23': ('net', 'LCD_B2'), '24': ('net', 'LCD_B3'),
        '25': ('net', 'LCD_B4'), '26': ('net', 'LCD_B5'), '27': ('pwr', 'GND'), '28': ('pwr', 'GND'),
        '29': ('pwr', 'VCC3V3'), '30': ('net', 'LCD_CLK'), '31': ('nc',), '32': ('net', 'LCD_HSYNC'),
        '33': ('net', 'LCD_VSYNC'), '34': ('net', 'LCD_DE'), '35': ('nc',), '36': ('nc',),
        '37': ('nc',), '38': ('nc',), '39': ('nc',), '40': ('nc',),
        '41': ('nc',), '42': ('nc',), '43': ('nc',), '44': ('nc',), '45': ('pwr', 'GND'),
        '46': ('net', 'TCH_XN'), '47': ('net', 'TCH_YN'), '48': ('net', 'TCH_XP'), '49': ('net', 'TCH_YP'),
        '50': ('pwr', 'GND'),
    }
    place_ic(s, 'FPC50', 'J1', 'LCD_50P_FPC', 50, 165, FPC_GEOM, pitch=2.54, pinmap=fpc)
    # backlight: MSK4010 boost from 5V -> VLED_A
    place_ic(s, 'MSK4010', 'U1', 'MSK4010', 120, 280, MSK4010_GEOM, {
        '1': ('net', 'BL_SW'), '2': ('pwr', 'GND'), '3': ('net', 'BL_FB'),
        '4': ('net', 'BL_EN'), '5': ('net', 'BL_OV'), '6': ('pwr', 'VCC5V')})
    cp(s, 'C1', '10uF', 120, 288, ('pwr', 'VCC5V'), ('pwr', 'GND'))
    c(s, 'C2', '100nF', 120, 294, ('pwr', 'VCC5V'), ('pwr', 'GND'))
    place2(s, 'L', 'L1', '22uH', 140, 280, ('net', 'BL_SW'), ('net', 'VLED_A'))
    r(s, 'R1', '1K', 132, 274, ('net', 'LCD_BL_PWM'), ('net', 'BL_EN'))
    place2(s, 'D', 'D1', 'SS14', 148, 268, ('net', 'BL_SW'), ('net', 'VLED_A'))
    c(s, 'C3', '10uF/50V', 152, 288, ('net', 'VLED_A'), ('pwr', 'GND'))
    r(s, 'R2', '5.1R', 140, 262, ('net', 'VLED_K'), ('net', 'BL_FB'))
    r(s, 'R3', '1.5K', 152, 262, ('net', 'BL_OV'), ('net', 'VLED_A'))
    # touch ESD caps
    c(s, 'C4', '10nF', 90, 120, ('net', 'TCH_XP'), ('pwr', 'GND'))
    c(s, 'C5', '10nF', 98, 120, ('net', 'TCH_XN'), ('pwr', 'GND'))
    c(s, 'C6', '10nF', 106, 120, ('net', 'TCH_YP'), ('pwr', 'GND'))
    c(s, 'C7', '10nF', 114, 120, ('net', 'TCH_YN'), ('pwr', 'GND'))
    return s


# ================= COMM =================
def gen_comm():
    s = Sheet('comm.kicad_sch', 'RS485 + RS232 + Ethernet RMII')
    s.note('RS485: UART4 (PB2/PB3) SP3485, DE/RE=PG9 | RS232: UART0 (PE2/PE3) SP3232 | ETH: RTL8201F RMII', 20, 320)
    s.note('RMII: PG0 CRS_DV, PG1 RXD0, PG2 RXD1, PG3 REFCLK(50M in), PG4 TXD0, PG5 TXD1, PG12 TXEN, PG13 RXER, PE8 MDC, PE9 MDIO', 20, 316)
    # RS485
    place_ic(s, 'SP3485', 'U1', 'SP3485EEN', 60, 260, SP3485_GEOM, {
        '1': ('net', 'UART4_RXD'), '2': ('net', 'RS485_DE'), '3': ('net', 'RS485_DE'),
        '4': ('net', 'UART4_TXD'), '5': ('pwr', 'GND'), '6': ('net', 'RS485_A'), '7': ('net', 'RS485_B'),
        '8': ('pwr', 'VCC3V3')})
    c(s, 'C1', '100nF', 65.08, 252, ('pwr', 'VCC3V3'), ('pwr', 'GND'))
    place_ic(s, 'HDR_1x2', 'J1', 'RS485_A/B', 95, 262.5, HDR2_GEOM, pitch=2.54, pinmap={'1': ('net', 'RS485_A'), '2': ('net', 'RS485_B')})
    r(s, 'R1', '120R', 95, 256, ('net', 'RS485_A'), ('net', 'RS485_B'))
    place2(s, 'TVS', 'D1', 'SMBJ6.5CA', 105, 256, ('pwr', 'GND'), ('net', 'RS485_A'))
    place2(s, 'TVS', 'D2', 'SMBJ6.5CA', 112.6, 256, ('pwr', 'GND'), ('net', 'RS485_B'))
    # RS232
    place_ic(s, 'SP3232', 'U2', 'SP3232EEN', 60, 200, SP3232_GEOM, {
        '1': ('net', 'R232_C1P'), '2': ('net', 'R232_VP'), '3': ('net', 'R232_C1N'),
        '4': ('net', 'R232_C2P'), '5': ('net', 'R232_C2N'), '6': ('net', 'R232_VN'),
        '7': ('nc',), '8': ('nc',), '9': ('nc',), '10': ('nc',),
        '11': ('net', 'UART0_TXD'), '12': ('net', 'UART0_RXD'), '13': ('net', 'R232_RXIN'),
        '14': ('net', 'R232_TXOUT'), '15': ('pwr', 'GND'), '16': ('pwr', 'VCC3V3')})
    c(s, 'C2', '100nF', 65.08, 190, ('pwr', 'VCC3V3'), ('pwr', 'GND'))
    c(s, 'C3', '100nF', 50, 216, ('net', 'R232_C1P'), ('net', 'R232_C1N'))
    c(s, 'C4', '100nF', 50, 200, ('net', 'R232_C2P'), ('net', 'R232_C2N'))
    c(s, 'C5', '100nF', 50, 184, ('net', 'R232_VP'), ('pwr', 'GND'))
    c(s, 'C6', '100nF', 75, 184, ('net', 'R232_VN'), ('pwr', 'GND'))
    place_ic(s, 'DB9', 'J2', 'DB9_MALE', 105, 200, DB9_GEOM, pitch=2.54, pinmap={
        '1': ('nc',), '2': ('net', 'R232_RXIN'), '3': ('net', 'R232_TXOUT'),
        '4': ('nc',), '5': ('pwr', 'GND'), '6': ('nc',), '7': ('nc',), '8': ('nc',), '9': ('nc',)})
    # Ethernet RTL8201F
    place_ic(s, 'RTL8201F', 'U3', 'RTL8201F', 220, 270, PHY_GEOM, {
        '1': ('nc',), '2': ('nc',), '3': ('net', 'PHY_X1'), '4': ('nc',),
        '5': ('pwr', 'VCC3V3'), '6': ('pwr', 'GND'), '7': ('net', 'NET_LINK'), '8': ('net', 'NET_ACT'),
        '9': ('net', 'RMII_TXD0'), '10': ('net', 'RMII_REFCLK'), '11': ('net', 'RMII_RXD1'),
        '12': ('net', 'NET_ACT'), '13': ('net', 'RMII_RXD0'), '14': ('net', 'RMII_CRS_DV'),
        '15': ('nc',), '16': ('nc',), '17': ('nc',), '18': ('net', 'RMII_RXER'),
        '19': ('net', 'MDIO'), '20': ('net', 'MDC'), '21': ('net', 'RMII_TXEN'), '22': ('net', 'RMII_TXD1'),
        '23': ('pwr', 'VCC3V3'), '24': ('pwr', 'GND'), '25': ('net', 'PHY_ISET'),
        '26': ('net', 'ETH_RXN'), '27': ('net', 'ETH_RXP'), '28': ('net', 'PHY_REGOUT'),
        '29': ('net', 'ETH_TXN'), '30': ('net', 'ETH_TXP'), '31': ('pwr', 'VCC3V3'), '32': ('net', 'PHY_RSTn'),
        '33': ('pwr', 'GND')})
    # 25M from T113 PG11 (EPHY-25M) -> PHY X1; PHY outputs 50M REFCLK on pin10 -> T113 PG3
    r(s, 'R2', '33R', 200, 283.5, ('net', 'CLK_25M'), ('net', 'PHY_X1'))
    r(s, 'R3', '6.19K', 205, 253.5, ('net', 'PHY_ISET'), ('pwr', 'GND'))
    c(s, 'C7', '100nF', 205, 247, ('net', 'PHY_REGOUT'), ('pwr', 'GND'))
    r(s, 'R4', '4.7K', 205, 240.5, ('pwr', 'VCC3V3'), ('net', 'PHY_RSTn'))
    r(s, 'R5', '4.7K', 200, 267.5, ('pwr', 'VCC3V3'), ('net', 'MDIO'))
    c(s, 'C8', '10uF', 230, 258.7, ('pwr', 'VCC3V3'), ('pwr', 'GND'))
    c(s, 'C9', '100nF', 230, 265, ('pwr', 'VCC3V3'), ('pwr', 'GND'))
    # MDI series + RJ45 with magnetics
    r(s, 'R6', '33R', 260, 263.5, ('net', 'ETH_TXP'), ('net', 'RJ_TXP'))
    r(s, 'R7', '33R', 260, 256.3, ('net', 'ETH_TXN'), ('net', 'RJ_TXN'))
    r(s, 'R8', '33R', 260, 246, ('net', 'ETH_RXP'), ('net', 'RJ_RXP'))
    r(s, 'R9', '33R', 260, 238.8, ('net', 'ETH_RXN'), ('net', 'RJ_RXN'))
    place_ic(s, 'RJ45', 'J3', 'RJ45+Mag', 295, 250, RJ45_GEOM, pitch=2.54, pinmap={
        '1': ('net', 'RJ_TXP'), '2': ('net', 'RJ_TXN'), '3': ('net', 'RJ_RXP'),
        '4': ('net', 'RJ_TXCT'), '5': ('net', 'RJ_TXCT'), '6': ('net', 'RJ_RXN'),
        '7': ('net', 'RJ_RXCT'), '8': ('net', 'RJ_RXCT'), '9': ('net', 'NET_LINK'),
        '10': ('pwr', 'GND'), '11': ('net', 'NET_ACT'), '12': ('pwr', 'GND'),
        '13': ('pwr', 'GND'), '14': ('pwr', 'GND')})
    r(s, 'R10', '49.9R', 270, 232, ('net', 'RJ_TXCT'), ('pwr', 'GND'))
    r(s, 'R11', '49.9R', 270, 226, ('net', 'RJ_RXCT'), ('pwr', 'GND'))
    c(s, 'C10', '6.8nF', 270, 220, ('net', 'RJ_TXCT'), ('pwr', 'GND'))
    c(s, 'C11', '6.8nF', 270, 214, ('net', 'RJ_RXCT'), ('pwr', 'GND'))
    return s


# ================= USB =================
def gen_usb():
    s = Sheet('usb.kicad_sch', 'USB: Debug CH340E + USB0 TypeC + GL850G HUB')
    s.note('CH340E debug on UART3 (PB6/PB7); USB0 device Type-C; USB1 host -> GL850G 4-port hub', 20, 300)
    s.note('Hub downstream: USB-A x2, WIFI/BT plug-in header, spare header. (SDIO WiFi conflicts with RMII on PG0-PG5)', 20, 296)
    # CH340E debug
    place_ic(s, 'CH340E', 'U1', 'CH340E', 60, 260, CH340_GEOM, {
        '1': ('net', 'DBG_DP'), '2': ('net', 'DBG_DM'), '3': ('pwr', 'GND'), '4': ('nc',),
        '5': ('nc',), '6': ('nc',), '7': ('pwr', 'VCC3V3'), '8': ('net', 'UART3_TXD'),
        '9': ('net', 'UART3_RXD'), '10': ('net', 'CH340_V3')})
    c(s, 'C1', '100nF', 65.08, 252, ('pwr', 'VCC3V3'), ('pwr', 'GND'))
    c(s, 'C2', '100nF', 50, 275, ('net', 'CH340_V3'), ('pwr', 'GND'))
    place_ic(s, 'USB_C', 'J1', 'USB_C_DBG', 95, 262, USBC_GEOM, pitch=2.54, pinmap={
        '1': ('pwr', 'GND'), '2': ('nc',), '3': ('nc',), '4': ('net', 'DBG_DP'), '5': ('net', 'DBG_DM'),
        '6': ('nc',), '7': ('pwr', 'GND'), '8': ('pwr', 'GND'), '9': ('nc',), '10': ('nc',),
        '11': ('net', 'DBG_DP'), '12': ('net', 'DBG_DM'), '13': ('nc',), '14': ('pwr', 'GND'),
        '15': ('pwr', 'GND'), '16': ('pwr', 'GND')})
    # USB0 device Type-C
    place_ic(s, 'USB_C', 'J2', 'USB_C_OTG', 60, 190, USBC_GEOM, pitch=2.54, pinmap={
        '1': ('pwr', 'GND'), '2': ('net', 'VBUS_SENSE'), '3': ('net', 'CC1'), '4': ('net', 'USB0_DP'),
        '5': ('net', 'USB0_DM'), '6': ('net', 'VBUS_SENSE'), '7': ('pwr', 'GND'), '8': ('pwr', 'GND'),
        '9': ('net', 'VBUS_SENSE'), '10': ('net', 'CC2'), '11': ('net', 'USB0_DP'), '12': ('net', 'USB0_DM'),
        '13': ('net', 'VBUS_SENSE'), '14': ('pwr', 'GND'), '15': ('pwr', 'GND'), '16': ('pwr', 'GND')})
    r(s, 'R1', '5.1K', 50, 203, ('net', 'CC1'), ('pwr', 'GND'))
    r(s, 'R2', '5.1K', 50, 196, ('net', 'CC2'), ('pwr', 'GND'))
    # GL850G hub
    place_ic(s, 'GL850G', 'U2', 'GL850G', 190, 240, GL850_GEOM, {
        '1': ('pwr', 'VCC3V3'), '2': ('pwr', 'GND'), '3': ('pwr', 'VCC3V3'),
        '4': ('net', 'HUB4_DM'), '5': ('net', 'HUB4_DP'), '6': ('net', 'HUB3_DM'), '7': ('net', 'HUB3_DP'),
        '8': ('net', 'HUB2_DM'), '9': ('net', 'HUB2_DP'), '10': ('net', 'HUB1_DM'), '11': ('net', 'HUB1_DP'),
        '12': ('nc',), '13': ('pwr', 'VCC3V3'), '14': ('net', 'HUB_REXT'), '15': ('net', 'HUB_DM'),
        '16': ('net', 'HUB_DP'), '17': ('pwr', 'VCC5V'), '18': ('net', 'VBUSM'), '19': ('pwr', 'GND'),
        '20': ('pwr', 'VCC5V'), '21': ('pwr', 'VCC3V3'), '22': ('net', 'HUB_DRV'), '23': ('pwr', 'GND'),
        '24': ('pwr', 'VCC3V3'), '25': ('nc',), '26': ('pwr', 'GND'), '27': ('pwr', 'GND'), '28': ('pwr', 'VCC3V3')})
    r(s, 'R3', '12K', 180, 232, ('net', 'HUB_REXT'), ('pwr', 'GND'))
    c(s, 'C3', '1uF', 180, 226, ('net', 'HUB_DRV'), ('pwr', 'GND'))
    r(s, 'R4', '10K', 200, 244, ('net', 'VBUSM'), ('pwr', 'VCC5V'))
    c(s, 'C4', '100nF', 195, 258, ('pwr', 'VCC3V3'), ('pwr', 'GND'))
    c(s, 'C5', '100nF', 195, 252, ('pwr', 'VCC3V3'), ('pwr', 'GND'))
    cp(s, 'C6', '10uF', 195, 222, ('pwr', 'VCC5V'), ('pwr', 'GND'))
    # downstream ports
    place_ic(s, 'USB_A', 'J3', 'USB_A1', 255, 262, USBA_GEOM, pitch=2.54, pinmap={
        '1': ('pwr', 'VCC5V'), '2': ('net', 'HUB1_DM'), '3': ('net', 'HUB1_DP'), '4': ('pwr', 'GND')})
    place_ic(s, 'USB_A', 'J4', 'USB_A2', 255, 244, USBA_GEOM, pitch=2.54, pinmap={
        '1': ('pwr', 'VCC5V'), '2': ('net', 'HUB2_DM'), '3': ('net', 'HUB2_DP'), '4': ('pwr', 'GND')})
    place_ic(s, 'USB_HDR', 'J5', 'WIFI_BT_PLUG', 255, 226, HDR4_GEOM, pitch=2.54, pinmap={
        '1': ('pwr', 'VCC5V'), '2': ('net', 'HUB3_DM'), '3': ('net', 'HUB3_DP'), '4': ('pwr', 'GND')})
    place_ic(s, 'USB_HDR', 'J6', 'USB_HDR', 255, 208, HDR4_GEOM, pitch=2.54, pinmap={
        '1': ('pwr', 'VCC5V'), '2': ('net', 'HUB4_DM'), '3': ('net', 'HUB4_DP'), '4': ('pwr', 'GND')})
    return s


# ================= IO =================
def gen_io():
    s = Sheet('io.kicad_sch', 'IO: LED/KEY/Expansion/VIN sense')
    s.note('LED_USER=PG15, KEY_USER=PG14, I2C2=PE12/PE13, CAN1=PB4/PB5, VIN_SENSE=GPADC0', 20, 300)
    place2(s, 'R', 'R1', '1K', 60, 260, ('net', 'LED_USER'), ('net', 'LED_A'))
    place2(s, 'LED', 'D1', 'LED_RED', 75, 260, ('net', 'LED_A'), ('pwr', 'GND'))
    r(s, 'R2', '10K', 60, 245, ('pwr', 'VCC3V3'), ('net', 'KEY_USER'))
    place2(s, 'SW', 'SW1', 'KEY_USER', 75, 245, ('net', 'KEY_USER'), ('pwr', 'GND'))
    # VIN sense divider 150K/10K -> 1.5V @ 24V (GPADC ref = AVCC 1.8V)
    r(s, 'R3', '150K', 60, 230, ('net', 'VIN24'), ('net', 'VIN_SENSE'))
    r(s, 'R4', '10K', 60, 224, ('net', 'VIN_SENSE'), ('pwr', 'GND'))
    c(s, 'C1', '100nF', 70, 224, ('net', 'VIN_SENSE'), ('pwr', 'GND'))
    # expansion 2x10
    place_ic(s, 'HDR_2x12', 'J1', 'EXP_2x12', 120, 250, EXP_GEOM, pitch=2.54, pinmap={
        '1': ('pwr', 'VCC5V'), '2': ('pwr', 'VCC3V3'), '3': ('pwr', 'GND'), '4': ('pwr', 'GND'),
        '5': ('net', 'I2C2_SCK'), '6': ('net', 'I2C2_SDA'), '7': ('net', 'UART0_TXD'), '8': ('net', 'UART0_RXD'),
        '9': ('net', 'CAN1_TX'), '10': ('net', 'CAN1_RX'), '11': ('net', 'GPIO_PE0'), '12': ('net', 'GPIO_PE1'),
        '13': ('net', 'GPIO_PE4'), '14': ('net', 'GPIO_PE5'), '15': ('net', 'GPIO_PE6'), '16': ('net', 'GPIO_PE7'),
        '17': ('net', 'GPIO_PE10'), '18': ('net', 'GPIO_PE11'), '19': ('net', 'GPIO_PG10'), '20': ('net', 'VIN_SENSE'),
        '21': ('net', 'GPIO_PG6'), '22': ('net', 'GPIO_PG7'), '23': ('net', 'GPIO_PG8'), '24': ('pwr', 'GND')})
    place_ic(s, 'HDR_1x4', 'J2', 'DEBUG_4P', 180, 280, HDR4_GEOM, pitch=2.54, pinmap={
        '1': ('pwr', 'VCC3V3'), '2': ('net', 'UART3_TXD'), '3': ('net', 'UART3_RXD'), '4': ('pwr', 'GND')})
    return s


# ================= ROOT =================
def gen_root():
    s = Sheet('hmi-board.kicad_sch', 'HMI Control Board T113-S3', paper='A3')
    s.note('ZHISHU HMI Control Board - T113-S3 - Rev 1.0 - 2026-08-19', 20, 120)
    s.note('Sheets: Power / Core / Display / COMM / USB / IO. Cross-sheet wiring via global labels.', 20, 116)
    sheets = [
        ('Power', 'power.kicad_sch', 25, 90),
        ('Core', 'core.kicad_sch', 75, 90),
        ('Display', 'display.kicad_sch', 125, 90),
        ('COMM', 'comm.kicad_sch', 25, 40),
        ('USB', 'usb.kicad_sch', 75, 40),
        ('IO', 'io.kicad_sch', 125, 40),
    ]
    for i, (name, fname, x, y) in enumerate(sheets):
        s.items.append(
            '  (sheet (at %.2f %.2f) (size 40 30) (fields_autoplaced)\n'
            '    (uuid "%s")\n'
            '    (property "Sheet name" "%s" (id 0) (at %.2f %.2f 0) (effects (font (size 1.27 1.27))))\n'
            '    (property "Sheet file" "%s" (id 1) (at %.2f %.2f 0) (effects (font (size 1.27 1.27))))\n'
            '    (fill (color 0 0 0 0)))' % (x, y, uuid(), name, x, y, fname, x, y))
    return s


if __name__ == '__main__':
    gen_power().emit()
    gen_core().emit()
    gen_display().emit()
    gen_comm().emit()
    gen_usb().emit()
    gen_io().emit()
    gen_root().emit()
