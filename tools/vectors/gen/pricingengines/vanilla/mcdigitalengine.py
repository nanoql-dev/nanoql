# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCDigitalEngine (ql/pricingengines/vanilla/mcdigitalengine.hpp, .cpp), on the evaluation
date 15 October 2026: American cash-or-nothing options priced by Monte Carlo with the Brownian bridge's crossing
correction, on fixed seeds.

Up-and-in calls and down-and-in puts paying 10, at the hit and at expiry, exercisable from the evaluation date for
half a year and a year, on two Merton processes on a constant volatility (flat curves; zero curves), under five
engine settings (pseudo-random numbers on steps or steps per year, with and without the Brownian bridge and
antithetic variates, to a number of samples or to a tolerance; low-discrepancy numbers on steps and steps per year):
the NPV and, for pseudo-random numbers, the error estimate. Then the refusals of a plain vanilla payoff and of a
European exercise.

The outputs nanoql fixes are left out (the S-van plan's VR4): the volatility is constant, where the bridge's read at
the step's start, nanoql's, is QuantLib's at its end (tests/NanoQL.Tests/Divergences/McDigitalStepVolatilityTests.cs);
every window opens on the evaluation date (McDigitalWindowTests.cs); no seed is 76 (McDigitalSeed76Tests.cs); and
every structure is on the risk-free curve's clock (McMismatchedTimesTests.cs).

An engine spec is tools/vectors/gen/pricingengines/vanilla/mceuropeanengine.py's; an exercise {"type": "American",
"date", "payoffAtExpiry"} from the evaluation date. The wheel's calls, by QuantLib-SWIG 1.44's options.i:
MCDigitalEngine(process, traits, ...) picks MCPRDigitalEngine or MCLDDigitalEngine, the C++ MCDigitalEngine on
PseudoRandom or LowDiscrepancy with QuantLib's Statistics, its arguments passed through in order. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/MCDigitalEngineVectorTests.cs."""
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
mce = _load("mceuropeanengine", "pricingengines/vanilla/mceuropeanengine.py")
R = bsp.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)

PROCESSES = [
    ("Merton, flat, constant", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, bsp.CONSTANT,
                                                bsp.FLAT_Q)),
    ("Merton, zero curves, constant", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, bsp.CONSTANT,
                                                       bsp.ZERO_Q)),
]

SETTINGS = [
    ("PR, steps", mce.engine_spec("PseudoRandom", 42, steps=50, samples=4000)),
    ("PR, steps per year, bridge, antithetic", mce.engine_spec("PseudoRandom", 5, per_year=100, bb=True, anti=True,
                                                               samples=2000)),
    ("PR, tolerance", mce.engine_spec("PseudoRandom", 9, steps=20, anti=True, tolerance=0.05, max_samples=50000)),
    ("LD, steps, bridge", mce.engine_spec("LowDiscrepancy", 1, steps=64, bb=True, samples=2047)),
    ("LD, steps per year", mce.engine_spec("LowDiscrepancy", 3, per_year=50, samples=1023)),
]


def engine(process, spec):
    return ql.MCDigitalEngine(process, spec["traits"], timeSteps=spec["timeSteps"],
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
            for days in (182, 365):
                for at_expiry in (False, True):
                    ex = {"type": "American", "date": (R + days).serialNumber(), "payoffAtExpiry": at_expiry}
                    payoffs = [bc.payoff_spec("CashOrNothing", CALL, 110.0), bc.payoff_spec("CashOrNothing", PUT, 90.0)]
                    npv, err = [], []
                    for ps in payoffs:
                        option = ql.VanillaOption(bc.payoff(ps), ql.AmericanExercise(R, R + days, at_expiry))
                        option.setPricingEngine(engine(p, spec))
                        npv.append(v.enc(option.NPV()))
                        err.append(v.result(lambda: option.errorEstimate()))
                    out.append({"name": f"{pname}, {sname}, {days} days" + (", at expiry" if at_expiry else ""),
                                "inputs": {"settings": st, "process": pspec, "engine": spec, "payoffs": payoffs,
                                           "exercise": ex},
                                "expected": {"NPV": npv, "errorEstimate": err}})
    pspec = PROCESSES[0][1]
    p = bsp.process(pspec)
    spec = SETTINGS[0][1]
    for name, ps, ex, qex in [
            ("plain vanilla payoff", bc.payoff_spec("PlainVanilla", CALL, 110.0),
             {"type": "American", "date": (R + 365).serialNumber()}, ql.AmericanExercise(R, R + 365)),
            ("European exercise", bc.payoff_spec("CashOrNothing", CALL, 110.0),
             {"type": "European", "date": (R + 365).serialNumber()}, ql.EuropeanExercise(R + 365))]:
        option = ql.VanillaOption(bc.payoff(ps), qex)
        option.setPricingEngine(engine(p, spec))
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "engine": spec, "payoffs": [ps], "exercise": ex},
                    "expected": {"NPV": [v.attempt(lambda: option.NPV())]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/mcdigitalengine.hpp", cases)
