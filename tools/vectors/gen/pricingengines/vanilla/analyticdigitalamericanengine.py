# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticDigitalAmericanEngine and AnalyticDigitalAmericanKOEngine
(ql/pricingengines/vanilla/analyticdigitalamericanengine.hpp, .cpp), on the evaluation date 15 October 2026: American
cash-or-nothing and asset-or-nothing options at the hit (knock-in) and at expiry (knock-in and knock-out).

Calls and puts at strikes on both sides of the spot and on it, exercisable from the evaluation date for a quarter, a
year and three years, on Merton processes on flat curves and constant volatilities, from negative to high rates and
volatilities of 0.1% to 60%, and on zero curves and a variance curve: at the hit the NPV, delta, gamma and rho, at
expiry the NPV. Then the refusals of a European exercise, of a window opening after the evaluation date and of a
spot at zero.

The outputs nanoql fixes are left out: at the hit where λ² = μ² − 2 ln D / σ²T is negative, which QuantLib prices NaN
(tests/NanoQL.Tests/Divergences/AmericanPayoffAtHitNegativeRateTests.cs); and below a variance of ε
(AmericanPayoffZeroVarianceTests.cs). The knock-out engine at the hit, which QuantLib prices as the knock-in and nanoql
refuses (DigitalAmericanKnockOutAtHitTests.cs), and the cases QuantLib's master's #2855 changes (its
tests/NanoQL.Tests/TestSuite/DigitalOptionTests.cs case) aren't written either: a knock-in or knock-out at expiry where
the formula's N(D₂) underflows or (H/S)^2μ overflows, judged here with a margin.

The process specs are tools/vectors/gen/processes/blackscholesprocess.py's and the payoff specs
tools/vectors/gen/pricingengines/blackcalculator.py's; an exercise {"type": "American", "date", "payoffAtExpiry"} from
the evaluation date. The wheel's calls, by QuantLib-SWIG 1.44's options.i: AnalyticDigitalAmericanEngine(process) and
AnalyticDigitalAmericanKOEngine(process), the C++ constructors, VanillaOption(payoff, exercise), and the option's NPV,
delta, gamma and rho, OneAssetOption's accessors. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/AnalyticDigitalAmericanEngineVectorTests.cs."""
import importlib.util
import math
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
EPS = 2.220446049250313e-16

MARKETS = [(r, q, vol) for r, q, vol in [(0.05, 0.02, 0.25), (-0.01, -0.02, 0.2), (0.0, 0.0, 0.3), (0.08, 0.0, 0.6),
                                          (0.03, 0.01, 0.01), (0.02, 0.06, 0.001), (0.05, 0.0, 0.1), (-0.005, 0.01, 0.15)]]


def flat_process(r, q, vol):
    return bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.flat_spec(R, r), bsp.bvts.vol_spec_constant(R, vol),
                            bsp.flat_spec(R, q))


def fixed(kind, option, strike, process, date, at_expiry):
    """Whether nanoql's fixes or #2855 reach the case, read from the process's structures at the exercise date."""
    discount = process.riskFreeRate().discount(date)
    dividend_discount = process.dividendYield().discount(date)
    variance = process.blackVolatility().blackVariance(date, strike)
    if variance < EPS:
        return True
    mu = math.log(dividend_discount / discount) / variance - 0.5
    if not at_expiry:
        return mu * mu - 2.0 * math.log(discount) / variance < 0.0
    reached = strike <= 100.0 if option == CALL else strike >= 100.0
    if reached:
        return False
    mu += 1.0 if kind == "AssetOrNothing" else 0.0
    sd = math.sqrt(variance)
    eta = -1.0 if option == CALL else 1.0
    d2 = eta * (math.log(strike / 100.0) / sd + mu * sd)
    return d2 < -37.0 or 2.0 * mu * math.log(strike / 100.0) > 700.0


def cases():
    out = []
    st = v.settings(R)
    processes = [(f"flat r {r} q {q} vol {vol}", flat_process(r, q, vol)) for r, q, vol in MARKETS]
    processes.append(("zero curves, variance curve",
                      bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, bsp.CURVE, bsp.ZERO_Q)))
    for pname, pspec in processes:
        p = bsp.process(pspec)
        for days in (91, 365, 1095):
            for at_expiry, knock_in in ((False, True), (True, True), (True, False)):
                for kind in ("CashOrNothing", "AssetOrNothing"):
                    for option_type in (CALL, PUT):
                        strikes = [k for k in (80.0, 95.0, 100.0, 105.0, 120.0)
                                   if not fixed(kind, option_type, k, p, R + days, at_expiry)]
                        if not strikes:
                            continue
                        payoffs = [bc.payoff_spec(kind, option_type, k) for k in strikes]
                        ex = {"type": "American", "date": (R + days).serialNumber(), "payoffAtExpiry": at_expiry}
                        results = {"NPV": []} if at_expiry else {"NPV": [], "delta": [], "gamma": [], "rho": []}
                        for ps in payoffs:
                            option = ql.VanillaOption(bc.payoff(ps), ql.AmericanExercise(R, R + days, at_expiry))
                            option.setPricingEngine(ql.AnalyticDigitalAmericanEngine(p) if knock_in
                                                    else ql.AnalyticDigitalAmericanKOEngine(p))
                            for g in results:
                                results[g].append(v.enc(getattr(option, g)()))
                        out.append({"name": f"{pname}, {days} days, {kind} {'call' if option_type == CALL else 'put'}"
                                            + (", at expiry" if at_expiry else "")
                                            + ("" if knock_in else ", knock-out"),
                                    "inputs": {"settings": st, "process": pspec, "payoffs": payoffs, "exercise": ex,
                                               "knockIn": knock_in},
                                    "expected": results})
    pspec = flat_process(0.05, 0.02, 0.25)
    p = bsp.process(pspec)
    cash = bc.payoff_spec("CashOrNothing", CALL, 110.0)
    for name, spec, ex, qex in [
            ("European exercise", pspec, {"type": "European", "date": (R + 365).serialNumber()},
             ql.EuropeanExercise(R + 365)),
            ("spot at zero", dict(pspec, x0=0.0), {"type": "American", "date": (R + 365).serialNumber()},
             ql.AmericanExercise(R, R + 365))]:
        option = ql.VanillaOption(bc.payoff(cash), qex)
        option.setPricingEngine(ql.AnalyticDigitalAmericanEngine(bsp.process(spec)))
        out.append({"name": name,
                    "inputs": {"settings": st, "process": spec, "payoffs": [cash], "exercise": ex, "knockIn": True},
                    "expected": {"NPV": [v.attempt(lambda: option.NPV())]}})
    window = {"type": "American", "date": (R + 365).serialNumber(), "earliestDate": (R + 30).serialNumber()}
    option = ql.VanillaOption(bc.payoff(cash), ql.AmericanExercise(R + 30, R + 365))
    option.setPricingEngine(ql.AnalyticDigitalAmericanEngine(p))
    out.append({"name": "window", "inputs": {"settings": st, "process": pspec, "payoffs": [cash], "exercise": window,
                                             "knockIn": True},
                "expected": {"NPV": [v.attempt(lambda: option.NPV())]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/analyticdigitalamericanengine.hpp", cases)
