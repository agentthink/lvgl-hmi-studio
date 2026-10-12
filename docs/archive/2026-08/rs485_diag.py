import serial, time, sys

PORT='/dev/ttyUSB3'
def ser():
    return serial.Serial(PORT,9600,timeout=0.5,rtscts=False,dsrdtr=False)

# 1) 自环：发前 RTS=1, 发后 RTS=0, 立即读
print('[1] 自环测试 (请先短接 %s 的 A/B 端)' % PORT)
try:
    s=ser(); s.rts=True; time.sleep(0.02)
    s.write(b'\xDE\xAD\xBE\xEF'); s.flush(); time.sleep(0.05)
    s.rts=False; time.sleep(0.02)
    d=s.read(20)
    print('    发送 DEADBEEF, 收到:', d.hex() if d else 'NONE')
    s.close()
except Exception as e:
    print('    错误:', e)

# 2) 监听 5s (接收态)
print('[2] 监听 5s (接收态, 等待 WavePi 发)')
try:
    s=ser(); s.rts=False
    buf=s.read(200)
    s.close()
    print('    收到 %d 字节:' % len(buf), buf.hex() if buf else 'NONE')
except Exception as e:
    print('    错误:', e)
