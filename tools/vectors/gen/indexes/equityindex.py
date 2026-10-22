# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The equity index (ql/indexes/equityindex.hpp) on flat Actual/365 (Fixed) curves, one case per set-up: with both
curves and the spot; without the dividend curve; without the spot (today's fixing stands in); without either curve; with
no spot and no stored fixing; with the dividend or the interest curve dated after the evaluation date; on an evaluation
date that isn't a business day; and on other rates. Each gives the name, isValidFixingDate over 40 dates, today's fixing
and today's forecast, and the fixings on 24 dates from a month before the evaluation date to 2050, past ones read from
the stored fixings and future ones forecast (QuantLib's refusals as throws).

Curves dated before the evaluation date, and fixings forecast before a later curve's reference date, are a documented
divergence (QuantLib reads each curve's discount from its own reference date, and refuses a date before it) and left
out: a set-up with a curve dated after the evaluation date has no forecast of today's fixing and no fixing date before
that curve's date. A case's inputs give each curve as {"rate", "referenceDate"} or null, the spot or null, and the
stored fixings in its settings. Read back by tests/NanoQL.Tests/Vectors/Indexes/EquityIndexVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

TODAY = ql.Date(27, ql.January, 2023)
SATURDAY = ql.Date(28, ql.January, 2023)


def handle(curve):
    if curve is None:
        return ql.YieldTermStructureHandle()
    return ql.YieldTermStructureHandle(ql.FlatForward(ql.Date(curve["referenceDate"]), curve["rate"],
                                                      ql.Actual365Fixed()))


def setups():
    t = TODAY.serialNumber()
    past = [(ql.Date(d, ql.January, 2023), 8600.0 + d) for d in (2, 3, 4, 5, 6, 9, 10, 26)]
    yield "full", TODAY, {"rate": 0.03, "referenceDate": t}, {"rate": 0.01, "referenceDate": t}, 8700.0, past + [(TODAY, 8690.0)]
    yield "no dividend", TODAY, {"rate": 0.03, "referenceDate": t}, None, 8700.0, past + [(TODAY, 8690.0)]
    yield "no spot", TODAY, {"rate": 0.03, "referenceDate": t}, {"rate": 0.01, "referenceDate": t}, None, past + [(TODAY, 8690.0)]
    yield "no curves", TODAY, None, None, 8700.0, past + [(TODAY, 8690.0)]
    yield "no spot, no fixing", TODAY, {"rate": 0.03, "referenceDate": t}, {"rate": 0.01, "referenceDate": t}, None, past
    yield "spot, no fixing", TODAY, {"rate": 0.03, "referenceDate": t}, {"rate": 0.01, "referenceDate": t}, 8700.0, past
    yield "dividend dated later", TODAY, {"rate": 0.05, "referenceDate": t}, {"rate": 0.02, "referenceDate": t + 2}, 100.0, []
    yield "interest dated later", TODAY, {"rate": 0.05, "referenceDate": t + 3}, {"rate": 0.02, "referenceDate": t}, 100.0, []
    yield "Saturday", SATURDAY, {"rate": 0.03, "referenceDate": SATURDAY.serialNumber()}, \
        {"rate": 0.01, "referenceDate": SATURDAY.serialNumber()}, None, past + [(TODAY, 8690.0)]
    yield "negative rates", TODAY, {"rate": -0.005, "referenceDate": t}, {"rate": 0.025, "referenceDate": t}, 4321.5, []
    yield "zero rates", TODAY, {"rate": 0.0, "referenceDate": t}, {"rate": 0.0, "referenceDate": t}, 1.0, []


def cases():
    out = []
    for name, today, interest, dividend, spot, fixings in setups():
        index = ql.EquityIndex("eqIndex", ql.TARGET(), ql.EURCurrency(), handle(interest), handle(dividend),
                               ql.QuoteHandle(ql.SimpleQuote(spot)) if spot is not None else ql.QuoteHandle())
        with v.case_settings(today, fixings=[(index, fixings)] if fixings else None) as st:
            valid = [today + k for k in range(-20, 20)]
            dates = [today - 25, today - 21, today - 1, today + 1, today + 2, today + 5, today + 30, today + 91,
                     today + 182, today + 365, today + 366, today + 731, today + 1096, today + 1826, today + 3653,
                     ql.Date(20, ql.May, 2030), ql.Date(30, ql.June, 2031), ql.Date(31, ql.December, 2035),
                     ql.Date(15, ql.March, 2040), ql.Date(29, ql.February, 2044), ql.Date(31, ql.December, 2049),
                     ql.Date(3, ql.January, 2050), ql.Date(2, ql.January, 2023), ql.Date(1, ql.January, 2023)]
            later = max([c["referenceDate"] for c in (interest, dividend) if c is not None], default=0)
            if later > today.serialNumber():
                dates = [d for d in dates if d <= today or d.serialNumber() >= later]
            expected = {"name": index.name(),
                        "isValidFixingDate": [index.isValidFixingDate(d) for d in valid],
                        "todaysFixing": v.attempt(lambda: index.fixing(today))}
            if later <= today.serialNumber():
                expected["todaysForecast"] = v.attempt(lambda: index.fixing(today, True))
            expected["fixing"] = [v.attempt(lambda d=d: index.fixing(d)) for d in dates]
            out.append({"name": name,
                        "inputs": {"settings": st, "interest": interest, "dividend": dividend, "spot": spot,
                                   "validDates": v.enc(valid), "dates": v.enc(dates)},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/equityindex.hpp", cases)
