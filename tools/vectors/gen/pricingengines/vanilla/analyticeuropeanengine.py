# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticEuropeanEngine (ql/pricingengines/vanilla/analyticeuropeanengine.hpp, .cpp), on
the evaluation date 15 October 2026: European options priced by the Black-Scholes formulas.

Options on four payoffs (plain vanilla, cash-or-nothing, asset-or-nothing, gap), both types and two strikes, with
four exercise dates (the evaluation date, with the reference date's events included so the option is live and its
time to expiry zero; a day later; six months; two years), on eight processes (flat and linear zero curves; constant,
zero, variance-curve and surface volatilities; the Merton, Black-Scholes, Black and Garman-Kohlhagen processes),
with the process's risk-free curve discounting or, for two processes, a separate discount curve: the NPV and every
Greek the engine reports (delta, forward delta, elasticity, gamma, theta, theta per day, vega, rho, dividend rho,
strike sensitivity and the in-the-money cash probability). An expired option's results are QuantLib's: every Greek
zero. Then the refusals: an American exercise ("not an European option") and a spot at zero ("negative or null
underlying given").

The outputs nanoql fixes are left out (the S-eq plan's SEQR7): at zero volatility (and at a zero time to expiry,
which gives a zero standard deviation) the delta, forward delta, strike sensitivity, elasticity, theta, theta per day,
rho and dividend rho of every payoff but a plain vanilla call, and the in-the-money probability of puts
(tests/NanoQL.Tests/Divergences/BlackCalculatorZeroVolatilityTests.cs and BlackCalculatorItmProbabilityTests.cs).
The wheel doesn't expose the additional results; they are vectored through the C++ harness
(tools/vectors/gen/cxx/pricingengines/vanilla/analyticeuropeanengine.py).

A case holds one process, exercise, payoff kind and type, its outputs one per strike. The process specs are
tools/vectors/gen/processes/blackscholesprocess.py's; a payoff spec is
tools/vectors/gen/pricingengines/blackcalculator.py's; an exercise is {"type": "European" | "American", "date"}.
The wheel's calls, by QuantLib-SWIG 1.44's options.i: AnalyticEuropeanEngine(process) and
AnalyticEuropeanEngine(process, discountCurve), the C++ constructors, VanillaOption(payoff, exercise), and the
option's NPV and Greeks, each a OneAssetOption accessor. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/AnalyticEuropeanEngineVectorTests.cs."""
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
GREEKS = ["delta", "deltaForward", "elasticity", "gamma", "theta", "thetaPerDay", "vega", "rho", "dividendRho",
          "strikeSensitivity", "itmCashProbability"]
FIXED_AT_ZERO_VOL = {"delta", "deltaForward", "elasticity", "theta", "thetaPerDay", "rho", "dividendRho",
                     "strikeSensitivity"}

PROCESSES = [
    ("Merton, flat, constant", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, bsp.CONSTANT,
                                                bsp.FLAT_Q)),
    ("Merton, zero curves, constant", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R,
                                                       bsp.CONSTANT, bsp.ZERO_Q)),
    ("Merton, zero curves, surface", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, bsp.SURFACE,
                                                      bsp.ZERO_Q)),
    ("Merton, zero volatility", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R,
                                                 bsp.bvts.vol_spec_constant(R, 0.0), bsp.FLAT_Q)),
    ("Black-Scholes, zero curve, variance curve", bsp.process_spec("BlackScholesProcess", 100.0, bsp.ZERO_R,
                                                                   bsp.CURVE)),
    ("Black, flat, constant, Actual/360",
     bsp.process_spec("BlackProcess", 100.0, bsp.flat_spec(R, 0.03, bsp.A360),
                      bsp.bvts.vol_spec_constant(R, 0.3, bsp.A360))),
    ("Garman-Kohlhagen, flat, constant", bsp.process_spec("GarmanKohlagenProcess", 100.0, bsp.FLAT_R,
                                                          bsp.bvts.vol_spec_constant(R, 0.12), bsp.FLAT_Q)),
    ("Merton, low spot", bsp.process_spec("BlackScholesMertonProcess", 60.0, bsp.ZERO_R, bsp.CONSTANT, bsp.ZERO_Q)),
]
EXPIRIES = [0, 1, 182, 730]
KINDS = ["PlainVanilla", "CashOrNothing", "AssetOrNothing", "Gap"]
STRIKES = [85.0, 110.0]
DISCOUNT = bsp.flat_spec(R, 0.05)
DISCOUNTED = ("Merton, flat, constant", "Merton, zero curves, surface")


