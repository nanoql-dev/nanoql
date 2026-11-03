# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the two option formulas of ql/pricingengines/blackformula.hpp that nanoql ports, blackFormula and
bachelierBlackFormula on an option type, and for checkParameters' refusals, which blackFormula runs first.

- "black" cases: for each option type, forward (5, from 0.002 to 100), displacement (0 and 0.01) and discount (1 and
  0.95), blackFormula at 5 standard deviations (0, the intrinsic-value branch, to 5) x 10 strikes, as
  [stdDev][strike]: a strike of 0, which gives the call's discounted forward with no displacement; a strike of
  -0.005, which checkParameters refuses without the displacement; and strikes from 1e-4 to 120.
- "bachelier" cases: bachelierBlackFormula, which has no displacement, for each option type, forward and discount, at 5
  normal standard deviations x the same 10 strikes.
- "arguments" cases: blackFormula or bachelierBlackFormula on lists of arguments, [type, strike, forward, stdDev,
  discount, displacement] (no displacement for Bachelier): each of checkParameters' refusals (a negative or NaN
  displacement; strike + displacement negative or NaN; forward + displacement not positive or NaN), the standard
  deviation's and the discount's (negative, zero for the discount, NaN), and infinite inputs, where an infinite
  forward gives an infinite price and an infinite strike or standard deviation a NaN result, which QL_ENSURE refuses.

Then the other functions the wheel wraps, in cases after these, so that the cases above keep their bytes:
- "implied" cases: for each option type, forward, displacement and discount, the prices blackFormula gives at 3
  standard deviations (1e-3, 0.2 and 1) x the 10 strikes ("prices", NaN where blackFormula refuses), and
  blackFormulaImpliedStdDev and blackFormulaImpliedStdDevLiRS of each, without a guess (the wheel passes
  Null<Real>() only by omission, so with their default accuracies and iterations); with the discount 0.95, also both
  with a guess, an accuracy and a number of iterations (and LiRS with an omega), LiRS on its payoff form.
- "itm" cases: for each option type, forward and displacement, blackFormulaCashItmProbability and
  blackFormulaAssetItmProbability at 3 standard deviations (0, 0.2 and 1) x the 10 strikes; on the forward 1, also
  their payoff forms.
- "bachelierImplied" cases: for each option type, forward and discount, the prices bachelierBlackFormula gives at 3
  normal standard deviations (1e-4, 0.005 and 0.02) x the 10 strikes over a time to expiry of 2, and
  bachelierBlackFormulaImpliedVolChoi and bachelierBlackFormulaImpliedVol of each. The case's "skip" input lists the
  [stdDev, strike] entries where QuantLib 1.44's exact inversion returns 0 on a positive time value, its price close to
  the intrinsic value, which QuantLib's master inverts (#2924, taken ahead of the pin); each is null in "impliedVol",
  and the reader nulls the same entries, its fixed value checked in
  tests/NanoQL.Tests/Divergences/BlackFormulaUpstreamFixesTests.cs.
- "bachelierItm" cases: for each option type and forward, bachelierBlackFormulaAssetItmProbability at 3 normal
  standard deviations (0, 0.005 and 0.02) x the 10 strikes; on the forward 1, also its payoff form.
- argument cases for the same functions: lists of arguments with their refusals (each parameter check, the put-call
  parity check, a guess outside the solver's [0, 24], no accuracy, too few iterations, the in-out duality's negative
  price, an inverse normal of a NaN probability) and edge values (a zero strike, a price at intrinsic value, infinite
  inputs, a zero or negative time and discount the Bachelier inversions don't check, and strikes so close to the
  forward that Choi's moneyness ratio takes Boost's series for atanh). bachelierBlackFormulaImpliedVol's case lists in
  "skip" the arguments #2924 moves, as the "bachelierImplied" cases do: an infinite price after the division by the
  discount, which QuantLib 1.44 finds close to its intrinsic value and returns 0 for, and QuantLib's master inverts.

A refused value is {"throws": true}. The wheel's functions are the proxies of the C++ functions of the same names,
with no logic of their own. Read back by tests/NanoQL.Tests/Vectors/PricingEngines/BlackFormulaVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

