# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's greeks.hpp (ql/pricingengines/greeks.hpp, .cpp), each output computed by the C++ harness,
since the wheel exposes neither function: blackScholesTheta, the theta the Black-Scholes equation implies from a
value, delta and gamma, on Black-Scholes-Merton processes over flat forwards and a constant volatility (eight
processes: rates negative, zero and positive, two day counters, volatilities from 0 to 90%), at three (value, delta,
gamma) triples each; and defaultThetaPerDay at seven thetas.

Read back by tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/GreeksCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

A365 = {"type": "Actual365Fixed"}
A360 = {"type": "Actual360"}
PROCESSES = [(100.0, 0.04, 0.02, 0.2, A365), (100.0, 0.0, 0.0, 0.3, A365), (50.0, -0.01, 0.01, 0.15, A360),
             (120.0, 0.08, 0.0, 0.9, A365), (100.0, 0.03, 0.03, 0.0, A360), (1.2, 0.02, 0.035, 0.11, A365),
             (100.0, 0.15, 0.05, 0.45, A360), (80.0, 0.04, 0.02, 0.25, A365)]
TRIPLES = [(10.45, 0.637, 0.0188), (0.0, 0.0, 0.0), (3.2, -0.42, 0.031)]
THETAS = [0.0, -4.31, 12.5, -0.0855, 1e-12, -365.0, 1e6]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(46310)
        for k, (spot, r, q, vol, dc) in enumerate(PROCESSES):
            out.append({"name": f"blackScholesTheta {k}",
                        "inputs": {"settings": st, "spot": spot, "riskFreeRate": r, "dividendYield": q,
                                   "volatility": vol, "referenceDate": 46310, "dayCounter": dc,
                                   "triples": [list(t) for t in TRIPLES]},
                        "expected": {"theta": [H.call("blackScholesTheta", spot, r, q, vol, 46310, dc, *t)
                                               for t in TRIPLES]}})
        out.append({"name": "defaultThetaPerDay", "inputs": {"settings": st, "thetas": THETAS},
                    "expected": {"thetaPerDay": [H.call("defaultThetaPerDay", t) for t in THETAS]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/greeks.hpp", cases)
