# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for LatticeRule and LatticeRsg (ql/math/randomnumbers/latticerules.hpp, .cpp and latticersg.hpp, .cpp), which
the wheel doesn't bind, each output computed by the C++ harness: each rule's generating vector, its 3,600 entries
(nanoql's generated table against QuantLib's arrays), and the first 50 points of a fresh LatticeRsg over each rule at
1,024 and 2^20 points in 1, 5 and 30 dimensions, and 5 in 300. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/RandomNumbers/LatticeRsgCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

RULES = ["A", "B", "C", "D"]
SIZES = [1024, 2**20]
DIMENSIONS = [1, 5, 30, 300]
DRAWS = 50


def cases():
    out = []
    with cxx.connect() as H:
        for rule in RULES:
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            out.append({"name": f"rule {rule}", "inputs": {"settings": st, "rule": rule, "n": 1024},
                        "expected": {"getRule": v.enc(H.call("LatticeRule::getRule", rule, 1024))}})
            for n in SIZES:
                for dim in DIMENSIONS:
                    draws = DRAWS if dim < 300 else 5
                    st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
                    seqs = H.call("LatticeRsg::nextSequence", rule, n, dim, draws)
                    out.append({"name": f"rule {rule}, {n} points, {dim} dimensions",
                                "inputs": {"settings": st, "rule": rule, "n": n, "dimensionality": dim, "draws": draws},
                                "expected": {"nextSequence": [v.enc(s) for s in seqs]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/latticersg.hpp", cases)
