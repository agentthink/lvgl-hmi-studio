#!/usr/bin/env python3
"""
GT911 Touch Status Reader - Upload & Test Script (v6)
Uses base64 -d stdin pipe + Ctrl+D with trailing newline. No shell quotes needed.
"""
import serial, time, sys, os, base64

SERIAL_PORT = "/dev/ttyUSB0"
BAUD_RATE = 115200
BINARY_FILE = "gt911_test_dyn"   # 4KB dynamic-linked
REMOTE_PATH = "/tmp/gt911_test"

def send_cmd(s, cmd, wait=0.5):
    s.write(cmd.encode() + b"\n")
    time.sleep(wait)
    return s.read(s.in_waiting or 1).decode('ascii', errors='ignore')

def main():
    print(f"Connecting to {SERIAL_PORT} @ {BAUD_RATE}...")
    try:
        s = serial.Serial(SERIAL_PORT, BAUD_RATE, timeout=0.5)
    except PermissionError:
        print("ERROR: Permission denied. Run: sudo python3 upload_and_test.py")
        sys.exit(1)
    print("Connected!")
    s.read_all()
    s.write(b"\n")
    time.sleep(0.5)
    s.read_all()

    # Read binary
    if not os.path.exists(BINARY_FILE):
        print(f"ERROR: {BINARY_FILE} not found!")
        sys.exit(1)
    with open(BINARY_FILE, 'rb') as f:
        binary_data = f.read()
    b64_data = base64.b64encode(binary_data).decode()
    print(f"Binary: {len(binary_data)}B, base64: {len(b64_data)}B")

    # 1. Cleanup
    print("\n[1/4] Cleanup...")
    send_cmd(s, f"rm -f {REMOTE_PATH}", 0.5)

    # 2. Disable echo (reduce serial traffic during upload)
    print("[2/4] Uploading binary via base64 -d stdin pipe...")
    send_cmd(s, "stty -echo", 0.3)

    # 3. Start base64 -d, reading from stdin, writing to file
    s.write(f"base64 -d > {REMOTE_PATH}\n".encode())
    time.sleep(0.5)
    s.read_all()  # discard any prompt

    # 4. Send base64 data directly (no shell quotes, pure text)
    # Send in small chunks with delays to avoid buffer overflow
    chunk_size = 512
    total = len(b64_data)
    for i in range(0, total, chunk_size):
        chunk = b64_data[i:i+chunk_size]
        s.write(chunk.encode())
        time.sleep(0.05)  # 50ms between chunks
    print(f"  Sent {total}B of base64 data")

    # 5. Send newline + Ctrl+D to close stdin
    #    KEY FIX: newline ensures Ctrl+D is at line start = EOF
    time.sleep(0.5)
    s.write(b"\n")
    time.sleep(0.2)
    s.write(b"\x04")  # Ctrl+D = EOF at line start
    time.sleep(1.5)
    resp = s.read_all().decode('ascii', errors='ignore')
    print(f"  base64 -d completed")

    # 6. Re-enable echo
    send_cmd(s, "stty echo", 0.3)

    # 7. Verify
    print("\n[3/4] Verifying...")
    resp = send_cmd(s, f"chmod +x {REMOTE_PATH}; ls -la {REMOTE_PATH}; echo VERIFY_DONE", 1.0)
    print(f"  {resp.strip()}")

    if "VERIFY_DONE" not in resp or "No such file" in resp:
        print("  ERROR: File not created! Upload failed.")
        # Try to recover shell
        s.write(b"\x03")  # Ctrl+C
        time.sleep(0.5)
        s.read_all()
        s.close()
        sys.exit(1)

    # 8. Run test
    print("\n[4/4] Running GT911 test (0x814E register)...")
    print("=" * 60)
    print(">>> TOUCH THE SCREEN NOW! (tap 5-10 times) <<<")
    print("=" * 60)
    s.write(f"{REMOTE_PATH}\n".encode())
    try:
        start = time.time()
        while time.time() - start < 20:
            time.sleep(0.3)
            data = s.read(s.in_waiting or 1)
            if data:
                print(data.decode('ascii', errors='ignore'), end='')
                sys.stdout.flush()
    except KeyboardInterrupt:
        print("\n[Stopped]")
    s.write(b"\x03")
    time.sleep(0.5)
    s.read_all()
    send_cmd(s, f"rm -f {REMOTE_PATH}", 0.3)
    s.close()
    print("\n\nDone! Send output to assistant.")

if __name__ == "__main__":
    main()
