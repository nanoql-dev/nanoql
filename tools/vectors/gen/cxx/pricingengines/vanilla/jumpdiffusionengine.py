# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's JumpDiffusionEngine (ql/pricingengines/vanilla/jumpdiffusionengine.hpp, .cpp), which the
wheel doesn't expose, each output computed by the C++ harness: European calls and puts at strikes 80 to 120 on a spot
of 100, expiring in 30 days to three years, on Merton-76 processes over flat forwards and a constant volatility of
10% to 40% on Actual/365 (Fixed) and Actual/360, with jump intensities, mean log jumps and log jump volatilities from
QuantLib's own test and beyond, at QuantLib's default accuracy (1e-4, 100 iterations) and at 1e-8: the NPV, delta,
gamma, theta, vega, rho and dividend rho; on the evaluation date 15 October 2026. Then the refusal of too few
iterations for the accuracy.

The outputs nanoql fixes are left out: structures dated apart, the rate's reference date where QuantLib reads the
volatility's (tests/NanoQL.Tests/Divergences/JumpDiffusionReferenceDateTests.cs), which a single reference date for
every structure avoids; and the vega and theta at a zero volatility (JumpDiffusionZeroVolatilityTests.cs), which the
volatilities here never are. Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/JumpDiffusionEngineCxxVectorTests.cs."""
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
KEYS = ["NPV", "delta", "gamma", "theta", "vega", "rho", "dividendRho"]
MARKETS = [(0.02, 0.05, 0.25, A365), (0.0, 0.03, 0.1, A360), (0.04, 0.08, 0.4, A365)]
JUMPS = [(1.0, -0.1, 0.25), (0.5, 0.0, 0.1), (5.0, -0.2, 0.3), (0.0, 0.0, 0.0)]
ACCURACIES = [(1e-4, 100), (1e-8, 200)]
DAYS = (30, 365, 1095)
STRIKES = (80.0, 100.0, 120.0)


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)

        def case(name, market, jumps, option_type, strike, days, accuracy, iterations):
            q, r, vol, dc = market
            intensity, mean, jump_vol = jumps
            results = v.attempt(lambda: H.call("JumpDiffusionEngine::results", 100.0, q, r, vol, R, dc, intensity,
                                               mean, jump_vol, "European", option_type, strike, R + days, accuracy,
                                               iterations))
            expected = ({k: dict(v.THROWS) for k in KEYS} if "throws" in results
                        else {k: v.enc(results[k]) for k in KEYS})
            return {"name": name,
                    "inputs": {"settings": st, "spot": 100.0, "dividendYield": q, "riskFreeRate": r,
                               "volatility": vol, "referenceDate": R, "dayCounter": dc, "jumpIntensity": intensity,
                               "logMeanJump": mean, "logJumpVolatility": jump_vol, "optionType": option_type,
                               "strike": strike, "exerciseDate": R + days, "relativeAccuracy": accuracy,
                               "maxIterations": iterations},
                    "expected": expected}

        for k, market in enumerate(MARKETS):
            for j, jumps in enumerate(JUMPS):
                for accuracy, iterations in ACCURACIES:
                    for days in DAYS:
                        for option_type in (1, -1):
                            for strike in STRIKES:
                                out.append(case(f"market {k}, jumps {j}, accuracy {accuracy}, {days} days, "
                                                f"{option_type}, K {strike}", market, jumps, option_type, strike,
                                                days, accuracy, iterations))
        out.append(case("too few iterations", MARKETS[0], JUMPS[2], 1, 100.0, 365, 1e-12, 3))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/jumpdiffusionengine.hpp", cases)
