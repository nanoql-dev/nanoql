# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MultiCompositeQuote (ql/quotes/multicompositequote.hpp): a quote whose value is a function
of an array of other quotes' values.

QuantLib's test's three functions of the values, each summing or multiplying in the array's order from 0 or 1 so that
the wheel's Python function and nanoql's delegate compute the same bits ("addAll", "mulAll", and "norm2", the square
root of the sum of the squares, QuantLib's Norm2), on arrays of 1, 2, 3 and 5 values: each quote's value and its
elements' values through inputValue. A case's inputs are the "function" by name and the "elements". The wheel's
MultiCompositeQuote takes a vector of quote handles and a Python callable, which it calls with the values. Read back by
tests/NanoQL.Tests/Vectors/Quotes/FunctionalQuoteVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def add_all(x):
    s = 0.0
    for a in x:
        s += a
    return s


def mul_all(x):
    p = 1.0
    for a in x:
        p *= a
    return p


def norm2(x):
    s = 0.0
    for a in x:
        s += a * a
    return math.sqrt(s)


FUNCTIONS = {"addAll": add_all, "mulAll": mul_all, "norm2": norm2}
ARRAYS = [[3.5], [1.0, 11.0], [1.0, 11.0, 21.0], [0.0123, -2.5, 1e-8, 7.25e5, 0.75]]


def cases():
    st = v.settings(ql.Date(15, ql.January, 2025))
    out = []
    for name, f in FUNCTIONS.items():
        for elements in ARRAYS:
            handles = ql.QuoteHandleVector([ql.QuoteHandle(ql.SimpleQuote(e)) for e in elements])
            q = ql.MultiCompositeQuote(handles, f)
            out.append({"name": f"{name} of {len(elements)} values",
                        "inputs": {"settings": st, "function": name, "elements": elements},
                        "expected": {"value": q.value(),
                                     "inputValues": [q.inputValue(i) for i in range(len(elements))]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/quotes/multicompositequote.hpp", cases)
