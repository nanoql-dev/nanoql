# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticBlackVasicekEngine (ql/pricingengines/vanilla/analyticeuropeanvasicekengine.hpp,
.cpp), which the wheel doesn't expose, each output computed by the C++ harness: the NPVs of European calls and puts at
strikes 80 to 120 on a spot of 100, expiring in 30 days to five years, on Black-Scholes-Merton processes over a flat
risk-free forward, no dividends and a constant volatility of 10% and 25% on Actual/365 (Fixed) and Actual/360, with
three Vasicek models of reversions 0.1 to 1 and correlations of -0.5, 0 and 0.5, on the evaluation date 15 October
2026. Then the refusal of an American exercise.

The outputs nanoql fixes are left out: a volatility that isn't flat, which QuantLib reads at time 0, and dividends,
which it drops (tests/NanoQL.Tests/Divergences/BlackVasicekVolatilityTests.cs); so are the Vasicek bonds below a
reversion of 0.1, where nanoql's bond is QuantLib's master's (VasicekCancellationTests.cs). Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/AnalyticBlackVasicekEngineCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
A365 = {"type": "Actual365Fixed"}
A360 = {"type": "Actual360"}
MARKETS = [(0.05, 0.25, A365), (0.03, 0.1, A360)]
MODELS = [(0.05, 0.1, 0.05, 0.01, 0.0), (0.02, 0.5, 0.04, 0.02, 0.0), (0.03, 1.0, 0.03, 0.005, 0.1)]
CORRELATIONS = (-0.5, 0.0, 0.5)
DAYS = (30, 365, 1825)
STRIKES = (80.0, 100.0, 120.0)


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)

        def case(name, r, vol, dc, exercise_type, option_type, strike, days, model, rho):
            r0, a, b, sigma, lam = model
            npv = v.attempt(lambda: H.call("AnalyticBlackVasicekEngine::NPV", 100.0, r, vol, R, dc, exercise_type,
                                           option_type, strike, R + days, r0, a, b, sigma, lam, rho))
            return {"name": name,
                    "inputs": {"settings": st, "spot": 100.0, "riskFreeRate": r, "volatility": vol,
                               "referenceDate": R, "dayCounter": dc, "exerciseType": exercise_type,
                               "optionType": option_type, "strike": strike, "exerciseDate": R + days,
                               "model": {"r0": r0, "a": a, "b": b, "sigma": sigma, "lambda": lam},
                               "correlation": rho},
                    "expected": {"NPV": npv}}

        for k, (r, vol, dc) in enumerate(MARKETS):
            for m, model in enumerate(MODELS):
                for rho in CORRELATIONS:
                    for days in DAYS:
                        for option_type in (1, -1):
                            for strike in STRIKES:
                                out.append(case(f"market {k}, model {m}, rho {rho}, {days} days, {option_type}, "
                                                f"K {strike}", r, vol, dc, "European", option_type, strike, days,
                                                model, rho))
        out.append(case("American exercise", 0.05, 0.25, A365, "American", 1, 100.0, 365, MODELS[0], 0.0))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/analyticeuropeanvasicekengine.hpp", cases)
