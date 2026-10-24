# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's QdPlusAmericanEngine's exercise boundary members (ql/pricingengines/vanilla/
qdplusamericanengine.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness: the put's
boundary at times to expiry from 0 to the maturity (with the solver's evaluations), and the Chebyshev interpolation of
the boundary at 21 points of [-1, 1], for puts on markets with positive, zero and negative rates and dividend yields
and volatilities of 5% to 60% over a month to five years, by the engine's defaults and by each other solver; and the
limit xMax for the signs of r and q, including its refusal of a NaN rate.

A put's market is (S, K, r, q, vol, T), an engine (interpolationPoints, solverType as QuantLib's integer, eps, maxIter
or null). Read back by tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/QdPlusAmericanEngineCxxVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
BRENT, NEWTON, RIDDER, HALLEY, SUPER_HALLEY = range(5)
MARKETS = [(100.0, 100.0, 0.05, 0.02, 0.25, 1.0), (100.0, 110.0, 0.08, 0.0, 0.1, 5.0),
           (80.0, 100.0, 0.03, 0.06, 0.6, 0.5), (100.0, 90.0, 0.1, 0.1, 0.05, 2.0),
           (100.0, 100.0, 0.02, -0.01, 0.3, 1.0 / 12), (100.0, 100.0, 0.0, -0.02, 0.2, 1.0)]
ENGINES = [("defaults", [8, HALLEY, 1e-6, None]), ("Brent", [8, BRENT, 1e-6, None]),
           ("Newton", [8, NEWTON, 1e-6, None]), ("Ridder", [8, RIDDER, 1e-6, None]),
           ("super-Halley", [8, SUPER_HALLEY, 1e-6, None]), ("16 nodes, 1e-10", [16, HALLEY, 1e-10, None])]
POINTS = [-1.0 + 0.1 * i for i in range(21)]
XMAX = [(100.0, r, q) for r, q in [(0.05, 0.02), (0.02, 0.05), (0.05, 0.0), (0.05, -0.01), (0.0, -0.01), (0.0, 0.0),
                                    (0.0, 0.02), (-0.01, -0.02), (-0.02, -0.01), (-0.01, 0.02), (math.nan, 0.02)]]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for k, market in enumerate(MARKETS):
            T = market[5]
            taus = [0.0, 1e-9, 0.01 * T, 0.25 * T, 0.5 * T, T]
            for name, engine in ENGINES:
                out.append({"name": f"market {k}, {name}",
                            "inputs": {"settings": st, "market": list(market), "engine": engine, "taus": taus,
                                       "points": POINTS},
                            "expected": {
                                "atTau": [v.attempt(lambda: H.call("QdPlusAmericanEngine::putExerciseBoundaryAtTau",
                                                                   *market, tau, *engine)) for tau in taus],
                                "boundary": v.attempt(lambda: H.call("QdPlusAmericanEngine::getPutExerciseBoundary",
                                                                     *market, *engine, POINTS))}})
        out.append({"name": "xMax", "inputs": {"settings": st, "xMax": [[v.enc(x) for x in row] for row in XMAX]},
                    "expected": {"xMax": [v.attempt(lambda: H.call("QdPlusAmericanEngine::xMax", *row))
                                          for row in XMAX]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/qdplusamericanengine.hpp", cases)
