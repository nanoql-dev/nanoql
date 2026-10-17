# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's OrnsteinUhlenbeckProcess (ql/processes/ornsteinuhlenbeckprocess.hpp, .cpp): 10 processes,
their speeds positive, negative, zero, just below and at the square root of machine epsilon (the two branches of
variance), with and without the defaulted x0 and level. For each process its four accessors (x0, speed, volatility,
level), and at 20 points (t, x, dt) its five overridden members of the process interface: drift(t, x), diffusion(t, x),
expectation(t, x, dt), stdDeviation(t, x, dt) and variance(t, x, dt). Then the constructor's refusal of a negative and
of a NaN volatility.

The wheel's calls, by QuantLib-SWIG's stochasticprocess.i: ql.OrnsteinUhlenbeckProcess(speed, vol, x0, level) is the
constructor, with QuantLib's defaults for x0 and level where the case leaves them out; speed, volatility and level
are its members, and x0, drift, diffusion, expectation, stdDeviation and variance those StochasticProcess1D
declares, each a virtual call to the process's own. The base's members on the process are
tools/vectors/gen/stochasticprocess.py's. Read back by
tests/NanoQL.Tests/Vectors/Processes/OrnsteinUhlenbeckProcessVectorTests.cs."""
import math
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

SQRT_EPS = math.sqrt(2.220446049250313e-16)

# (speed, volatility, x0, level); None leaves the argument to QuantLib's default
PROCESSES = [
    (0.1, 0.2, None, None),
    (1.5, 0.35, 0.8, None),
    (0.03, 0.01, -0.5, 0.04),
    (-0.2, 0.15, 1.0, 0.5),
    (0.0, 0.25, 0.3, -0.1),
    (SQRT_EPS, 0.3, 0.1, 0.2),
    (math.nextafter(SQRT_EPS, 0.0), 0.3, 0.1, 0.2),
    (-1e-10, 0.12, 2.0, 1.0),
    (4.0, 0.0, 0.6, 0.6),
    (0.7, 1.2, -3.0, 2.5),
]


def build(speed, vol, x0, level):
    args = [speed, vol] + ([x0] if x0 is not None else []) + ([level] if level is not None else [])
    return ql.OrnsteinUhlenbeckProcess(*args)


def cases():
    st = v.settings(ql.Date(16, ql.October, 2026))
    rnd = random.Random(v.SEED)
    out = []
    for k, spec in enumerate(PROCESSES):
        p = build(*spec)
        points = [[rnd.uniform(0.0, 10.0), rnd.uniform(-3.0, 3.0), rnd.choice([0.0, 1e-6, 0.01, 0.25, 1.0, 5.0,
                                                                               rnd.uniform(0.0, 3.0)])]
                  for _ in range(20)]
        speed, vol, x0, level = spec
        inputs = {"settings": st, "speed": speed, "volatility": vol}
        if x0 is not None:
            inputs["x0"] = x0
        if level is not None:
            inputs["level"] = level
        inputs["points"] = points
        out.append({"name": f"{k} speed {speed!r}",
                    "inputs": inputs,
                    "expected": {
                        "x0": p.x0(),
                        "speed": p.speed(),
                        "volatility": p.volatility(),
                        "level": p.level(),
                        "drift": [p.drift(t, x) for t, x, _ in points],
                        "diffusion": [p.diffusion(t, x) for t, x, _ in points],
                        "expectation": [p.expectation(t, x, dt) for t, x, dt in points],
                        "stdDeviation": [p.stdDeviation(t, x, dt) for t, x, dt in points],
                        "variance": [p.variance(t, x, dt) for t, x, dt in points]}})
    for name, vol in (("negative volatility", -0.01), ("NaN volatility", math.nan)):
        out.append({"name": name,
                    "inputs": {"settings": st, "speed": 0.1, "volatility": v.enc(vol)},
                    "expected": {"x0": v.attempt(lambda: ql.OrnsteinUhlenbeckProcess(0.1, vol).x0())}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/ornsteinuhlenbeckprocess.hpp", cases)
