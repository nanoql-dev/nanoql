# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BlackCalculator (ql/pricingengines/blackcalculator.hpp, .cpp): the Black formula's value
and sensitivities for plain vanilla, cash-or-nothing, asset-or-nothing and gap payoffs.

Calculators over the four payoffs, both option types, five strikes (zero among them), three forwards, six standard
deviations (zero and QL_EPSILON, the zero-volatility branches, among them) and a discount, at a spot below the
forward and three maturities: the value, alpha and beta, the forward delta, gamma and elasticity, the delta, gamma and
elasticity at the spot, the theta and theta per day, the vega, rho and dividend rho, the strike sensitivity and strike
gamma, the in-the-money probabilities, the vanna and the volga.

The outputs nanoql fixes are left out (the S-eq plan's SEQR7; each a tests/NanoQL.Tests/Divergences/ test): at zero
volatility, the delta, forward delta, strike sensitivity, elasticity, forward elasticity, theta, theta per day, rho
and dividend rho of every payoff but a plain vanilla call (BlackCalculatorZeroVolatilityTests); the in-the-money probabilities of puts
(BlackCalculatorItmProbabilityTests); the vanna and volga of payoffs other than the plain vanilla, and its volga at a
maturity other than 1, where QuantLib's is the true volga over the square root of the maturity
(BlackCalculatorVannaVolgaTests). Then the refusals: a negative strike, a forward and a discount at zero, a negative
standard deviation, a spot at zero for the delta, gamma and vanna, a negative maturity for the theta, vega, rho,
dividend rho and volga, and a payoff the calculator doesn't support (percentage-strike).

A payoff is {"type": "PlainVanilla" | "CashOrNothing" (with "cashPayoff") | "AssetOrNothing" | "Gap" (with
"secondStrike") | "PercentageStrike" (with "moneyness"),
"optionType", "strike"}. The wheel's calls, by QuantLib-SWIG 1.44's blackformula.i: BlackCalculator(payoff,
forward, stdDev, discount), the C++ constructor taking a payoff (the wheel binds no other), and every public member
under its C++ name. Read back by tests/NanoQL.Tests/Vectors/PricingEngines/BlackCalculatorVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

EPS = 2.220446049250313e-16
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)


def payoff_spec(kind, option_type, strike):
    spec = {"type": kind, "optionType": option_type, "strike": strike}
    if kind == "CashOrNothing":
        spec["cashPayoff"] = 10.0
    elif kind == "Gap":
        spec["secondStrike"] = strike * 0.95
    elif kind == "PercentageStrike":
        spec["moneyness"] = strike / 100.0
    return spec


def payoff(spec):
    t, o, k = spec["type"], spec["optionType"], spec["strike"]
    if t == "PlainVanilla":
        return ql.PlainVanillaPayoff(o, k)
    if t == "CashOrNothing":
        return ql.CashOrNothingPayoff(o, k, spec["cashPayoff"])
    if t == "AssetOrNothing":
        return ql.AssetOrNothingPayoff(o, k)
    if t == "Gap":
        return ql.GapPayoff(o, k, spec["secondStrike"])
    if t == "PercentageStrike":
        return ql.PercentageStrikePayoff(o, spec["moneyness"])
    raise ValueError(f"unknown payoff {t}")


KINDS = ["PlainVanilla", "CashOrNothing", "AssetOrNothing", "Gap"]
STRIKES = [0.0, 80.0, 100.0, 104.0, 125.0]
FORWARDS = [80.0, 104.0, 130.0]
STD_DEVS = [0.0, EPS, 1e-9, 0.05, 0.3, 1.5]
DISCOUNTS = [0.93]
SPOT_RATIO = 0.97
MATURITIES = [0.25, 1.0, 4.0]


