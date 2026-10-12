#!/usr/bin/env python3
"""Create a reproducible manifest for the LCEDA-Pro handoff source package."""
from __future__ import annotations
import hashlib
import json
from pathlib import Path

BASE = Path(__file__).resolve().parent
OUT = BASE / "RELEASE-MANIFEST.sha256"
EXCLUDE = {"RELEASE-MANIFEST.sha256"}


def main() -> int:
    files = []
    for path in sorted(BASE.rglob("*")):
        if not path.is_file() or any(part == "__pycache__" for part in path.parts):
            continue
        if path.name in EXCLUDE:
            continue
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        files.append((path.relative_to(BASE).as_posix(), digest))
    OUT.write_text("\n".join(f"{digest}  {name}" for name, digest in files) + "\n", encoding="utf-8")
    print(f"wrote {OUT} ({len(files)} files)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
