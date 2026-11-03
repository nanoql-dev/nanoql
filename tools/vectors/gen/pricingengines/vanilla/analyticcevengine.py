# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticCEVEngine (ql/pricingengines/vanilla/analyticcevengine.hpp, .cpp), on the evaluation
date 15 October 2026: the NPVs of European calls and puts on a CEV forward of 100 (and 2.1), at strikes 60 to 150,
expiring in 30 days to five years, for elasticities from 0.1 to 2 on both sides of 1 (δ below and above 2) and
volatility parameters α = σ·f0^(1 - β) at σ 10% and 30%, discounted on flat curves at 3%, 0 and -1%. Then an expired
option and the refusal of an American exercise.

The outputs nanoql fixes are left out: |β - 1| < 1e-3, where nanoql prices Black at Hagan's volatility
(tests/NanoQL.Tests/Divergences/CevNearOneTests.cs), none of the grid's; and every option whose tail QuantLib takes as 1
- cdf (or gamma_p - cdf) is below 1.01e-3, where nanoql takes Boost's complement from 1e-3
(tests/NanoQL.Tests/Divergences/CevTailTests.cs). A put's tail is the wheel's CEVRNDCalculator.cdf at the strike, the
same Boost expression; a call's comes from the NPV and that cdf, f0·tail = NPV/discount + K·(1 - cdf), whose rounding
the margin covers. The wheel's calls, by QuantLib-SWIG 1.44: AnalyticCEVEngine(f0, alpha, beta, discountCurve),
CEVRNDCalculator(f0, alpha, beta).cdf(f, t), VanillaOption(payoff, exercise) and its NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/AnalyticCEVEngineVectorTests.cs."""
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

BETAS = [0.1, 0.3, 0.5, 0.7, 0.9, 0.99, 1.01, 1.1, 1.3, 1.6, 2.0]
SIGMAS = [0.1, 0.3]
RATES = [0.03, 0.0, -0.01]
DAYS = (30, 365, 1825)
STRIKES = (60.0, 80.0, 90.0, 100.0, 110.0, 120.0, 150.0)
SMALL_TAIL = 1.01e-3


def tail(option_type, f0, alpha, beta, strike, t, npv, discount):
    """The tail QuantLib takes as a difference, or None where the wheel can't read it."""
    try:
        c = ql.CEVRNDCalculator(f0, alpha, beta).cdf(strike, t)
    except RuntimeError:
        return None
    if option_type == PUT:
        return c
    return (npv / discount + strike * (1.0 - c)) / f0


def cases():
    st = v.settings(R)
    out = []
    for f0 in (100.0, 2.1):
        for r in RATES if f0 == 100.0 else RATES[:1]:
            curve_spec = bsp.flat_spec(R, r)
            discount = ql.YieldTermStructureHandle(bsp.curve(curve_spec))
            for beta in BETAS:
                for sigma in SIGMAS:
                    alpha = sigma * f0 ** (1.0 - beta)
                    for days in DAYS:
                        exercise_date = R + days
                        t = discount.timeFromReference(exercise_date)
                        disc = discount.discount(exercise_date)
                        for option_type in (CALL, PUT):
                            payoffs, npvs = [], []
                            for k in STRIKES:
                                k = k * f0 / 100.0
                                ps = bc.payoff_spec("PlainVanilla", option_type, k)
                                option = ql.VanillaOption(bc.payoff(ps), ql.EuropeanExercise(exercise_date))
                                option.setPricingEngine(ql.AnalyticCEVEngine(f0, alpha, beta, discount))
                                npv = v.attempt(lambda: option.NPV())
                                if isinstance(npv, float):
                                    tl = tail(option_type, f0, alpha, beta, k, t, npv, disc)
                                    if tl is None or tl < SMALL_TAIL:
                                        continue
                                payoffs.append(ps)
                                npvs.append(npv)
                            if not payoffs:
                                continue
                            out.append({"name": f"f0 {f0} r {r} beta {beta} sigma {sigma}, {days} days, "
                                                f"{'call' if option_type == CALL else 'put'}",
                                        "inputs": {"settings": st, "f0": f0, "alpha": alpha, "beta": beta,
                                                   "discountCurve": curve_spec,
                                                   "exercise": {"type": "European",
                                                                "date": exercise_date.serialNumber()},
                                                   "payoffs": payoffs},
                                        "expected": {"NPV": npvs}})
    curve_spec = bsp.flat_spec(R, 0.03)
    discount = ql.YieldTermStructureHandle(bsp.curve(curve_spec))
    alpha = 0.3 * 100.0 ** 0.5
    for name, exercise in [("expired", {"type": "European", "date": (R - 1).serialNumber()}),
                           ("American exercise refused",
                            {"type": "American", "earliestDate": R.serialNumber(), "date": (R + 365).serialNumber()})]:
        ps = bc.payoff_spec("PlainVanilla", CALL, 100.0)
        ex = (ql.EuropeanExercise(ql.Date(exercise["date"])) if exercise["type"] == "European"
              else ql.AmericanExercise(ql.Date(exercise["earliestDate"]), ql.Date(exercise["date"])))
        option = ql.VanillaOption(bc.payoff(ps), ex)
        option.setPricingEngine(ql.AnalyticCEVEngine(100.0, alpha, 0.5, discount))
        out.append({"name": name,
                    "inputs": {"settings": st, "f0": 100.0, "alpha": alpha, "beta": 0.5, "discountCurve": curve_spec,
                               "exercise": exercise, "payoffs": [ps]},
                    "expected": {"NPV": [v.attempt(lambda: option.NPV())]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/analyticcevengine.hpp", cases)
