# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCForwardEuropeanHestonEngine (ql/pricingengines/forward/mcforwardeuropeanhestonengine.hpp),
on the evaluation date 15 October 2026: forward-start calls and puts at moneyness 0.9, 1 and 1.1, reset in three months
and expiring in a year, or reset in six and expiring in two, on Heston processes (the Heston generators' equity
parameters with the quadratic-exponential martingale and full truncation schemes, flat and zero curves), priced by
pseudo-random numbers (with and without the control variate, antithetic, steps per year) and by low-discrepancy
numbers, over a fixed number of samples: the NPV, and the error estimate where the numbers give one.

A case holds forwardengine.py's option spec, a process spec of
tools/vectors/gen/pricingengines/vanilla/mceuropeanhestonengine.py and an engine {"traits" ("PseudoRandom" |
"LowDiscrepancy"), "timeSteps", "timeStepsPerYear", "antitheticVariate", "requiredSamples", "requiredTolerance",
"maxSamples", "seed", "controlVariate"}, nulls for QuantLib's Null. The wheel's calls, by QuantLib-SWIG 1.44's
forward.i: MCForwardEuropeanHestonEngine(process, traits, timeSteps, timeStepsPerYear, antitheticVariate,
requiredSamples, requiredTolerance, maxSamples, seed, controlVariate), and the option's NPV and errorEstimate. Read
back by tests/NanoQL.Tests/Vectors/PricingEngines/Forward/MCForwardEuropeanHestonEngineVectorTests.cs."""
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
mch = _load("mceuropeanhestonengine", "pricingengines/vanilla/mceuropeanhestonengine.py")
bsp, hp = mch.bsp, mch.hp
R = fwd.R

PROCESSES = [
    ("QE martingale, flat", mch.heston_spec(hp.PARAMETERS[0], mch.QE)),
    ("full truncation, zero curves", mch.heston_spec(hp.PARAMETERS[0], mch.FULL, bsp.ZERO_R, bsp.ZERO_Q)),
]


def engine_spec(traits, seed, steps=None, per_year=None, anti=False, samples=None, control=False):
    return {"traits": traits, "timeSteps": steps, "timeStepsPerYear": per_year, "antitheticVariate": anti,
            "requiredSamples": samples, "requiredTolerance": None, "maxSamples": None, "seed": seed,
            "controlVariate": control}


ENGINES = [
    ("PR, steps", engine_spec("PseudoRandom", 42, steps=8, samples=2000)),
    ("PR, steps, control variate", engine_spec("PseudoRandom", 42, steps=8, samples=2000, control=True)),
    ("PR, steps per year, antithetic", engine_spec("PseudoRandom", 7, per_year=12, anti=True, samples=1000)),
    ("LD, steps", engine_spec("LowDiscrepancy", 1, steps=8, samples=511)),
]


def engine(p, e):
    return ql.MCForwardEuropeanHestonEngine(p, e["traits"], e["timeSteps"], e["timeStepsPerYear"],
                                            e["antitheticVariate"], e["requiredSamples"], e["requiredTolerance"],
                                            e["maxSamples"], e["seed"], e["controlVariate"])


def cases():
    st = v.settings(R)
    out = []
    for pname, pspec in PROCESSES:
        for name, ospec, _ in fwd.setups(markets=[(pname, None)], periods=[(91, 365), (182, 730)]):
            for ename, e in ENGINES:
                o = fwd.option(ospec)
                o.setPricingEngine(engine(mch.process(pspec), e))
                expected = {"NPV": o.NPV()}
                if e["traits"] == "PseudoRandom":
                    expected["errorEstimate"] = o.errorEstimate()
                out.append({"name": f"{name}, {ename}",
                            "inputs": {"settings": st, "option": ospec, "process": pspec, "engine": e},
                            "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/forward/mcforwardeuropeanhestonengine.hpp", cases)
