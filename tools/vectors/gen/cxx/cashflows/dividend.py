# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FixedDividend and FractionalDividend (ql/cashflows/dividend.hpp, .cpp), each output
computed by the C++ harness: the members the wheel doesn't expose. A fractional dividend with a nominal and without
one, at four rates: its rate, nominal (null without one), date, its amount (the rate times the nominal, refused
without one: "no nominal given") and its amount on four underlying values (the rate times the value); a fixed
dividend's amount on the same values (its amount, whatever the value).

Read back by tests/NanoQL.Tests/Vectors/Cxx/Coupons/DividendCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
RATES = [0.0, 0.02, -0.01, 1.5]
NOMINALS = [None, 100.0, 0.0, 2500.5]
UNDERLYINGS = [0.0, 50.0, 100.0, 1e9]
AMOUNTS = [0.0, 1.25, -3.0, 1e-9]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for rate in RATES:
            for nominal in NOMINALS:
                date = R + 91
                fields = H.call("FractionalDividend", rate, nominal, date)
                out.append({"name": f"fractional {rate}, nominal {nominal}",
                            "inputs": {"settings": st, "rate": rate, "nominal": nominal, "date": date,
                                       "underlyings": UNDERLYINGS},
                            "expected": {
                                "rate": fields[0], "nominal": fields[1], "date": fields[2],
                                "amount": v.attempt(lambda: H.call("FractionalDividend::amount", rate, nominal, date)),
                                "amountOn": [H.call("FractionalDividend::amountOn", rate, nominal, date, u)
                                             for u in UNDERLYINGS]}})
        for amount in AMOUNTS:
            out.append({"name": f"fixed {amount}",
                        "inputs": {"settings": st, "amount": amount, "date": R + 30, "underlyings": UNDERLYINGS},
                        "expected": {"amountOn": [H.call("FixedDividend::amountOn", amount, R + 30, u)
                                                  for u in UNDERLYINGS]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/dividend.hpp", cases)
