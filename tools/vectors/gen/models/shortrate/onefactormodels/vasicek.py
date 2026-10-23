# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Vasicek model (ql/models/shortrate/onefactormodels/vasicek.hpp, .cpp) and the affine
members it inherits (ql/models/shortrate/onefactormodel.hpp's OneFactorAffineModel), through the members the wheel
binds.

Twenty models: the defaults; mean reversions from 1e-9 to 10, among them 1.4e-8 and 1.5e-8 either side of
sqrt(QL_EPSILON), where B and A switch to their small-reversion forms; positive, zero and negative short rates and
levels; and risk premiums from -0.2 to 0.5. For each: params() ([a, b, sigma, lambda]; r0 has no inspector in the
wheel, so the harness's vectors read it); discount(t) at fifteen times, through the dynamics' short rate at the
process's start; discountBond(now, maturity, rate) and discountBond(now, maturity, factors) at twelve points (zero
and negative tenors among them), the factors one rate or two (the second unread); and the four-argument
discountBondOption at sixteen points, calls and puts, a zero and a tiny maturity among them (its zero-volatility
branch). Then the constructor's refusals, a then sigma checked: a or sigma zero, negative or NaN ("<value>: invalid
value"), both bad.

The four models at a = 1e-9 to 1.5e-8 are generated and left out: there QuantLib 1.44's forms cut to the a = 0 limit,
dropping terms of order a*tau, or cancel just above sqrt(QL_EPSILON), and nanoql's are QuantLib's master's (#2882, the
S-sr plan's SSR3), which move their outputs beyond the tolerance; upstream's 60-digit values check them
(tests/NanoQL.Tests/Divergences/VasicekCancellationTests.cs). At a = 2e-12 the dropped terms stay within the
tolerance, and that model stays. Elsewhere the forms move the last bits only.

The wheel's calls, by QuantLib-SWIG's shortratemodels.i: ql.Vasicek(r0, a, b, sigma, lambda_parameter), positional;
params(); discount(t); discountBond(now, maturity, rate) and discountBond(now, maturity, Array); and
discountBondOption(type, strike, maturity, bondMaturity), each forwarding to the C++ member of that name. Each
case's inputs hold its settings, the "model" (tools/cxx/src/models/modelspec.hpp's Vasicek spec, "params" null), the
"times", the "bondPoints" ([now, maturity, rate]) and the "options" ([type, strike, maturity, bondMaturity]). Read
back by tests/NanoQL.Tests/Vectors/Models/ShortRate/OneFactorModels/VasicekVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)

# (r0, a, b, sigma, lambda)
MODELS = [
    (0.05, 0.1, 0.05, 0.01, 0.0),
    (0.03, 1e-9, 0.04, 0.012, 0.0),
    (0.03, 1.4e-8, 0.04, 0.012, 0.1),
    (0.03, 1.5e-8, 0.04, 0.012, 0.1),
    (0.02, 0.5, 0.06, 0.02, 0.1),
    (0.02, 3.0, 0.06, 0.02, 0.0),
    (0.04, 10.0, 0.03, 0.05, 0.5),
    (-0.01, 0.2, -0.005, 0.008, -0.2),
    (0.0, 0.15, 0.0, 0.01, 0.0),
    (0.1, 0.05, 0.08, 0.03, 0.2),
    (0.05, 0.25, 0.05, 0.1, -0.1),
    (0.06, 0.08, 0.02, 0.005, 0.0),
    (-0.02, 1.0, 0.01, 0.015, 0.3),
    (0.035, 0.12, 0.045, 0.011, -0.05),
    (0.01, 2e-12, 0.02, 0.009, 0.25),
    (0.045, 0.6, -0.01, 0.025, 0.0),
    (0.07, 0.02, 0.07, 0.018, 0.05),
    (0.025, 0.3, 0.035, 0.007, 0.15),
    (0.0, 1e-9, 0.0, 0.01, -0.2),
    (0.05, 5.0, 0.05, 0.04, 0.0),
]

