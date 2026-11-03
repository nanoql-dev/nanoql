# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticCompoundOptionEngine (ql/pricingengines/exotic/analyticcompoundoptionengine.hpp,
.cpp), on the evaluation date 15 October 2026: compound options, calls and puts on calls and puts, the mother's strike
3 or 8 expiring in three or six months, the daughter's 95 or 105 expiring in a year or eighteen months, on three flat
markets: the NPV, delta, gamma, vega and theta.

Flat markets only: on term structures QuantLib's Geske form reads one constant rate, yield and volatility per maturity,
nanoql's form the discounts and variances by date (tests/NanoQL.Tests/Divergences/CompoundTermStructureTests.cs), and on
structures dated or counted apart QuantLib's times differ (ExoticMismatchedTimesTests.cs). QuantLib's engine reads
Drezner's bivariate normal, nanoql's West's, so the value, delta and theta, which read it, carry an override
(docs/porting-playbook.md §7.3): Drezner's accuracy, |Dr78 - We04DP| <= 6e-7 times the bivariate terms' coefficients,
relative to the output; the gamma and vega read univariate densities only. A put daughter whose largest value doesn't
reach the mother's strike, where QuantLib finds no critical spot (CompoundNoCriticalSpotTests.cs), is left out.

The setups, which the other exotic generators' markets share: a process spec of
tools/vectors/gen/processes/blackscholesprocess.py and an option {"motherPayoff", "motherExercise", "daughterPayoff",
"daughterExercise"}, the payoffs tools/vectors/gen/pricingengines/blackcalculator.py plain-vanilla specs and the
exercises European. The wheel's calls, by QuantLib-SWIG 1.44's options.i: CompoundOption(motherPayoff,
motherExercise, daughterPayoff, daughterExercise), AnalyticCompoundOptionEngine(process), and the option's NPV and
Greeks. Read back by tests/NanoQL.Tests/Vectors/PricingEngines/Exotic/AnalyticCompoundOptionEngineVectorTests.cs."""
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
REASON = ("QuantLib's engine reads Drezner's bivariate normal, nanoql's West's; |Dr78 - We04DP| <= 6e-7 times the "
          "bivariate terms' coefficients")


def flat(spot, r, q, vol):
    return bsp.process_spec("BlackScholesMertonProcess", spot, bsp.flat_spec(R, r), bsp.bvts.vol_spec_constant(R, vol),
                            bsp.flat_spec(R, q))


MARKETS = [
    ("spot 100, r 5%, q 2%, vol 25%", flat(100.0, 0.05, 0.02, 0.25)),
    ("spot 90, r 3%, q 0, vol 35%", flat(90.0, 0.03, 0.0, 0.35)),
    ("spot 110, r 8%, q 4%, vol 20%", flat(110.0, 0.08, 0.04, 0.20)),
]


def exercise_spec(days):
    return {"type": "European", "date": (R + days).serialNumber()}


def exercise(spec):
    return ql.EuropeanExercise(ql.Date(spec["date"]))


def override(value, error):
    return {"value": value, "tolerance": error / max(abs(value), 1.0), "reason": REASON}


def cases():
    st = v.settings(R)
    out = []
    for mname, pspec in MARKETS:
        p = bsp.process(pspec)
        rate = pspec["riskFree"]["forward"]
        yield_ = pspec["dividend"]["forward"]
        for wm in (CALL, PUT):
            for wd in (CALL, PUT):
                for km, tm in ((3.0, 91), (8.0, 182)):
                    for kd, td in ((95.0, 365), (105.0, 547)):
                        ospec = {"motherPayoff": bc.payoff_spec("PlainVanilla", wm, km),
                                 "motherExercise": exercise_spec(tm),
                                 "daughterPayoff": bc.payoff_spec("PlainVanilla", wd, kd),
                                 "daughterExercise": exercise_spec(td)}
                        o = ql.CompoundOption(bc.payoff(ospec["motherPayoff"]), exercise(ospec["motherExercise"]),
                                              bc.payoff(ospec["daughterPayoff"]), exercise(ospec["daughterExercise"]))
                        o.setPricingEngine(ql.AnalyticCompoundOptionEngine(p))
                        s = pspec["x0"]
                        name = (f"{mname}, {'call' if wm == CALL else 'put'} {km:g} at {tm} on "
                                f"{'call' if wd == CALL else 'put'} {kd:g} at {td}")
                        expected = {"NPV": override(o.NPV(), 6e-7 * (s + kd)),
                                    "delta": override(o.delta(), 6e-7),
                                    "gamma": o.gamma(),
                                    "vega": o.vega(),
                                    "theta": override(o.theta(), 6e-7 * (yield_ * s + rate * kd))}
                        out.append({"name": name, "inputs": {"settings": st, "option": ospec, "process": pspec},
                                    "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/exotic/analyticcompoundoptionengine.hpp", cases)