def exercise(spec):
    if spec["type"] == "European":
        return ql.EuropeanExercise(ql.Date(spec["date"]))
    return ql.AmericanExercise(R, ql.Date(spec["date"]))


def results(option, zero_sd, kind, option_type):
    out = {"NPV": option.NPV()}
    for g in GREEKS:
        if zero_sd and g in FIXED_AT_ZERO_VOL and not (kind == "PlainVanilla" and option_type == CALL):
            continue
        if g == "itmCashProbability" and option_type != CALL:
            continue
        out[g] = v.result(lambda: getattr(option, g)())
    return out


def cases():
    out = []
    for pname, pspec in PROCESSES:
        zero_vol = pspec["volatility"].get("volatility") == 0.0
        for days in EXPIRIES:
            st = v.settings(R, include_reference_date_events=(days == 0))
            p = bsp.process(pspec)
            for discount in (None, DISCOUNT):
                if discount is not None and (days not in (182, 730) or pname not in DISCOUNTED):
                    continue
                engine = (ql.AnalyticEuropeanEngine(p) if discount is None
                          else ql.AnalyticEuropeanEngine(p, ql.YieldTermStructureHandle(bsp.curve(discount))))
                for kind in KINDS:
                    for option_type in (CALL, int(ql.Option.Put)):
                        payoffs = [bc.payoff_spec(kind, option_type, strike) for strike in STRIKES]
                        ex = {"type": "European", "date": (R + days).serialNumber()}
                        per_strike = []
                        for payoff_spec in payoffs:
                            option = ql.VanillaOption(bc.payoff(payoff_spec), exercise(ex))
                            option.setPricingEngine(engine)
                            per_strike.append(results(option, zero_vol or days == 0, kind, option_type))
                        inputs = {"settings": st, "process": pspec, "payoffs": payoffs, "exercise": ex}
                        if discount is not None:
                            inputs["discountCurve"] = discount
                        out.append({
                            "name": f"{pname}, {days} days, {kind} {option_type}"
                                    + ("" if discount is None else ", discount curve"),
                            "inputs": inputs,
                            "expected": {k: [r[k] for r in per_strike] for k in per_strike[0]}})
    # an expired option: every Greek zero
    st = v.settings(R)
    pspec = PROCESSES[0][1]
    p = bsp.process(pspec)
    for kind in ("PlainVanilla", "CashOrNothing"):
        payoff_spec = bc.payoff_spec(kind, CALL, 100.0)
        ex = {"type": "European", "date": (R - 1).serialNumber()}
        option = ql.VanillaOption(bc.payoff(payoff_spec), exercise(ex))
        option.setPricingEngine(ql.AnalyticEuropeanEngine(p))
        out.append({"name": f"expired {kind}", "inputs": {"settings": st, "process": pspec, "payoffs": [payoff_spec],
                                                         "exercise": ex},
                    "expected": {"NPV": [option.NPV()], **{g: [getattr(option, g)()] for g in GREEKS}}})
    refusals = [
        ("American exercise", pspec, {"type": "American", "date": (R + 182).serialNumber()}),
        ("spot at zero", bsp.process_spec("BlackScholesMertonProcess", 0.0, bsp.FLAT_R, bsp.CONSTANT, bsp.FLAT_Q),
         {"type": "European", "date": (R + 182).serialNumber()}),
    ]
    for name, spec, ex in refusals:
        payoff_spec = bc.payoff_spec("PlainVanilla", CALL, 100.0)
        option = ql.VanillaOption(bc.payoff(payoff_spec), exercise(ex))
        option.setPricingEngine(ql.AnalyticEuropeanEngine(bsp.process(spec)))
        out.append({"name": name, "inputs": {"settings": st, "process": spec, "payoffs": [payoff_spec],
                                             "exercise": ex},
                    "expected": {"NPV": [v.attempt(lambda: option.NPV())]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/analyticeuropeanengine.hpp", cases)
