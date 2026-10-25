# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Generates the node table QuantLib's exponentially fitted Gauss-Laguerre Heston engine reads, from
ql/pricingengines/vanilla/exponentialfittinghestonengine.cpp at the pin (docs/porting-playbook.md §1, generated tables;
the S-hes plan's HD15).

Usage: .venv\\Scripts\\python -I tools/tables/exponentialfittinghestonengine.py <QuantLib v1.44 worktree> [<output root>]

It writes src/NanoQL/PricingEngines/Vanilla/ExponentialFittingHestonEngine/ExponentialFittingHestonEngineValues.bin
under the output root (by default the repository): QuantLib's array values4, 147 rows of 129 doubles (each row's
moneyness, its 64 nodes and its 64 weights), row by row, as little-endian IEEE 754 doubles, each literal parsed to the
double C++ rounds it to. The loader, ExponentialFittingHestonEngineValues.cs beside it, reads it back.
tools/check-upstream.py (G4 (6)) regenerates it into a temporary folder and compares the bytes with the committed file.
"""
import os
import re
import struct
import sys

SOURCE = "ql/pricingengines/vanilla/exponentialfittinghestonengine.cpp"
OUTPUT = ("src/NanoQL/PricingEngines/Vanilla/ExponentialFittingHestonEngine/"
          "ExponentialFittingHestonEngineValues.bin")
ROWS, COLUMNS = 147, 129
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def tables(ql_root):
    """{the committed file's path under the repository: its bytes}."""
    with open(os.path.join(ql_root, SOURCE), encoding="utf-8", errors="replace") as f:
        text = re.sub(r"//[^\n]*", "", re.sub(r"/\*.*?\*/", " ", f.read(), flags=re.S))
    m = re.search(r"const double values4\[\]\[(\d+)\]\s*=\s*\{(.*?)\};", text, re.S)
    if not m or int(m.group(1)) != COLUMNS:
        raise SystemExit(f"{SOURCE}: no values4[][{COLUMNS}] array")
    rows = re.findall(r"\{([^{}]*)\}", m.group(2))
    if len(rows) != ROWS:
        raise SystemExit(f"{SOURCE}: values4 has {len(rows)} rows, not {ROWS}")
    data = bytearray()
    for row in rows:
        tokens = [t.strip() for t in row.split(",") if t.strip()]
        if len(tokens) != COLUMNS:
            raise SystemExit(f"{SOURCE}: a row of {len(tokens)} values, not {COLUMNS}")
        for token in tokens:
            if not re.fullmatch(r"-?\d+(\.\d*)?([eE][-+]?\d+)?", token):
                raise SystemExit(f"{SOURCE}: '{token}' is not a double literal")
        data += struct.pack(f"<{COLUMNS}d", *(float(t) for t in tokens))
    return {OUTPUT: bytes(data)}


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
