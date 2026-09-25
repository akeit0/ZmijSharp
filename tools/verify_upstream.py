#!/usr/bin/env python3
"""Compare the local decimal decomposition with a pinned upstream Żmij build."""

import argparse
import os
from pathlib import Path
import subprocess
import tempfile
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parent.parent
REVISION = "d1682cb47e67474319ed146d3ca2c0e1a70f9429"
SOURCE = f"https://raw.githubusercontent.com/vitaut/zmij/{REVISION}"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--count", type=int, default=1_000_000, help="random values per type")
    parser.add_argument("--compiler", default="g++", help="C++20 compiler")
    args = parser.parse_args()
    if args.count < 0:
        parser.error("--count must be nonnegative")

    with tempfile.TemporaryDirectory(prefix="zmij-reference-") as temporary:
        directory = Path(temporary)
        for name in ("zmij.h", "zmij.cc"):
            with urlopen(f"{SOURCE}/{name}") as response:
                (directory / name).write_bytes(response.read())

        executable = directory / ("zmij-reference.exe" if os.name == "nt" else "zmij-reference")
        subprocess.run(
            [args.compiler, "-std=c++20", "-O2", "-I", str(directory),
             str(ROOT / "tools/zmij_reference.cpp"), str(directory / "zmij.cc"), "-o", str(executable)],
            check=True,
        )
        print(f"upstream reference: vitaut/zmij {REVISION}", flush=True)
        subprocess.run(
            ["dotnet", "run", "-c", "Release", "--project", str(ROOT / "tests/ZmijSharp.Verify"),
             "--", "--producer-only", "--upstream", str(executable), str(args.count)],
            cwd=ROOT,
            check=True,
        )


if __name__ == "__main__":
    main()