def cases():
    out = []
    st = v.settings(ql.Date(15, ql.October, 2026))
    for kind in KINDS:
        for option_type in (CALL, PUT):
            for strike in STRIKES:
                for forward in FORWARDS:
                    for std_dev in STD_DEVS:
                        for discount in DISCOUNTS:
                            spec = payoff_spec(kind, option_type, strike)
                            c = ql.BlackCalculator(payoff(spec), forward, std_dev, discount)
                            spot = forward * SPOT_RATIO
                            zero_vol = std_dev <= EPS
                            plain = kind == "PlainVanilla"
                            fixed_zero = zero_vol and not (plain and option_type == CALL)
                            e = {
                                "value": c.value(),
                                "alpha": c.alpha(),
                                "beta": c.beta(),
                                "gammaForward": c.gammaForward(),
                                "gamma": c.gamma(spot),
                                "vega": [c.vega(t) for t in MATURITIES],
                                "strikeGamma": c.strikeGamma(),
                            }
                            if not fixed_zero:
                                e.update({
                                    "deltaForward": c.deltaForward(),
                                    "elasticityForward": c.elasticityForward(),
                                    "delta": c.delta(spot),
                                    "elasticity": c.elasticity(spot),
                                    "theta": [c.theta(spot, t) for t in MATURITIES],
                                    "thetaPerDay": [c.thetaPerDay(spot, t) for t in MATURITIES],
                                    "rho": [c.rho(t) for t in MATURITIES],
                                    "dividendRho": [c.dividendRho(t) for t in MATURITIES],
                                    "strikeSensitivity": c.strikeSensitivity(),
                                })
                            if option_type == CALL:
                                e.update({"itmCashProbability": c.itmCashProbability(),
                                          "itmAssetProbability": c.itmAssetProbability()})
                            if plain or zero_vol:
                                e["vanna"] = [c.vanna(spot, t) for t in MATURITIES]
                                e["volga"] = c.volga(1.0)
                            out.append({
                                "name": f"{kind} {option_type} K {strike} F {forward} sd {std_dev!r} df {discount}",
                                "inputs": {"settings": st, "payoff": spec, "forward": forward, "stdDev": std_dev,
                                           "discount": discount, "spot": spot, "maturities": MATURITIES},
                                "expected": v.enc(e)})
    good = payoff_spec("PlainVanilla", CALL, 100.0)
    refusals = [
        ("negative strike", payoff_spec("PlainVanilla", CALL, -1.0), 100.0, 0.2, 1.0),
        ("zero forward", good, 0.0, 0.2, 1.0),
        ("negative standard deviation", good, 100.0, -0.1, 1.0),
        ("zero discount", good, 100.0, 0.2, 0.0),
        ("percentage-strike payoff", payoff_spec("PercentageStrike", CALL, 100.0), 100.0, 0.2, 1.0),
    ]
    for name, spec, forward, std_dev, discount in refusals:
        out.append({"name": name, "inputs": {"settings": st, "payoff": spec, "forward": forward, "stdDev": std_dev,
                                             "discount": discount},
                    "expected": {"value": v.attempt(lambda: ql.BlackCalculator(payoff(spec), forward, std_dev,
                                                                               discount).value())}})
    c = ql.BlackCalculator(ql.PlainVanillaPayoff(ql.Option.Call, 100.0), 104.0, 0.3, 0.95)
    out.append({"name": "argument refusals", "inputs": {"settings": st, "payoff": good, "forward": 104.0,
                                                        "stdDev": 0.3, "discount": 0.95},
                "expected": {
                    "deltaAtZeroSpot": v.attempt(lambda: c.delta(0.0)),
                    "gammaAtZeroSpot": v.attempt(lambda: c.gamma(0.0)),
                    "vannaAtZeroSpot": v.attempt(lambda: c.vanna(0.0, 1.0)),
                    "thetaAtNegativeMaturity": v.attempt(lambda: c.theta(100.0, -0.1)),
                    "vegaAtNegativeMaturity": v.attempt(lambda: c.vega(-0.1)),
                    "rhoAtNegativeMaturity": v.attempt(lambda: c.rho(-0.1)),
                    "dividendRhoAtNegativeMaturity": v.attempt(lambda: c.dividendRho(-0.1)),
                    "vannaAtNegativeMaturity": v.attempt(lambda: c.vanna(100.0, -0.1)),
                    "volgaAtNegativeMaturity": v.attempt(lambda: c.volga(-0.1))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/blackcalculator.hpp", cases)
