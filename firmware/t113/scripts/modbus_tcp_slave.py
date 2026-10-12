#!/usr/bin/env python3
"""
zhishu-HMI T113 F5 测试用 Modbus TCP 从站（pymodbus 3.14 API）

用途：在主机上起一个 Modbus TCP 从站，让 T113 板子的 hmi_runtime 连过来
      验证 F5 Modbus TCP 通信用例。

依赖：pymodbus 3.14+ （pipx install pymodbus）

启动：
    ~/.local/share/pipx/venvs/pymodbus/bin/python3 scripts/modbus_tcp_slave.py

监听 0.0.0.0:1502（由 iptables 转发 502→1502），unit_id=1。
板子默认连 192.168.100.10:502，对应 hmi_runtime 的 modbus_tcp 通道。

寄存器映射（对齐 firmware/t113/generated/tags.c 的 TAG_TABLE）：
  addr=0 (40001) = PV   (过程值，每秒+1 模拟传感器)
  addr=1 (40002) = SV   (设定值，可通过 06 写入)
  addr=2 (40003) = 状态字 (bit0=运行, bit1=报警)
  addr=3 (40004) = 产量计数

注意：
  1. pymodbus 3.14 要求 address >= 1，但 SparseDataBlock 支持 address=0
     （hmi_runtime 用 0-based 协议地址，"40001" → off=0，
      见 firmware/t113/app/comm/comm_core.c:58）
  2. 监听 1502 避免需要 root 权限，主机用 iptables 转发 502→1502：
     sudo iptables -t nat -A PREROUTING -p tcp --dport 502 -j REDIRECT --to-ports 1502
     sudo iptables -t nat -A OUTPUT -p tcp -o lo --dport 502 -j REDIRECT --to-ports 1502
"""
import logging
import time
import threading
from pymodbus.server import StartTcpServer
from pymodbus.datastore import (
    ModbusServerContext,
    ModbusDeviceContext,
    ModbusSparseDataBlock,
)
from pymodbus import ModbusDeviceIdentification

logging.basicConfig(level=logging.INFO, format='%(asctime)s [%(levelname)s] %(message)s')
log = logging.getLogger("hmi-slave")

# 保持寄存器（HR）：用 SparseDataBlock 精确控制每个地址
# 板子 hmi_runtime 用 0-based 协议地址（"40001" → off=0）
# 所以从站必须包含 address=0
block_hr = ModbusSparseDataBlock({
    0: 0,         # 40001 (PV)  - 板子请求 off=0
    1: 100,       # 40002 (SV)  - 板子请求 off=1
    2: 0x0001,    # 40003 (状态)
    3: 0,         # 40004 (产量)
})
# 线圈（CO）
block_co = ModbusSparseDataBlock({i: False for i in range(0, 9)})

# 设备上下文（unit_id=1）
device = ModbusDeviceContext(
    di=ModbusSparseDataBlock({i: False for i in range(0, 9)}),
    co=block_co,
    hr=block_hr,
    ir=ModbusSparseDataBlock({i: 0 for i in range(0, 9)}),
)

# 服务器上下文
context = ModbusServerContext(devices={1: device})

# 设备标识
identity = ModbusDeviceIdentification()
identity.VendorName = 'zhishu-HMI Test Slave'
identity.ProductName = 'pymodbus F5 Simulator'
identity.MajorMinorRevision = '1.0'


def pv_updater():
    """每秒更新 PV 寄存器（40001, address=0）模拟过程值变化"""
    pv = 0
    while True:
        pv = (pv + 1) % 1000
        # 通过 simdata 修改 PV 值（simdata[0] 对应 address=0）
        block_hr.simdata[0].values = pv
        sv = block_hr.simdata[1].values
        log.info(f"PV 更新: HR[40001]={pv}, SV=HR[40002]={sv}")
        time.sleep(1)


def main():
    log.info("=" * 60)
    log.info("zhishu-HMI Modbus TCP 测试从站启动")
    log.info("监听: 0.0.0.0:1502 (iptables 转发 502→1502)")
    log.info("板子将连接: 192.168.100.10:502 → 转发到本机 1502")
    log.info("寄存器初始值:")
    log.info("  40001 (PV)   = 0       (每秒自动 +1)")
    log.info("  40002 (SV)   = 100     (可写)")
    log.info("  40003 (状态) = 0x0001  (bit0=运行)")
    log.info("  40004 (产量) = 0")
    log.info("=" * 60)

    # 启动 PV 更新线程
    t = threading.Thread(target=pv_updater, daemon=True)
    t.start()

    # 启动 Modbus TCP 服务器（1502，避免 root）
    StartTcpServer(context=context, identity=identity,
                   address=("0.0.0.0", 1502))


if __name__ == "__main__":
    main()
