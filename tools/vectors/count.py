# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Counts each vector file's cases, outputs, overridden outputs and bytes: the vector gate's denominators.

Usage: python -I tools/vectors/count.py [<vector path> ...]

Paths are relative to tests/vectors, without ".json" (for example time/date); with none, every file is counted.
An output is one scalar in a case's "expected": an array counts its elements, and a {value, scale} object or a
{"throws": true} counts once (docs/porting-playbook.md §7.2). It needs no QuantLib.
"""
import json
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
VECTORS = os.path.join(REPO, "tests", "vectors")


def outputs(expected):
    if isinstance(expected, dict):
        if "throws" in expected or "value" in expected:
            return 1, 1 if "tolerance" in expected else 0
        raise ValueError(f"an expected value is a scalar, an array, {{value, scale}} or {{throws}}: {expected}")
    if isinstance(expected, list):
        m = o = 0
        for x in expected:
            dm, do = outputs(x)
            m, o = m + dm, o + do
        return m, o
    return 1, 0


def count(path):
    with open(path, "rb") as f:
        data = f.read()
    doc = json.loads(data.decode("utf-8"))
    m = o = 0
    for case in doc["cases"]:
        for value in case["expected"].values():
            dm, do = outputs(value)
            m, o = m + dm, o + do
    return len(doc["cases"]), m, o, len(data)


def main(args):
    if args:
        rels = [a.replace("\\", "/").removesuffix(".json") for a in args]
    else:
        rels = sorted(os.path.relpath(os.path.join(dp, fn), VECTORS).replace(os.sep, "/")[:-5]
                      for dp, _, fns in os.walk(VECTORS) for fn in fns if fn.endswith(".json"))
    rows = [(rel, *count(os.path.join(VECTORS, *rel.split("/")) + ".json")) for rel in rels]
    width = max([len(r[0]) for r in rows] + [5])
    print(f"{'file':<{width}} {'cases':>8} {'outputs M':>10} {'overridden O':>13} {'bytes':>10}")
    for rel, cases, m, o, size in rows:
        print(f"{rel:<{width}} {cases:>8,} {m:>10,} {o:>13,} {size:>10,}")
    total = [sum(r[i] for r in rows) for i in range(1, 5)]
    print(f"{'total':<{width}} {total[0]:>8,} {total[1]:>10,} {total[2]:>13,} {total[3]:>10,}  ({len(rows)} files)")


if __name__ == "__main__":
    main(sys.argv[1:])
