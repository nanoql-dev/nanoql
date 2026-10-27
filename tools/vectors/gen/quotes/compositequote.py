# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CompositeQuote (ql/quotes/compositequote.hpp): a quote whose value is a function of two
other quotes' values.

Five functions of the two values, each plain arithmetic so that the wheel's Python function and nanoql's delegate
compute the same bits ("add", "mul" and "sub", QuantLib's test's three; "div" x / y and "affine" x * y + 1), on six
pairs: each quote's value and its two elements' values. A case's inputs are the "function" by name and the elements
"element1" and "element2". The wheel's CompositeQuote takes two quote handles and a Python callable. Read back by
tests/NanoQL.Tests/Vectors/Quotes/FunctionalQuoteVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

FUNCTIONS = {"add": lambda x, y: x + y, "mul": lambda x, y: x * y, "sub": lambda x, y: x - y,
             "div": lambda x, y: x / y, "affine": lambda x, y: x * y + 1}
PAIRS = [(12.0, 13.0), (23.0, 24.0), (34.0, 35.0), (0.0123, -0.5), (1e-8, 3.0), (-2.5, 7.25e5)]


def cases():
    st = v.settings(ql.Date(15, ql.January, 2025))
    out = []
    for name, f in FUNCTIONS.items():
        for x, y in PAIRS:
            q = ql.CompositeQuote(ql.QuoteHandle(ql.SimpleQuote(x)), ql.QuoteHandle(ql.SimpleQuote(y)), f)
            out.append({"name": f"{name} of {x!r} and {y!r}",
                        "inputs": {"settings": st, "function": name, "element1": x, "element2": y},
                        "expected": {"value": q.value(), "value1": q.value1(), "value2": q.value2()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/quotes/compositequote.hpp", cases)
