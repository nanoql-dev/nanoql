# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticDividendEuropeanEngine (ql/pricingengines/vanilla/analyticdividendeuropeanengine.hpp,
.cpp), on the evaluation date 15 October 2026: European options with discrete dividends, priced by the Black-Scholes
formulas on the spot less the dividends' present value.

Options on the four payoffs, both types and two strikes, expiring in six months and two years, on four processes (flat
and linear zero curves, constant and surface volatilities, different day counters for the curves), with six dividend
schedules (none; one on the evaluation date; dividends before the evaluation date, between it and the expiry, and after
the expiry, which the engine skips; dividends of zero; a large dividend): the NPV, delta, gamma, vega, theta and rho the
engine reports. Expired options, every Greek zero, one on a schedule the engine would refuse to price. Then the
refusals: an American exercise ("not an European option"), dividends larger than the spot ("negative or null underlying
after subtracting dividends") and a fractional dividend with no nominal, whose amount QuantLib refuses ("no nominal
given").

A case holds one process, schedule, expiry, payoff kind and type, its outputs one per strike. The process specs
are tools/vectors/gen/processes/blackscholesprocess.py's, a payoff spec is
tools/vectors/gen/pricingengines/blackcalculator.py's, a dividend {"type": "Fixed" | "Fractional", "amount" | "rate",
"date"}. The wheel's calls, by QuantLib-SWIG 1.44's options.i: AnalyticDividendEuropeanEngine(process, dividends), the
C++ constructor, on a DividendSchedule of FixedDividend and FractionalDividend; the option's NPV and Greeks, each a
OneAssetOption accessor. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/AnalyticDividendEuropeanEngineVectorTests.cs."""
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
CALL = int(ql.Option.Call)
GREEKS = ["delta", "gamma", "vega", "theta", "rho"]


def fixed(amount, offset):
    return {"type": "Fixed", "amount": amount, "date": (R + offset).serialNumber()}


def dividend(spec):
    if spec["type"] == "Fixed":
        return ql.FixedDividend(spec["amount"], ql.Date(spec["date"]))
    return ql.FractionalDividend(spec["rate"], ql.Date(spec["date"]))


SCHEDULES = [
    ("none", []),
    ("on the evaluation date", [fixed(2.0, 0)]),
    ("before, between and after", [fixed(1.0, -30), fixed(1.5, 91), fixed(1.5, 273), fixed(2.0, 800)]),
    ("quarterly", [fixed(0.75, d) for d in (45, 136, 227, 318, 409, 500, 591, 682)]),
    ("zero dividends", [fixed(0.0, 60), fixed(0.0, 400)]),
    ("a large dividend", [fixed(30.0, 120)]),
]
PROCESSES = [
    ("Merton, flat, constant", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, bsp.CONSTANT,
                                                bsp.FLAT_Q)),
    ("Merton, zero curves, surface", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, bsp.SURFACE,
                                                      bsp.ZERO_Q)),
    ("Black-Scholes, zero curve, constant", bsp.process_spec("BlackScholesProcess", 100.0, bsp.ZERO_R,
                                                             bsp.CONSTANT)),
    ("Merton, Actual/360 curves", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.flat_spec(R, 0.05, bsp.A360),
                                                   bsp.CONSTANT, bsp.flat_spec(R, 0.01, bsp.A360))),
]
KINDS = ["PlainVanilla", "CashOrNothing", "AssetOrNothing", "Gap"]
STRIKES = [90.0, 105.0]
EXPIRIES = [182, 730]


def price(pspec, payoff_spec, ex_date, schedule, exercise_type="European"):
    p = bsp.process(pspec)
    ex = ql.EuropeanExercise(ex_date) if exercise_type == "European" else ql.AmericanExercise(R, ex_date)
    option = ql.VanillaOption(bc.payoff(payoff_spec), ex)
    option.setPricingEngine(ql.AnalyticDividendEuropeanEngine(p, [dividend(d) for d in schedule]))
    return option


def cases():
    out = []
    st = v.settings(R)
    for pname, pspec in PROCESSES:
        for sname, schedule in SCHEDULES:
            for days in EXPIRIES:
                for kind in KINDS:
                    for option_type in (CALL, int(ql.Option.Put)):
                        payoffs = [bc.payoff_spec(kind, option_type, strike) for strike in STRIKES]
                        options = [price(pspec, payoff_spec, R + days, schedule) for payoff_spec in payoffs]
                        out.append({
                            "name": f"{pname}, {sname}, {days} days, {kind} {option_type}",
                            "inputs": {"settings": st, "process": pspec, "payoffs": payoffs,
                                       "exercise": {"type": "European", "date": (R + days).serialNumber()},
                                       "dividends": schedule},
                            "expected": {"NPV": [o.NPV() for o in options],
                                         **{g: [v.result(lambda: getattr(o, g)()) for o in options]
                                            for g in GREEKS}}})
    pspec = PROCESSES[0][1]
    # an expired option: every Greek zero, before the dividends are read (the fractional dividend with no nominal
    # refuses when priced)
    expired = {"type": "European", "date": (R - 1).serialNumber()}
    for sname, schedule in (("one dividend", [fixed(1.0, 91)]),
                            ("a fractional dividend with no nominal",
                             [{"type": "Fractional", "rate": 0.02, "date": (R + 91).serialNumber()}])):
        for kind in ("PlainVanilla", "CashOrNothing"):
            payoff_spec = bc.payoff_spec(kind, CALL, 100.0)
            option = price(pspec, payoff_spec, R - 1, schedule)
            out.append({"name": f"expired {kind}, {sname}",
                        "inputs": {"settings": st, "process": pspec, "payoffs": [payoff_spec], "exercise": expired,
                                   "dividends": schedule},
                        "expected": {"NPV": [option.NPV()], **{g: [getattr(option, g)()] for g in GREEKS}}})
    payoff_spec = bc.payoff_spec("PlainVanilla", CALL, 100.0)
    refusals = [
        ("American exercise", [fixed(1.0, 91)], "American"),
        ("dividends larger than the spot", [fixed(60.0, 30), fixed(60.0, 60)], "European"),
        ("a fractional dividend with no nominal",
         [{"type": "Fractional", "rate": 0.02, "date": (R + 91).serialNumber()}], "European"),
    ]
    for name, schedule, ex_type in refusals:
        option = price(pspec, payoff_spec, R + 182, schedule, ex_type)
        out.append({"name": name, "inputs": {"settings": st, "process": pspec, "payoffs": [payoff_spec],
                                             "exercise": {"type": ex_type, "date": (R + 182).serialNumber()},
                                             "dividends": schedule},
                    "expected": {"NPV": [v.attempt(lambda: option.NPV())]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/analyticdividendeuropeanengine.hpp", cases)
