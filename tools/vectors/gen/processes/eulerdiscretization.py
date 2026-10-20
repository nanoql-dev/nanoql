# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's EulerDiscretization (ql/processes/eulerdiscretization.hpp, .cpp), which the wheel doesn't
expose: through the Black-Scholes processes whose moments are the discretization's, on the evaluation date 15
October 2026.

A process on a strike-dependent volatility (a variance surface, whose local volatility is a LocalVolSurface), or
given a local volatility of its own, takes its standard deviation, variance and evolution from its discretization,
Euler's by default: sigma(t0, x0) sqrt(dt), sigma(t0, x0)^2 dt, and x0 exp(mu(t0, x0) dt + sigma(t0, x0) sqrt(dt)
dw). For four such processes, at 12 points (t0, x0, dt, dw): stdDeviation, variance and evolve, and covariance on the
state as an array, through StochasticProcess1D's multi-dimensional interface, which reads the variance. The
discretization's multi-dimensional members are reached through the Heston process, a later commit's.

The specs are tools/vectors/gen/processes/blackscholesprocess.py's. The wheel's calls, by QuantLib-SWIG 1.44's
stochasticprocess.i: the processes' constructors (each with its default discretization, Euler's), stdDeviation,
variance and evolve, StochasticProcess1D's, and covariance, StochasticProcess's. Read back by
tests/NanoQL.Tests/Vectors/Processes/EulerDiscretizationVectorTests.cs."""
import importlib.util
import os
import random
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
LOCAL = {"referenceDate": R.serialNumber(), "volatility": 0.3, "dayCounter": bsp.A365}
PROCESSES = [
    ("Merton on a surface", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, bsp.SURFACE,
                                             bsp.ZERO_Q), None),
    ("Black on a surface", bsp.process_spec("BlackProcess", 100.0, bsp.FLAT_R, bsp.SURFACE), None),
    ("generalized on a surface, spot 120",
     bsp.process_spec("GeneralizedBlackScholesProcess", 120.0, bsp.FLAT_R, bsp.SURFACE, bsp.ZERO_Q), None),
    ("generalized, its own local volatility",
     bsp.process_spec("GeneralizedBlackScholesProcess", 100.0, bsp.ZERO_R, bsp.CONSTANT, bsp.ZERO_Q), LOCAL),
]


def build(spec, local):
    if local is None:
        return bsp.process(spec)
    return ql.GeneralizedBlackScholesProcess(
        ql.QuoteHandle(ql.SimpleQuote(spec["x0"])), ql.YieldTermStructureHandle(bsp.curve(spec["dividend"])),
        ql.YieldTermStructureHandle(bsp.curve(spec["riskFree"])),
        ql.BlackVolTermStructureHandle(bsp.bvts.black_vol(spec["volatility"])),
        ql.LocalVolTermStructureHandle(ql.LocalConstantVol(ql.Date(local["referenceDate"]), local["volatility"],
                                                           bsp.bvts.day_counter(local["dayCounter"]))))


def cases():
    out = []
    st = v.settings(R)
    rnd = random.Random(v.SEED + 1)
    for name, spec, local in PROCESSES:
        p = build(spec, local)
        pts = [[rnd.choice([0.0, rnd.uniform(0.0, 4.0)]), rnd.uniform(70.0, 140.0),
                rnd.choice([1e-4, 0.02, 0.5, rnd.uniform(0.0, 1.5)]), rnd.uniform(-2.5, 2.5)] for _ in range(12)]
        inputs = {"settings": st, "process": spec}
        if local is not None:
            inputs["localVolatility"] = local
        inputs["points"] = pts
        out.append({
            "name": name,
            "inputs": inputs,
            "expected": v.enc({
                "stdDeviation": [p.stdDeviation(t, x, dt) for t, x, dt, _ in pts],
                "variance": [p.variance(t, x, dt) for t, x, dt, _ in pts],
                "evolve": [p.evolve(t, x, dt, dw) for t, x, dt, dw in pts],
                "covariance": [p.covariance(t, ql.Array([x]), dt)[0][0] for t, x, dt, _ in pts]})})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/eulerdiscretization.hpp", cases)
