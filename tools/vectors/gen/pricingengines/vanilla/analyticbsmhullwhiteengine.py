# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticBSMHullWhiteEngine (ql/pricingengines/vanilla/analyticbsmhullwhiteengine.hpp,
.cpp), on the evaluation date 15 October 2026: European calls and puts on plain vanilla payoffs at strikes 80 to 120
on a spot of 100, expiring in 30 days to ten years, on Merton processes on flat curves and on zero curves and a
Black-Scholes process with no dividends, each with a constant volatility, with Hull-White models on the process's
risk-free curve of reversions 0.03 to 1 and correlations of the equity with the short rate of -0.5, 0 and 0.5: the
NPV and the eleven Greeks the analytic European engine gives on the shifted variance. Then an expired option and the
refusal of an American exercise.

The outputs nanoql fixes are left out: below a·t = 1e-2, where nanoql's offset is its series
(tests/NanoQL.Tests/Divergences/BsmHullWhiteSmallReversionTests.cs), with a margin; and the puts' in-the-money
probability (BlackCalculatorItmProbabilityTests.cs). Every structure is dated on the evaluation date, counts on
Actual/365 (Fixed) and quotes a constant volatility, so the mismatched-dates class (BsmHullWhiteMismatchedTimesTests.cs)
doesn't reach the file.

A case's inputs are the process spec (tools/vectors/gen/processes/blackscholesprocess.py's), the payoffs
(tools/vectors/gen/pricingengines/blackcalculator.py's), the exercise, "model": {"a", "sigma"} and "correlation". The
wheel's calls, by QuantLib-SWIG 1.44's options.i and shortratemodels.i: HullWhite(termStructure, a, sigma) and
AnalyticBSMHullWhiteEngine(correlation, process, model), the C++ constructors, VanillaOption(payoff, exercise), and the
option's NPV and Greeks. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/AnalyticBSMHullWhiteEngineVectorTests.cs."""
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


GRID = _load("baroneadesiwhaleyengine", "pricingengines/vanilla/baroneadesiwhaleyengine.py")
bsp, bc = GRID.bsp, GRID.bc
R, CALL, PUT = GRID.R, GRID.CALL, GRID.PUT

DAYS = (30, 365, 1095, 3650)
STRIKES = (80.0, 100.0, 120.0)
MODELS = [(0.03, 0.005), (0.1, 0.01), (1.0, 0.02)]
CORRELATIONS = (-0.5, 0.0, 0.5)
SERIES_CUT = 1e-2


def cases():
    st = v.settings(R)
    processes = [("flat r 0.05 q 0.02 vol 0.25", GRID.flat_process(0.05, 0.02, 0.25)),
                 ("flat r 0.0 q 0.03 vol 0.4", GRID.flat_process(0.0, 0.03, 0.4)),
                 ("zero curves, vol 0.22", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R,
                                                            bsp.CONSTANT, bsp.ZERO_Q)),
                 ("Black-Scholes, flat r 0.05, vol 0.3",
                  bsp.process_spec("BlackScholesProcess", 100.0, bsp.flat_spec(R, 0.05),
                                   bsp.bvts.vol_spec_constant(R, 0.3)))]
    out = []
    for pname, pspec in processes:
        p = bsp.process(pspec)
        for a, sigma in MODELS:
            model = ql.HullWhite(p.riskFreeRate(), a, sigma)
            for rho in CORRELATIONS:
                engine = ql.AnalyticBSMHullWhiteEngine(rho, p, model)
                for days in DAYS:
                    t = p.riskFreeRate().dayCounter().yearFraction(R, R + days)
                    if a * t <= SERIES_CUT * (1 + 1e-6):
                        continue
                    ex = {"type": "European", "date": (R + days).serialNumber()}
                    for option_type in (CALL, PUT):
                        greeks = GRID.GREEKS if option_type == CALL else GRID.put_greeks(GRID.GREEKS)
                        payoffs = [bc.payoff_spec("PlainVanilla", option_type, k) for k in STRIKES]
                        results = {g: [] for g in ["NPV"] + greeks}
                        for ps in payoffs:
                            option = ql.VanillaOption(bc.payoff(ps), ql.EuropeanExercise(R + days))
                            option.setPricingEngine(engine)
                            for g, x in GRID.outputs(option, greeks).items():
                                results[g].append(x)
                        out.append({"name": f"{pname}, a {a} sigma {sigma}, rho {rho}, {days} days, "
                                            + ("call" if option_type == CALL else "put"),
                                    "inputs": {"settings": st, "process": pspec, "payoffs": payoffs, "exercise": ex,
                                               "model": {"a": a, "sigma": sigma}, "correlation": rho},
                                    "expected": results})
    pspec = GRID.flat_process(0.05, 0.02, 0.25)
    p = bsp.process(pspec)
    engine = ql.AnalyticBSMHullWhiteEngine(0.5, p, ql.HullWhite(p.riskFreeRate(), 0.1, 0.01))
    call = bc.payoff_spec("PlainVanilla", CALL, 100.0)
    for name, ex, qex in [("expired", {"type": "European", "date": (R - 1).serialNumber()}, ql.EuropeanExercise(R - 1)),
                          ("American exercise", {"type": "American", "date": (R + 365).serialNumber()},
                           ql.AmericanExercise(R, R + 365))]:
        option = ql.VanillaOption(bc.payoff(call), qex)
        option.setPricingEngine(engine)
        out.append({"name": name, "inputs": {"settings": st, "process": pspec, "payoffs": [call], "exercise": ex,
                                             "model": {"a": 0.1, "sigma": 0.01}, "correlation": 0.5},
                    "expected": {g: [x] for g, x in GRID.outputs(option, GRID.GREEKS).items()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/analyticbsmhullwhiteengine.hpp", cases)
