# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's IntegralEngine (ql/pricingengines/vanilla/integralengine.hpp, .cpp), on the evaluation date
15 October 2026: the NPVs of European options on plain vanilla, cash-or-nothing, asset-or-nothing and gap payoffs,
calls and puts at strikes 80 to 120 on a spot of 100, expiring in 30 days to three years, on Merton processes on flat
curves and constant volatilities, a Merton process on zero curves and a variance curve, and a Black-Scholes process
with no dividends. Then an expired option, the refusal of an American exercise, and a percentage-strike call at a
moneyness of 0.9, a striked payoff whose variance the engine reads at the moneyness.

The outputs nanoql fixes are left out (the S-van plan's VR4 (x)): at a zero variance, where QuantLib divides by it
(tests/NanoQL.Tests/Divergences/IntegralEngineZeroVarianceTests.cs); the processes here have none.

The process specs are tools/vectors/gen/processes/blackscholesprocess.py's and the payoff specs
tools/vectors/gen/pricingengines/blackcalculator.py's. The wheel's calls, by QuantLib-SWIG 1.44's options.i:
IntegralEngine(process), the C++ constructor, VanillaOption(payoff, exercise) and its NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/IntegralEngineVectorTests.cs."""
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

MARKETS = [(0.05, 0.02, 0.25), (0.0, 0.03, 0.4), (-0.01, 0.01, 0.1), (0.08, 0.0, 0.6)]
DAYS = (30, 365, 1095)
STRIKES = (80.0, 100.0, 120.0)
KINDS = ["PlainVanilla", "CashOrNothing", "AssetOrNothing", "Gap"]


def flat_process(r, q, vol):
    return bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.flat_spec(R, r), bsp.bvts.vol_spec_constant(R, vol),
                            bsp.flat_spec(R, q))


def price(payoff, exercise, p):
    option = ql.VanillaOption(payoff, exercise)
    option.setPricingEngine(ql.IntegralEngine(p))
    return v.attempt(lambda: option.NPV())


def cases():
    st = v.settings(R)
    processes = [(f"flat r {r} q {q} vol {vol}", flat_process(r, q, vol)) for r, q, vol in MARKETS]
    processes += [("zero curves, variance curve",
                   bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, bsp.CURVE, bsp.ZERO_Q)),
                  ("Black-Scholes, flat r 0.05, vol 0.3",
                   bsp.process_spec("BlackScholesProcess", 100.0, bsp.flat_spec(R, 0.05),
                                    bsp.bvts.vol_spec_constant(R, 0.3)))]
    out = []
    for pname, pspec in processes:
        p = bsp.process(pspec)
        for days in DAYS:
            ex = {"type": "European", "date": (R + days).serialNumber()}
            for kind in KINDS:
                for option_type in (CALL, PUT):
                    payoffs = [bc.payoff_spec(kind, option_type, k) for k in STRIKES]
                    out.append({"name": f"{pname}, {days} days, {kind} {'call' if option_type == CALL else 'put'}",
                                "inputs": {"settings": st, "process": pspec, "payoffs": payoffs, "exercise": ex},
                                "expected": {"NPV": [price(bc.payoff(ps), ql.EuropeanExercise(R + days), p)
                                                     for ps in payoffs]}})
    pspec = flat_process(0.05, 0.02, 0.25)
    p = bsp.process(pspec)
    call = bc.payoff_spec("PlainVanilla", CALL, 100.0)
    for name, ex, qex in [("expired", {"type": "European", "date": (R - 1).serialNumber()}, ql.EuropeanExercise(R - 1)),
                          ("American exercise", {"type": "American", "date": (R + 365).serialNumber()},
                           ql.AmericanExercise(R, R + 365))]:
        out.append({"name": name, "inputs": {"settings": st, "process": pspec, "payoffs": [call], "exercise": ex},
                    "expected": {"NPV": [price(bc.payoff(call), qex, p)]}})
    percentage = bc.payoff_spec("PercentageStrike", CALL, 90.0)
    ex = {"type": "European", "date": (R + 365).serialNumber()}
    out.append({"name": "percentage strike payoff",
                "inputs": {"settings": st, "process": pspec, "payoffs": [percentage], "exercise": ex},
                "expected": {"NPV": [price(bc.payoff(percentage), ql.EuropeanExercise(R + 365), p)]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/integralengine.hpp", cases)
