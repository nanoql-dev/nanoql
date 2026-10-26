# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CashDividendEuropeanEngine (ql/pricingengines/vanilla/cashdividendeuropeanengine.hpp, .cpp),
on the evaluation date 15 October 2026: European plain-vanilla calls and puts with discrete cash dividends, priced by
the spot model (the option a put on an average basket of the dividends and the strike, by Choi's engine, or by
Black-Scholes when no dividend falls in the option's life, or by the escrowed formula when one dividend falls on the
evaluation date alone) and by the escrowed model (AnalyticDividendEuropeanEngine).

Options at two strikes, expiring in six months and two years, on three processes (flat curves and a constant
volatility; zero curves and a surface, the variance read at the strike; a Black-Scholes process on a zero curve), with
seven dividend schedules (none; one on the evaluation date; one on the evaluation date and later ones; dividends before
the evaluation date, between it and the expiry, and after it, which the engine skips; one on the six-month expiry,
which the spot model adds to the strike; quarterly; dividends of zero, which the spot model skips): the NPV. Then an
expired option, and the refusals: an American exercise ("not an European option") and a cash-or-nothing payoff
("non-striked payoff given"). Every structure is dated at the evaluation date and counts its days on Actual/365
(Fixed); structures dated or day-counted apart are left out (the S-exo plan's §2.10), where nanoql reads them on the
process's clock.

A case holds one process, schedule, model, expiry and type, its outputs one per strike. The specs are
tools/vectors/gen/pricingengines/vanilla/analyticdividendeuropeanengine.py's, with the model as the enum's integer. The
wheel's calls, by QuantLib-SWIG 1.44's options.i: CashDividendEuropeanEngine(process, dividends, model) on a
DividendSchedule of FixedDividend; the option's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/CashDividendEuropeanEngineVectorTests.cs."""
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


ade = _load("analyticdividendeuropeanengine", "pricingengines/vanilla/analyticdividendeuropeanengine.py")
bsp, bc = ade.bsp, ade.bc
R = ade.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
fixed = ade.fixed
MODELS = [("spot", int(ql.CashDividendEuropeanEngine.Spot)), ("escrowed", int(ql.CashDividendEuropeanEngine.Escrowed))]

SCHEDULES = [
    ("none", []),
    ("on the evaluation date", [fixed(2.0, 0)]),
    ("on the evaluation date and later", [fixed(2.0, 0), fixed(1.5, 91), fixed(1.0, 400)]),
    ("before, between and after", [fixed(1.0, -30), fixed(1.5, 91), fixed(1.5, 273), fixed(2.0, 800)]),
    ("on the six-month expiry", [fixed(1.0, 60), fixed(1.5, 182)]),
    ("quarterly", [fixed(0.75, d) for d in (45, 136, 227, 318, 409, 500, 591, 682)]),
    ("zero dividends", [fixed(0.0, 60), fixed(0.0, 400)]),
]
PROCESSES = [
    ("Merton, flat, constant", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, bsp.CONSTANT,
                                                bsp.FLAT_Q)),
    ("Merton, zero curves, surface", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, bsp.SURFACE,
                                                      bsp.ZERO_Q)),
    ("Black-Scholes, zero curve, constant", bsp.process_spec("BlackScholesProcess", 100.0, bsp.ZERO_R,
                                                             bsp.CONSTANT)),
]
STRIKES = [90.0, 105.0]
EXPIRIES = [182, 730]


def option(pspec, payoff_spec, exercise_s, schedule, model):
    p = bsp.process(pspec)
    date = ql.Date(exercise_s["date"])
    ex = ql.EuropeanExercise(date) if exercise_s["type"] == "European" else ql.AmericanExercise(R, date)
    o = ql.VanillaOption(bc.payoff(payoff_spec), ex)
    o.setPricingEngine(ql.CashDividendEuropeanEngine(p, [ade.dividend(d) for d in schedule], model))
    return o


def case(name, st, pspec, payoffs, exercise_s, schedule, model, value):
    return {"name": name,
            "inputs": {"settings": st, "process": pspec, "payoffs": payoffs, "exercise": exercise_s,
                       "dividends": schedule, "model": model},
            "expected": {"NPV": [value(p) for p in payoffs]}}


def cases():
    out = []
    st = v.settings(R)
    for pname, pspec in PROCESSES:
        for sname, schedule in SCHEDULES:
            for mname, model in MODELS:
                for days in EXPIRIES:
                    exercise_s = {"type": "European", "date": (R + days).serialNumber()}
                    for t, tname in ((CALL, "call"), (PUT, "put")):
                        payoffs = [bc.payoff_spec("PlainVanilla", t, k) for k in STRIKES]
                        out.append(case(f"{pname}, {sname}, {mname}, {days} days, {tname}", st, pspec, payoffs,
                                        exercise_s, schedule, model,
                                        lambda p: option(pspec, p, exercise_s, schedule, model).NPV()))
    pspec = PROCESSES[0][1]
    schedule = [fixed(1.0, 91)]
    expired = {"type": "European", "date": (R - 1).serialNumber()}
    payoffs = [bc.payoff_spec("PlainVanilla", CALL, 100.0)]
    for mname, model in MODELS:
        out.append(case(f"expired, {mname}", st, pspec, payoffs, expired, schedule, model,
                        lambda p: option(pspec, p, expired, schedule, model).NPV()))
    refusals = [
        ("American exercise", bc.payoff_spec("PlainVanilla", CALL, 100.0), "American"),
        ("cash-or-nothing payoff", bc.payoff_spec("CashOrNothing", CALL, 100.0), "European"),
    ]
    for name, payoff_spec, ex_type in refusals:
        exercise_s = {"type": ex_type, "date": (R + 182).serialNumber()}
        for mname, model in MODELS:
            out.append(case(f"{name}, {mname}", st, pspec, [payoff_spec], exercise_s, schedule, model,
                            lambda p: v.attempt(lambda: option(pspec, p, exercise_s, schedule, model).NPV())))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/cashdividendeuropeanengine.hpp", cases)
