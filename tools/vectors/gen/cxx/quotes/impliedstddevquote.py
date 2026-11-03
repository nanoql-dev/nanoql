# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ImpliedStdDevQuote (ql/quotes/impliedstddevquote.hpp, .cpp), which the wheel doesn't
expose, each output computed by the C++ harness: the standard deviation Black's formula implies, solved once from the
guess, 0 where the solve fails.

Calls and puts at forwards of 4% and 5%, strikes of 3%, 4% and 6%, prices of half a basis point to 2%, guesses of 0.05,
0.15 and 0.5, accuracies of 1e-6 and 1e-10, and 100 evaluations; and prices the formula can't reach (a call above its
forward, a negative price), where the quote is 0. Each quote's first value; QuantLib's later values, which start from
the last solve, are left out. A case's inputs are the "optionType" (QuantLib's integer), "forward", "price", "strike",
"guess", "accuracy" and "maxIterations". The harness unit is tools/cxx/src/quotes/impliedstddevquote.cpp. Read back by
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
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)


def cases():
    rows = []
    for option_type in (CALL, PUT):
        for forward in (0.04, 0.05):
            for strike in (0.03, 0.04, 0.06):
                for price in (0.00005, 0.002, 0.012, 0.02):
                    for guess in (0.05, 0.15, 0.5):
                        rows.append((option_type, forward, price, strike, guess, 1.0e-6))
    rows += [(CALL, 0.05, 0.02, 0.04, 0.15, 1.0e-10), (PUT, 0.04, 0.012, 0.05, 0.15, 1.0e-10),
             (CALL, 0.05, 0.11, 0.04, 0.15, 1.0e-6), (PUT, 0.04, -0.001, 0.05, 0.15, 1.0e-6)]
    out = []
    with cxx.connect() as H:
        st = H.settings(TODAY.serialNumber())
        for option_type, forward, price, strike, guess, accuracy in rows:
            args = [option_type, forward, price, strike, guess, accuracy, 100]
            out.append({"name": f"type {option_type} forward {forward} strike {strike} price {price} guess {guess} "
                                f"accuracy {accuracy}",
                        "inputs": {"settings": st, "optionType": option_type, "forward": forward, "price": price,
                                   "strike": strike, "guess": guess, "accuracy": accuracy, "maxIterations": 100},
                        "expected": {"value": v.enc(H.call("ImpliedStdDevQuote", *args))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/quotes/impliedstddevquote.hpp", cases)
