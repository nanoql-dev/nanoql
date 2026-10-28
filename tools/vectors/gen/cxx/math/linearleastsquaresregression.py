# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for LinearRegression and LinearLeastSquaresRegression (ql/math/linearleastsquaresregression.hpp), which the
wheel doesn't bind, each output computed by the C++ harness: regressions on one variable with intercepts of 1, 2.5 and 0
(none), on two and three variables, and on named basis functions, each on noisy samples from a fixed random sample, the
coefficients, residuals, errors, dimension and size, and the standard errors where the basis has two functions:
QuantLib's take n - 2 degrees of freedom whatever the basis, which nanoql replaces by n - p (the S-van plan's VR4 (vi);
tests/NanoQL.Tests/Divergences/LeastSquaresStandardErrorTests.cs). Then fewer samples than basis functions, which
QuantLib refuses. Read back by tests/NanoQL.Tests/Vectors/Cxx/Numerics/LinearLeastSquaresRegressionCxxVectorTests.cs."""
import math
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

FIELDS = ("coefficients", "residuals", "standardErrors", "error", "dim", "size")
BASES = [["1", "x"], ["1", "x", "x x"], ["sin(x)", "exp(-x)"], ["1", "x", "x x", "sin(x)", "exp(-x)"]]


def cases():
    rng = random.Random(v.SEED)
    out = []
    with cxx.connect() as H:
        def case(name, method, args, inputs):
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            r = v.attempt(lambda: H.call(method, *args))
            expected = ({k: r[k] for k in FIELDS if k != "standardErrors" or r["dim"] == 2} if "throws" not in r
                        else {"regression": r})
            out.append({"name": name, "inputs": dict({"settings": st, "method": method}, **inputs),
                        "expected": expected})

        for n in (5, 40):
            x = [rng.uniform(-2.0, 3.0) for _ in range(n)]
            y = [1.5 - 0.7 * t + rng.gauss(0.0, 0.2) for t in x]
            for intercept in (1.0, 2.5, 0.0):
                case(f"one variable, {n} samples, intercept {intercept}", "LinearRegression", [x, y, intercept],
                     {"x": v.enc(x), "y": v.enc(y), "intercept": intercept})
        for m, n in ((2, 12), (3, 30)):
            xs = [[rng.uniform(-1.0, 1.0) for _ in range(m)] for _ in range(n)]
            y = [0.3 + sum((i + 1) * xi for i, xi in enumerate(row)) + rng.gauss(0.0, 0.1) for row in xs]
            for intercept in (1.0, 0.0):
                case(f"{m} variables, {n} samples, intercept {intercept}", "LinearRegressionMulti", [xs, y, intercept],
                     {"x": v.enc(xs), "y": v.enc(y), "intercept": intercept})
        x = [rng.uniform(0.0, 4.0) for _ in range(25)]
        y = [math.sin(t) + 0.5 * math.exp(-t) + rng.gauss(0.0, 0.05) for t in x]
        for basis in BASES:
            case(f"basis {basis}", "LinearLeastSquaresRegression", [x, y, basis],
                 {"x": v.enc(x), "y": v.enc(y), "basis": basis})
        case("two samples, three basis functions (refused)", "LinearLeastSquaresRegression",
             [x[:2], y[:2], BASES[1]], {"x": v.enc(x[:2]), "y": v.enc(y[:2]), "basis": BASES[1]})
        case("one sample, intercept 1 (refused)", "LinearRegression", [x[:1], y[:1], 1.0],
             {"x": v.enc(x[:1]), "y": v.enc(y[:1]), "intercept": 1.0})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/linearleastsquaresregression.hpp", cases)
