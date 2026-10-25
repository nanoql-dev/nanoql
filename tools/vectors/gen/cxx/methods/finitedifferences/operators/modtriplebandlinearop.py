# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ModTripleBandLinearOp (ql/methods/finitedifferences/operators/modtriplebandlinearop.hpp),
which the wheel doesn't bind, each output computed by the C++ harness (docs/porting-playbook.md §9.4): a triple-band
operator whose bands are written through its accessors.

A first and a second derivative on an uneven 1-D mesher of 7 points, copied and then written at the ends and inside;
a second derivative in direction 1 of a 4 x 5 mesher, written at a line's ends; and the (direction, mesher)
constructor on a 6-point mesher with every band written. For each: the bands read back at every point, the operator
applied to a seeded vector, and solve_splitting at s = 0.3.

The harness's call is tools/cxx/src/methods/finitedifferences/operators/modtriplebandlinearop.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/FiniteDifferences/Operators/ModTripleBandLinearOpCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
UNEVEN = [0.0, 0.1, 0.25, 0.5, 0.8, 1.2, 2.0]
S = 0.3


def full_writes(n, rng):
    return [[band, i, rng.uniform(-2.0, 2.0)] for i in range(n) for band in ("lower", "diag", "upper")]


def cases():
    rng = random.Random(20261025)
    specs = [
        ("first derivative, written", [UNEVEN], 0, "first",
         [["lower", 0, 0.0], ["diag", 0, 1.5], ["upper", 0, -0.5], ["lower", 3, 2.25], ["upper", 6, 0.0],
          ["diag", 6, -1.0]]),
        ("second derivative, written", [UNEVEN], 0, "second",
         [["diag", 2, 7.0], ["upper", 2, -3.0], ["lower", 5, 0.125]]),
        ("second derivative in direction 1, written", [[0.0, 1.0, 2.0, 3.0], [0.0, 0.3, 0.5, 1.1, 2.0]], 1,
         "second", [["lower", 0, 0.0], ["diag", 0, 1.0], ["upper", 4, 0.0], ["diag", 19, 1.0]]),
        ("the direction constructor, every band written", [[0.0, 0.2, 0.5, 0.9, 1.4, 2.0]], 0, "none",
         full_writes(6, rng)),
    ]
    with cxx.connect() as H:
        st = H.settings(R)
        out = []
        for name, locations, direction, derivative, writes in specs:
            n = 1
            for x in locations:
                n *= len(x)
            r = [rng.uniform(-1.0, 1.0) for _ in range(n)]
            result = H.call("ModTripleBandLinearOp::outputs", locations, direction, derivative, writes, r, S)
            out.append({"name": name,
                        "inputs": {"settings": st, "locations": locations, "direction": direction,
                                   "derivative": derivative, "writes": writes, "r": r, "s": S},
                        "expected": {k: result[k] for k in ("lower", "diag", "upper", "apply", "solveSplitting")}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/modtriplebandlinearop.hpp", cases)