INF = float("inf")
NAN = float("nan")
TYPES = [("Call", ql.Option.Call), ("Put", ql.Option.Put)]
FORWARDS = [0.002, 0.025, 0.05, 1.0, 100.0]
STRIKES = [0.0, -0.005, 1e-4, 0.01, 0.025, 0.05, 0.075, 0.2, 1.0, 120.0]
STDDEVS = [0.0, 1e-3, 0.2, 1.0, 5.0]
NORMAL_STDDEVS = [0.0, 1e-4, 0.005, 0.02, 0.5]
DISPLACEMENTS = [0.0, 0.01]
DISCOUNTS = [1.0, 0.95]
QL_EPSILON = 2.220446049250313e-16

CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
# [type, strike, forward, stdDev, discount, displacement]
BLACK_ARGUMENTS = [
    [CALL, 0.03, 0.05, 0.2, 0.9, -0.01], [PUT, 0.03, 0.05, 0.2, 0.9, NAN], [CALL, -0.03, 0.05, 0.2, 0.9, 0.01],
    [PUT, -0.011, 0.05, 0.2, 1.0, 0.01], [CALL, NAN, 0.05, 0.2, 1.0, 0.0], [CALL, 0.03, 0.0, 0.2, 1.0, 0.0],
    [PUT, 0.03, -0.05, 0.2, 0.9, 0.01], [CALL, 0.03, -0.01, 0.2, 0.9, 0.01], [PUT, 0.03, NAN, 0.2, 1.0, 0.0],
    [CALL, 0.03, 0.05, -0.2, 0.9, 0.01], [PUT, 0.03, 0.05, NAN, 0.9, 0.0], [CALL, 0.03, 0.05, 0.2, 0.0, 0.0],
    [PUT, 0.03, 0.05, 0.2, -0.9, 0.01], [CALL, 0.03, 0.05, 0.2, NAN, 0.0], [CALL, -0.01, 0.05, -0.2, 0.0, -0.01],
    [CALL, 0.03, INF, 0.2, 0.9, 0.0], [PUT, 0.03, INF, 0.2, 0.9, 0.0], [CALL, INF, 0.05, 0.2, 0.9, 0.0],
    [PUT, INF, 0.05, 0.2, 0.9, 0.0], [CALL, 0.03, 0.05, INF, 0.9, 0.0], [CALL, 0.03, 0.05, 0.2, INF, 0.0],
    [CALL, 0.03, 0.05, 0.2, 0.9, INF], [CALL, 0.0, 0.05, 0.2, 0.9, 0.0], [PUT, 0.0, 0.05, 0.2, 0.9, 0.0],
    [CALL, 0.03, 0.05, 0.0, 0.9, 0.01], [PUT, 0.06, 0.05, 0.0, 0.9, 0.01]]
# [type, strike, forward, stdDev, discount]
BACHELIER_ARGUMENTS = [
    [CALL, 0.03, 0.05, -0.01, 0.9], [PUT, 0.03, 0.05, NAN, 0.9], [CALL, 0.03, 0.05, 0.01, 0.0],
    [PUT, 0.03, 0.05, 0.01, -1.0], [CALL, 0.03, 0.05, 0.01, NAN], [CALL, -0.03, -0.05, 0.01, 0.9],
    [PUT, -0.03, -0.05, 0.01, 0.9], [CALL, 0.03, 0.05, 0.0, 0.9], [PUT, 0.03, 0.05, 0.0, 0.9],
    [CALL, NAN, 0.05, 0.01, 0.9], [CALL, 0.03, INF, 0.01, 0.9], [CALL, INF, 0.05, 0.01, 0.9],
    [PUT, 0.03, 0.05, INF, 0.9], [CALL, 0.03, 0.05, 0.01, INF]]


