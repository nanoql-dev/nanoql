# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's least squares (ql/math/optimization/leastsquare.hpp, .cpp), which the wheel doesn't expose,
each output computed by the C++ harness on its problem, the fit of a·exp(b·x) to points: the cost function's value,
values and gradient at points, and the non-linear least-squares fit by the default conjugate gradient from initial
values at two accuracies and two iteration limits, its residual norm, last value and exit flag. The fit's results are
generated and left out: the conjugate gradient's returned point is the last one its line search accepted
(Divergences/LineSearchReturnTests.cs, the S-sr plan's SSR6 (xvii)), where QuantLib's is the point before; its cost,
the residual norm, is the same.

The harness unit is tools/cxx/src/math/optimization/leastsquare.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Optimization/LeastSquare/LeastSquareCxxVectorTests.cs."""
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

R = 46310
CURVES = [(2.0, -0.5, 0.0), (1.5, 0.3, 0.01), (5.0, -1.2, 0.05)]


def cases():
    rng = random.Random(v.SEED)
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for a, b, noise in CURVES:
            xs = [round(0.25 * k, 2) for k in range(12)]
            ys = [round(a * math.exp(b * x) * (1 + rng.gauss(0, noise)), 8) for x in xs]
            label = f"{a}·exp({b}x), noise {noise}"
            for point in ([a, b], [1.0, 0.0], [a * 1.3, b * 0.7]):
                f = H.call("LeastSquareFunction", xs, ys, point)
                out.append({"name": f"{label}, the cost at {point}",
                            "inputs": {"settings": st, "call": "function", "xs": xs, "ys": ys, "point": point},
                            "expected": {"value": f["value"], "values": f["values"], "gradient": f["gradient"]}})
            for initial in ([1.0, 0.0], [a * 1.3, b * 0.7]):
                for accuracy, maxiter in ((1e-4, 100), (1e-8, 1000)):
                    fit = H.call("NonLinearLeastSquare.perform", xs, ys, initial, accuracy, maxiter)
                    out.append({"name": f"{label}, fitted from {initial} at {accuracy}, {maxiter} iterations",
                                "inputs": {"settings": st, "call": "perform", "xs": xs, "ys": ys, "initial": initial,
                                           "accuracy": accuracy, "maxiter": maxiter},
                                "expected": {"residualNorm": fit["residualNorm"],
                                             "lastValue": fit["lastValue"], "exitFlag": fit["exitFlag"]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/leastsquare.hpp", cases)
