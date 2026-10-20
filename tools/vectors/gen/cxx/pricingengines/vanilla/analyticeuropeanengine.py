# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticEuropeanEngine (ql/pricingengines/vanilla/analyticeuropeanengine.hpp, .cpp), each
output computed by the C++ harness: the additional results the wheel doesn't expose, "spot", "dividendDiscount",
"riskFreeDiscount", "forward", "strike", "volatility" and "timeToExpiry", of European plain vanilla options on
Black-Scholes-Merton processes over flat forwards and a constant volatility, with and without a separate discount
curve: eight processes, both option types, two strikes and three exercise dates, on the evaluation date 15 October
2026. An option expiring on the evaluation date (live with the reference date's events included) is left out: its
"volatility" is QuantLib's 0/0, where nanoql reports the structure's volatility
(tests/NanoQL.Tests/Divergences/AnalyticEuropeanVolatilityResultTests.cs).

Read back by tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/AnalyticEuropeanEngineCxxVectorTests.cs."""
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
PROCESSES = [(100.0, 0.04, 0.02, 0.2, A365, None), (100.0, 0.0, 0.0, 0.3, A365, None),
             (50.0, -0.01, 0.01, 0.15, A360, None), (120.0, 0.08, 0.0, 0.9, A365, 0.05),
             (100.0, 0.03, 0.03, 0.0, A360, None), (1.2, 0.02, 0.035, 0.11, A365, 0.01),
             (100.0, 0.15, 0.05, 0.45, A360, 0.1), (80.0, 0.04, 0.02, 0.25, A365, None)]
TYPES = [1, -1]
STRIKE_RATIOS = [0.9, 1.15]
EXPIRIES = [1, 182, 1095]
KEYS = ["spot", "dividendDiscount", "riskFreeDiscount", "forward", "strike", "volatility", "timeToExpiry"]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for k, (spot, r, q, vol, dc, discount) in enumerate(PROCESSES):
            for option_type in TYPES:
                for ratio in STRIKE_RATIOS:
                    for days in EXPIRIES:
                        strike = spot * ratio
                        results = H.call("AnalyticEuropeanEngine::additionalResults", spot, r, q, vol, R, dc,
                                         option_type, strike, R + days, discount)
                        out.append({"name": f"process {k}, {option_type}, K {strike}, {days} days",
                                    "inputs": {"settings": st, "spot": spot, "riskFreeRate": r, "dividendYield": q,
                                               "volatility": vol, "referenceDate": R, "dayCounter": dc,
                                               "optionType": option_type, "strike": strike,
                                               "exerciseDate": R + days, "discountRate": discount},
                                    "expected": {key: results[key] for key in KEYS}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/analyticeuropeanengine.hpp", cases)
