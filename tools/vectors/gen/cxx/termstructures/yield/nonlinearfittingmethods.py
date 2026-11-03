# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's NaturalCubicFitting (ql/termstructures/yield/nonlinearfittingmethods.hpp, .cpp), which the
wheel doesn't expose (docs/porting-playbook.md §9.4), each output computed by the C++ harness: its size and its discount
function, through a FittedBondDiscountCurve dated Monday 16 June 2025 on Actual/365 (Fixed) that evaluates it on given
nodal discounts, at 24 times from 0 to 40 years.

Six knot sets (with and without 0 among them, unsorted, and two within 1e-14 of each other, which QuantLib takes as
one), each with smooth nodal discounts; one with flat-forward cut-offs at 0.5 and 15 years; and the refusals: a single
knot time, 0 alone, and parameters of the wrong count. Two readings of QuantLib's that the vectors record: its doc's
"If constrainAtZero is true" for a class that always fixes d(0) = 1, and its discount held at the last node's past the
last knot, the time clamped to the knots, unless a maximum cut-off is set.

The harness's call is tools/cxx/src/termstructures/yield/nonlinearfittingmethods.cpp's. Each case's inputs hold its
settings, the "knotTimes", the "parameters", the "minCutoffTime" and "maxCutoffTime" (null for QuantLib's defaults),
the "referenceDate", the "maxDate" and the "times". Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/NaturalCubicFittingCxxVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TODAY = ql.Date(16, ql.June, 2025)
MAX_DATE = TODAY + ql.Period(40, ql.Years)
TIMES = [0.0, 0.01, 0.1, 0.25, 0.5, 0.75, 1.0, 1.5, 2.0, 3.0, 4.0, 5.0, 6.0, 7.5, 9.0, 10.0, 12.0, 15.0, 18.0, 20.0,
         25.0, 30.0, 35.0, 40.0]


def discounts(knots, rate=0.03, slope=0.0004):
    """Nodal discounts of a smooth curve at the knots but 0, the parameters."""
    ts = sorted({k for k in knots if k > 1e-14})
    return [math.exp(-(rate + slope * t) * t) for t in ts]


KNOTS = [
    ("five knots", [1.0, 3.0, 5.0, 10.0, 20.0], None, None),
    ("with 0 given", [0.0, 2.0, 7.0, 15.0, 30.0], None, None),
    ("unsorted", [10.0, 1.0, 30.0, 5.0, 2.0], None, None),
    ("two within 1e-14", [1.0, 1.0 + 5e-15, 4.0, 12.0], None, None),
    ("one knot", [5.0], None, None),
    ("dense", [0.25, 0.5, 1.0, 2.0, 3.0, 5.0, 7.0, 10.0, 15.0, 20.0, 30.0], None, None),
    ("with cut-offs", [1.0, 3.0, 5.0, 10.0, 20.0], 0.5, 15.0),
]
REFUSALS = [
    ("0 alone", [0.0], []),
    ("too few parameters", [1.0, 3.0, 5.0], [0.97, 0.91]),
    ("too many parameters", [1.0, 3.0], [0.97, 0.91, 0.86]),
]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(v.enc(TODAY))

        def call(knots, params, lo, hi):
            return H.call("NaturalCubicFitting::discount", knots, params, lo, hi, v.enc(TODAY), v.enc(MAX_DATE), TIMES)

        def inputs(knots, params, lo, hi):
            return {"settings": st, "knotTimes": knots, "parameters": params, "minCutoffTime": lo,
                    "maxCutoffTime": hi, "referenceDate": v.enc(TODAY), "maxDate": v.enc(MAX_DATE), "times": TIMES}

        for name, knots, lo, hi in KNOTS:
            unique = [knots[0]] if name == "two within 1e-14" else []
            params = discounts([k for k in knots if not (unique and k == 1.0 + 5e-15)])
            r = call(knots, params, lo, hi)
            out.append({"name": name, "inputs": inputs(knots, params, lo, hi),
                        "expected": {"size": r["size"], "discount": r["discount"]}})
        for name, knots, params in REFUSALS:
            out.append({"name": "refusal: " + name, "inputs": inputs(knots, params, None, None),
                        "expected": {"discount": v.attempt(lambda k=knots, p=params:
                                                           call(k, p, None, None)["discount"])}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/nonlinearfittingmethods.hpp", cases)
