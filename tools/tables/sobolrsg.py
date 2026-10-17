# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Generates the tables of QuantLib's Sobol generator, from ql/math/randomnumbers/sobolrsg.cpp at the pin
(docs/porting-playbook.md §1, generated tables): the alternative primitive polynomials and the nine sets of
coefficients of the free direction integers.

Usage: .venv\\Scripts\\python -I tools/tables/sobolrsg.py <QuantLib v1.44 worktree> [<output root>]

It writes one file per pointer table of the .cpp's anonymous namespace under src/NanoQL/Numerics/RandomNumbers/SobolRsg/
of the output root (by default the repository), named after QuantLib's table with its first letter capitalised:
AltPrimitivePolynomials.bin, SLinitializers.bin, Initializers.bin (Jäckel's), Linitializers.bin, Kuoinitializers.bin,
Kuo2initializers.bin, Kuo3initializers.bin, JoeKuoD5initializers.bin, JoeKuoD6initializers.bin and
JoeKuoD7initializers.bin. Each holds the arrays its table points to, in the table's order, each as QuantLib lays it
out: the alternative polynomials of each degree followed by -1, as little-endian 32-bit signed integers; a
dimension's coefficients followed by 0, as little-endian 32-bit unsigned integers. A table that points to an array
twice (Jäckel's starts with Sobol and Levitan's first seven) holds it twice. The loader,
src/NanoQL/Numerics/RandomNumbers/SobolRsg/SobolRsgTables.cs, reads them back. tools/check-upstream.py (G4 (6))
regenerates them into a temporary folder and compares the bytes with the committed files.
"""
import os
import re
import struct
import sys

SOURCE = "ql/math/randomnumbers/sobolrsg.cpp"
FOLDER = "src/NanoQL/Numerics/RandomNumbers/SobolRsg"
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
# QuantLib's pointer tables, which SobolRsg's constructor reads; a pin that changes them stops the script.
POLYNOMIALS = "AltPrimitivePolynomials"
INITIALIZERS = ["SLinitializers", "initializers", "Linitializers", "Kuoinitializers", "Kuo2initializers",
                "Kuo3initializers", "JoeKuoD6initializers", "JoeKuoD7initializers", "JoeKuoD5initializers"]


def read(ql_root, rel):
    # The comments hold a byte that isn't UTF-8 ("J�ckel"); the tables are ASCII.
    with open(os.path.join(ql_root, rel), encoding="utf-8", errors="replace") as f:
        return f.read()


def strip_comments(text):
    return re.sub(r"//[^\n]*", "", re.sub(r"/\*.*?\*/", " ", text, flags=re.S))


def numbers(name, body):
    out = []
    for token in body.split(","):
        token = token.strip()
        m = re.fullmatch(r"(-?\d+)(?:UL|U|L)?", token)
        if not m:
            raise SystemExit(f"{SOURCE}: {name} holds '{token}', which is not an integer literal")
        out.append(int(m.group(1)))
    return out


def file_name(table):
    return table[0].upper() + table[1:] + ".bin"


def tables(ql_root):
    """{each committed file's path under the repository: its bytes}."""
    text = read(ql_root, SOURCE)
    end = text.find("SobolRsg::SobolRsg(")
    if end < 0:
        raise SystemExit(f"{SOURCE}: no SobolRsg constructor")
    text = strip_comments(text[:end])
    macros = {k: int(v) for k, v in re.findall(r"#define (\w+) (\d+)", text)}
    arrays = {name: (kind, numbers(name, body)) for kind, name, body in
              re.findall(r"const\s+(long|std::uint32_t)\s+(\w+)\s*\[\s*\]\s*=\s*\{([^}]*)\}\s*;", text)}
    pointers = {}
    for kind, name, size, body in re.findall(
            r"const\s+(long|std::uint32_t)\s*\*\s*const\s+(\w+)\s*\[\s*(\w+)\s*\]\s*=\s*\{([^}]*)\}\s*;", text):
        entries = [e.strip() for e in body.split(",") if e.strip()]
        declared = int(size) if size.isdigit() else macros.get(size)
        if declared != len(entries):
            raise SystemExit(f"{SOURCE}: {name}[{size}] lists {len(entries)} arrays")
        for e in entries:
            if e not in arrays or arrays[e][0] != kind:
                raise SystemExit(f"{SOURCE}: {name} points to {e}, which is no {kind} array of the file")
        pointers[name] = (kind, entries)
    if sorted(pointers) != sorted([POLYNOMIALS] + INITIALIZERS):
        raise SystemExit(f"{SOURCE}: the pointer tables are {sorted(pointers)}")
    out = {}
    for name, (kind, entries) in pointers.items():
        data = bytearray()
        for e in entries:
            values = arrays[e][1]
            if name == POLYNOMIALS:
                if values[-1] != -1 or -1 in values[:-1] or min(values[:-1], default=0) < 0:
                    raise SystemExit(f"{SOURCE}: {e} isn't non-negative polynomials ended by one -1")
                data += struct.pack(f"<{len(values)}i", *values)
            else:
                if values[-1] != 0 or 0 in values[:-1] or min(values) < 0 or max(values) >= 2**32:
                    raise SystemExit(f"{SOURCE}: {e} isn't positive 32-bit coefficients ended by one 0")
                data += struct.pack(f"<{len(values)}I", *values)
        out[f"{FOLDER}/{file_name(name)}"] = bytes(data)
    return out


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
