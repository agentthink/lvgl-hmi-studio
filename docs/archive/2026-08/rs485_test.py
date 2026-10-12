#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""RS485 two-port loopback test (corrected for half-duplex)."""
import argparse, os, sys, time, threading, random, traceback
try:
    import serial, serial.tools.list_ports
    HAS_SERIAL = True
except Exception as e:
    HAS_SERIAL = False
    print("warning: no pyserial:", e, file=sys.stderr)

BAUDS = [9600, 19200, 38400, 57600, 115200]


def make_virtual_pair():
    import pty, select
    ma, sa = pty.openpty(); mb, sb = pty.openpty()
    na, nb = os.ttyname(sa), os.ttyname(sb)
    def loop():
        while True:
            try:
                r, _, _ = select.select([ma, mb], [], [], 0.05)
                for fd in r:
                    try: d = os.read(fd, 4096)
                    except OSError: continue
                    try: os.write(mb if fd == ma else ma, d)
                    except OSError: pass
            except Exception:
                time.sleep(0.05)
    threading.Thread(target=loop, daemon=True).start()
    return na, nb


class Node:
    def __init__(self, port, baud=9600, timeout=0.5):
        self.ser = serial.Serial(port, baudrate=baud, timeout=timeout)
        self.sent_bytes = self.recv_bytes = 0

    def send(self, data):
        n = self.ser.write(data); self.sent_bytes += len(data); self.ser.flush(); return n

    def recv(self, n=4096, timeout=None):
        old = self.ser.timeout
        if timeout is not None: self.ser.timeout = timeout
        d = self.ser.read(n); self.ser.timeout = old; self.recv_bytes += len(d); return d

    def set_baud(self, baud):
        self.ser.baudrate = baud

    def close(self):
        try: self.ser.close()
        except: pass


def test_pingpong(a, b, baud=9600):
    a.set_baud(baud); b.set_baud(baud); time.sleep(0.1)
    msg = b"RS485_PING_%d" % random.randint(1000, 9999)
    a.send(msg); time.sleep(0.08)
    got = b.recv(64, timeout=1.0)
    ok = msg in got
    print("  [%s] A->B (baud=%d, recv=%dB)" % ("OK" if ok else "FAIL", baud, len(got)))
    return ok


def test_baudsweep(a, b):
    return all(test_pingpong(a, b, bd) for bd in BAUDS)


def test_bidirectional_seq(a, b):
    a.set_baud(115200); b.set_baud(115200); time.sleep(0.1)
    a.send(b"FROM_A"); time.sleep(0.05)
    rb = b.recv(64, timeout=0.5); ok_a2b = b"FROM_A" in rb
    print("  [%s] A->B dir (B got=%s)" % ("OK" if ok_a2b else "FAIL", ok_a2b))
    b.send(b"FROM_B"); time.sleep(0.05)
    ra = a.recv(64, timeout=0.5); ok_b2a = b"FROM_B" in ra
    print("  [%s] B->A dir (A got=%s)" % ("OK" if ok_b2a else "FAIL", ok_b2a))
    return ok_a2b and ok_b2a


def test_master_slave(a, b, rounds=20):
    a.set_baud(115200); b.set_baud(115200); time.sleep(0.1)
    for i in range(rounds):
        a.send(b"REQ%03d" % i); time.sleep(0.02)
        r = b.recv(64, timeout=0.5)
        if b"REQ%03d" % i not in r:
            print("  [FAIL] round %d slave no req" % i); return False
        b.send(b"REP%03d" % i); time.sleep(0.02)
        r = a.recv(64, timeout=0.5)
        if b"REP%03d" % i not in r:
            print("  [FAIL] round %d master no rep" % i); return False
    print("  [OK] master-slave pingpong %d/%d" % (rounds, rounds))
    return True


def test_stress(a, b, n=300):
    a.set_baud(115200); b.set_baud(115200); time.sleep(0.1)
    for i in range(n): a.send(b"PKT%05d" % i); time.sleep(0.001)
    buf = b""; dl = time.time() + 8
    while time.time() < dl:
        c = b.recv(4096, timeout=0.3)
        if c: buf += c
        if len([1 for i in range(n) if (b"PKT%05d" % i) in buf]) >= n: break
    cnt_a2b = len([i for i in range(n) if (b"PKT%05d" % i) in buf])
    for i in range(n): b.send(b"PKT%05d" % i); time.sleep(0.001)
    buf = b""; dl = time.time() + 8
    while time.time() < dl:
        c = a.recv(4096, timeout=0.3)
        if c: buf += c
        if len([1 for i in range(n) if (b"PKT%05d" % i) in buf]) >= n: break
    cnt_b2a = len([i for i in range(n) if (b"PKT%05d" % i) in buf])
    ok = cnt_a2b == n and cnt_b2a == n
    print("  [%s] stress A->B=%d/%d B->A=%d/%d" % ("OK" if ok else "FAIL", cnt_a2b, n, cnt_b2a, n))
    return ok


def auto_detect():
    return [p.device for p in serial.tools.list_ports.comports()
            if any(k in p.device for k in ("ttyUSB", "ttyACM", "ttyS"))]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--portA"); ap.add_argument("--portB")
    ap.add_argument("--sim", action="store_true")
    ap.add_argument("--stress", type=int, default=300)
    args = ap.parse_args()
    if not HAS_SERIAL: print("no pyserial"); sys.exit(2)
    if args.sim:
        print("=== sim pty ==="); na, nb = make_virtual_pair()
    else:
        na, nb = (args.portA, args.portB) if (args.portA and args.portB) else (auto_detect()[:2] or (None, None))
        if not na or not nb:
            print("no ports, use --portA/--portB or --sim"); sys.exit(1)
    print("=== A=%s B=%s ===" % (na, nb))
    try:
        a = Node(na); b = Node(nb)
    except Exception as e:
        print("open fail:", e); traceback.print_exc(); sys.exit(1)
    results = []
    print("--- baud sweep A->B ---"); results.append(("baud-sweep", test_baudsweep(a, b)))
    print("--- bidirectional seq ---"); results.append(("bi-seq", test_bidirectional_seq(a, b)))
    print("--- master-slave ---"); results.append(("master-slave", test_master_slave(a, b)))
    print("--- stress ---"); results.append(("stress", test_stress(a, b, args.stress)))
    a.close(); b.close()
    print("===== summary =====")
    passed = sum(1 for _, ok in results if ok)
    for name, ok in results:
        print("  [%s] %s" % ("OK" if ok else "FAIL", name))
    print("passed %d/%d" % (passed, len(results)))
    print("result:", "RS485 OK (half-duplex)" if passed == len(results) else "anomaly")


if __name__ == "__main__":
    main()
