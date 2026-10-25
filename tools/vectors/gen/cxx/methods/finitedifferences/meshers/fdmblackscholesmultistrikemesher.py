# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmBlackScholesMultiStrikeMesher
(ql/methods/finitedifferences/meshers/fdmblackscholesmultistrikemesher.hpp, .cpp), which the wheel doesn't bind, each
output computed by the C++ harness (docs/porting-playbook.md §9.4), on the evaluation date 15 October 2026: the
log-spot mesher wide enough for every strike's forward, uniform or concentrated at a point.

On generalized Black-Scholes processes over flat forwards and a constant volatility (spot 100, rates 4% and 2%, and
spot 50 with rates 1% and 3%; volatilities 25% and 60%), at 51 and 100 points, maturities of a quarter, a year and five
years, and four strike sets (one strike, three about the spot, five spread wide, three out of order); then QuantLib's
epsilon 1e-4 against 1e-6, scale factors 1.5 and 2, and a concentration point inside the grid and outside it (where the
mesher stays uniform). Outputs: locations, dplus and dminus (QuantLib's null at the ends as null).

Left out: an empty strike list, which QuantLib reads past (std::min_element of nothing) and nanoql refuses.

A case's inputs are the harness call's arguments by name: "size", "spot", "riskFreeRate", "dividendYield",
"volatility", "referenceDate", "dayCounter", "maturity", "strikes", "eps", "scaleFactor" and "cPoint" ([point,
density] or null). The harness's call: FdmBlackScholesMultiStrikeMesher::outputs
(tools/cxx/src/methods/finitedifferences/meshers/fdmblackscholesmultistrikemesher.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/FiniteDifferences/Meshers/FdmBlackScholesMultiStrikeMesherCxxVectorTests.cs."""
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
MARKETS = [("spot 100", 100.0, 0.04, 0.02, 0.25), ("spot 50, high volatility", 50.0, 0.01, 0.03, 0.6)]
STRIKE_SETS = [("one strike", [1.0]), ("three about the spot", [0.8, 1.0, 1.2]),
               ("five spread wide", [0.5, 0.9, 1.0, 1.1, 2.0]), ("out of order", [1.2, 0.8, 1.0])]
KEYS = ["locations", "dplus", "dminus"]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)

        def add(name, size, market, maturity, strikes, eps=1e-4, scale=1.5, c_point=None):
            _, spot, r, q, vol = market
            inputs = {"settings": st, "size": size, "spot": spot, "riskFreeRate": r, "dividendYield": q,
                      "volatility": vol, "referenceDate": R, "dayCounter": A365, "maturity": maturity,
                      "strikes": strikes, "eps": eps, "scaleFactor": scale, "cPoint": c_point}
            result = H.call("FdmBlackScholesMultiStrikeMesher::outputs", size, spot, r, q, vol, R, A365, maturity,
                            strikes, eps, scale, c_point)
            out.append({"name": name, "inputs": inputs, "expected": {k: v.enc(result[k]) for k in KEYS}})

        for market in MARKETS:
            for size in (51, 100):
                for maturity in (0.25, 1.0, 5.0):
                    for sname, ks in STRIKE_SETS:
                        strikes = [round(k * market[1], 8) for k in ks]
                        add(f"{market[0]}, {size} points, {maturity:g} years, {sname}", size, market, maturity,
                            strikes)
        market = MARKETS[0]
        strikes = [80.0, 100.0, 120.0]
        add("epsilon 1e-6", 51, market, 1.0, strikes, eps=1e-6)
        add("scale factor 2", 51, market, 1.0, strikes, scale=2.0)
        add("concentrated at 100", 51, market, 1.0, strikes, c_point=[100.0, 0.1])
        add("concentrated at 90, density 0.01", 100, market, 0.25, strikes, c_point=[90.0, 0.01])
        add("concentration point outside the grid", 51, market, 1.0, strikes, c_point=[1.0e6, 0.1])
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/meshers/fdmblackscholesmultistrikemesher.hpp", cases)