TIMES = [0.0, 0.1, 0.25, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 5.0, 7.5, 10.0, 15.0, 20.0, 30.0]
BOND_POINTS = [(0.0, 1.0, 0.05), (0.5, 2.0, 0.03), (1.0, 1.0, 0.05), (2.0, 7.0, -0.01), (0.0, 30.0, 0.1),
               (3.0, 1.0, 0.04), (0.25, 0.5, 0.0), (5.0, 10.0, 0.2), (1.5, 4.0, 0.025), (0.0, 0.0, 0.05),
               (10.0, 30.0, 0.03), (0.75, 1.25, -0.02)]
OPTIONS = [(CALL, 0.95, 1.0, 3.0), (PUT, 0.95, 1.0, 3.0), (CALL, 0.7, 2.0, 5.0), (PUT, 0.9, 0.5, 1.5),
           (CALL, 1.0, 0.0, 2.0), (PUT, 1.0, 0.0, 2.0), (CALL, 0.98, 1e-17, 1.0), (PUT, 0.85, 4.0, 10.0),
           (CALL, 0.5, 0.25, 30.0), (PUT, 1.2, 3.0, 3.5), (CALL, 0.9, 0.5, 1.5), (PUT, 0.7, 2.0, 5.0),
           (CALL, 1.1, 1.0, 1.25), (PUT, 0.99, 0.1, 0.2), (CALL, 0.6, 5.0, 15.0), (PUT, 0.5, 10.0, 30.0)]

REFUSALS = [
    ("a zero", (0.05, 0.0, 0.05, 0.01, 0.0)),
    ("a negative", (0.05, -0.1, 0.05, 0.01, 0.0)),
    ("a NaN", (0.05, math.nan, 0.05, 0.01, 0.0)),
    ("sigma zero", (0.05, 0.1, 0.05, 0.0, 0.0)),
    ("sigma negative", (0.05, 0.1, 0.05, -0.01, 0.0)),
    ("sigma NaN", (0.05, 0.1, 0.05, math.nan, 0.0)),
    ("a and sigma negative", (0.05, -0.1, 0.05, -0.01, 0.0)),
]


def spec(m):
    r0, a, b, sigma, lam = m
    return {"type": "Vasicek", "r0": r0, "a": a, "b": b, "sigma": sigma, "lambda": lam, "params": None}


# The models SSR3's forms move beyond the tolerance, generated and left out.
LEFT_OUT = {1, 2, 3, 18}


def cases():
    st = v.settings(ql.Date(16, ql.October, 2026))
    out = []
    for k, m in enumerate(MODELS):
        model = ql.Vasicek(*m)
        if k in LEFT_OUT:
            continue
        out.append({"name": f"model {k}",
                    "inputs": {"settings": st, "model": spec(m), "times": TIMES, "bondPoints": BOND_POINTS,
                               "options": OPTIONS},
                    "expected": {
                        "params": v.enc(list(model.params())),
                        "discount": v.enc([model.discount(t) for t in TIMES]),
                        "discountBond": v.enc([model.discountBond(now, t, r) for now, t, r in BOND_POINTS]),
                        "discountBondFactors": v.enc([model.discountBond(now, t, ql.Array([r]))
                                                      for now, t, r in BOND_POINTS]),
                        "discountBondTwoFactors": v.enc([model.discountBond(now, t, ql.Array([r, 9.0]))
                                                         for now, t, r in BOND_POINTS]),
                        "discountBondOption": [v.attempt(lambda o=o: model.discountBondOption(*o))
                                               for o in OPTIONS]}})
    for name, m in REFUSALS:
        out.append({"name": "refused: " + name,
                    "inputs": {"settings": st, "model": v.enc(spec(m))},
                    "expected": {"built": v.attempt(lambda m=m: bool(ql.Vasicek(*m)))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/shortrate/onefactormodels/vasicek.hpp", cases)
