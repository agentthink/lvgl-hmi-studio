#!/usr/bin/env python3
"""Convert KiCad 10 (20251024/20241122) symbols+schematics to KiCad 6 format
(20200904/20211108) for import into 嘉立创EDA专业版.

Key transforms:
- Drop (embedded_fonts no) from symbol library
- Pin (name "X" (effects (font ...))) -> (name "X")
- Pin (number "X" (effects (font ...))) -> (number "X")
- Property (effects (font ...) (justify ...) ...) -> (effects (font ...))
- Schematic: remove embedded (lib_symbols ...) section
- version headers downgraded
"""
import re
import sys
import os

SYM_IN = "hmi_lib.kicad_sym"
SCH_FILES = ["hmi-board.kicad_sch", "power.kicad_sch", "core.kicad_sch",
             "display.kicad_sch", "comm.kicad_sch", "usb.kicad_sch", "io.kicad_sch"]


def clean_effects(text):
    """Remove (justify ...) and other v8+ effects sub-fields, keep (font ...)."""
    # Remove (justify ...) occurrences
    text = re.sub(r'\s*\(justify [^)]*\)', '', text)
    # Remove (hide yes) inside effects if present is fine to keep; skip
    return text


def remove_balanced(text, start):
    """Given text and index of '(' at start, return index just after the matching ')'."""
    depth = 0
    i = start
    n = len(text)
    while i < n:
        if text[i] == '(':
            depth += 1
        elif text[i] == ')':
            depth -= 1
            if depth == 0:
                return i + 1
        i += 1
    return n


def strip_pin_attr(text, attr):
    """Replace (attr "X" (effects ...) ) -> (attr "X") for pin name/number.

    Handles arbitrarily nested effects blocks via a paren counter.
    """
    search = '(%s "' % attr
    out = []
    i = 0
    n = len(text)
    while i < n:
        idx = text.find(search, i)
        if idx == -1:
            out.append(text[i:])
            break
        # append everything up to and including the closing quote of the name
        q = text.index('"', idx + len(search))
        out.append(text[i:q + 1])  # includes '(name "X"'
        # check if (effects follows the name
        j = q + 1
        while j < n and text[j] in ' \t\n\r':
            j += 1
        if text.startswith('(effects', j):
            # find matching close paren via counter
            depth = 0
            k = j
            while k < n:
                if text[k] == '(':
                    depth += 1
                elif text[k] == ')':
                    depth -= 1
                    if depth == 0:
                        k += 1
                        break
                k += 1
            # The name's own closing ')' is at position k (right after the
            # effects block's ')'); it remains in the text and will be appended
            # by the next iteration, so we must NOT add an extra ')'.
            i = k
        else:
            i = j
    return ''.join(out)


def convert_sym(text):
    text = text.replace("(version 20251024)", "(version 20200904)")
    text = text.replace('(generator_version "10.0")', '(generator_version "6.0")')
    # Remove embedded_fonts
    text = text.replace("(embedded_fonts no)", "")
    # Pin name/number: (name "X" (effects ...)) -> (name "X")
    text = strip_pin_attr(text, "name")
    text = strip_pin_attr(text, "number")
    # Clean effects (justify etc) in properties
    text = clean_effects(text)
    return text


def remove_lib_symbols(text):
    """Remove the top-level (lib_symbols ...) block."""
    idx = text.find("(lib_symbols")
    if idx == -1:
        return text
    # find the matching close paren
    depth = 0
    i = idx
    n = len(text)
    while i < n:
        if text[i] == '(':
            depth += 1
        elif text[i] == ')':
            depth -= 1
            if depth == 0:
                # include this ')'
                end = i + 1
                # optionally consume trailing whitespace/newline
                j = end
                while j < n and text[j] in (' ', '\t', '\n', '\r'):
                    j += 1
                return text[:idx] + text[j:]
        i += 1
    return text


def convert_sch(text):
    text = text.replace("(version 20241122)", "(version 20211108)")
    text = text.replace('(generator "gen_sch")', '(generator "kicad_sch")')
    # Pin name/number: (name "X" (effects ...)) -> (name "X")
    text = strip_pin_attr(text, "name")
    text = strip_pin_attr(text, "number")
    text = clean_effects(text)
    text = remove_lib_symbols(text)
    # Remove leftover empty (lib_symbols ) shell
    text = re.sub(r'\n\s*\(lib_symbols\s*\)\n', '\n', text)
    return text


def main():
    src = "/home/chen/Git/zhishu-hmi/hardware/hmi-control-board-t113"
    dst = os.path.join(src, "lceda-import-v6")
    os.makedirs(dst, exist_ok=True)

    # symbol library
    with open(os.path.join(src, SYM_IN), encoding="utf-8") as f:
        sym_text = f.read()
    sym_out = convert_sym(sym_text)
    # rename to .sym for KiCad 6 compatibility (some importers want .sym)
    with open(os.path.join(dst, "hmi_lib.kicad_sym"), "w", encoding="utf-8") as f:
        f.write(sym_out)
    print("symbol ->", os.path.join(dst, "hmi_lib.kicad_sym"))

    # schematics
    for fn in SCH_FILES:
        with open(os.path.join(src, fn), encoding="utf-8") as f:
            t = f.read()
        t = convert_sch(t)
        with open(os.path.join(dst, fn), "w", encoding="utf-8") as f:
            f.write(t)
        print("schematic ->", fn)

    # copy project file
    for fn in ["hmi-board.kicad_pro", "sym-lib-table"]:
        if os.path.exists(os.path.join(src, fn)):
            with open(os.path.join(src, fn), encoding="utf-8") as f:
                t = f.read()
            # fix sym-lib-table to point to kicad_sym
            t = t.replace("hmi_lib.kicad_sym", "hmi_lib.kicad_sym")
            with open(os.path.join(dst, fn), "w", encoding="utf-8") as f:
                f.write(t)
            print("copy ->", fn)


if __name__ == "__main__":
    main()
