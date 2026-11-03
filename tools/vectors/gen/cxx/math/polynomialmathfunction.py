# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for PolynomialFunction (ql/math/polynomialmathfunction.hpp, .cpp) and PascalTriangle
(ql/math/pascaltriangle.hpp, .cpp), which the wheel doesn't bind, each output computed by the C++ harness: polynomials
of 1 to 10 coefficients, fixed and from a fixed random sample, their values, derivatives and primitives at points across
both signs, their definite integrals and the definite integral's and derivative's coefficients on windows forward and
backward, and an empty window, whose definite derivative's coefficients QuantLib refuses (its matrix singular); and
Pascal's rows 0 to 34. From row 35 MSVC's 32-bit BigNatural wraps, where nanoql's rows are exact
(tests/NanoQL.Tests/Divergences/PascalTriangleTests.cs), so those rows are left out. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/PolynomialFunctionCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TS = [-2.0, -0.5, 0.0, 0.3, 1.0, 2.5]
WINDOWS = [[0.0, 1.0], [-1.0, 2.0], [2.0, -1.0], [-1.5, -0.25]]
FIELDS = ("order", "value", "derivative", "primitive", "definiteIntegral", "coefficients", "derivativeCoefficients",
          "primitiveCoefficients", "definiteIntegralCoefficients", "definiteDerivativeCoefficients")


def polynomials():
    rng = random.Random(v.SEED)
    return [("constant", [3.0]), ("linear", [1.0, -2.0]), ("cubic", [0.5, -1.25, 2.0, 0.75]),
            ("random, 6 coefficients", [rng.uniform(-2.0, 2.0) for _ in range(6)]),
            ("random, 10 coefficients", [rng.uniform(-1.0, 1.0) for _ in range(10)])]


def cases():
    out = []
    with cxx.connect() as H:
        for name, c in polynomials():
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            r = H.call("PolynomialFunction", c, TS, WINDOWS)
            out.append({"name": name, "inputs": {"settings": st, "coefficients": v.enc(c), "t": TS, "windows": WINDOWS},
                        "expected": {k: v.enc(r[k]) for k in FIELDS}})
        c = polynomials()[2][1]
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out.append({"name": "an empty window (refused)",
                    "inputs": {"settings": st, "coefficients": c, "t": [], "windows": [[0.5, 0.5]]},
                    "expected": {"window": v.attempt(lambda: H.call("PolynomialFunction", c, [], [[0.5, 0.5]]))}})
        for order in range(35):
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            out.append({"name": f"Pascal's row {order}", "inputs": {"settings": st, "order": order},
                        "expected": {"row": H.call("PascalTriangle::get", order)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/polynomialmathfunction.hpp", cases)
