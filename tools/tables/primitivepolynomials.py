# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Generates the primitive polynomials modulo two that QuantLib's Sobol generator reads, from
ql/math/randomnumbers/primitivepolynomials.cpp at the pin (docs/porting-playbook.md §1, generated tables).

Usage: .venv\\Scripts\\python -I tools/tables/primitivepolynomials.py <QuantLib v1.44 worktree> [<output root>]

It writes src/NanoQL/Numerics/RandomNumbers/PrimitivePolynomials.bin under the output root (by default the
repository): QuantLib's array PrimitivePolynomials, degree by degree from 1 to N_MAX_DEGREE, each degree's
polynomials followed by its -1, as little-endian 32-bit signed integers. QuantLib compiles the degrees PPMT_MAX_DIM
asks for; at its default, N_PRIMITIVES_UP_TO_DEGREE_18, that is all 18 arrays of the file, 21,200 polynomials. The
loader, src/NanoQL/Numerics/RandomNumbers/PrimitivePolynomials.cs, reads it back. tools/check-upstream.py (G4 (6))
regenerates it into a temporary folder and compares the bytes with the committed file.
"""
import os
import re
import struct
import sys

SOURCE = "ql/math/randomnumbers/primitivepolynomials.cpp"
HEADER = "ql/math/randomnumbers/primitivepolynomials.hpp"
OUTPUT = "src/NanoQL/Numerics/RandomNumbers/PrimitivePolynomials.bin"
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def read(ql_root, rel):
    # The comments hold "Jäckel" and "©"; the numbers are ASCII.
    with open(os.path.join(ql_root, rel), encoding="utf-8", errors="replace") as f:
        return f.read()


def strip_comments(text):
    return re.sub(r"//[^\n]*", "", re.sub(r"/\*.*?\*/", " ", text, flags=re.S))


def numbers(body):
    out = []
    for token in body.split(","):
        token = token.strip()
        m = re.fullmatch(r"(-?\d+)L?", token)
        if not m:
            raise SystemExit(f"{SOURCE}: '{token}' is not an integer literal")
        out.append(int(m.group(1)))
    return out


def tables(ql_root):
    """{the committed file's path under the repository: its bytes}."""
    header = read(ql_root, HEADER)
    counts = {int(d): int(n) for d, n in re.findall(r"#define N_PRIMITIVES_UP_TO_DEGREE_(\d\d)\s+(\d+)", header)}
    # PPMT_MAX_DIM's default, which QuantLib's build keeps: it compiles the degrees up to 18.
    if not re.search(r"#ifndef\s+PPMT_MAX_DIM\s*\n#\s*define PPMT_MAX_DIM N_PRIMITIVES_UP_TO_DEGREE_18\b", header):
        raise SystemExit(f"{HEADER}: PPMT_MAX_DIM's default isn't N_PRIMITIVES_UP_TO_DEGREE_18")
    max_dim, max_degree = counts[18], 18
    text = strip_comments(read(ql_root, SOURCE))
    arrays = {name: numbers(body) for name, body in
              re.findall(r"static const long (\w+)\[\]\s*=\s*\{([^}]*)\}\s*;", text)}
    m = re.search(r"const long \*const PrimitivePolynomials\[N_MAX_DEGREE\]\s*=\s*\{(.*?)\}\s*;", text, re.S)
    if not m:
        raise SystemExit(f"{SOURCE}: no PrimitivePolynomials[N_MAX_DEGREE] array")
    # Each degree after the first sits under "#if PPMT_MAX_DIM > N_PRIMITIVES_UP_TO_DEGREE_<degree - 1>".
    names = re.findall(r"PrimitivePolynomialDegree(\d\d)", m.group(1))
    names = [int(d) for d in names if int(d) == 1 or max_dim > counts[int(d) - 1]]
    if names != list(range(1, max_degree + 1)):
        raise SystemExit(f"{SOURCE}: PrimitivePolynomials lists the degrees {names}, not 1 to {max_degree}")
    data = bytearray()
    total = 0
    for degree in names:
        values = arrays[f"PrimitivePolynomialDegree{degree:02d}"]
        if values[-1] != -1 or -1 in values[:-1] or min(values[:-1], default=0) < 0:
            raise SystemExit(f"{SOURCE}: degree {degree}'s polynomials aren't non-negative and ended by one -1")
        total += len(values) - 1
        data += struct.pack(f"<{len(values)}i", *values)
    if total != max_dim:
        raise SystemExit(f"{SOURCE}: {total} polynomials, not PPMT_MAX_DIM's {max_dim}")
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
