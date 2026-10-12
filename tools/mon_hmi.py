#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
实机 HMI 监控脚本（运行在 T113 板上，python3.5 兼容）
监控三通道：
  1. /dev/input/event2 触摸事件流  -> 反映用户点击/屏幕切换操作
  2. /mnt/UDISK/hmi_runtime.log    -> 运行时日志增量（设置操作/键盘/错误）
  3. hmi_runtime 进程存活心跳      -> 崩溃/重启检测
用法: python3 -u /tmp/mon_hmi.py [PID]
"""
import struct
import time
import os
import sys
import select

LOG = "/mnt/UDISK/hmi_runtime.log"
EV = "/dev/input/event2"
PID = int(sys.argv[1]) if len(sys.argv) > 1 else 0


def ts():
    return time.strftime("%H:%M:%S")


# struct input_event: timeval(tv_sec long, tv_usec long) + type/ushort code/ushort value/int
FMT = "llHHI"
SZ = struct.calcsize(FMT)

CODES = {
    (3, 0): "ABS_X",
    (3, 1): "ABS_Y",
    (3, 47): "ABS_MT_SLOT",
    (3, 48): "ABS_MT_TOUCH_MAJOR",
    (3, 50): "ABS_MT_WIDTH_MAJOR",
    (3, 53): "ABS_MT_POSITION_X",
    (3, 54): "ABS_MT_POSITION_Y",
    (3, 57): "ABS_MT_TRACKING_ID",
    (1, 330): "BTN_TOUCH",
}


def main():
    global PID
    HB_INT = float(os.environ.get("MON_HB", "15"))
    offset = os.path.getsize(LOG) if os.path.exists(LOG) else 0
    start = time.time()
    last_hb = start

    # 触摸聚合状态：只在按下/抬起边沿输出，位置取最近一次 MT 坐标
    t = {"down": False, "x": -1, "y": -1, "slot": 0, "tid": {}}

    print("[mon] started %s log_off=%d ev=%s pid=%s" % (ts(), offset, EV, PID or "auto"), flush=True)

    # 若未指定 PID，自动探测 hmi_runtime
    if not PID:
        for p in os.listdir("/proc"):
            if p.isdigit():
                try:
                    with open("/proc/%s/cmdline" % p, "rb") as cf:
                        cmd = cf.read()
                    if b"hmi_runtime" in cmd:
                        PID = int(p)
                        break
                except OSError:
                    pass
        print("[mon] auto pid=%s" % PID, flush=True)

    try:
        f = open(EV, "rb")
        f_fd = f.fileno()
        os.set_blocking(f_fd, False)  # 非阻塞：避免内层读卡死心跳/日志检查
    except OSError as e:
        print("[mon] cannot open %s: %s (touch only)" % (EV, e), flush=True)
        f = None

    try:
        while True:
            r, _, _ = select.select([f], [], [], 0.5) if f else ([], [], [], 0.5)
            now = time.time()

            if r and f:
                # 非阻塞排空事件队列（不阻塞在主循环外，心跳/日志检查不被饿死）
                while True:
                    try:
                        data = f.read(SZ)
                    except (BlockingIOError, OSError):
                        break
                    if not data or len(data) != SZ:
                        break
                    sec, usec, etype, code, value = struct.unpack(FMT, data)
                    key = (etype, code)
                    name = CODES.get(key)
                    if name:
                        if name == "ABS_MT_SLOT":
                            t["slot"] = value
                        elif name == "ABS_MT_TRACKING_ID":
                            t["tid"][t["slot"]] = value
                        elif name == "ABS_MT_POSITION_X":
                            t["x"] = value
                        elif name == "ABS_MT_POSITION_Y":
                            t["y"] = value
                        elif name == "BTN_TOUCH":
                            if value == 1 and not t["down"]:
                                print("%s [touch] DOWN x=%d y=%d slot=%d tid=%s" %
                                      (ts(), t["x"], t["y"], t["slot"],
                                       t["tid"].get(t["slot"], "?")), flush=True)
                                t["down"] = True
                            elif value == 0 and t["down"]:
                                print("%s [touch] UP   x=%d y=%d" % (ts(), t["x"], t["y"]), flush=True)
                                t["down"] = False

            # 日志增量
            try:
                nsz = os.path.getsize(LOG)
            except OSError:
                nsz = 0
            if nsz < offset:
                offset = 0  # 日志被截断/轮转
            if nsz > offset:
                with open(LOG, "rb") as lf:
                    lf.seek(offset)
                    chunk = lf.read(nsz - offset)
                for line in chunk.decode("utf-8", "replace").splitlines():
                    print("%s [log] %s" % (ts(), line), flush=True)
                offset = nsz

            # 心跳
            if now - last_hb >= HB_INT:
                alive = os.path.exists("/proc/%d" % PID) if PID else False
                print("%s [hb] pid=%s alive=%s elapsed=%ds" % (ts(), PID, alive, int(now - start)), flush=True)
                last_hb = now
    except KeyboardInterrupt:
        print("[mon] stopped", flush=True)


if __name__ == "__main__":
    main()
