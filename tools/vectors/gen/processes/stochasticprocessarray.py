# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's StochasticProcessArray (ql/processes/stochasticprocessarray.hpp, .cpp): arrays of two to four
one-dimensional processes (Ornstein-Uhlenbeck, geometric Brownian and Black-Scholes-Merton) over correlation
matrices, positive definite, singular and with a negative eigenvalue, which the spectral pseudo square root salvages.
For each array, its size and factors and initial values; and at 10 points (t, x, dt, dw) the drift, diffusion,
expectation, standard deviation, covariance and evolved state. Then the constructor's refusals: a correlation of
another size, and one that isn't square.

The wheel's calls, by QuantLib-SWIG's stochasticprocess.i: ql.StochasticProcessArray(processes, correlation) is the
constructor, and size, factors, initialValues, drift, diffusion, expectation, stdDeviation, covariance and evolve
the StochasticProcess members, each a virtual call to the array's own; a Matrix result is read as its rows. The
array's apply, time, process and correlation, which the wheel doesn't bind, are the harness's
(cxx/processes/stochasticprocessarray). Read back by tests/NanoQL.Tests/Vectors/Processes/
StochasticProcessArrayVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

TODAY = ql.Date(22, ql.October, 2026)

CORRELATIONS = {
    "two, positive definite": [[1.0, 0.6], [0.6, 1.0]],
    "three, positive definite": [[1.0, 0.9, 0.7], [0.9, 1.0, 0.4], [0.7, 0.4, 1.0]],
    "three, negative eigenvalue": [[1.0, 0.9, 0.7], [0.9, 1.0, -0.4], [0.7, -0.4, 1.0]],
    "four, singular": [[1.0, 1.0, 0.5, 0.0], [1.0, 1.0, 0.5, 0.0], [0.5, 0.5, 1.0, 0.2], [0.0, 0.0, 0.2, 1.0]],
}

# Each process as (kind, parameters): "ou" (speed, vol, x0, level), "gbm" (s0, mue, sigma), "bsm" (s0, r, q, vol).
PROCESSES = {
    "two, positive definite": [("ou", (0.1, 0.2, 0.05, 0.03)), ("gbm", (100.0, 0.03, 0.2))],
    "three, positive definite": [("bsm", (100.0, 0.05, 0.02, 0.2)), ("bsm", (80.0, 0.05, 0.0, 0.3)),
                                 ("gbm", (100.0, 0.03, 0.2))],
    "three, negative eigenvalue": [("ou", (0.5, 0.1, 0.0, 0.0)), ("gbm", (10.0, -0.01, 0.4)),
                                   ("bsm", (100.0, 0.03, 0.01, 0.25))],
    "four, singular": [("gbm", (100.0, 0.03, 0.2)), ("gbm", (100.0, 0.03, 0.2)), ("ou", (1.0, 0.3, 0.0, 0.1)),
                       ("bsm", (50.0, 0.04, 0.0, 0.15))],
}


def make(kind, params):
    if kind == "ou":
        return ql.OrnsteinUhlenbeckProcess(*params)
    if kind == "gbm":
        return ql.GeometricBrownianMotionProcess(*params)
    s0, r, q, vol = params
    dc = ql.Actual365Fixed()
    return ql.BlackScholesMertonProcess(
        ql.QuoteHandle(ql.SimpleQuote(s0)), ql.YieldTermStructureHandle(ql.FlatForward(TODAY, q, dc)),
        ql.YieldTermStructureHandle(ql.FlatForward(TODAY, r, dc)),
        ql.BlackVolTermStructureHandle(ql.BlackConstantVol(TODAY, ql.NullCalendar(), vol, dc)))


def rows(m):
    return [[m[i][j] for j in range(m.columns())] for i in range(m.rows())]


def process_input(kind, params):
    return {"kind": kind, "parameters": list(params)}


def cases():
    st = v.settings(TODAY)
    rnd = random.Random(v.SEED)
    out = []
    for name, corr in CORRELATIONS.items():
        specs = PROCESSES[name]
        n = len(specs)
        p = ql.StochasticProcessArray([make(*s) for s in specs], ql.Matrix(corr))
        points = [[rnd.uniform(0.0, 5.0), [rnd.uniform(20.0, 120.0) if s[0] != "ou" else rnd.uniform(-0.5, 0.5)
                                           for s in specs],
                   rnd.choice([1e-6, 0.01, 0.25, 1.0]), [rnd.gauss(0.0, 1.0) for _ in range(n)]]
                  for _ in range(10)]
        out.append({"name": name,
                    "inputs": {"settings": st, "processes": [process_input(*s) for s in specs], "correlation": corr,
                               "points": points},
                    "expected": {
                        "size": p.size(), "factors": p.factors(), "initialValues": list(p.initialValues()),
                        "drift": [list(p.drift(t, ql.Array(x))) for t, x, _, _ in points],
                        "diffusion": [rows(p.diffusion(t, ql.Array(x))) for t, x, _, _ in points],
                        "expectation": [list(p.expectation(t, ql.Array(x), dt)) for t, x, dt, _ in points],
                        "stdDeviation": [rows(p.stdDeviation(t, ql.Array(x), dt)) for t, x, dt, _ in points],
                        "covariance": [rows(p.covariance(t, ql.Array(x), dt)) for t, x, dt, _ in points],
                        "evolve": [list(p.evolve(t, ql.Array(x), dt, ql.Array(dw))) for t, x, dt, dw in points]}})
    specs = PROCESSES["two, positive definite"]
    for name, corr in (("correlation of another size", CORRELATIONS["three, positive definite"]),
                       ("correlation not square", [[1.0, 0.5, 0.2], [0.5, 1.0, 0.1]])):
        out.append({"name": name,
                    "inputs": {"settings": st, "processes": [process_input(*s) for s in specs], "correlation": corr},
                    "expected": {"size": v.attempt(
                        lambda corr=corr: ql.StochasticProcessArray([make(*s) for s in specs],
                                                                    ql.Matrix(corr)).size())}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/stochasticprocessarray.hpp", cases)
