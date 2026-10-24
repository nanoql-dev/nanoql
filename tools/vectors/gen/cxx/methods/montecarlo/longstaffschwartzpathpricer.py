# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LongstaffSchwartzPathPricer (ql/methods/montecarlo/longstaffschwartzpathpricer.hpp), which
the wheel doesn't expose, on QuantLib's AmericanPathPricer (ql/pricingengines/vanilla/mcamericanengine.hpp, .cpp), each
output computed by the C++ harness (docs/porting-playbook.md §9.4).

On fixed path sets, geometric Brownian paths drawn here with a fixed seed: a put and a call (a dividend yield making
its exercise worth it), on grids of four to eight steps, on the monomials, Laguerre, Hermite and hyperbolic
polynomials and the second-kind Chebyshev polynomials (on the put, whose regressions stay below a scaled spot of 1),
of orders 1 to 3, with fewer in-the-money paths than functions at some steps of the smallest set. Each case prices
its calibration paths (each stored, priced 0), calibrates, and prices its pricing paths: the outputs are the
calibration's values, the regression's coefficients per step, each call of the post-processing hook (the step, the
paths' states, values and exercise values), the pricing paths' values and the exercise probability. Every time of
these grids is an exercise time, where nanoql's pricer, which exercises only at the times given beside its grid, is
QuantLib's (tests/NanoQL.Tests/Divergences/LongstaffSchwartzExerciseTimesTests.cs).

A path is its values, one per time of the grid, which TimeGrid(begin, end) builds from the times; the curve a flat
forward from 15 October 2026 at the rate on Actual/365 (Fixed). The harness's method is
tools/cxx/src/methods/montecarlo/longstaffschwartzpathpricer.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/MonteCarlo/LongstaffSchwartzPathPricerCxxVectorTests.cs."""
import math
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

CALL, PUT = 1, -1
MONOMIAL, LAGUERRE, HERMITE, HYPERBOLIC, CHEBYSHEV2ND = 0, 1, 2, 3, 6


def paths(rnd, times, n, s0, r, q, vol):
    grid = [0.0] + times
    out = []
    for _ in range(n):
        s, p = s0, [s0]
        for k in range(1, len(grid)):
            dt = grid[k] - grid[k - 1]
            s *= math.exp((r - q - 0.5 * vol * vol) * dt + vol * math.sqrt(dt) * rnd.gauss(0.0, 1.0))
            p.append(s)
        out.append(p)
    return out


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        reference = H.call("Date(Day,Month,Year)", 15, 10, 2026)
        rnd = random.Random(v.SEED)
        out = []
        for name, times, option, strike, s0, r, q, vol, order, kind, nc, npr in [
                ("put, monomials", [0.25, 0.5, 0.75, 1.0], PUT, 40.0, 36.0, 0.06, 0.0, 0.2, 2, MONOMIAL, 300, 60),
                ("put, Laguerre", [0.2 * k for k in range(1, 6)], PUT, 100.0, 100.0, 0.05, 0.0, 0.3, 3, LAGUERRE, 300, 60),
                ("put, Hermite", [0.125 * k for k in range(1, 9)], PUT, 40.0, 40.0, 0.03, 0.01, 0.25, 2, HERMITE, 250, 50),
                ("put, hyperbolic", [0.5, 1.0, 1.5, 2.0], PUT, 110.0, 100.0, 0.04, 0.0, 0.2, 1, HYPERBOLIC, 200, 40),
                ("put, second-kind Chebyshev", [0.25, 0.5, 0.75, 1.0], PUT, 40.0, 36.0, 0.06, 0.0, 0.2, 2, CHEBYSHEV2ND,
                 300, 60),
                ("call with dividends", [0.25, 0.5, 0.75, 1.0], CALL, 100.0, 100.0, 0.02, 0.08, 0.25, 2, MONOMIAL, 300,
                 60),
                ("few paths", [0.25, 0.5, 0.75, 1.0], PUT, 30.0, 36.0, 0.06, 0.0, 0.2, 3, MONOMIAL, 8, 10)]:
            calibration = paths(rnd, times, nc, s0, r, q, vol)
            pricing = paths(rnd, times, npr, s0, r, q, vol)
            payoff = {"type": "PlainVanillaPayoff", "optionType": option, "strike": strike}
            res = H.call("LongstaffSchwartzPathPricer", times, payoff, order, kind, reference, r, calibration, pricing)
            hook = res["postProcessing"]
            out.append({"name": name,
                        "inputs": {"settings": st, "times": times, "payoff": payoff, "polynomialOrder": order,
                                   "polynomialType": kind, "referenceDate": reference, "rate": r,
                                   "calibrationPaths": calibration, "pricingPaths": pricing},
                        "expected": {"calibration": v.enc(res["calibration"]),
                                     "coefficients": v.enc(res["coefficients"]),
                                     "hookSteps": [h["i"] for h in hook],
                                     "hookStates": v.enc([h["state"] for h in hook]),
                                     "hookPrices": v.enc([h["price"] for h in hook]),
                                     "hookExercises": v.enc([h["exercise"] for h in hook]),
                                     "prices": v.enc(res["prices"]),
                                     "exerciseProbability": v.enc(res["exerciseProbability"])}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/montecarlo/longstaffschwartzpathpricer.hpp", cases)
