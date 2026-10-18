# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Euribor presets (ql/indexes/ibor/euribor.hpp): Euribor1W, Euribor1M, Euribor3M, Euribor6M
and Euribor1Y, and Euribor365 at 1W and 6M, one case each, and four refused constructions: Euribor and Euribor365 at a
daily tenor ("for daily tenors (1D) dedicated DailyTenor constructor must be used") and at an intraday one ("invalid
time units"), each recorded as its name's {"throws": true}.

Each index gives its name, family name, tenor ([length, units]), fixing days, currency code, fixing calendar's name,
day counter's name, business-day convention and end-of-month flag, and fixingDate, valueDate and maturityDate at 30
dates, DATES below: month ends, a leap day, weekend days, and holidays of each preset's calendar (TARGET, the SOFR
calendar, the London stock exchange, Japan and Canada) among them. valueDate refuses a date that isn't a fixing date,
{"throws": true}.

The other preset generators (sofr.py, estr.py, sonia.py, tonar.py, tibor.py, corra.py) import DATES and index_case
from this file. Each case's inputs hold the preset ("index": {"type": <class>}, with "tenor" for the classes that take
one) and the dates ("dates"). The wheel's preset classes are QuantLib-SWIG's proxies of the C++ classes of those names,
their constructors QuantLib's with the default empty forwarding handle; the inspectors are the C++ members of
InterestRateIndex and IborIndex. Read back by tests/NanoQL.Tests/Vectors/Indexes/Ibor/EuriborVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

# 15 January 2025, a business day on every preset's calendar; no case reads it.
TODAY = ql.Date(15, ql.January, 2025)

# The 30 dates: 29 February 2024; Good Friday and Easter Monday 2024; month ends (30 April, 31 May, the last business
# day of August, 30 September (a Canadian holiday since 2021), 31 December 2024 and 2025 (Japanese bank holidays),
# 28 February, 31 March, the last business day of May 2025); New Year's Day and Japan's bank holidays on 2 and
# 3 January 2025; Japan's Coming of Age Day, National Foundation Day, observed Emperor's Birthday, vernal equinox,
# Children's Day and its observed day, and Mountain Day 2025; the evaluation date and the Saturday after it; Martin
# Luther King Day and Presidents' Day (US, the latter Canada's Family Day too); the UK's early May bank holiday (5 May,
# Japan's Children's Day); Victoria Day and Canada Day; the Friday after Thanksgiving; and Boxing Day 2025.
DATES = [ql.Date(29, 2, 2024), ql.Date(29, 3, 2024), ql.Date(1, 4, 2024), ql.Date(30, 4, 2024),
         ql.Date(31, 5, 2024), ql.Date(30, 8, 2024), ql.Date(30, 9, 2024), ql.Date(31, 12, 2024),
         ql.Date(1, 1, 2025), ql.Date(2, 1, 2025), ql.Date(3, 1, 2025), ql.Date(13, 1, 2025),
         ql.Date(15, 1, 2025), ql.Date(18, 1, 2025), ql.Date(20, 1, 2025), ql.Date(11, 2, 2025),
         ql.Date(17, 2, 2025), ql.Date(24, 2, 2025), ql.Date(28, 2, 2025), ql.Date(20, 3, 2025),
         ql.Date(31, 3, 2025), ql.Date(5, 5, 2025), ql.Date(6, 5, 2025), ql.Date(19, 5, 2025),
         ql.Date(30, 5, 2025), ql.Date(1, 7, 2025), ql.Date(11, 8, 2025), ql.Date(28, 11, 2025),
         ql.Date(26, 12, 2025), ql.Date(31, 12, 2025)]


def tenor_spec(tenor):
    return [tenor[0], int(tenor[1])]


def index_case(spec, make):
    """One preset's case: its spec ({"type", optionally "tenor"}) and the function that builds it with the wheel."""
    st = v.settings(TODAY)
    idx = make()
    return {"name": idx.name(),
            "inputs": {"settings": st, "index": spec, "dates": v.enc(DATES)},
            "expected": {
                "name": idx.name(),
                "familyName": idx.familyName(),
                "tenor": [idx.tenor().length(), int(idx.tenor().units())],
                "fixingDays": idx.fixingDays(),
                "currency": idx.currency().code(),
                "fixingCalendar": idx.fixingCalendar().name(),
                "dayCounter": idx.dayCounter().name(),
                "businessDayConvention": int(idx.businessDayConvention()),
                "endOfMonth": idx.endOfMonth(),
                "fixingDate": [v.attempt(lambda d=d: idx.fixingDate(d)) for d in DATES],
                "valueDate": [v.attempt(lambda d=d: idx.valueDate(d)) for d in DATES],
                "maturityDate": [v.attempt(lambda d=d: idx.maturityDate(d)) for d in DATES]}}


def refused_case(label, spec, make):
    """A construction QuantLib refuses: its name's {"throws": true}."""
    return {"name": label, "inputs": {"settings": v.settings(TODAY), "index": spec},
            "expected": {"name": v.attempt(lambda: make().name())}}


def cases():
    out = []
    for cls in ["Euribor1W", "Euribor1M", "Euribor3M", "Euribor6M", "Euribor1Y"]:
        out.append(index_case({"type": cls}, getattr(ql, cls)))
    for tenor in [(1, ql.Weeks), (6, ql.Months)]:
        out.append(index_case({"type": "Euribor365", "tenor": tenor_spec(tenor)},
                              lambda t=tenor: ql.Euribor365(ql.Period(*t))))
    for cls in ["Euribor", "Euribor365"]:
        for label, tenor in [("a daily tenor", (1, ql.Days)), ("an intraday tenor", (1, ql.Hours))]:
            out.append(refused_case(f"{cls} at {label}", {"type": cls, "tenor": tenor_spec(tenor)},
                                    lambda c=cls, t=tenor: getattr(ql, c)(ql.Period(*t))))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/ibor/euribor.hpp", cases)
