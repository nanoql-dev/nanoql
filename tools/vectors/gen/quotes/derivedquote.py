# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DerivedQuote (ql/quotes/derivedquote.hpp): a quote whose value is a function of another
quote's.

Six functions of the element's value, each plain arithmetic so that the wheel's Python function and nanoql's delegate
compute the same bits ("add10" x + 10, "mul10" x * 10 and "sub10" x - 10, QuantLib's test's three; "square" x * x,
"reciprocal" 1 / x and "negThird" -x / 3), on seven element values: each quote's value. A case's inputs are the
"function" by name and the "element". The wheel's DerivedQuote takes a quote handle and a Python callable. Read back
by tests/NanoQL.Tests/Vectors/Quotes/FunctionalQuoteVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

FUNCTIONS = {"add10": lambda x: x + 10, "mul10": lambda x: x * 10, "sub10": lambda x: x - 10,
             "square": lambda x: x * x, "reciprocal": lambda x: 1.0 / x, "negThird": lambda x: -x / 3}
ELEMENTS = [12.0, 23.0, 34.0, 0.0123, -2.5, 1e-8, 7.25e5]


def cases():
    st = v.settings(ql.Date(15, ql.January, 2025))
    out = []
    for name, f in FUNCTIONS.items():
        for element in ELEMENTS:
            q = ql.DerivedQuote(ql.QuoteHandle(ql.SimpleQuote(element)), f)
            out.append({"name": f"{name} of {element!r}", "inputs": {"settings": st, "function": name,
                                                                     "element": element},
                        "expected": {"value": q.value()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/quotes/derivedquote.hpp", cases)
