# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's StochasticProcess and StochasticProcess1D (ql/stochasticprocess.hpp, .cpp) through the
members the wheel exposes, on 10 Ornstein-Uhlenbeck processes, the one concrete process S2 ports. For each process
the multi-dimensional interface's size, factors (the base's default, size()) and initialValues; and at 20 points
(t0, x0, dt, dw, dx): the 1-D defaults the process doesn't override, evolve(t0, x0, dt, dw) (expectation plus
stdDeviation times dw, through apply) and apply(x0, dx); and the multi-dimensional forms StochasticProcess1D
implements through the 1-D members on arrays of size 1: drift(t0, x), diffusion(t0, x), expectation(t0, x, dt),
stdDeviation(t0, x, dt), covariance(t0, x, dt) and evolve(t0, x, dt, dw). A 1 x 1 matrix is written as its rows.
Left out: the outputs that read the variance of the processes at speed 1e-9, below the square root of machine epsilon,
where QuantLib's variance takes its a = 0 limit and nanoql's is the cancellation-free form, by the user's ruling of
2026-10-22 (the S-sr plan's SSR3 class; tests/NanoQL.Tests/Divergences/OrnsteinUhlenbeckVarianceTests.cs).

The wheel's calls, by QuantLib-SWIG's stochasticprocess.i: the process is ql.OrnsteinUhlenbeckProcess(speed, vol,
x0, level). Its proxy class declares the 1-D members, which hide the base's array forms of the same names, so those
are called through the base proxy, ql.StochasticProcess.drift(process, t, ql.Array([x])) and the like, each a virtual
call to StochasticProcess1D's own implementation (private in C++, reached through the base). ql.StochasticProcess1D's
evolve and apply are QuantLib's 1-D defaults, which the Ornstein-Uhlenbeck process inherits. The multi-dimensional
apply, which the wheel doesn't expose, and the base class's own defaults are the C++ harness's
(tools/vectors/gen/cxx/stochasticprocess.py). Read back by tests/NanoQL.Tests/Vectors/StochasticProcessVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

SP = ql.StochasticProcess


def rows(m):
    return [[m[i][j] for j in range(m.columns())] for i in range(m.rows())]


# The outputs that read the variance, left out at speed 1e-9.
VARIANCE_OUTPUTS = ("evolve", "stdDeviationArray", "covarianceArray", "evolveArray")


def cases():
    st = v.settings(ql.Date(16, ql.October, 2026))
    rnd = random.Random(v.SEED)
    out = []
    for k in range(10):
        speed = rnd.choice([rnd.uniform(-0.5, 2.0), 0.0, 1e-9])
        vol = rnd.uniform(0.0, 0.6)
        x0 = rnd.uniform(-1.0, 1.0)
        level = rnd.uniform(-0.5, 0.5)
        p = ql.OrnsteinUhlenbeckProcess(speed, vol, x0, level)
        points = [[rnd.uniform(0.0, 10.0), rnd.uniform(-2.0, 2.0), rnd.uniform(0.0, 2.0), rnd.gauss(0.0, 1.0),
                   rnd.uniform(-0.5, 0.5)] for _ in range(20)]
        process = {"type": "OrnsteinUhlenbeckProcess", "speed": speed, "volatility": vol, "x0": x0, "level": level}
        out.append({"name": f"{k} speed {speed!r}",
                    "inputs": {"settings": st, "process": process, "points": points},
                    "expected": {
                        "size": SP.size(p),
                        "factors": SP.factors(p),
                        "initialValues": list(SP.initialValues(p)),
                        "evolve": [ql.StochasticProcess1D.evolve(p, t0, x, dt, dw) for t0, x, dt, dw, _ in points],
                        "apply": [ql.StochasticProcess1D.apply(p, x, dx) for _, x, _, _, dx in points],
                        "driftArray": [list(SP.drift(p, t0, ql.Array([x]))) for t0, x, _, _, _ in points],
                        "diffusionArray": [rows(SP.diffusion(p, t0, ql.Array([x]))) for t0, x, _, _, _ in points],
                        "expectationArray": [list(SP.expectation(p, t0, ql.Array([x]), dt))
                                             for t0, x, dt, _, _ in points],
                        "stdDeviationArray": [rows(SP.stdDeviation(p, t0, ql.Array([x]), dt))
                                              for t0, x, dt, _, _ in points],
                        "covarianceArray": [rows(SP.covariance(p, t0, ql.Array([x]), dt))
                                            for t0, x, dt, _, _ in points],
                        "evolveArray": [list(SP.evolve(p, t0, ql.Array([x]), dt, ql.Array([dw])))
                                        for t0, x, dt, dw, _ in points]}})
        if speed == 1e-9:
            for m in VARIANCE_OUTPUTS:
                out[-1]["expected"].pop(m)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/stochasticprocess.hpp", cases)
