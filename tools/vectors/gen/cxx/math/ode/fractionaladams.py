# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Harness vectors for QuantLib's fractional Adams scheme, Riemann-Liouville weights and Riemann-Liouville integral
(ql/math/ode/fractionaladams.hpp), which the wheel doesn't expose.

The scheme over Real on the harness's "power" and "linear" equations (test-suite/roughhestonmodel.cpp's) and over
std::complex<Real> on "rotation" and the rough Heston engine's "riccati" equation, at orders from 0.55 to 1 and step
counts from 1 to 200; the reusable weights' integral at orders 0, 0.25, 0.6 and 1 on real and complex grids at two
spacings; and the integral function on the same grids.

A scheme case holds "alpha", "equation", "y0" (a real, or [real, imaginary]), "tMax" and "steps"; a weights case
"alpha", "steps", "y" (reals, or [real, imaginary] each) and "dt"; an integral case "y", "alpha" and "dt". The harness's
calls: FractionalAdams::solve, RiemannLiouvilleWeights::integrate and riemannLiouvilleIntegral
(tools/cxx/src/math/ode/fractionaladams.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Ode/FractionalAdamsCxxVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
SCHEMES = [(0.6, "power", 0.0, 1.0, 1), (0.6, "power", 0.0, 1.0, 100), (0.6, "power", 0.0, 1.0, 200),
           (1.0, "power", 0.0, 1.0, 50), (0.6, "linear", 1.0, 1.0, 64), (0.55, "linear", 1.0, 2.0, 37),
           (0.6, "rotation", [1.0, 0.0], 1.0, 128), (0.9, "rotation", [0.5, -0.25], 0.5, 20),
           (0.6, "riccati", [0.0, 0.0], 1.0, 256), (0.8, "riccati", [0.0, 0.0], 2.0, 64)]
ALPHAS = [0.0, 0.25, 0.6, 1.0]
STEPS = [1, 2, 64]
DTS = [1e-3, 0.5]


def grid(steps):
    xs = [i / steps for i in range(steps + 1)]
    real = [math.exp(-0.7 * x) * (1.0 + x * x) for x in xs]
    complex_ = [[r, math.sin(37.0 * x)] for r, x in zip(real, xs)]
    return real, complex_


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for alpha, equation, y0, t_max, steps in SCHEMES:
            r = H.call("FractionalAdams::solve", alpha, equation, y0, t_max, steps)
            out.append({"name": f"scheme, {equation}, alpha {alpha}, {steps} steps",
                        "inputs": {"settings": st, "kind": "scheme", "alpha": alpha, "equation": equation, "y0": y0,
                                   "tMax": t_max, "steps": steps},
                        "expected": {"y": [v.enc(x) for x in r]}})
        for steps in STEPS:
            real, complex_ = grid(steps)
            for alpha in ALPHAS:
                for dt in DTS:
                    for kind, y in (("real", real), ("complex", complex_)):
                        w = H.call("RiemannLiouvilleWeights::integrate", alpha, steps, y, dt)
                        i = H.call("riemannLiouvilleIntegral", y, alpha, dt)
                        out.append({"name": f"weights and integral, {kind}, alpha {alpha}, {steps} steps, dt {dt}",
                                    "inputs": {"settings": st, "kind": "integral", "alpha": alpha, "steps": steps,
                                               "y": y, "dt": dt},
                                    "expected": {"weights": v.enc(w), "integral": v.enc(i)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/ode/fractionaladams.hpp", cases)
