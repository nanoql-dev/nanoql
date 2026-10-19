# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the 20 overloads of ql/pricingengines/blackformula.hpp the wheel doesn't wrap, each output computed by
the C++ harness, over the grid of tools/vectors/gen/pricingengines/blackformula.py (its option types, forwards,
strikes, standard deviations, displacements and discounts):

- "black" cases, for each option type, forward, displacement and discount: blackFormulaForwardDerivative at the 5
  standard deviations x 10 strikes; and, of the prices blackFormula gives at 3 standard deviations (1e-3, 0.2 and 1)
  x the 10 strikes ("prices", NaN where blackFormula refuses, with the at-the-money prices "atmPrices" for Chambers),
  blackFormulaImpliedStdDevApproximation, blackFormulaImpliedStdDevChambers and
  blackFormulaImpliedStdDevApproximationRS.
- "strike" cases, for each forward, displacement and discount: blackFormulaStdDevDerivative,
  blackFormulaStdDevSecondDerivative and blackFormulaVolDerivative (over an expiry of 2) at the 5 standard deviations
  x 10 strikes.
- "bachelier" cases, for each option type, forward and discount: bachelierBlackFormulaForwardDerivative at 5 normal
  standard deviations x 10 strikes; and, the option type aside, bachelierBlackFormulaStdDevDerivative.
- "payoff" cases, for each option type and forward, with the displacement 0.01 and the discount 0.95: the 11 payoff
  overloads on a PlainVanillaPayoff of each strike, at the standard deviation 0.2 (the normal one 0.005), the
  inversions of the prices blackFormula gives there; blackFormulaImpliedStdDev both without a guess (Null<Real>())
  and with one.
- argument cases: each overload on lists of arguments, the refusals of its checks (checkParameters', the standard
  deviation's, the discount's, the price's and the at-the-money price's) and edge values (a zero strike, a zero
  standard deviation, infinite inputs, ∞ − ∞ at a zero standard deviation, a NaN of whose sign bit Boost's sign
  decides, and prices far below the at-the-money volatility's, for which Chambers' second-order step has no root and
  the first-order step runs, once to a negative standard deviation, which QL_ENSURE refuses). A NaN forward or strike isn't given to Bachelier's forward derivative at a zero standard deviation: there
  the result follows the NaN's sign bit, which the vectors' "NaN" doesn't carry.

