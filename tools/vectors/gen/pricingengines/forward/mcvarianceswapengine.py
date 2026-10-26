# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCVarianceSwapEngine (ql/pricingengines/forward/mcvarianceswapengine.hpp), on the
evaluation date 15 October 2026: variance swaps priced by the mean of each path's integrated local variance.

Long and short swaps at two strikes and notionals, maturing in 182 days and a year, on flat and zero curves with a
constant volatility and a variance curve, whose local variance is the same on every path, and on
blackscholesprocess.py's surface and the Derman, Kamal and Zou skew (test-suite/varianceswaps.cpp's), whose local
variance follows the path; priced by pseudo-random numbers (plain, antithetic, with a Brownian bridge, steps per year)
and by low-discrepancy numbers over a fixed number of samples, and by pseudo-random numbers to an absolute tolerance:
the variance, the NPV, and the error estimate where the numbers give one. Every structure is dated on the evaluation
date and counted on Actual/365 (Fixed), where QuantLib's times and nanoql's clock agree (structures on other dates or
day counters are tests/NanoQL.Tests/Divergences/McVarianceSwapMismatchedTimesTests.cs's). Then an expired swap.

A case holds one swap {"position" (QuantLib's Long 0, Short 1), "strike", "notional", "startDate", "maturityDate"},
blackscholesprocess.py's process spec and an engine {"traits" ("PseudoRandom" | "LowDiscrepancy"), "timeSteps",
"timeStepsPerYear", "brownianBridge", "antitheticVariate", "requiredSamples", "requiredTolerance", "maxSamples",
"seed"}, nulls for QuantLib's Null. The wheel's calls, by QuantLib-SWIG 1.44's varianceswaps.i:
VarianceSwap(position, strike, notional, startDate, maturityDate), MCVarianceSwapEngine(process, traits, timeSteps,
timeStepsPerYear, brownianBridge, antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed); variance,
NPV and errorEstimate the swap's accessors. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Forward/MCVarianceSwapEngineVectorTests.cs."""
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


rep = _load("replicatingvarianceswapengine", "pricingengines/forward/replicatingvarianceswapengine.py")
bsp = rep.bsp
R = bsp.R


def engine_spec(traits, steps, per_year, bb, anti, samples, tolerance, seed):
    return {"traits": traits, "timeSteps": steps, "timeStepsPerYear": per_year, "brownianBridge": bb,
            "antitheticVariate": anti, "requiredSamples": samples, "requiredTolerance": tolerance, "maxSamples": None,
            "seed": seed}


PROCESSES = [
    ("flat, constant", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, bsp.CONSTANT, bsp.FLAT_Q)),
    ("zero curves, variance curve",
     bsp.process_spec("BlackScholesMertonProcess", 95.0, bsp.ZERO_R, bsp.CURVE, bsp.ZERO_Q)),
    ("zero curves, surface", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, bsp.SURFACE, bsp.ZERO_Q)),
    ("flat, the DKZ skew", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, rep.dkz_surface(365),
                                            rep.ZERO_Q)),
]
ENGINES = [
    ("pseudo-random", engine_spec("PseudoRandom", 26, None, False, False, 1024, None, 42)),
    ("antithetic", engine_spec("PseudoRandom", 26, None, False, True, 1024, None, 7)),
    ("Brownian bridge", engine_spec("PseudoRandom", 26, None, True, False, 1024, None, 42)),
    ("steps per year", engine_spec("PseudoRandom", None, 12, False, False, 1024, None, 42)),
    ("low discrepancy", engine_spec("LowDiscrepancy", 26, None, True, False, 1023, None, 42)),
    ("tolerance", engine_spec("PseudoRandom", 12, None, False, False, None, 0.0005, 42)),
]
SWAPS = [("long 0.04", int(ql.Position.Long), 0.04, 50000.0, 365),
         ("short 0.0625", int(ql.Position.Short), 0.0625, 1.0, 182)]


def priced(swap, pspec, e):
    s = ql.VarianceSwap(swap["position"], swap["strike"], swap["notional"], ql.Date(swap["startDate"]),
                        ql.Date(swap["maturityDate"]))
    s.setPricingEngine(ql.MCVarianceSwapEngine(
        bsp.process(pspec), e["traits"], e["timeSteps"], e["timeStepsPerYear"], e["brownianBridge"],
        e["antitheticVariate"], e["requiredSamples"], e["requiredTolerance"], e["maxSamples"], e["seed"]))
    out = {"variance": v.result(s.variance), "NPV": s.NPV()}
    if e["traits"] == "PseudoRandom":
        out["errorEstimate"] = v.result(s.errorEstimate)
    return out


def cases():
    st = v.settings(R)
    out = []
    for pname, pspec in PROCESSES:
        for sname, position, strike, notional, days in SWAPS:
            swap = rep.swap_spec(position, strike, notional, days)
            for ename, e in ENGINES:
                out.append({"name": f"{pname}, {sname}, {days} days, {ename}",
                            "inputs": {"settings": st, "process": pspec, "swap": swap, "engine": e},
                            "expected": priced(swap, pspec, e)})
    swap = rep.swap_spec(int(ql.Position.Long), 0.04, 1.0, -1, -91)
    pspec, e = PROCESSES[0][1], ENGINES[0][1]
    out.append({"name": "expired", "inputs": {"settings": st, "process": pspec, "swap": swap, "engine": e},
                "expected": priced(swap, pspec, e)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/forward/mcvarianceswapengine.hpp", cases)
