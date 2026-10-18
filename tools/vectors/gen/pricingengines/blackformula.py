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

A refused value is {"throws": true}. The wheel's blackFormula and bachelierBlackFormula are the proxies of the C++
functions of the same names, with no logic of their own. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/BlackFormulaVectorTests.cs."""
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


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    return black_cases(st) + bachelier_cases(st) + argument_cases(st)


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/blackformula.hpp", cases)
