# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's EurodollarFuturesImpliedStdDevQuote (ql/quotes/eurodollarfuturesquote.hpp, .cpp), which the
wheel doesn't expose, each output computed by the C++ harness: the rate's standard deviation implied by a Eurodollar
futures option's price.

Futures prices of 95.5 and 96.0 and strikes of 95.0, 95.5 and 96.5 (rate strikes below, at and above the rate forward,
so that QuantLib solves on the call's price or the put's), option prices of 0.05 to 0.6, guesses of 0.15 and 0.5, at an
accuracy of 1e-6 and 100 evaluations; and prices the formula can't reach, which QuantLib refuses. Each quote's first
value; the price QuantLib doesn't read is set apart from the one it does. A case's inputs are the "forward",
"callPrice", "putPrice", "strike", "guess", "accuracy" and "maxIterations". The harness unit is
tools/cxx/src/quotes/eurodollarfuturesquote.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Quotes/QuoteCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TODAY = ql.Date(15, ql.January, 2025)


def cases():
    rows = []
    for forward in (95.5, 96.0):
        for strike in (95.0, 95.5, 96.5):
            for price in (0.05, 0.2, 0.6):
                for guess in (0.15, 0.5):
                    rows.append((forward, price, price + 0.25, strike, guess))
    # a rate strike above the rate forward priced above the forward, and one below it at a negative price
    rows += [(96.0, 0.3, 5.0, 95.0, 0.15), (95.5, -0.1, 0.3, 96.5, 0.15)]
    out = []
    with cxx.connect() as H:
        st = H.settings(TODAY.serialNumber())
        for forward, call, put, strike, guess in rows:
            args = [forward, call, put, strike, guess, 1.0e-6, 100]
            out.append({"name": f"forward {forward} strike {strike} call {call} put {put} guess {guess}",
                        "inputs": {"settings": st, "forward": forward, "callPrice": call, "putPrice": put,
                                   "strike": strike, "guess": guess, "accuracy": 1.0e-6, "maxIterations": 100},
                        "expected": {"value": v.attempt(lambda a=args: H.call("EurodollarFuturesImpliedStdDevQuote",
                                                                              *a))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/quotes/eurodollarfuturesquote.hpp", cases)
