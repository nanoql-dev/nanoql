# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GeometricBrownianMotionProcess (ql/processes/geometricbrownianprocess.hpp, .cpp), dS = μS dt
+ σS dW on Euler's discretization: 8 processes (positive, negative and zero drift and volatility, a negative initial
value), each with its x0, and at 20 points (t, x, dt, dw) its drift and diffusion, and the Euler discretization's
expectation, standard deviation and variance, the evolved state and apply.

The wheel's calls, by QuantLib-SWIG's stochasticprocess.i: ql.GeometricBrownianMotionProcess(initialValue, mue,
sigma) is the constructor; x0, drift, diffusion, expectation, stdDeviation, variance, evolve and apply are the members
StochasticProcess1D declares, each a virtual call to the process's own or to its discretization's. Read back by
tests/NanoQL.Tests/Vectors/Processes/GeometricBrownianMotionProcessVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

# (initialValue, mue, sigma)
PROCESSES = [
    (100.0, 0.03, 0.20),
    (50.0, -0.05, 0.35),
    (1.0, 0.0, 0.1),
    (100.0, 0.08, 0.0),
    (25.0, 1.5, 2.0),
    (-10.0, 0.02, 0.3),
    (0.0, 0.05, 0.25),
    (1e6, 0.001, 0.05),
]


def cases():
    st = v.settings(ql.Date(22, ql.October, 2026))
    rnd = random.Random(v.SEED)
    out = []
    for k, (s0, mue, sigma) in enumerate(PROCESSES):
        p = ql.GeometricBrownianMotionProcess(s0, mue, sigma)
        points = [[rnd.uniform(0.0, 10.0), rnd.uniform(-50.0, 200.0), rnd.choice([0.0, 1e-6, 0.01, 0.25, 1.0,
                                                                                    rnd.uniform(0.0, 3.0)]),
                   rnd.gauss(0.0, 1.0)]
                  for _ in range(20)]
        out.append({"name": f"{k} S0 {s0!r} mue {mue!r} sigma {sigma!r}",
                    "inputs": {"settings": st, "initialValue": s0, "mue": mue, "sigma": sigma, "points": points},
                    "expected": {
                        "x0": p.x0(),
                        "drift": [p.drift(t, x) for t, x, _, _ in points],
                        "diffusion": [p.diffusion(t, x) for t, x, _, _ in points],
                        "expectation": [p.expectation(t, x, dt) for t, x, dt, _ in points],
                        "stdDeviation": [p.stdDeviation(t, x, dt) for t, x, dt, _ in points],
                        "variance": [p.variance(t, x, dt) for t, x, dt, _ in points],
                        "evolve": [p.evolve(t, x, dt, dw) for t, x, dt, dw in points],
                        "apply": [p.apply(x, dw) for _, x, _, dw in points]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/geometricbrownianprocess.hpp", cases)