def black_cases(st):
    out = []
    for tn, t in TYPES:
        for f in FORWARDS:
            for disp in DISPLACEMENTS:
                for df in DISCOUNTS:
                    out.append({
                        "name": f"black {tn} forward {f!r} displacement {disp!r} discount {df!r}",
                        "inputs": {"settings": st, "kind": "black", "optionType": int(t), "forward": f,
                                   "displacement": disp, "discount": df, "stdDevs": STDDEVS, "strikes": STRIKES},
                        "expected": {"blackFormula": [
                            [v.attempt(lambda sd=sd, k=k: ql.blackFormula(t, k, f, sd, df, disp)) for k in STRIKES]
                            for sd in STDDEVS]}})
    return out


def bachelier_cases(st):
    out = []
    for tn, t in TYPES:
        for f in FORWARDS:
            for df in DISCOUNTS:
                out.append({
                    "name": f"bachelier {tn} forward {f!r} discount {df!r}",
                    "inputs": {"settings": st, "kind": "bachelier", "optionType": int(t), "forward": f,
                               "discount": df, "stdDevs": NORMAL_STDDEVS, "strikes": STRIKES},
                    "expected": {"bachelierBlackFormula": [
                        [v.attempt(lambda sd=sd, k=k: ql.bachelierBlackFormula(t, k, f, sd, df)) for k in STRIKES]
                        for sd in NORMAL_STDDEVS]}})
    return out


def argument_cases(st):
    return [
        {"name": "blackFormula arguments",
         "inputs": {"settings": st, "kind": "blackArguments", "arguments": v.enc(BLACK_ARGUMENTS)},
         "expected": {"blackFormula": [v.attempt(lambda a=a: ql.blackFormula(*a)) for a in BLACK_ARGUMENTS]}},
        {"name": "bachelierBlackFormula arguments",
         "inputs": {"settings": st, "kind": "bachelierArguments", "arguments": v.enc(BACHELIER_ARGUMENTS)},
         "expected": {"bachelierBlackFormula": [v.attempt(lambda a=a: ql.bachelierBlackFormula(*a))
                                                for a in BACHELIER_ARGUMENTS]}}]


IMPLIED_STDDEVS = [1e-3, 0.2, 1.0]
ITM_STDDEVS = [0.0, 0.2, 1.0]
BACHELIER_TTE = 2.0
BACHELIER_IMPLIED_STDDEVS = [1e-4, 0.005, 0.02]
BACHELIER_ITM_STDDEVS = [0.0, 0.005, 0.02]
# The guessed implied standard deviations' guess, accuracy and iterations, and LiRS's omega.
GUESS, ACCURACY, MAX_ITERATIONS = 0.3, 1e-9, 60
OMEGA = 0.5

# [type, strike, forward, price, discount, displacement], then optionally [guess, accuracy, maxIterations]
IMPLIED_ARGUMENTS = [
    [CALL, 0.03, 0.05, 0.03, 0.9, -0.01], [PUT, -0.02, 0.05, 0.03, 0.9, 0.01], [CALL, 0.03, 0.0, 0.03, 0.9, 0.0],
    [PUT, 0.03, 0.05, 0.03, 0.0, 0.0], [CALL, 0.03, 0.05, -1e-3, 0.9, 0.0], [PUT, 0.03, 0.05, NAN, 0.9, 0.0],
    [CALL, 0.03, 0.05, 0.01, 1.0, 0.0], [PUT, 0.05, 0.03, 0.01, 1.0, 0.0],
    [CALL, 0.03, 0.05, 0.03, 0.9, 0.0, -0.1, 1e-6, 100], [PUT, 0.03, 0.05, 0.03, 0.9, 0.0, 30.0, 1e-6, 100],
    [CALL, 0.03, 0.05, 0.03, 0.9, 0.0, 0.2, 0.0, 100], [PUT, 0.03, 0.05, 0.01, 0.9, 0.0, 0.2, 1e-6, 0],
    [CALL, 0.03, 0.05, 0.03, 0.9, 0.0, 0.2, 1e-6, 1], [CALL, 0.03, 0.05, 0.03, 0.9, 0.0, 0.0, 1e-6, 100],
    [CALL, 0.0, 0.05, 0.045, 0.9, 0.0], [PUT, 0.0, 0.05, 0.0, 0.9, 0.0], [CALL, 0.03, 0.05, 0.018, 0.9, 0.0],
    [PUT, 0.06, 0.05, 0.009, 0.9, 0.0], [CALL, 0.03, INF, 0.03, 0.9, 0.0], [PUT, 0.03, INF, 0.03, 0.9, 0.0],
    [CALL, INF, 0.05, 0.03, 0.9, 0.0], [PUT, 0.03, 0.05, INF, 0.9, 0.0], [CALL, 0.03, 0.05, 0.03, INF, 0.0],
    [CALL, 0.03, 0.05, 0.03, 0.9, 0.0, NAN, 1e-6, 100], [PUT, 0.03, 0.05, 0.03, 0.9, 0.01, 1.0, 1e-12, 100],
    [CALL, 0.03, 0.05, 0.03, 0.9, 0.01, 23.999, 1e-6, 100]]
