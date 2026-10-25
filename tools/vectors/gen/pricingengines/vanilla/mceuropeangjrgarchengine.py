# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCEuropeanGJRGARCHEngine (ql/pricingengines/vanilla/mceuropeangjrgarchengine.hpp), on the
evaluation date 15 October 2026: European plain vanilla options priced by Monte Carlo on GJR-GARCH processes, on fixed
seeds.

Calls and puts at two strikes and two expiries, on three processes (tools/vectors/gen/processes/gjrgarchprocess.py's
first set on flat curves under full truncation and on zero curves under partial truncation, its second set on flat
curves under reflection), under four engine settings (pseudo-random numbers on steps, and on steps per year with
antithetic variates, to a number of samples; pseudo-random numbers to a tolerance; low-discrepancy numbers on steps):
the NPV and, for pseudo-random numbers, the error estimate (low-discrepancy numbers give none, QuantLib's "not
provided", read as null), or the engine's refusal. Then the refusal of a cash-or-nothing payoff.

Left out (the S-hes plan's §2.10): every structure is dated on the evaluation date on Actual/365 (Fixed), the
risk-free curve's clock (QuantLib's engine runs on the risk-free curve's times,
tests/NanoQL.Tests/Divergences/GjrGarchMismatchedTimesTests.cs).

An engine spec is tools/vectors/gen/pricingengines/vanilla/mceuropeanhestonengine.py's; a process spec
gjrgarchprocess.py's; the payoff specs tools/vectors/gen/pricingengines/blackcalculator.py's. The wheel's calls, by
QuantLib-SWIG 1.44's options.i: MCEuropeanGJRGARCHEngine(process, traits, ...) picks MCPREuropeanGJRGARCHEngine or
MCLDEuropeanGJRGARCHEngine, the C++ MCEuropeanGJRGARCHEngine on PseudoRandom or LowDiscrepancy with QuantLib's
Statistics, its arguments passed through in order. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/MCEuropeanGJRGARCHEngineVectorTests.cs."""
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
gp = _load("gjrgarchprocess", "processes/gjrgarchprocess.py")
bc = _load("blackcalculator", "pricingengines/blackcalculator.py")
mch = _load("mceuropeanhestonengine", "pricingengines/vanilla/mceuropeanhestonengine.py")
R = bsp.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
PROCESSES = [
    ("gjrgarchmodel's set, full truncation, flat",
     gp.process_spec(gp.PARAMETERS[0], bsp.FLAT_R, bsp.FLAT_Q, int(ql.GJRGARCHProcess.FullTruncation))),
    ("gjrgarchmodel's set, partial truncation, zero curves",
     gp.process_spec(gp.PARAMETERS[0], bsp.ZERO_R, bsp.ZERO_Q, int(ql.GJRGARCHProcess.PartialTruncation))),
    ("trading days, reflection, flat",
     gp.process_spec(gp.PARAMETERS[1], bsp.FLAT_R, bsp.FLAT_Q, int(ql.GJRGARCHProcess.Reflection))),
]
SETTINGS = mch.SETTINGS


def engine(p, spec):
    return ql.MCEuropeanGJRGARCHEngine(p, spec["traits"], timeSteps=spec["timeSteps"],
                                       timeStepsPerYear=spec["timeStepsPerYear"],
                                       antitheticVariate=spec["antitheticVariate"],
                                       requiredSamples=spec["requiredSamples"],
                                       requiredTolerance=spec["requiredTolerance"], maxSamples=spec["maxSamples"],
                                       seed=spec["seed"])


def cases():
    out = []
    st = v.settings(R)
    for pname, pspec in PROCESSES:
        p = gp.process(pspec)
        for sname, spec in SETTINGS:
            for days in (182, 365):
                for option_type in (CALL, PUT):
                    s0 = pspec["s0"]
                    payoffs = [bc.payoff_spec("PlainVanilla", option_type, round(k * s0, 8)) for k in (0.9, 1.1)]
                    ex = {"type": "European", "date": (R + days).serialNumber()}
                    npv, err = [], []
                    for ps in payoffs:
                        option = ql.VanillaOption(bc.payoff(ps), ql.EuropeanExercise(R + days))
                        option.setPricingEngine(engine(p, spec))
                        value = v.attempt(lambda: v.enc(option.NPV()))
                        npv.append(value)
                        err.append(dict(v.THROWS) if value == v.THROWS else v.result(lambda: option.errorEstimate()))
                    out.append({"name": f"{pname}, {sname}, {days} days, {'call' if option_type == CALL else 'put'}",
                                "inputs": {"settings": st, "process": pspec, "engine": spec, "payoffs": payoffs,
                                           "exercise": ex},
                                "expected": {"NPV": npv, "errorEstimate": err}})
    pspec = PROCESSES[0][1]
    spec = SETTINGS[0][1]
    ps = bc.payoff_spec("CashOrNothing", CALL, 50.0)
    ex = {"type": "European", "date": (R + 365).serialNumber()}
    option = ql.VanillaOption(bc.payoff(ps), ql.EuropeanExercise(R + 365))
    option.setPricingEngine(engine(gp.process(pspec), spec))
    out.append({"name": "cash-or-nothing payoff",
                "inputs": {"settings": st, "process": pspec, "engine": spec, "payoffs": [ps], "exercise": ex},
                "expected": {"NPV": [v.attempt(lambda: option.NPV())]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/mceuropeangjrgarchengine.hpp", cases)
