# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Merton76Process (ql/processes/merton76process.hpp, .cpp), on the evaluation date 15 October
2026: three processes on flat and zero curves and a constant and a curve volatility, with jump intensities, mean log
jumps and log jump volatilities, each with its x0, and at five points (t, x, dt, dw) the members that refuse: the
drift, diffusion and apply, which QuantLib's process doesn't implement, and the Euler discretization's expectation,
standard deviation and variance and the evolved state, which read them.

The process specs are tools/vectors/gen/processes/blackscholesprocess.py's, the curves and volatility of the
Black-Scholes-Merton process the Merton process wraps, with "jumpIntensity", "logMeanJump" and "logJumpVolatility".
The wheel's calls, by QuantLib-SWIG 1.44's stochasticprocess.i: Merton76Process(stateVariable, dividendTS,
riskFreeTS, volTS, jumpIntensity, meanLogJump, jumpVolatility), the C++ constructor with Euler's discretization, and
x0, drift, diffusion, expectation, stdDeviation, variance, evolve and apply, the members StochasticProcess1D declares.
Read back by tests/NanoQL.Tests/Vectors/Processes/Merton76ProcessVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
R = bsp.R

PROCESSES = [
    ("flat, constant", dict(bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, bsp.CONSTANT, bsp.FLAT_Q),
                            jumpIntensity=1.0, logMeanJump=-0.1, logJumpVolatility=0.25)),
    ("zero curves, variance curve",
     dict(bsp.process_spec("BlackScholesMertonProcess", 95.0, bsp.ZERO_R, bsp.CURVE, bsp.ZERO_Q),
          jumpIntensity=0.25, logMeanJump=0.05, logJumpVolatility=0.1)),
    ("no jumps", dict(bsp.process_spec("BlackScholesMertonProcess", 50.0, bsp.FLAT_R, bsp.CONSTANT, bsp.FLAT_Q),
                      jumpIntensity=0.0, logMeanJump=0.0, logJumpVolatility=0.0)),
]
POINTS = [[0.0, 100.0, 0.25, 0.5], [0.5, 90.0, 0.01, -1.0], [1.0, 110.0, 1.0, 0.0], [2.5, 50.0, 0.5, 1.5],
          [5.0, 100.0, 0.0, -0.3]]


def process(spec):
    def quote(x):
        return ql.QuoteHandle(ql.SimpleQuote(x))

    return ql.Merton76Process(quote(spec["x0"]), ql.YieldTermStructureHandle(bsp.curve(spec["dividend"])),
                              ql.YieldTermStructureHandle(bsp.curve(spec["riskFree"])),
                              ql.BlackVolTermStructureHandle(bsp.bvts.black_vol(spec["volatility"])),
                              quote(spec["jumpIntensity"]), quote(spec["logMeanJump"]),
                              quote(spec["logJumpVolatility"]))


def cases():
    st = v.settings(R)
    out = []
    for name, spec in PROCESSES:
        p = process(spec)
        out.append({"name": name, "inputs": {"settings": st, "process": spec, "points": POINTS},
                    "expected": {
                        "x0": p.x0(),
                        "drift": [v.attempt(lambda: p.drift(t, x)) for t, x, _, _ in POINTS],
                        "diffusion": [v.attempt(lambda: p.diffusion(t, x)) for t, x, _, _ in POINTS],
                        "expectation": [v.attempt(lambda: p.expectation(t, x, dt)) for t, x, dt, _ in POINTS],
                        "stdDeviation": [v.attempt(lambda: p.stdDeviation(t, x, dt)) for t, x, dt, _ in POINTS],
                        "variance": [v.attempt(lambda: p.variance(t, x, dt)) for t, x, dt, _ in POINTS],
                        "evolve": [v.attempt(lambda: p.evolve(t, x, dt, dw)) for t, x, dt, dw in POINTS],
                        "apply": [v.attempt(lambda: p.apply(x, dw)) for _, x, _, dw in POINTS]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/merton76process.hpp", cases)
