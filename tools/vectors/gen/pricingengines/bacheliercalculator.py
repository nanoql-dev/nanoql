# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BachelierCalculator (ql/pricingengines/bacheliercalculator.hpp, .cpp): plain-vanilla calls
and puts under the normal model, on the evaluation date 15 October 2026.

Forwards of 100 and of 0.02 (a rate), the second with strikes below zero, at moneyness from deep out of the money to
deep in it, at deviations from 0.4% to 30% of the level, discounted by 1 and by 0.95: the value, the forward Greeks
(delta, gamma, elasticity), the spot Greeks at a spot apart from the forward (delta, gamma, elasticity), vega, rho and
the dividend rho at a maturity, the in-the-money probabilities, the strike sensitivity and gamma, alpha and beta. Every
case keeps |F - K| / deviation below 7, since where N(d) rounds to 1 QuantLib prices a put as the call
(tests/NanoQL.Tests/Divergences/BachelierPutBranchTests.cs); theta, vanna and volga are left out, nanoql's formulas
fixing each (BachelierGreeksTests.cs), and so are the non-vanilla payoffs (BachelierPayoffTests.cs). Then QuantLib's
checks: a negative deviation, a discount of zero, and a negative maturity in vega, rho and the dividend rho.

A case holds one calculator {"type" (QuantLib's Call 1, Put -1), "strike", "forward", "stdDev", "discount"}, the spot
and the maturity the Greeks take. The wheel's calls, by QuantLib-SWIG 1.44's bacheliercalculator.i:
BachelierCalculator(PlainVanillaPayoff(type, strike), forward, stdDev, discount) and its accessors. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/BachelierCalculatorVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

R = ql.Date(15, ql.October, 2026)

# (name, forward, strikes, deviations, the spot apart from the forward, maturity)
LEVELS = [
    ("level 100", 100.0, [60.0, 85.0, 95.0, 100.0, 105.0, 115.0, 140.0], [0.4, 5.0, 12.0, 30.0], 98.0, 1.5),
    ("rate 2%", 0.02, [-0.01, 0.0, 0.015, 0.02, 0.025, 0.04, 0.06], [0.00008, 0.004, 0.01, 0.02], 0.019, 2.0),
]
DISCOUNTS = [1.0, 0.95]
OUTPUTS = ["value", "deltaForward", "gammaForward", "elasticityForward", "itmCashProbability", "itmAssetProbability",
           "strikeSensitivity", "strikeGamma", "alpha", "beta"]
SPOT_OUTPUTS = ["delta", "gamma", "elasticity"]
MATURITY_OUTPUTS = ["vega", "rho", "dividendRho"]


def spec(option_type, strike, forward, std_dev, discount, spot, maturity):
    return {"type": int(option_type), "strike": strike, "forward": forward, "stdDev": std_dev, "discount": discount,
            "spot": spot, "maturity": maturity}


def calculator(s):
    return ql.BachelierCalculator(ql.PlainVanillaPayoff(s["type"], s["strike"]), s["forward"], s["stdDev"],
                                  s["discount"])


def expected(s):
    try:
        c = calculator(s)
    except RuntimeError:
        return {name: dict(v.THROWS) for name in OUTPUTS + SPOT_OUTPUTS + MATURITY_OUTPUTS}
    out = {name: getattr(c, name)() for name in OUTPUTS}
    out.update({name: getattr(c, name)(s["spot"]) for name in SPOT_OUTPUTS})
    out.update({name: v.attempt(lambda name=name: getattr(c, name)(s["maturity"])) for name in MATURITY_OUTPUTS})
    return out


def cases():
    st = v.settings(R)
    out = []
    for lname, forward, strikes, deviations, spot, maturity in LEVELS:
        for option_type, tname in [(ql.Option.Call, "call"), (ql.Option.Put, "put")]:
            for strike in strikes:
                for dev in deviations:
                    if abs(forward - strike) / dev >= 7.0:
                        continue
                    for discount in DISCOUNTS:
                        s = spec(option_type, strike, forward, dev, discount, spot, maturity)
                        out.append({"name": f"{lname}, {tname} {strike}, deviation {dev}, discount {discount}",
                                    "inputs": {"settings": st, "calculator": s}, "expected": expected(s)})
    for name, s in [("a negative deviation", spec(ql.Option.Call, 100.0, 100.0, -1.0, 1.0, 98.0, 1.0)),
                    ("a discount of zero", spec(ql.Option.Put, 100.0, 100.0, 5.0, 0.0, 98.0, 1.0)),
                    ("a negative maturity", spec(ql.Option.Call, 100.0, 100.0, 5.0, 0.95, 98.0, -0.5))]:
        out.append({"name": name, "inputs": {"settings": st, "calculator": s}, "expected": expected(s)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/bacheliercalculator.hpp", cases)
