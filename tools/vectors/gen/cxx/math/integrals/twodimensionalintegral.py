# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for TwoDimensionalIntegral (ql/math/integrals/twodimensionalintegral.hpp), which the wheel doesn't bind, each
output computed by the C++ harness: three integrands over two rectangles and a reversed one, nested by the trapezoid,
Simpson and Gauss-Lobatto integrators at two accuracies, and by integrators whose maximum evaluations run out, which
QuantLib refuses. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Integrals/TwoDimensionalIntegralCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

INTEGRANDS = ["x y", "exp(-x) sin(x y)", "1/(1+x x+y y)"]
RECTANGLES = [([0.0, 0.0], [1.0, 1.0]), ([0.0, -1.0], [2.0, 3.0]), ([1.0, 1.0], [0.0, -0.5])]
PAIRS = [(["Trapezoid", 1e-6, 10000], ["Trapezoid", 1e-6, 10000]),
         (["Simpson", 1e-8, 10000], ["Simpson", 1e-8, 10000]),
         (["GaussLobatto", 1e-10, 10000], ["GaussLobatto", 1e-10, 10000]),
         (["Simpson", 1e-6, 1000], ["GaussLobatto", 1e-8, 1000]),
         (["GaussLobatto", 1e-12, 100000], ["Trapezoid", 1e-5, 10000])]
EXHAUSTED = [(["Trapezoid", 1e-12, 50], ["Trapezoid", 1e-6, 10000]),
             (["Simpson", 1e-6, 10000], ["GaussLobatto", 1e-14, 30])]


def cases():
    out = []
    with cxx.connect() as H:
        def case(name, outer, inner, f, a, b):
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            out.append({"name": name,
                        "inputs": {"settings": st, "outer": outer, "inner": inner, "integrand": f, "a": a, "b": b},
                        "expected": {"integral": v.attempt(lambda: H.call("TwoDimensionalIntegral", outer, inner, f,
                                                                          a, b))}})

        for outer, inner in PAIRS:
            for f in INTEGRANDS:
                for a, b in RECTANGLES:
                    case(f"{outer[0]} {outer[1]!r} by {inner[0]} {inner[1]!r}, {f} on {a} to {b}", outer, inner, f, a, b)
        for outer, inner in EXHAUSTED:
            case(f"{outer[0]} {outer[2]} by {inner[0]} {inner[2]} evaluations (refused)", outer, inner,
                 "exp(-x) sin(x y)", [0.0, 0.0], [2.0, 3.0])
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/integrals/twodimensionalintegral.hpp", cases)
