# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCEuropeanEngine (ql/pricingengines/vanilla/mceuropeanengine.hpp), on the evaluation date 15
October 2026: European plain vanilla options priced by Monte Carlo, on fixed seeds.

Calls and puts at two strikes and two expiries, on four processes (the Merton process on flat curves and a constant
volatility, on zero curves and a variance curve, and on flat curves and a volatility surface, whose local volatility
the Euler discretization reads; the Black-Scholes process on a zero curve), under five engine settings (pseudo-random
numbers on steps or steps per year, with and without the Brownian bridge and antithetic variates, to a number of
samples or to a tolerance; low-discrepancy numbers on steps and steps per year): the NPV and, for pseudo-random
numbers, the error estimate (low-discrepancy numbers give none, QuantLib's "not provided", read as null). Then the
refusal of a cash-or-nothing payoff. Every structure is dated on the evaluation date on Actual/365 (Fixed), the
risk-free curve's clock, where nanoql's engine reads QuantLib's (tests/NanoQL.Tests/Divergences/
McMismatchedTimesTests.cs).

An engine spec is {"traits": "PseudoRandom" | "LowDiscrepancy", "timeSteps", "timeStepsPerYear", "brownianBridge",
"antitheticVariate", "requiredSamples", "requiredTolerance", "maxSamples", "seed"}, nulls for QuantLib's Null; the
process specs are tools/vectors/gen/processes/blackscholesprocess.py's and the payoff specs
tools/vectors/gen/pricingengines/blackcalculator.py's. The wheel's calls, by QuantLib-SWIG 1.44's options.i:
MCEuropeanEngine(process, traits, ...) picks MCPREuropeanEngine or MCLDEuropeanEngine, the C++ MCEuropeanEngine on
PseudoRandom or LowDiscrepancy with QuantLib's Statistics, its arguments passed through in order. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/MCEuropeanEngineVectorTests.cs."""
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
bc = _load("blackcalculator", "pricingengines/blackcalculator.py")
R = bsp.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)

PROCESSES = [
    ("Merton, flat, constant", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, bsp.CONSTANT,
                                                bsp.FLAT_Q)),
    ("Merton, zero curves, variance curve", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R,
                                                             bsp.CURVE, bsp.ZERO_Q)),
    ("Merton, flat, surface", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, bsp.SURFACE,
                                               bsp.FLAT_Q)),
    ("Black-Scholes, zero curve, constant", bsp.process_spec("BlackScholesProcess", 100.0, bsp.ZERO_R, bsp.CONSTANT)),
]


def engine_spec(traits, seed, steps=None, per_year=None, bb=False, anti=False, samples=None, tolerance=None,
                max_samples=None):
    return {"traits": traits, "timeSteps": steps, "timeStepsPerYear": per_year, "brownianBridge": bb,
            "antitheticVariate": anti, "requiredSamples": samples, "requiredTolerance": tolerance,
            "maxSamples": max_samples, "seed": seed}


SETTINGS = [
    ("PR, steps", engine_spec("PseudoRandom", 42, steps=10, samples=4000)),
    ("PR, steps per year, bridge, antithetic", engine_spec("PseudoRandom", 7, per_year=12, bb=True, anti=True,
                                                           samples=2000)),
    ("PR, tolerance", engine_spec("PseudoRandom", 11, steps=5, anti=True, tolerance=0.08, max_samples=40000)),
    ("LD, steps, bridge", engine_spec("LowDiscrepancy", 1, steps=8, bb=True, samples=1023)),
    ("LD, steps per year", engine_spec("LowDiscrepancy", 3, per_year=6, samples=511)),
]


def engine(process, spec):
    return ql.MCEuropeanEngine(process, spec["traits"], timeSteps=spec["timeSteps"],
                               timeStepsPerYear=spec["timeStepsPerYear"], brownianBridge=spec["brownianBridge"],
                               antitheticVariate=spec["antitheticVariate"], requiredSamples=spec["requiredSamples"],
                               requiredTolerance=spec["requiredTolerance"], maxSamples=spec["maxSamples"],
                               seed=spec["seed"])


def cases():
    out = []
    st = v.settings(R)
    for pname, pspec in PROCESSES:
        p = bsp.process(pspec)
        for sname, spec in SETTINGS:
            if spec["requiredTolerance"] is not None and "surface" in pname:
                continue
            for days in (182, 365):
                for option_type in (CALL, PUT):
                    payoffs = [bc.payoff_spec("PlainVanilla", option_type, k) for k in (90.0, 110.0)]
                    ex = {"type": "European", "date": (R + days).serialNumber()}
                    npv, err = [], []
                    for ps in payoffs:
                        option = ql.VanillaOption(bc.payoff(ps), ql.EuropeanExercise(R + days))
                        option.setPricingEngine(engine(p, spec))
                        npv.append(v.enc(option.NPV()))
                        err.append(v.result(lambda: option.errorEstimate()))
                    out.append({"name": f"{pname}, {sname}, {days} days, {'call' if option_type == CALL else 'put'}",
                                "inputs": {"settings": st, "process": pspec, "engine": spec, "payoffs": payoffs,
                                           "exercise": ex},
                                "expected": {"NPV": npv, "errorEstimate": err}})
    pspec = PROCESSES[0][1]
    spec = SETTINGS[0][1]
    ps = bc.payoff_spec("CashOrNothing", CALL, 100.0)
    ex = {"type": "European", "date": (R + 365).serialNumber()}
    option = ql.VanillaOption(bc.payoff(ps), ql.EuropeanExercise(R + 365))
    option.setPricingEngine(engine(bsp.process(pspec), spec))
    out.append({"name": "cash-or-nothing payoff",
                "inputs": {"settings": st, "process": pspec, "engine": spec, "payoffs": [ps], "exercise": ex},
                "expected": {"NPV": [v.attempt(lambda: option.NPV())]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/mceuropeanengine.hpp", cases)