# [type, strike, forward, price, discount, displacement], then optionally [guess, omega, accuracy, maxIterations]
LIRS_ARGUMENTS = [
    [CALL, 0.03, 0.05, 0.03, 0.0, 0.0], [PUT, 0.03, 0.05, -1e-3, 0.9, 0.0], [CALL, 0.03, 0.05, NAN, 0.9, 0.0],
    [CALL, -0.01, 0.03, 0.04, 1.0, 0.0, 0.1, 1.0, 1e-6, 100], [CALL, 0.01, -0.03, 0.0, 1.0, 0.0, 0.1, 1.0, 1e-6, 100],
    [CALL, 0.0, 0.03, 0.04, 1.0, 0.0], [CALL, 0.03, 0.05, 0.03, 0.9, -0.01], [PUT, -0.02, 0.05, 0.03, 0.9, 0.01],
    [CALL, 0.03, 0.05, 0.03, 0.9, 0.0, -0.1, 1.0, 1e-6, 100], [CALL, 0.03, 0.05, 0.03, 0.9, 0.0, 0.2, 1.0, 1e-6, 1],
    [PUT, 0.03, 0.05, 0.003, 0.9, 0.0, 0.2, 1.0, 0.0, 30], [CALL, 0.03, 0.05, 0.03, 0.9, 0.0, 0.2, 1.0, 1e-6, 0],
    [CALL, 0.03, 0.05, 0.019, 0.95, 0.0], [PUT, 0.05, 0.03, 0.019, 0.95, 0.0], [CALL, 0.03, 0.05, 0.003, 0.95, 0.0],
    [PUT, 0.03, 0.05, 0.0, 0.95, 0.0], [CALL, 0.03, 0.05, 0.03, 0.9, 0.0, 0.0, 1.0, 1e-6, 100],
    [PUT, 0.03, 0.05, 0.004, 0.9, 0.0, 5.0, 2.0, 1e-10, 200], [CALL, 0.03, INF, 0.03, 0.9, 0.0],
    [CALL, 0.03, 0.05, INF, 0.9, 0.0, 0.2, 1.0, 1e-6, 100], [PUT, 0.03, 0.05, 0.004, 0.9, 0.02, 0.2, 1.0, 1e-6, 100]]
# [type, strike, forward, stdDev, displacement]
ITM_ARGUMENTS = [
    [CALL, 0.03, 0.05, 0.2, -0.01], [PUT, -0.02, 0.05, 0.2, 0.01], [CALL, 0.03, 0.0, 0.2, 0.0],
    [PUT, NAN, 0.05, 0.2, 0.0], [CALL, 0.03, 0.05, -0.2, 0.0], [PUT, 0.03, 0.05, -0.2, 0.01],
    [CALL, 0.03, 0.05, NAN, 0.0], [CALL, 0.03, 0.05, INF, 0.0], [PUT, 0.03, INF, 0.2, 0.0],
    [CALL, INF, 0.05, 0.2, 0.0], [CALL, 0.0, 0.05, 0.2, 0.0], [PUT, 0.0, 0.05, 0.2, 0.0],
    [CALL, 0.05, 0.05, 0.0, 0.0], [PUT, 0.05, 0.05, 0.0, 0.01]]
