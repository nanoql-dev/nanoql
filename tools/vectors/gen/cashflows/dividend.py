# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Dividend, FixedDividend and FractionalDividend and the DividendVector function
(ql/cashflows/dividend.hpp, .cpp), on the evaluation date 15 October 2026.

- Fixed dividends: six amounts (negative, zero, positive) on dates before, on and after the evaluation date: the
  date, the amount, and whether the dividend has occurred on the evaluation date and on the day after it.
- Fractional dividends with no nominal: the date, and the amount, which QuantLib refuses ("no nominal given"); a
  fractional dividend's nominal form, its rate, nominal and amount on a given underlying, which the wheel doesn't
  expose, are vectored through the C++ harness (tools/vectors/gen/cxx/cashflows/dividend.py).
- DividendVector: schedules of zero to four dividends, each dividend's date and amount; and the refusal of dates and
  amounts differing in number.

The wheel's calls, by QuantLib-SWIG 1.44's cashflows.i: FixedDividend(amount, date) and FractionalDividend(rate,
date), the C++ constructors, and date, amount and hasOccurred, the CashFlow members; DividendVector(dates, amounts),
the C++ function. Read back by tests/NanoQL.Tests/Vectors/Coupons/DividendVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

R = ql.Date(15, ql.October, 2026)
FIXED = [(1.5, -30), (0.0, 0), (2.25, 0), (-0.5, 1), (100.0, 365), (1e-8, 3650)]
FRACTIONAL = [(0.02, -1), (0.035, 0), (0.0, 182)]
VECTORS = [
    ([], []),
    ([91], [0.5]),
    ([30, 120, 210], [1.0, 1.0, 1.25]),
    ([400, 10, 200, 0], [0.75, 0.5, 0.0, 3.0]),
]


def cases():
    out = []
    st = v.settings(R)
    for amount, offset in FIXED:
        d = ql.FixedDividend(amount, R + offset)
        out.append({
            "name": f"fixed {amount} at {offset}",
            "inputs": {"settings": st, "amount": amount, "date": (R + offset).serialNumber()},
            "expected": v.enc({"date": d.date(), "amount": d.amount(), "hasOccurred": d.hasOccurred(),
                               "hasOccurredTomorrow": d.hasOccurred(R + 1)})})
    for rate, offset in FRACTIONAL:
        d = ql.FractionalDividend(rate, R + offset)
        out.append({
            "name": f"fractional {rate} at {offset}",
            "inputs": {"settings": st, "rate": rate, "date": (R + offset).serialNumber()},
            "expected": v.enc({"date": d.date(), "amount": v.attempt(lambda: d.amount()),
                               "hasOccurred": d.hasOccurred()})})
    for offsets, amounts in VECTORS:
        dates = [R + o for o in offsets]
        schedule = ql.DividendVector(dates, amounts)
        out.append({
            "name": f"vector of {len(offsets)}",
            "inputs": {"settings": st, "dates": [d.serialNumber() for d in dates], "amounts": amounts},
            "expected": v.enc({"dates": [d.date() for d in schedule], "amounts": [d.amount() for d in schedule]})})
    out.append({
        "name": "vector, dates and amounts differ in number",
        "inputs": {"settings": st, "dates": [(R + 30).serialNumber(), (R + 60).serialNumber()], "amounts": [1.0]},
        "expected": {"build": v.attempt(lambda: len(ql.DividendVector([R + 30, R + 60], [1.0])))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/dividend.hpp", cases)
