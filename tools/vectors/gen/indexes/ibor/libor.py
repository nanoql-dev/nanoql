# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Libor and DailyTenorLibor (ql/indexes/ibor/libor.hpp, .cpp), the bases of the currency
LIBORs, built with a currency's own arguments: Libor for CAD at 1W, 3M and 1Y and for CHF at 6M, on the London
exchange's fixing calendar with value and maturity dates on its joint calendar with the financial center's, and
DailyTenorLibor for CAD (0 settlement days) and CHF (2), fixed on that joint calendar; one case each, as
tools/vectors/gen/indexes/ibor/euribor.py writes them, with its DATES and index_case. And four refused constructions,
each recorded as its name's {"throws": true}: Libor in EUR ("for EUR Libor dedicated EurLibor constructor must be
used"), at a daily tenor ("for daily tenors (1D) dedicated DailyTenor constructor must be used") and at an intraday
one ("invalid time units"), and DailyTenorLibor in EUR.

Each case's "index" spec holds the type ("Libor" or "DailyTenorLibor") and the constructor's arguments: the family
name, the tenor (Libor's), the settlement days, the currency's code, the financial center's calendar ("Canada",
"Switzerland" or "TARGET") and the day counter ("Actual365Fixed" or "Actual360"). The wheel's Libor and
DailyTenorLibor are QuantLib-SWIG's proxies of the C++ classes, their constructors QuantLib's with the default empty
forwarding handle. Read back by tests/NanoQL.Tests/Vectors/Indexes/Ibor/LiborVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_euribor",
                                               os.path.join(v.GEN_ROOT, "indexes", "ibor", "euribor.py"))
eur = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(eur)

CALENDARS = {"Canada": ql.Canada, "Switzerland": ql.Switzerland, "TARGET": ql.TARGET}
DAY_COUNTERS = {"Actual365Fixed": ql.Actual365Fixed, "Actual360": ql.Actual360}
CURRENCIES = {"CAD": ql.CADCurrency, "CHF": ql.CHFCurrency, "EUR": ql.EURCurrency}


def libor(family, tenor, days, currency, calendar, dc):
    spec = {"type": "Libor", "familyName": family, "tenor": eur.tenor_spec(tenor), "settlementDays": days,
            "currency": currency, "calendar": calendar, "dayCounter": dc}
    return spec, lambda: ql.Libor(family, ql.Period(*tenor), days, CURRENCIES[currency](), CALENDARS[calendar](),
                                  DAY_COUNTERS[dc]())


def daily(family, days, currency, calendar, dc):
    spec = {"type": "DailyTenorLibor", "familyName": family, "settlementDays": days, "currency": currency,
            "calendar": calendar, "dayCounter": dc}
    return spec, lambda: ql.DailyTenorLibor(family, days, CURRENCIES[currency](), CALENDARS[calendar](),
                                            DAY_COUNTERS[dc]())


def cases():
    out = []
    for tenor in [(1, ql.Weeks), (3, ql.Months), (1, ql.Years)]:
        out.append(eur.index_case(*libor("CADLibor", tenor, 2, "CAD", "Canada", "Actual365Fixed")))
    out.append(eur.index_case(*libor("CHFLibor", (6, ql.Months), 2, "CHF", "Switzerland", "Actual360")))
    out.append(eur.index_case(*daily("CADLibor", 0, "CAD", "Canada", "Actual365Fixed")))
    out.append(eur.index_case(*daily("CHFLibor", 2, "CHF", "Switzerland", "Actual360")))
    for label, (spec, make) in [
            ("Libor in EUR", libor("EURLibor", (3, ql.Months), 2, "EUR", "TARGET", "Actual360")),
            ("Libor at a daily tenor", libor("CADLibor", (1, ql.Days), 2, "CAD", "Canada", "Actual365Fixed")),
            ("Libor at an intraday tenor", libor("CADLibor", (1, ql.Hours), 2, "CAD", "Canada", "Actual365Fixed")),
            ("DailyTenorLibor in EUR", daily("EURLibor", 0, "EUR", "TARGET", "Actual360"))]:
        out.append(eur.refused_case(label, spec, make))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/ibor/libor.hpp", cases)
