# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Generates the lattice rules' generating vectors that QuantLib's LatticeRule returns, from
ql/math/randomnumbers/latticerules.cpp at the pin (docs/porting-playbook.md §1, generated tables).

Usage: .venv\\Scripts\\python -I tools/tables/latticerules.py <QuantLib v1.44 worktree> [<output root>]

It writes src/NanoQL/Numerics/RandomNumbers/LatticeRules/LatticeRules.bin under the output root (by default the
repository): QuantLib's arrays latticeA, latticeB, latticeC and latticeD, in that order, each its 3,600 entries, the
rules of Cools, Kuo and Nuyens (SIAM J. Sci. Comp., 2006) for up to 2^20 points, as little-endian 32-bit signed
integers. QuantLib's arrays are of Real, every entry an integer literal below 2^20, so each converts exactly. The
loader, src/NanoQL/Numerics/RandomNumbers/LatticeRules/LatticeRule.cs, reads it back. tools/check-upstream.py
regenerates it into a temporary folder and compares the bytes with the committed file.
"""
import os
import re
import struct
import sys

SOURCE = "ql/math/randomnumbers/latticerules.cpp"
OUTPUT = "src/NanoQL/Numerics/RandomNumbers/LatticeRules/LatticeRules.bin"
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
NAMES = ("latticeA", "latticeB", "latticeC", "latticeD")
# QuantLib's ruleLength: the entries getRule copies from each array
RULE_LENGTH = 3600


def tables(ql_root):
    """{the committed file's path under the repository: its bytes}."""
    with open(os.path.join(ql_root, SOURCE), encoding="utf-8") as f:
        text = re.sub(r"//[^\n]*", "", re.sub(r"/\*.*?\*/", " ", f.read(), flags=re.S))
    arrays = {name: body for name, body in re.findall(r"const Real (\w+)\[\]\s*=\s*\{([^}]*)\}\s*;", text)}
    if sorted(arrays) != sorted(NAMES):
        raise SystemExit(f"{SOURCE}: the arrays are {sorted(arrays)}, not {list(NAMES)}")
    if not re.search(r"Size ruleLength = 3600;", text):
        raise SystemExit(f"{SOURCE}: getRule's ruleLength isn't 3600")
    data = bytearray()
    for name in NAMES:
        values = []
        for token in arrays[name].split(","):
            token = token.strip()
            if not re.fullmatch(r"\d+", token):
                raise SystemExit(f"{SOURCE}: {name}'s '{token}' is not an integer literal")
            values.append(int(token))
        if len(values) != RULE_LENGTH:
            raise SystemExit(f"{SOURCE}: {name} holds {len(values)} entries, not {RULE_LENGTH}")
        if max(values) >= 1 << 20:
            raise SystemExit(f"{SOURCE}: {name} holds an entry of 2^20 or more")
        data += struct.pack(f"<{len(values)}i", *values)
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
