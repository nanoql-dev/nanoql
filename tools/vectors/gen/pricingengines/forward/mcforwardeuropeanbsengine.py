# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCForwardEuropeanBSEngine (ql/pricingengines/forward/mcforwardeuropeanbsengine.hpp), on the
evaluation date 15 October 2026: forward-start calls and puts at moneyness 1 on forwardengine.py's flat markets and
periods, priced by pseudo-random numbers (plain, antithetic, with a Brownian bridge, steps per year) and by
low-discrepancy numbers, over a fixed number of samples, and by pseudo-random numbers to an absolute tolerance: the NPV,
and the error estimate where the numbers give one.

A case holds forwardengine.py's option and process specs and an engine {"traits" ("PseudoRandom" | "LowDiscrepancy"),
"timeSteps", "timeStepsPerYear", "brownianBridge", "antitheticVariate", "requiredSamples", "requiredTolerance",
"maxSamples", "seed"}, nulls for QuantLib's Null. The wheel's calls, by QuantLib-SWIG 1.44's forward.i:
MCForwardEuropeanBSEngine(process, traits, timeSteps, timeStepsPerYear, brownianBridge, antitheticVariate,
requiredSamples, requiredTolerance, maxSamples, seed), and the option's NPV and errorEstimate. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Forward/MCForwardEuropeanBSEngineVectorTests.cs."""
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


fwd = _load("forwardengine", "pricingengines/forward/forwardengine.py")
bsp = fwd.bsp
R = fwd.R


def engine_spec(traits, steps, per_year, bb, anti, samples, tolerance, seed):
    return {"traits": traits, "timeSteps": steps, "timeStepsPerYear": per_year, "brownianBridge": bb,
            "antitheticVariate": anti, "requiredSamples": samples, "requiredTolerance": tolerance, "maxSamples": None,
            "seed": seed}


ENGINES = [
    ("pseudo-random", engine_spec("PseudoRandom", 8, None, False, False, 4096, None, 42)),
    ("antithetic", engine_spec("PseudoRandom", 8, None, False, True, 4096, None, 7)),
    ("Brownian bridge", engine_spec("PseudoRandom", 8, None, True, False, 4096, None, 42)),
    ("steps per year", engine_spec("PseudoRandom", None, 12, False, False, 4096, None, 42)),
    ("low discrepancy", engine_spec("LowDiscrepancy", 8, None, True, False, 4095, None, 42)),
    ("tolerance", engine_spec("PseudoRandom", 4, None, False, False, None, 0.05, 42)),
]


def engine(process, e):
    return ql.MCForwardEuropeanBSEngine(process, e["traits"], e["timeSteps"], e["timeStepsPerYear"],
                                        e["brownianBridge"], e["antitheticVariate"], e["requiredSamples"],
                                        e["requiredTolerance"], e["maxSamples"], e["seed"])


def cases():
    st = v.settings(R)
    out = []
    for name, ospec, pspec in fwd.setups(markets=fwd.MARKETS[:2], moneyness=[1.0]):
        for ename, e in ENGINES:
            o = fwd.option(ospec)
            o.setPricingEngine(engine(bsp.process(pspec), e))
            expected = {"NPV": o.NPV()}
            if e["traits"] == "PseudoRandom":
                expected["errorEstimate"] = o.errorEstimate()
            out.append({"name": f"{name}, {ename}",
                        "inputs": {"settings": st, "option": ospec, "process": pspec, "engine": e},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/forward/mcforwardeuropeanbsengine.hpp", cases)
