# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmMesherIntegral (ql/methods/finitedifferences/utilities/fdmmesherintegral.hpp, .cpp),
which the wheel doesn't bind, each output computed by the C++ harness (docs/porting-playbook.md §9.4): the integral of
a function's values over a mesher by Simpson's and the trapezoid rule, one direction at a time.

Uneven 1-D meshers of 7 and 8 points (Simpson's odd and even counts), a 2-D mesher with a direction of two points, a
2-D mesher of 9 x 6 points, and QuantLib's fdmlinearop test's 3-D mesher (21 x 11 x 5, its concentrating meshers'
locations); on a quadratic polynomial (QuantLib's test's on its mesher) and on seeded values. For each, the integral
by each rule.

The harness's call is tools/cxx/src/methods/finitedifferences/utilities/fdmmesherintegral.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/FiniteDifferences/Utilities/FdmMesherIntegralCxxVectorTests.cs."""
import itertools
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
RULES = ["simpson", "trapezoid"]


def concentrating(start, end, size, point, density):
    return list(ql.Concentrating1dMesher(start, end, size, (point, density)).locations())


def polynomial(p):
    """QuantLib's fdmlinearop test's function, the missing coordinates 0."""
    x, y, z = (list(p) + [0.0, 0.0])[:3]
    return x * x + 3 * y * y - 3 * z * z + 2 * x * y - x * z - 3 * y * z + 4 * x - y - 3 * z + 2


def values(locations, f):
    # the layout's order: the first direction fastest
    return [f(tuple(reversed(p))) for p in itertools.product(*reversed(locations))]


def cases():
    rng = random.Random(20261025)
    meshers = [
        ("1-D, 7 points", [[-1.0, -0.6, -0.1, 0.3, 0.4, 1.0, 1.6]]),
        ("1-D, 8 points", [[-1.0, -0.6, -0.1, 0.3, 0.4, 1.0, 1.3, 1.6]]),
        ("2-D, a direction of two points", [[0.0, 0.5, 1.2, 2.0, 3.0], [-1.0, 1.0]]),
        ("2-D, 9 x 6", [[0.1 * i * i for i in range(9)], [-2.0, -1.2, -0.3, 0.4, 1.5, 3.0]]),
        ("3-D, QuantLib's test's mesher", [concentrating(-1, 1.6, 21, 0, 0.1), concentrating(-3, 4, 11, 1, 0.01),
                                           concentrating(-2, 1, 5, 0.5, 0.1)]),
    ]
    with cxx.connect() as H:
        st = H.settings(R)
        out = []
        for name, locations in meshers:
            for fname, f in (("the polynomial", values(locations, polynomial)),
                             ("seeded values", [rng.uniform(-1.0, 1.0) for _ in values(locations, polynomial)])):
                integrals = [H.call("FdmMesherIntegral::integrate", locations, f, rule) for rule in RULES]
                out.append({"name": f"{name}, {fname}",
                            "inputs": {"settings": st, "locations": locations, "f": f, "rules": RULES},
                            "expected": {"integral": integrals}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/fdmmesherintegral.hpp", cases)