# [type, strike, forward, tte, price, discount]
BACHELIER_IMPLIED_ARGUMENTS = [
    [CALL, 0.03, 0.05, 0.0, 0.021, 0.9], [PUT, 0.03, 0.05, -1.0, 0.021, 0.9], [CALL, 0.03, 0.05, 1.0, NAN, 0.9],
    [CALL, 0.03, 0.05, 1.0, 0.01, 1.0], [PUT, 0.05, 0.03, 1.0, 0.01, 1.0], [CALL, 0.03, 0.05, 1.0, 0.021, 0.0],
    [PUT, 0.03, 0.05, 1.0, 0.001, -0.9], [CALL, 0.05, 0.05, 1.0, 0.004, 0.9], [PUT, 0.05, 0.05, 1.0, 0.0, 0.9],
    [CALL, 0.03, 0.05, 1.0, 0.02, 1.0], [PUT, 0.03, 0.05, 1.0, 0.0, 1.0], [CALL, 0.03, 0.05, 1.0, 0.0201, 1.0],
    [CALL, 0.03, 0.05, INF, 0.021, 0.9], [CALL, 0.03, 0.05, 1.0, INF, 0.9], [PUT, -0.03, -0.05, 0.5, 0.0215, 0.95],
    [CALL, 0.0499995, 0.05, 1.0, 0.004, 1.0], [PUT, 0.05, 0.0500002, 1.0, 0.004, 0.95]]
# [type, strike, forward, stdDev]
BACHELIER_ITM_ARGUMENTS = [
    [CALL, 0.03, 0.05, -0.01], [PUT, 0.03, 0.05, NAN], [CALL, 0.05, 0.05, 0.0], [PUT, 0.05, 0.05, 0.0],
    [CALL, 0.03, INF, 0.01], [PUT, 0.03, 0.05, INF], [CALL, -0.03, -0.05, 0.01], [PUT, NAN, 0.05, 0.01]]


def black_price(t, k, f, sd, df, disp):
    """blackFormula's price, or NaN where it refuses, as an input for the inversions."""
    try:
        return ql.blackFormula(t, k, f, sd, df, disp)
    except RuntimeError:
        return NAN


def bachelier_price(t, k, f, sd, df):
    """bachelierBlackFormula's price, or NaN where it refuses."""
    try:
        return ql.bachelierBlackFormula(t, k, f, sd, df)
    except RuntimeError:
        return NAN


def implied_cases(st):
    out = []
    for tn, t in TYPES:
        for f in FORWARDS:
            for disp in DISPLACEMENTS:
                for df in DISCOUNTS:
                    prices = [[black_price(t, k, f, sd, df, disp) for k in STRIKES] for sd in IMPLIED_STDDEVS]
                    pairs = [list(zip(STRIKES, row)) for row in prices]
                    inputs = {"settings": st, "kind": "implied", "optionType": int(t), "forward": f,
                              "displacement": disp, "discount": df, "strikes": STRIKES, "prices": v.enc(prices)}
                    expected = {
                        "impliedStdDev": [[v.attempt(lambda k=k, p=p: ql.blackFormulaImpliedStdDev(
                            t, k, f, p, df, disp)) for k, p in row] for row in pairs],
                        "liRS": [[v.attempt(lambda k=k, p=p: ql.blackFormulaImpliedStdDevLiRS(
                            t, k, f, p, df, disp)) for k, p in row] for row in pairs]}
                    if df != 1.0:
                        inputs.update({"guess": GUESS, "accuracy": ACCURACY, "maxIterations": MAX_ITERATIONS,
                                       "omega": OMEGA})
                        expected["impliedStdDevGuessed"] = [[v.attempt(lambda k=k, p=p: ql.blackFormulaImpliedStdDev(
                            t, k, f, p, df, disp, GUESS, ACCURACY, MAX_ITERATIONS)) for k, p in row] for row in pairs]
                        expected["liRSPayoffGuessed"] = [[v.attempt(lambda k=k, p=p: ql.blackFormulaImpliedStdDevLiRS(
                            ql.PlainVanillaPayoff(t, k), f, p, df, disp, GUESS, OMEGA, ACCURACY, MAX_ITERATIONS))
                            for k, p in row] for row in pairs]
                    out.append({"name": f"implied {tn} forward {f!r} displacement {disp!r} discount {df!r}",
                                "inputs": inputs, "expected": expected})
    return out


