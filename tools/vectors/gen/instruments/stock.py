# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Stock (ql/instruments/stock.hpp, .cpp), on the evaluation date 15 October 2026: stocks
quoted at zero, negative, small, ordinary and large values, the NPV and isExpired; the error estimate and the
valuation date, which a stock doesn't provide, as nulls.

A case holds one stock {"quote"}. The wheel's calls, by QuantLib-SWIG 1.44's stocks.i: Stock(QuoteHandle(quote)),
NPV, errorEstimate, valuationDate and isExpired the instrument's. Read back by
tests/NanoQL.Tests/Vectors/Instruments/StockVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

R = ql.Date(15, ql.October, 2026)
QUOTES = [0.0, -3.25, 1e-8, 12.5, 100.0, 1234.5678, 2.5e7]


def cases():
    st = v.settings(R)
    out = []
    for q in QUOTES:
        s = ql.Stock(ql.QuoteHandle(ql.SimpleQuote(q)))
        out.append({"name": f"quoted at {q}", "inputs": {"settings": st, "stock": {"quote": q}},
                    "expected": {"NPV": s.NPV(), "errorEstimate": v.result(s.errorEstimate),
                                 "valuationDate": v.result(s.valuationDate), "isExpired": s.isExpired()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/stock.hpp", cases)
