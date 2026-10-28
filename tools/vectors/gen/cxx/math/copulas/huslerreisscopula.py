# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the Hüsler-Reiss copula (ql/math/copulas/huslerreisscopula.hpp, .cpp), each output computed by the C++
harness, since the wheel doesn't bind it.

At theta of 0.5, 1.0, 3.0, on the points 0.05 to 0.95 in each argument; the unit square's edges and corners are left
out, where nanoql returns the boundary values before the formula (the S-core plan's CR7 (xxxiii);
tests/NanoQL.Tests/Divergences/CopulaEdgeTests.cs). Each case's "values" are the rows of C(x, y), one per x. The harness
unit is tools/cxx/src/math/copulas/copulas.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Copulas/CopulaCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

GRID = [0.05, 0.2, 0.35, 0.5, 0.65, 0.8, 0.95]
PARAMETERS = [(0.5,), (1.0,), (3.0,)]
NAMES = ['theta']


def points(p):
    """The case's points: below theta 0 for Clayton, only those whose base is positive."""
    if False and p[0] < 0:
        return [x for x in GRID if x >= 0.35]
    return GRID


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []
        for p in PARAMETERS:
            xs = ys = points(p)
            label = ", ".join(f"{n} {x}" for n, x in zip(NAMES, p)) or "the copula"
            out.append({"name": label,
                        "inputs": {"settings": st, **dict(zip(NAMES, p)), "xs": xs, "ys": ys},
                        "expected": {"values": v.enc(H.call("HuslerReissCopula", *p, xs, ys))}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/copulas/huslerreisscopula.hpp", cases)