def itm_cases(st):
    out = []
    for tn, t in TYPES:
        for f in FORWARDS:
            for disp in DISPLACEMENTS:
                expected = {
                    "cashItmProbability": [[v.attempt(lambda sd=sd, k=k: ql.blackFormulaCashItmProbability(
                        t, k, f, sd, disp)) for k in STRIKES] for sd in ITM_STDDEVS],
                    "assetItmProbability": [[v.attempt(lambda sd=sd, k=k: ql.blackFormulaAssetItmProbability(
                        t, k, f, sd, disp)) for k in STRIKES] for sd in ITM_STDDEVS]}
                if f == 1.0:
                    expected["cashItmProbabilityPayoff"] = [[v.attempt(
                        lambda sd=sd, k=k: ql.blackFormulaCashItmProbability(ql.PlainVanillaPayoff(t, k), f, sd, disp))
                        for k in STRIKES] for sd in ITM_STDDEVS]
                    expected["assetItmProbabilityPayoff"] = [[v.attempt(
                        lambda sd=sd, k=k: ql.blackFormulaAssetItmProbability(ql.PlainVanillaPayoff(t, k), f, sd, disp))
                        for k in STRIKES] for sd in ITM_STDDEVS]
                out.append({"name": f"itm {tn} forward {f!r} displacement {disp!r}",
                            "inputs": {"settings": st, "kind": "itm", "optionType": int(t), "forward": f,
                                       "displacement": disp, "stdDevs": ITM_STDDEVS, "strikes": STRIKES},
                            "expected": expected})
    return out


def close_enough(x, y, n=42):
    """QuantLib's close_enough(x, y, n)."""
    if x == y:
        return True
    diff, tolerance = abs(x - y), n * QL_EPSILON
    if x * y == 0.0:
        return diff < tolerance * tolerance
    return diff <= tolerance * abs(x) or diff <= tolerance * abs(y)


def moved(t, k, f, p, df):
    """Whether QuantLib 1.44's bachelierBlackFormulaImpliedVol returns 0 on a positive time value
    (blackformula.cpp:903-920), which QuantLib's master inverts (#2924)."""
    # C++'s division, infinite at a zero discount
    p = p / df if df != 0.0 else (NAN if p == 0.0 or p != p else math.copysign(INF, p) * math.copysign(1.0, df))
    if close_enough(k, f):
        return False
    intrinsic = max((1.0 if t == ql.Option.Call else -1.0) * (f - k), 0.0)
    return close_enough(p, intrinsic) and p - intrinsic > 0.0


def close(x, y, n=42):
    """QuantLib's close(x, y, n)."""
    if x == y:
        return True
    diff, tolerance = abs(x - y), n * QL_EPSILON
    return diff <= tolerance * abs(x) and diff <= tolerance * abs(y)


def smile_normal_moved(s, x):
    """Whether a smile section's normal volatility at x, s.volatility(x, Normal, 0.0), is QuantLib 1.44's 0 that
    QuantLib's master inverts (#2924): the conversion's bachelierBlackFormulaImpliedVol on the section's price at a
    discount of 1 (smilesection.cpp:131-155), where moved() holds. The smile generators' "skip" lists."""
    if s.volatilityType() == ql.Normal and close(0.0, s.shift()):
        return False
    try:
        atm = s.atmLevel()
        if atm is None:
            return False
        t = ql.Option.Call if x >= atm else ql.Option.Put
        return moved(t, x, atm, s.optionPrice(x, t), 1.0)
    except RuntimeError:
        return False


