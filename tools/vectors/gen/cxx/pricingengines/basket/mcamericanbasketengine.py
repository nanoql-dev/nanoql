# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AmericanBasketPathPricer and MakeMCAmericanBasketEngine
(ql/pricingengines/basket/mcamericanbasketengine.hpp, .cpp), which the wheel doesn't expose, each output computed by
the C++ harness (docs/porting-playbook.md §9.4), on the evaluation date 15 October 2026.

- The path pricer: on fixed two- and three-asset paths on a grid of two steps, at each grid index the state (the
  assets' values over the strike), the exercise value and every basis function's value at the state; on minimum,
  maximum, average and spread baskets of plain vanilla calls and puts at positive strikes, the monomial basis of order
  2, and on a maximum call every basis family the pricer takes (monomial, Laguerre, Hermite, hyperbolic and
  second-kind Chebyshev) at orders 1 to 3; then its refusals, their messages QuantLib's: the Legendre and first-kind
  Chebyshev families, a payoff that isn't a basket's, and a multi-path of another asset count.
- The builder's and the engine's refusals, their messages QuantLib's: steps after steps per year and the reverse, no
  steps, samples after a tolerance and the reverse, a tolerance under low-discrepancy numbers, a European exercise, an
  American one paying at expiry, and the Legendre family.

Left out: strikes at or below zero, where nanoql scales the state by 1/|K| and by 1 at K = 0
(tests/NanoQL.Tests/Divergences/AmericanBasketStrikeScalingTests.cs); and the builder's prices, which read the
Longstaff-Schwartz calibration seed, QuantLib's the narrowed null (4294967295 through C++) where nanoql's is the seed
plus QuantLib's offset: mcamericanbasketengine.json's wheel vectors price the engine with the calibration seed given.

A path case holds the "assetNumber", "payoff" ([kind, optionType, strike, weights or null]), "polynomialOrder",
"polynomialType" (the enum's integer), "times" and "paths" (one list of values per asset, each of the grid's size); a
builder case the "traits", "processes" ([spot, r, q, volatility] each), "correlation", "exercise" ([kind, date]),
"payoff" and "setters" ([name, value]). The harness's calls: AmericanBasketPathPricer::values and
MakeMCAmericanBasketEngine::NPV (tools/cxx/src/pricingengines/basket/mcamericanbasketengine.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/Basket/MCAmericanBasketEngineCxxVectorTests.cs."""
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
MONOMIAL, LAGUERRE, HERMITE, HYPERBOLIC, LEGENDRE, CHEBYSHEV, CHEBYSHEV2ND = range(7)
TIMES = [0.0, 0.5, 1.0]
TWO = [[100.0, 108.0, 95.0], [90.0, 97.5, 120.0]]
THREE = TWO + [[110.0, 104.0, 101.0]]

PAYOFFS = [
    ("maximum call", 2, ["Max", CALL, 100.0, None], TWO),
    ("minimum put", 3, ["Min", PUT, 105.0, None], THREE),
    ("average call", 3, ["Average", CALL, 100.0, [0.5, 0.3, 0.2]], THREE),
    ("average put", 2, ["Average", PUT, 100.0, [0.6, 0.4]], TWO),
    ("spread call", 2, ["Spread", CALL, 5.0, None], TWO),
    ("spread put", 2, ["Spread", PUT, 12.0, None], TWO),
]
FAMILIES = [("monomial", MONOMIAL), ("Laguerre", LAGUERRE), ("Hermite", HERMITE), ("hyperbolic", HYPERBOLIC),
            ("second-kind Chebyshev", CHEBYSHEV2ND)]
PATH_REFUSALS = [
    ("the Legendre family", 2, ["Max", CALL, 100.0, None], 2, LEGENDRE, TWO),
    ("the first-kind Chebyshev family", 2, ["Max", CALL, 100.0, None], 2, CHEBYSHEV, TWO),
    ("a payoff that isn't a basket's", 2, ["Plain", CALL, 100.0, None], 2, MONOMIAL, TWO),
    ("a multi-path of three assets", 2, ["Max", CALL, 100.0, None], 2, MONOMIAL, THREE),
]

PROCESSES = [[100.0, 0.05, 0.02, 0.2], [95.0, 0.05, 0.01, 0.3]]
AMERICAN = ["American", R + 365]
MAX_CALL = ["Max", CALL, 100.0, None]
BUILDER_REFUSALS = [
    ("PseudoRandom", "steps after steps per year", AMERICAN,
     [["withSeed", 1], ["withStepsPerYear", 4], ["withSteps", 4], ["withSamples", 16]]),
    ("PseudoRandom", "steps per year after steps", AMERICAN,
     [["withSeed", 1], ["withSteps", 4], ["withStepsPerYear", 4], ["withSamples", 16]]),
    ("PseudoRandom", "no steps", AMERICAN, [["withSeed", 1], ["withSamples", 16]]),
    ("PseudoRandom", "samples after a tolerance", AMERICAN,
     [["withSeed", 1], ["withSteps", 4], ["withAbsoluteTolerance", 0.1], ["withSamples", 16]]),
    ("PseudoRandom", "a tolerance after samples", AMERICAN,
     [["withSeed", 1], ["withSteps", 4], ["withSamples", 16], ["withAbsoluteTolerance", 0.1]]),
    ("LowDiscrepancy", "a tolerance", AMERICAN, [["withSeed", 1], ["withSteps", 4], ["withAbsoluteTolerance", 0.1]]),
    ("PseudoRandom", "a European exercise", ["European", R + 365],
     [["withSeed", 1], ["withSteps", 2], ["withSamples", 16], ["withCalibrationSamples", 16]]),
    ("PseudoRandom", "an American exercise paying at expiry", ["AmericanPayoffAtExpiry", R + 365],
     [["withSeed", 1], ["withSteps", 2], ["withSamples", 16], ["withCalibrationSamples", 16]]),
    ("PseudoRandom", "the Legendre family", AMERICAN,
     [["withSeed", 1], ["withSteps", 2], ["withSamples", 16], ["withCalibrationSamples", 16],
      ["withBasisSystem", LEGENDRE]]),
]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)

        def path(name, n, payoff, order, family, paths, refused=False):
            try:
                r = H.call("AmericanBasketPathPricer::values", n, payoff, order, family, TIMES, paths)
                expected = {k: v.enc(r[k]) for k in ("states", "values", "basis")}
                assert not refused, name
            except RuntimeError as e:
                assert refused, (name, e)
                expected = {"error": str(e)}
            out.append({"name": f"path pricer, {name}",
                        "inputs": {"settings": st, "kind": "path", "assetNumber": n, "payoff": payoff,
                                   "polynomialOrder": order, "polynomialType": family, "times": TIMES,
                                   "paths": paths},
                        "expected": expected})

        for name, n, payoff, paths in PAYOFFS:
            path(f"{name}, monomial order 2", n, payoff, 2, MONOMIAL, paths)
        for fname, family in FAMILIES:
            for order in (1, 2, 3):
                path(f"maximum call, {fname} order {order}", 2, MAX_CALL, order, family, TWO)
        for name, n, payoff, order, family, paths in PATH_REFUSALS:
            path(f"refused: {name}", n, payoff, order, family, paths, refused=True)
        for traits, name, exercise, setters in BUILDER_REFUSALS:
            try:
                H.call("MakeMCAmericanBasketEngine::NPV", traits, PROCESSES, 0.3, exercise, MAX_CALL, setters)
                raise AssertionError(name)
            except RuntimeError as e:
                expected = {"error": str(e)}
            out.append({"name": f"builder, {traits}, refused: {name}",
                        "inputs": {"settings": st, "kind": "builder", "traits": traits, "processes": PROCESSES,
                                   "correlation": 0.3, "exercise": exercise, "payoff": MAX_CALL,
                                   "setters": setters},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/basket/mcamericanbasketengine.hpp", cases)
