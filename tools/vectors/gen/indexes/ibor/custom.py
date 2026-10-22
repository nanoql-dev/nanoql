# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CustomIborIndex (ql/indexes/ibor/custom.hpp), the LIBOR-like index with its own value and
maturity calendars: three setups, as tools/vectors/gen/indexes/ibor/euribor.py writes a preset's case, with its DATES and
index_forecast_case: EUR LIBOR's conventions (fixed on London and TARGET, valued and matured on TARGET), USD LIBOR's
(fixed and valued on London, matured on London and the US settlement calendar), and a one-week index fixed on London and
valued and matured on Japan's calendar, following and without the end-of-month rule. Each case's inputs hold the
constructor's arguments ("index": {"type": "CustomIborIndex", "familyName", "tenor", "settlementDays", "currency" (a
code), "fixingCalendar", "valueCalendar", "maturityCalendar" (a name, or a pair of names joined on their holidays),
"convention", "endOfMonth", "dayCounter" (a name)}). Read back by
tests/NanoQL.Tests/Vectors/Indexes/Ibor/CustomIborIndexVectorTests.cs."""
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

CALENDARS = {"TARGET": lambda: ql.TARGET(), "UnitedKingdom": lambda: ql.UnitedKingdom(ql.UnitedKingdom.Exchange),
             "UnitedStates": lambda: ql.UnitedStates(ql.UnitedStates.Settlement), "Japan": lambda: ql.Japan()}
CURRENCIES = {"EUR": ql.EURCurrency, "USD": ql.USDCurrency, "JPY": ql.JPYCurrency}
DAY_COUNTERS = {"Actual360": ql.Actual360, "Actual365Fixed": ql.Actual365Fixed}


def calendar(spec):
    if isinstance(spec, list):
        return ql.JointCalendar(calendar(spec[0]), calendar(spec[1]), ql.JoinHolidays)
    return CALENDARS[spec]()


def setup(family, tenor, days, ccy, fixing, value, maturity, convention, eom, dc):
    spec = {"type": "CustomIborIndex", "familyName": family, "tenor": eur.tenor_spec(tenor), "settlementDays": days,
            "currency": ccy, "fixingCalendar": fixing, "valueCalendar": value, "maturityCalendar": maturity,
            "convention": int(convention), "endOfMonth": eom, "dayCounter": dc}

    def make(h):
        return ql.CustomIborIndex(family, ql.Period(*tenor), days, CURRENCIES[ccy](), calendar(fixing), calendar(value),
                                  calendar(maturity), convention, eom, DAY_COUNTERS[dc](), h)
    return eur.index_forecast_case(spec, make)


def cases():
    return [
        setup("CustomEUR", (3, ql.Months), 2, "EUR", ["UnitedKingdom", "TARGET"], "TARGET", "TARGET",
              ql.ModifiedFollowing, True, "Actual360"),
        setup("CustomUSD", (6, ql.Months), 2, "USD", "UnitedKingdom", "UnitedKingdom", ["UnitedKingdom", "UnitedStates"],
              ql.ModifiedFollowing, True, "Actual360"),
        setup("CustomJPY", (1, ql.Weeks), 2, "JPY", "UnitedKingdom", "Japan", "Japan", ql.Following, False,
              "Actual365Fixed"),
    ]


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/ibor/custom.hpp", cases)