def bachelier_implied_cases(st):
    out = []
    for tn, t in TYPES:
        for f in FORWARDS:
            for df in DISCOUNTS:
                prices = [[bachelier_price(t, k, f, sd, df) for k in STRIKES] for sd in BACHELIER_IMPLIED_STDDEVS]
                pairs = [list(zip(STRIKES, row)) for row in prices]
                skip = [[r, j] for r, row in enumerate(pairs) for j, (k, p) in enumerate(row) if moved(t, k, f, p, df)]
                implied = [[v.attempt(lambda k=k, p=p: ql.bachelierBlackFormulaImpliedVol(
                    t, k, f, BACHELIER_TTE, p, df)) for k, p in row] for row in pairs]
                for r, j in skip:
                    implied[r][j] = None
                out.append({
                    "name": f"bachelierImplied {tn} forward {f!r} discount {df!r}",
                    "inputs": {"settings": st, "kind": "bachelierImplied", "optionType": int(t), "forward": f,
                               "discount": df, "tte": BACHELIER_TTE, "strikes": STRIKES, "prices": v.enc(prices),
                               "skip": skip},
                    "expected": {
                        "impliedVolChoi": [[v.attempt(lambda k=k, p=p: ql.bachelierBlackFormulaImpliedVolChoi(
                            t, k, f, BACHELIER_TTE, p, df)) for k, p in row] for row in pairs],
                        "impliedVol": implied}})
    return out


def bachelier_itm_cases(st):
    out = []
    for tn, t in TYPES:
        for f in FORWARDS:
            expected = {"assetItmProbability": [[v.attempt(
                lambda sd=sd, k=k: ql.bachelierBlackFormulaAssetItmProbability(t, k, f, sd)) for k in STRIKES]
                for sd in BACHELIER_ITM_STDDEVS]}
            if f == 1.0:
                expected["assetItmProbabilityPayoff"] = [[v.attempt(
                    lambda sd=sd, k=k: ql.bachelierBlackFormulaAssetItmProbability(ql.PlainVanillaPayoff(t, k), f, sd))
                    for k in STRIKES] for sd in BACHELIER_ITM_STDDEVS]
            out.append({"name": f"bachelierItm {tn} forward {f!r}",
                        "inputs": {"settings": st, "kind": "bachelierItm", "optionType": int(t), "forward": f,
                                   "stdDevs": BACHELIER_ITM_STDDEVS, "strikes": STRIKES},
                        "expected": expected})
    return out


def other_argument_cases(st):
    def listed(output, arguments, f):
        return {"name": f"{output} arguments",
                "inputs": {"settings": st, "kind": output, "arguments": v.enc(arguments)},
                "expected": {output: [v.attempt(lambda a=a: f(*a)) for a in arguments]}}

    def skipped(case):
        output = case["inputs"]["kind"]
        arguments = BACHELIER_IMPLIED_ARGUMENTS
        skip = [i for i, (t, k, f, _, p, df) in enumerate(arguments) if moved(t, k, f, p, df)]
        for i in skip:
            case["expected"][output][i] = None
        case["inputs"]["skip"] = skip
        return case

    return [
        listed("blackFormulaImpliedStdDev", IMPLIED_ARGUMENTS, ql.blackFormulaImpliedStdDev),
        listed("blackFormulaImpliedStdDevLiRS", LIRS_ARGUMENTS, ql.blackFormulaImpliedStdDevLiRS),
        listed("blackFormulaCashItmProbability", ITM_ARGUMENTS, ql.blackFormulaCashItmProbability),
        listed("blackFormulaAssetItmProbability", ITM_ARGUMENTS, ql.blackFormulaAssetItmProbability),
        listed("bachelierBlackFormulaImpliedVolChoi", BACHELIER_IMPLIED_ARGUMENTS,
               ql.bachelierBlackFormulaImpliedVolChoi),
        skipped(listed("bachelierBlackFormulaImpliedVol", BACHELIER_IMPLIED_ARGUMENTS,
                       ql.bachelierBlackFormulaImpliedVol)),
        listed("bachelierBlackFormulaAssetItmProbability", BACHELIER_ITM_ARGUMENTS,
               ql.bachelierBlackFormulaAssetItmProbability)]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    return (black_cases(st) + bachelier_cases(st) + argument_cases(st)
            + implied_cases(st) + itm_cases(st) + bachelier_implied_cases(st) + bachelier_itm_cases(st)
            + other_argument_cases(st))


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/blackformula.hpp", cases)