A refused value is {"throws": true}. tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/BlackFormulaCxxVectorTests.cs
reads it back."""
import importlib.util
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_blackformula", os.path.join(v.GEN_ROOT, "pricingengines", "blackformula.py"))
bf = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bf)

INF = math.inf
NAN = math.nan
CALL, PUT = 1, -1
TYPES = [("Call", CALL), ("Put", PUT)]
EXPIRY = 2.0
PAYOFF_DISPLACEMENT, PAYOFF_DISCOUNT = 0.01, 0.95
PAYOFF_STDDEV, PAYOFF_NORMAL_STDDEV = 0.2, 0.005
GUESS, ACCURACY, MAX_ITERATIONS = 0.3, 1e-9, 60

FWD = "blackFormulaForwardDerivative(Option::Type,Real,Real,Real,Real,Real)"
APPROX = "blackFormulaImpliedStdDevApproximation(Option::Type,Real,Real,Real,Real,Real)"
CHAMBERS = "blackFormulaImpliedStdDevChambers(Option::Type,Real,Real,Real,Real,Real,Real)"
RS = "blackFormulaImpliedStdDevApproximationRS(Option::Type,Real,Real,Real,Real,Real)"
STDDEV_DERIVATIVE = "blackFormulaStdDevDerivative(Real,Real,Real,Real,Real)"
SECOND_DERIVATIVE = "blackFormulaStdDevSecondDerivative(Rate,Rate,Real,Real,Real)"
VOL_DERIVATIVE = "blackFormulaVolDerivative"
BACHELIER_FWD = "bachelierBlackFormulaForwardDerivative(Option::Type,Real,Real,Real,Real)"
BACHELIER_STDDEV = "bachelierBlackFormulaStdDevDerivative(Real,Real,Real,Real)"
PAYOFF = {
    "blackFormula": "blackFormula(PlainVanillaPayoff,Real,Real,Real,Real)",
    "forwardDerivative": "blackFormulaForwardDerivative(PlainVanillaPayoff,Real,Real,Real,Real)",
    "approximation": "blackFormulaImpliedStdDevApproximation(PlainVanillaPayoff,Real,Real,Real,Real)",
    "chambers": "blackFormulaImpliedStdDevChambers(PlainVanillaPayoff,Real,Real,Real,Real,Real)",
    "approximationRS": "blackFormulaImpliedStdDevApproximationRS(PlainVanillaPayoff,Real,Real,Real,Real)",
    "impliedStdDev": "blackFormulaImpliedStdDev(PlainVanillaPayoff,Real,Real,Real,Real,Real,Real,Natural)",
    "stdDevDerivative": "blackFormulaStdDevDerivative(PlainVanillaPayoff,Real,Real,Real,Real)",
    "secondDerivative": "blackFormulaStdDevSecondDerivative(PlainVanillaPayoff,Real,Real,Real,Real)",
    "bachelierBlackFormula": "bachelierBlackFormula(PlainVanillaPayoff,Real,Real,Real)",
    "bachelierForwardDerivative": "bachelierBlackFormulaForwardDerivative(PlainVanillaPayoff,Real,Real,Real)",
    "bachelierStdDevDerivative": "bachelierBlackFormulaStdDevDerivative(PlainVanillaPayoff,Real,Real,Real)"}

# [type, strike, forward, stdDev, discount, displacement]
FWD_ARGUMENTS = [
    [CALL, 0.03, 0.05, 0.2, 0.9, -0.01], [PUT, -0.03, 0.05, 0.2, 0.9, 0.01], [CALL, 0.03, -0.01, 0.2, 0.9, 0.01],
    [PUT, NAN, 0.05, 0.2, 0.9, 0.0], [CALL, 0.03, 0.05, -0.2, 0.9, 0.0], [PUT, 0.03, 0.05, NAN, 0.9, 0.0],
    [CALL, 0.03, 0.05, 0.2, 0.0, 0.0], [PUT, 0.03, 0.05, 0.2, NAN, 0.0], [CALL, 0.0, 0.05, 0.2, 0.9, 0.0],
    [PUT, 0.0, 0.05, 0.2, 0.9, 0.0], [CALL, 0.05, 0.05, 0.0, 0.9, 0.0], [PUT, 0.05, 0.05, 0.0, 0.9, 0.01],
    [CALL, INF, INF, 0.0, 0.9, 0.0], [PUT, INF, INF, 0.0, 0.9, 0.0], [CALL, 0.03, INF, 0.2, 0.9, 0.0],
    [PUT, INF, 0.05, 0.2, 0.9, 0.0], [CALL, 0.03, 0.05, INF, 0.9, 0.0]]
# [type, strike, forward, price, discount, displacement]
APPROX_ARGUMENTS = [
    [CALL, 0.03, 0.05, 0.02, 0.9, -0.01], [PUT, -0.03, 0.05, 0.02, 0.9, 0.01], [CALL, 0.03, 0.0, 0.02, 0.9, 0.0],
    [PUT, 0.03, 0.05, -0.02, 0.9, 0.0], [CALL, 0.03, 0.05, NAN, 0.9, 0.0], [PUT, 0.03, 0.05, 0.02, 0.0, 0.0],
    [CALL, 0.05, 0.05, 0.004, 0.9, 0.0], [PUT, 0.0, 0.05, 0.0, 0.9, 0.0], [CALL, 0.03, 0.05, 0.0, 0.9, 0.0],
    [PUT, 0.03, INF, 0.02, 0.9, 0.0], [CALL, 0.03, 0.05, INF, 0.9, 0.0]]
# [type, strike, forward, price, atmPrice, discount, displacement]
CHAMBERS_ARGUMENTS = [
    [CALL, 0.03, 0.05, 0.02, 0.004, 0.9, -0.01], [PUT, -0.03, 0.05, 0.02, 0.004, 0.9, 0.0],
    [CALL, 0.03, 0.0, 0.02, 0.004, 0.9, 0.0], [PUT, 0.03, 0.05, -0.02, 0.004, 0.9, 0.0],
    [CALL, 0.03, 0.05, 0.02, -0.004, 0.9, 0.0], [PUT, 0.03, 0.05, 0.02, NAN, 0.9, 0.0],
    [CALL, 0.03, 0.05, 0.02, 0.004, 0.0, 0.0], [PUT, 0.03, 0.05, 0.0, 0.0, 0.9, 0.0],
    [CALL, 0.05, 0.05, 0.004, 0.004, 0.9, 0.0], [PUT, 0.03, 0.05, 0.02, 0.004, 0.9, 0.01],
    [CALL, 0.03, 0.05, INF, 0.004, 0.9, 0.0], [PUT, 0.03, 0.05, 0.02, INF, 0.9, 0.0],
    [CALL, 0.03, 0.05, 1e-12, 0.5, 0.9, 0.0], [CALL, 0.05, 0.05, 0.0, 0.03, 1.0, 0.0],
    [PUT, 0.05, 0.05, 0.002, 0.04, 1.0, 0.0], [CALL, 0.01, 0.05, 0.0, 0.01, 1.0, 0.0]]
# [type, strike, forward, price, discount, displacement]
RS_ARGUMENTS = [
    [CALL, 0.03, 0.05, 0.02, 0.9, -0.01], [PUT, -0.03, 0.05, 0.02, 0.9, 0.01], [CALL, 0.03, 0.0, 0.02, 0.9, 0.0],
    [PUT, 0.03, 0.05, -0.02, 0.9, 0.0], [CALL, 0.03, 0.05, 0.02, 0.0, 0.0], [PUT, 0.0, 0.05, 0.0, 0.9, 0.0],
    [CALL, 0.0, 0.05, 0.045, 0.9, 0.0], [CALL, 0.05, 0.05, 0.004, 0.9, 0.0], [PUT, 0.05, 0.05, 0.004, 0.9, 0.0],
    [CALL, INF, INF, 0.02, 0.9, 0.0], [CALL, 0.03, 0.05, 0.0, 0.9, 0.0], [PUT, 0.03, 0.05, 1.0, 0.9, 0.0],
    [CALL, 0.03, INF, 0.02, 0.9, 0.0]]
# [strike, forward, stdDev, discount, displacement]
STRIKE_ARGUMENTS = [
    [0.03, 0.05, 0.2, 0.9, -0.01], [-0.03, 0.05, 0.2, 0.9, 0.01], [0.03, 0.0, 0.2, 0.9, 0.0],
    [0.03, 0.05, -0.2, 0.9, 0.0], [0.03, 0.05, NAN, 0.9, 0.0], [0.03, 0.05, 0.2, 0.0, 0.0],
    [0.03, 0.05, 0.2, NAN, 0.0], [0.0, 0.05, 0.2, 0.9, 0.0], [0.03, 0.05, 0.0, 0.9, 0.0],
    [0.03, INF, 0.2, 0.9, 0.0], [INF, 0.05, 0.2, 0.9, 0.0], [0.03, 0.05, INF, 0.9, 0.0]]
# [type, strike, forward, stdDev, discount]
BACHELIER_FWD_ARGUMENTS = [
    [CALL, 0.03, 0.05, -0.01, 0.9], [PUT, 0.03, 0.05, NAN, 0.9], [CALL, 0.03, 0.05, 0.01, 0.0],
    [PUT, 0.03, 0.05, 0.01, NAN], [CALL, 0.05, 0.05, 0.0, 0.9], [PUT, 0.05, 0.05, 0.0, 0.9],
    [CALL, -0.03, -0.05, 0.0, 0.9], [PUT, -0.03, -0.05, 0.0, 0.9], [CALL, INF, INF, 0.0, 0.9],
    [PUT, INF, INF, 0.0, 0.9], [CALL, 0.03, INF, 0.01, 0.9], [PUT, 0.03, 0.05, INF, 0.9],
    [CALL, NAN, 0.05, 0.01, 0.9], [PUT, 0.03, NAN, 0.01, 0.9]]
# [strike, forward, stdDev, discount]
BACHELIER_STDDEV_ARGUMENTS = [
    [0.03, 0.05, -0.01, 0.9], [0.03, 0.05, NAN, 0.9], [0.03, 0.05, 0.01, 0.0], [0.03, 0.05, 0.01, NAN],
    [0.03, 0.05, 0.0, 0.9], [-0.03, -0.05, 0.01, 0.9], [0.03, INF, 0.01, 0.9], [0.03, 0.05, INF, 0.9],
    [NAN, 0.05, 0.01, 0.9]]
# [type, strike, forward, x, discount, displacement] on a payoff: x a standard deviation or a price, as the overload takes it
PAYOFF_ARGUMENTS = [
    [CALL, -0.03, 0.05, 0.02, 0.9, 0.01], [PUT, 0.03, 0.0, 0.02, 0.9, 0.0], [CALL, 0.03, 0.05, -0.02, 0.9, 0.0],
    [PUT, 0.03, 0.05, 0.02, 0.0, 0.0], [CALL, 0.03, 0.05, 0.02, 0.9, -0.01]]


def payoff(t, k):
    return {"type": "PlainVanillaPayoff", "optionType": t, "strike": k}


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []

        def grid(method, rows, columns, args):
            return [[v.attempt(lambda r=r, c=c: H.call(method, *args(r, c))) for c in columns] for r in rows]

        for tn, t in TYPES:
            for f in bf.FORWARDS:
                for disp in bf.DISPLACEMENTS:
                    for df in bf.DISCOUNTS:
                        prices = [[bf.black_price(t, k, f, sd, df, disp) for k in bf.STRIKES]
                                  for sd in bf.IMPLIED_STDDEVS]
                        atm = [bf.black_price(t, f, f, sd, df, disp) for sd in bf.IMPLIED_STDDEVS]
                        rows = list(zip(prices, atm))
                        out.append({
                            "name": f"black {tn} forward {f!r} displacement {disp!r} discount {df!r}",
                            "inputs": {"settings": st, "kind": "black", "optionType": t, "forward": f,
                                       "displacement": disp, "discount": df, "stdDevs": bf.STDDEVS,
                                       "strikes": bf.STRIKES, "prices": v.enc(prices), "atmPrices": v.enc(atm)},
                            "expected": {
                                "forwardDerivative": grid(FWD, bf.STDDEVS, bf.STRIKES,
                                                          lambda sd, k: (t, k, f, sd, df, disp)),
                                "approximation": [[v.attempt(lambda k=k, p=p: H.call(APPROX, t, k, f, p, df, disp))
                                                   for k, p in zip(bf.STRIKES, row)] for row, _ in rows],
                                "chambers": [[v.attempt(lambda k=k, p=p, a=a: H.call(CHAMBERS, t, k, f, p, a, df, disp))
                                              for k, p in zip(bf.STRIKES, row)] for row, a in rows],
                                "approximationRS": [[v.attempt(lambda k=k, p=p: H.call(RS, t, k, f, p, df, disp))
                                                     for k, p in zip(bf.STRIKES, row)] for row, _ in rows]}})
        for f in bf.FORWARDS:
            for disp in bf.DISPLACEMENTS:
                for df in bf.DISCOUNTS:
                    out.append({
                        "name": f"strike forward {f!r} displacement {disp!r} discount {df!r}",
                        "inputs": {"settings": st, "kind": "strike", "forward": f, "displacement": disp,
                                   "discount": df, "expiry": EXPIRY, "stdDevs": bf.STDDEVS, "strikes": bf.STRIKES},
                        "expected": {
                            "stdDevDerivative": grid(STDDEV_DERIVATIVE, bf.STDDEVS, bf.STRIKES,
                                                     lambda sd, k: (k, f, sd, df, disp)),
                            "secondDerivative": grid(SECOND_DERIVATIVE, bf.STDDEVS, bf.STRIKES,
                                                     lambda sd, k: (k, f, sd, df, disp)),
                            "volDerivative": grid(VOL_DERIVATIVE, bf.STDDEVS, bf.STRIKES,
                                                  lambda sd, k: (k, f, sd, EXPIRY, df, disp))}})
        for f in bf.FORWARDS:
            for df in bf.DISCOUNTS:
                expected = {}
                for tn, t in TYPES:
                    expected["forwardDerivative" + tn] = grid(BACHELIER_FWD, bf.NORMAL_STDDEVS, bf.STRIKES,
                                                              lambda sd, k: (t, k, f, sd, df))
                expected["stdDevDerivative"] = grid(BACHELIER_STDDEV, bf.NORMAL_STDDEVS, bf.STRIKES,
                                                    lambda sd, k: (k, f, sd, df))
                out.append({"name": f"bachelier forward {f!r} discount {df!r}",
                            "inputs": {"settings": st, "kind": "bachelier", "forward": f, "discount": df,
                                       "stdDevs": bf.NORMAL_STDDEVS, "strikes": bf.STRIKES},
                            "expected": expected})
        disp, df, sd, nsd = PAYOFF_DISPLACEMENT, PAYOFF_DISCOUNT, PAYOFF_STDDEV, PAYOFF_NORMAL_STDDEV
        for tn, t in TYPES:
            for f in bf.FORWARDS:
                prices = [bf.black_price(t, k, f, sd, df, disp) for k in bf.STRIKES]
                atm = bf.black_price(t, f, f, sd, df, disp)

                def each(name, args):
                    return [v.attempt(lambda k=k, p=p: H.call(PAYOFF[name], payoff(t, k), *args(p)))
                            for k, p in zip(bf.STRIKES, prices)]

                out.append({
                    "name": f"payoff {tn} forward {f!r}",
                    "inputs": {"settings": st, "kind": "payoff", "optionType": t, "forward": f, "displacement": disp,
                               "discount": df, "stdDev": sd, "normalStdDev": nsd, "strikes": bf.STRIKES,
                               "prices": v.enc(prices), "atmPrice": v.enc(atm), "guess": GUESS, "accuracy": ACCURACY,
                               "maxIterations": MAX_ITERATIONS},
                    "expected": {
                        "blackFormula": each("blackFormula", lambda p: (f, sd, df, disp)),
                        "forwardDerivative": each("forwardDerivative", lambda p: (f, sd, df, disp)),
                        "approximation": each("approximation", lambda p: (f, p, df, disp)),
                        "chambers": each("chambers", lambda p: (f, p, atm, df, disp)),
                        "approximationRS": each("approximationRS", lambda p: (f, p, df, disp)),
                        "impliedStdDev": each("impliedStdDev", lambda p: (f, p, df, disp, None, 1e-6, 100)),
                        "impliedStdDevGuessed": each("impliedStdDev",
                                                     lambda p: (f, p, df, disp, GUESS, ACCURACY, MAX_ITERATIONS)),
                        "stdDevDerivative": each("stdDevDerivative", lambda p: (f, sd, df, disp)),
                        "secondDerivative": each("secondDerivative", lambda p: (f, sd, df, disp)),
                        "bachelierBlackFormula": each("bachelierBlackFormula", lambda p: (f, nsd, df)),
                        "bachelierForwardDerivative": each("bachelierForwardDerivative", lambda p: (f, nsd, df)),
                        "bachelierStdDevDerivative": each("bachelierStdDevDerivative", lambda p: (f, nsd, df))}})

        def listed(name, method, arguments):
            return {"name": f"{name} arguments", "inputs": {"settings": st, "kind": name, "arguments": v.enc(arguments)},
                    "expected": {name: [v.attempt(lambda a=a: H.call(method, *a)) for a in arguments]}}

        out += [
            listed("forwardDerivative", FWD, FWD_ARGUMENTS),
            listed("approximation", APPROX, APPROX_ARGUMENTS),
            listed("chambers", CHAMBERS, CHAMBERS_ARGUMENTS),
            listed("approximationRS", RS, RS_ARGUMENTS),
            listed("stdDevDerivative", STDDEV_DERIVATIVE, STRIKE_ARGUMENTS),
            listed("secondDerivative", SECOND_DERIVATIVE, STRIKE_ARGUMENTS),
            listed("volDerivative", VOL_DERIVATIVE, [a[:3] + [EXPIRY] + a[3:] for a in STRIKE_ARGUMENTS]
                   + [[0.03, 0.05, 0.2, -1.0, 0.9, 0.0], [0.03, 0.05, 0.2, NAN, 0.9, 0.0]]),
            listed("bachelierForwardDerivative", BACHELIER_FWD, BACHELIER_FWD_ARGUMENTS),
            listed("bachelierStdDevDerivative", BACHELIER_STDDEV, BACHELIER_STDDEV_ARGUMENTS)]
        payoff_arguments = {}
        for name in ("blackFormula", "forwardDerivative", "approximation", "approximationRS", "stdDevDerivative",
                     "secondDerivative"):
            payoff_arguments[name] = [v.attempt(lambda a=a: H.call(PAYOFF[name], payoff(a[0], a[1]), *a[2:]))
                                      for a in PAYOFF_ARGUMENTS]
        payoff_arguments["chambers"] = [v.attempt(lambda a=a: H.call(
            PAYOFF["chambers"], payoff(a[0], a[1]), a[2], a[3], 0.004, a[4], a[5])) for a in PAYOFF_ARGUMENTS]
        payoff_arguments["impliedStdDev"] = [v.attempt(lambda a=a: H.call(
            PAYOFF["impliedStdDev"], payoff(a[0], a[1]), *a[2:], None, 1e-6, 100)) for a in PAYOFF_ARGUMENTS]
        for name in ("bachelierBlackFormula", "bachelierForwardDerivative", "bachelierStdDevDerivative"):
            payoff_arguments[name] = [v.attempt(lambda a=a: H.call(PAYOFF[name], payoff(a[0], a[1]), *a[2:5]))
                                      for a in PAYOFF_ARGUMENTS]
        out.append({"name": "payoff arguments",
                    "inputs": {"settings": st, "kind": "payoffArguments", "arguments": v.enc(PAYOFF_ARGUMENTS),
                               "atmPrice": 0.004},
                    "expected": payoff_arguments})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/blackformula.hpp", cases)
