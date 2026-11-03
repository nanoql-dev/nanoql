# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BSMRNDCalculator (ql/methods/finitedifferences/utilities/bsmrndcalculator.hpp, .cpp): the
density, cumulative distribution and inverse of the log spot under Black-Scholes-Merton processes with a constant
volatility, on flat and zero curves at 2% to 7.5%, with a constant volatility and a variance curve, at times from a
month to five years: the density and distribution at log spots from 3 standard deviations below the log forward to 3
above, the inverse at probabilities from 1e-4 to 0.9999.

Left out: structures on another clock, which nanoql reads at the instants the risk-free curve's times denote and
QuantLib at those times (tests/NanoQL.Tests/Divergences/LocalVolMismatchedTimesTests.cs): every structure here is dated
15 October 2026 on Actual/365 (Fixed). The process specs are tools/vectors/gen/processes/blackscholesprocess.py's. The
wheel's calls, by QuantLib-SWIG 1.44: BSMRNDCalculator(process) and its pdf, cdf and invcdf. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Utilities/BSMRNDCalculatorVectorTests.cs."""
import importlib.util
import math
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
bvts = bsp.bvts
R = bsp.R

PROCESSES = [
    ("flat curves, 27%", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.flat_spec(R, 0.075),
                                          bvts.vol_spec_constant(R, 0.27), bsp.flat_spec(R, 0.04))),
    ("zero curves, 22%", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, bsp.CONSTANT, bsp.ZERO_Q)),
    ("zero curves, a variance curve", bsp.process_spec("BlackScholesMertonProcess", 95.0, bsp.ZERO_R, bsp.CURVE,
                                                       bsp.ZERO_Q)),
    ("flat curves, low spot", bsp.process_spec("BlackScholesMertonProcess", 2.5, bsp.FLAT_R, bsp.CONSTANT,
                                               bsp.FLAT_Q)),
]
TIMES = [1.0 / 12, 0.5, 1.0, 2.0, 5.0]
SIGMAS = [-3.0, -1.5, -0.5, 0.0, 0.5, 1.5, 3.0]
PROBABILITIES = [1e-4, 0.01, 0.1, 0.25, 0.5, 0.75, 0.9, 0.99, 0.9999]


def cases():
    st = v.settings(R)
    out = []
    for name, spec in PROCESSES:
        process = bsp.process(spec)
        calc = ql.BSMRNDCalculator(process)
        points = []
        for t in TIMES:
            stdDev = process.blackVolatility().blackVol(t, spec["x0"]) * math.sqrt(t)
            fwd = spec["x0"] * process.dividendYield().discount(t) / process.riskFreeRate().discount(t)
            points += [[t, math.log(fwd) + s * stdDev] for s in SIGMAS]
        out.append({"name": name,
                    "inputs": {"settings": st, "process": spec, "points": points, "times": TIMES,
                               "probabilities": PROBABILITIES},
                    "expected": {
                        "pdf": [v.attempt(lambda t=t, x=x: calc.pdf(x, t)) for t, x in points],
                        "cdf": [v.attempt(lambda t=t, x=x: calc.cdf(x, t)) for t, x in points],
                        "invcdf": [[v.attempt(lambda t=t, p=p: calc.invcdf(p, t)) for p in PROBABILITIES]
                                   for t in TIMES]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/bsmrndcalculator.hpp", cases)
