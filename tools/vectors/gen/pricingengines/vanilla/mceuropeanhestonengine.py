# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCEuropeanHestonEngine (ql/pricingengines/vanilla/mceuropeanhestonengine.hpp), on the
evaluation date 15 October 2026: European plain vanilla options priced by Monte Carlo on Heston and Bates processes,
on fixed seeds.

Calls and puts at two strikes and two expiries, on four processes (tools/vectors/gen/processes/hestonprocess.py's first
set on flat curves under the quadratic-exponential martingale discretization and on zero curves under full truncation,
its second set under the quadratic-exponential martingale discretization, and tools/vectors/gen/processes/
batesprocess.py's process on that first set with batesmodel's jumps, under its default full truncation), under four
engine settings (pseudo-random numbers on steps, and on steps per year with antithetic variates, to a number of
samples; pseudo-random numbers to a tolerance; low-discrepancy numbers on steps): the NPV and, for pseudo-random
numbers, the error estimate (low-discrepancy numbers give none, QuantLib's "not provided", read as null), or the engine's refusal. Then the refusal of a
cash-or-nothing payoff.

Left out: every structure is dated on the evaluation date on Actual/365 (Fixed), the risk-free curve's clock (QuantLib's
engine runs on the risk-free curve's times, tests/NanoQL.Tests/Divergences/McHestonMismatchedTimesTests.cs).

An engine spec is {"traits": "PseudoRandom" | "LowDiscrepancy", "timeSteps", "timeStepsPerYear",
"antitheticVariate", "requiredSamples", "requiredTolerance", "maxSamples", "seed"}, nulls for QuantLib's Null; a
process spec hestonprocess.py's with a "discretization", or batesprocess.py's with "lambda", "nu" and "delta"; the
payoff specs tools/vectors/gen/pricingengines/blackcalculator.py's. The wheel's calls, by QuantLib-SWIG 1.44's
options.i: MCEuropeanHestonEngine(process, traits, ...) picks MCPREuropeanHestonEngine or MCLDEuropeanHestonEngine, the
C++ MCEuropeanHestonEngine on PseudoRandom or LowDiscrepancy with QuantLib's Statistics, its arguments passed through
in order. Read back by tests/NanoQL.Tests/Vectors/PricingEngines/MCEuropeanHestonEngineVectorTests.cs."""
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
hp = _load("hestonprocess", "processes/hestonprocess.py")
bp = _load("batesprocess", "processes/batesprocess.py")
bc = _load("blackcalculator", "pricingengines/blackcalculator.py")
R = bsp.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
QE = int(ql.HestonProcess.QuadraticExponentialMartingale)
FULL = int(ql.HestonProcess.FullTruncation)


def heston_spec(params, discretization, rf=None, q=None):
    _, s0, v0, kappa, theta, sigma, rho = params
    return {"riskFree": rf or bsp.FLAT_R, "dividend": q or bsp.FLAT_Q, "s0": s0, "v0": v0, "kappa": kappa,
            "theta": theta, "sigma": sigma, "rho": rho, "discretization": discretization}


_BATES = {k: x for k, x in heston_spec(hp.PARAMETERS[0], FULL).items() if k != "discretization"}
_BATES.update({"lambda": bp.JUMPS[0][1], "nu": bp.JUMPS[0][2], "delta": bp.JUMPS[0][3]})
PROCESSES = [
    ("equity, QE martingale, flat", heston_spec(hp.PARAMETERS[0], QE)),
    ("equity, full truncation, zero curves", heston_spec(hp.PARAMETERS[0], FULL, bsp.ZERO_R, bsp.ZERO_Q)),
    ("high vol of variance, QE martingale, flat", heston_spec(hp.PARAMETERS[1], QE)),
    ("Bates, flat", _BATES),
]


def engine_spec(traits, seed, steps=None, per_year=None, anti=False, samples=None, tolerance=None, max_samples=None):
    return {"traits": traits, "timeSteps": steps, "timeStepsPerYear": per_year, "antitheticVariate": anti,
            "requiredSamples": samples, "requiredTolerance": tolerance, "maxSamples": max_samples, "seed": seed}


SETTINGS = [
    ("PR, steps", engine_spec("PseudoRandom", 42, steps=10, samples=2000)),
    ("PR, steps per year, antithetic", engine_spec("PseudoRandom", 7, per_year=12, anti=True, samples=1000)),
    ("PR, tolerance", engine_spec("PseudoRandom", 11, steps=5, anti=True, tolerance=0.2, max_samples=50000)),
    ("LD, steps", engine_spec("LowDiscrepancy", 1, steps=8, samples=511)),
]


def process(spec):
    if "lambda" in spec:
        return bp.process(spec)
    return hp.process(spec)


def engine(p, spec):
    return ql.MCEuropeanHestonEngine(p, spec["traits"], timeSteps=spec["timeSteps"],
                                     timeStepsPerYear=spec["timeStepsPerYear"],
                                     antitheticVariate=spec["antitheticVariate"],
                                     requiredSamples=spec["requiredSamples"],
                                     requiredTolerance=spec["requiredTolerance"], maxSamples=spec["maxSamples"],
                                     seed=spec["seed"])


def cases():
    out = []
    st = v.settings(R)
    for pname, pspec in PROCESSES:
        p = process(pspec)
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
    ps = bc.payoff_spec("CashOrNothing", CALL, 100.0)
    ex = {"type": "European", "date": (R + 365).serialNumber()}
    option = ql.VanillaOption(bc.payoff(ps), ql.EuropeanExercise(R + 365))
    option.setPricingEngine(engine(process(pspec), spec))
    out.append({"name": "cash-or-nothing payoff",
                "inputs": {"settings": st, "process": pspec, "engine": spec, "payoffs": [ps], "exercise": ex},
                "expected": {"NPV": [v.attempt(lambda: option.NPV())]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/mceuropeanhestonengine.hpp", cases)
