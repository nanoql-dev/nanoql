# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Generates the absorption table QuantLib's no-arbitrage SABR model reads, from
ql/experimental/volatility/noarbsabrabsprobs.cpp at the pin (docs/porting-playbook.md §1, generated tables; the S-xeq
plan's XED4).

Usage: .venv\\Scripts\\python -I tools/tables/noarbsabrabsprobs.py <QuantLib v1.44 worktree> [<output root>]

It writes src/NanoQL/Experimental/Volatility/NoArbSabrAbsProbs.bin under the output root (by default the repository):
QuantLib's array sabrabsprob, its 1,209,600 absorption counts out of 2,500,000 simulated paths on Doust's grid of
expiries, initial volatilities, correlations, volatilities of volatility and betas, in QuantLib's order, as
little-endian 32-bit signed integers. QuantLib's array is of unsigned long, every entry an integer literal below 2^31,
so each converts exactly. The loader, src/NanoQL/Experimental/Volatility/NoArbSabrAbsProbs.cs, reads it back.
tools/check-upstream.py (G4 (6)) regenerates it into a temporary folder and compares the bytes with the committed file.
"""
import os
import re
import struct
import sys

SOURCE = "ql/experimental/volatility/noarbsabrabsprobs.cpp"
OUTPUT = "src/NanoQL/Experimental/Volatility/NoArbSabrAbsProbs.bin"
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
# QuantLib's declared size, noarbsabr.hpp's sabrabsprob[1209600]
SIZE = 1209600


def tables(ql_root):
    """{the committed file's path under the repository: its bytes}."""
    with open(os.path.join(ql_root, SOURCE), encoding="utf-8") as f:
        text = re.sub(r"//[^\n]*", "", re.sub(r"/\*.*?\*/", " ", f.read(), flags=re.S))
    arrays = re.findall(r"const unsigned long (\w+)\[\]\s*=\s*\{([^}]*)\}\s*;", text)
    if [name for name, _ in arrays] != ["sabrabsprob"]:
        raise SystemExit(f"{SOURCE}: the arrays are {[name for name, _ in arrays]}, not ['sabrabsprob']")
    values = []
    for token in arrays[0][1].split(","):
        token = token.strip()
        if not re.fullmatch(r"\d+", token):
            raise SystemExit(f"{SOURCE}: sabrabsprob's '{token}' is not an integer literal")
        values.append(int(token))
    if len(values) != SIZE:
        raise SystemExit(f"{SOURCE}: sabrabsprob holds {len(values)} entries, not {SIZE}")
    if max(values) >= 1 << 31:
        raise SystemExit(f"{SOURCE}: sabrabsprob holds an entry of 2^31 or more")
    return {OUTPUT: struct.pack(f"<{len(values)}i", *values)}


def write(ql_root, out_root):
    """Writes the tables under out_root and returns their paths, relative to it."""
    out = tables(ql_root)
    for rel, data in out.items():
        path = os.path.join(out_root, *rel.split("/"))
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "wb") as f:
            f.write(data)
    return sorted(out)


def main(argv):
    if len(argv) not in (1, 2):
        raise SystemExit(__doc__)
    for rel in write(argv[0], argv[1] if len(argv) == 2 else REPO):
        print(rel)


if __name__ == "__main__":
    main(sys.argv[1:])
