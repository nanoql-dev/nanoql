# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCPerformanceEngine (ql/pricingengines/cliquet/mcperformanceengine.hpp, .cpp), on the
evaluation date 15 October 2026: performance options on analyticcliquetengine.py's flat-market setups at moneyness 1,
priced by pseudo-random numbers (plain, antithetic, with a Brownian bridge) and by low-discrepancy numbers, over a
fixed number of samples, and by pseudo-random numbers to an absolute tolerance: the NPV, and the error estimate where
the numbers give one.

Left out: a cliquet with a reset on the evaluation date, which QuantLib's path pricer refuses
(tests/NanoQL.Tests/Divergences/McPerformanceResetTodayTests.cs).

A case holds analyticcliquetengine.py's option and process specs and an engine {"traits" ("PseudoRandom" |
"LowDiscrepancy"), "brownianBridge", "antitheticVariate", "requiredSamples", "requiredTolerance", "maxSamples",
"seed"}, nulls for QuantLib's Null. The wheel's calls, by QuantLib-SWIG 1.44's montecarlo.i: MCPRPerformanceEngine
and MCLDPerformanceEngine(process, brownianBridge, antitheticVariate, requiredSamples, requiredTolerance, maxSamples,
seed), and the option's NPV and errorEstimate.
Read back by tests/NanoQL.Tests/Vectors/PricingEngines/Cliquet/MCPerformanceEngineVectorTests.cs."""
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


cliquet = _load("analyticcliquetengine", "pricingengines/cliquet/analyticcliquetengine.py")
bsp = cliquet.bsp
R = cliquet.R


def engine_spec(traits, bb, anti, samples, tolerance, seed):
    return {"traits": traits, "brownianBridge": bb, "antitheticVariate": anti, "requiredSamples": samples,
            "requiredTolerance": tolerance, "maxSamples": None, "seed": seed}


ENGINES = [
    ("pseudo-random", engine_spec("PseudoRandom", False, False, 4096, None, 42)),
    ("antithetic", engine_spec("PseudoRandom", False, True, 4096, None, 7)),
    ("Brownian bridge", engine_spec("PseudoRandom", True, False, 4096, None, 42)),
    ("low discrepancy", engine_spec("LowDiscrepancy", True, False, 4095, None, 42)),
    ("tolerance", engine_spec("PseudoRandom", False, False, None, 0.004, 42)),
]


def engine(pspec, e):
    cls = ql.MCPRPerformanceEngine if e["traits"] == "PseudoRandom" else ql.MCLDPerformanceEngine
    return cls(bsp.process(pspec), e["brownianBridge"], e["antitheticVariate"], e["requiredSamples"],
               e["requiredTolerance"], e["maxSamples"], e["seed"])


def cases():
    st = v.settings(R)
    out = []
    for name, ospec, pspec, flat in cliquet.setups():
        if not flat or ospec["payoff"]["strike"] != 100.0:
            continue
        for ename, e in ENGINES:
            o = cliquet.option(ospec)
            o.setPricingEngine(engine(pspec, e))
            expected = {"NPV": o.NPV()}
            if e["traits"] == "PseudoRandom":
                expected["errorEstimate"] = o.errorEstimate()
            out.append({"name": f"{name}, {ename}",
                        "inputs": {"settings": st, "option": ospec, "process": pspec, "engine": e},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/cliquet/mcperformanceengine.hpp", cases)
