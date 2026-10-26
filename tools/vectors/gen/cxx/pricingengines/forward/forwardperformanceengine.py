# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ForwardPerformanceVanillaEngine (ql/pricingengines/forward/forwardperformanceengine.hpp,
over AnalyticEuropeanEngine) through the C++ harness, which the wheel doesn't expose: forward-start performance calls
and puts at moneyness 0.9, 1 and 1.1, reset today, in three months or in six and expiring in six months, a year or
two, on three flat markets under constant volatilities: the NPV, delta, gamma, theta, vega, rho and dividend rho.

Left out (the S-exo plan's §2.10): volatilities off flat (tests/NanoQL.Tests/Divergences/
ForwardVegaTermStructureTests.cs), non-plain payoffs (ForwardNonPlainPayoffTests.cs), and structures dated or counted
apart (ExoticMismatchedTimesTests.cs).

A case holds the "process" ([spot, r, q, volatility], flat Act/365 (Fixed) forwards and a constant volatility from the
evaluation date) and the "option" ([moneyness, resetDate, optionType, exerciseDate]). The harness's call:
ForwardPerformanceVanillaEngine::results (tools/cxx/src/pricingengines/forward/forwardperformanceengine.cpp). Read back
by tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/Forward/ForwardPerformanceVanillaEngineCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
CALL, PUT = 1, -1
MARKETS = [
    ("spot 100, r 4%, q 2%, vol 22%", [100.0, 0.04, 0.02, 0.22]),
    ("spot 80, r 6%, q 0, vol 45%", [80.0, 0.06, 0.0, 0.45]),
    ("spot 120, r 1%, q 3%, vol 15%", [120.0, 0.01, 0.03, 0.15]),
]
PERIODS = [(0, 182), (91, 365), (182, 730)]
KEYS = ("NPV", "delta", "gamma", "theta", "vega", "rho", "dividendRho")


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for mname, process in MARKETS:
            for reset, expiry in PERIODS:
                for option_type in (CALL, PUT):
                    for moneyness in (0.9, 1.0, 1.1):
                        option = [moneyness, R + reset, option_type, R + expiry]
                        r = H.call("ForwardPerformanceVanillaEngine::results", process, option)
                        out.append({"name": f"{mname}, reset {reset}, expiry {expiry}, "
                                            f"{'call' if option_type == CALL else 'put'}, moneyness {moneyness:g}",
                                    "inputs": {"settings": st, "process": process, "option": option},
                                    "expected": {k: v.enc(r[k]) for k in KEYS}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/forward/forwardperformanceengine.hpp", cases)
