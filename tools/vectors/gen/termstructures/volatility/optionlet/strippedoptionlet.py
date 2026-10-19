# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's StrippedOptionlet (ql/termstructures/volatility/optionlet/strippedoptionlet.hpp), built on its
constructor with one strike vector for every date (the one the wheel binds), on the evaluation date 16 June 2025: 8
stripped optionlets, and the 7 QuantLib refuses to build.

The optionlets have one to twelve dates, two to six strikes, settlement days 0 to 3, the TARGET and UK calendars, the
Actual/365 (Fixed) and Actual/360 day counters, both volatility types and a displacement, on Euribor 6M and 3M over
flat curves. The outputs are the maturities, the fixing dates and times, every date's strikes and volatilities, the
at-the-money rates (the index's fixings with today's forecast, read at the call), the day counter's and calendar's
names, the settlement days, the business-day convention, the volatility type, the displacement, and the volatilities
and strikes at the date past the last ({"throws": true}). The refused optionlets are QuantLib's checks: no dates, fewer
volatility rows than dates, a first date before or on the evaluation date, non-increasing dates, a row of another
length than the strikes, and non-increasing strikes; each "built": {"throws": true}.

An optionlet is {"settlementDays", "calendar", "convention", "index" ({"type": "Euribor6M" | "Euribor3M", "curve"}),
"optionletDates", "strikes", "volatilities" (a row per date), "dayCounter", "type", "displacement"}; the calendar and
day counter are specs of indexes/interestrateindex.py, the curve a flat forward of it. The wheel's StrippedOptionlet is
the proxy of the C++ constructor on quotes (QuantLib-SWIG 1.44, SWIG/volatilities.i); the generator builds the quotes
from the values, as nanoql takes them. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/StrippedOptionletVectorTests.cs."""
import importlib.util
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


irx = _load("interestrateindex", "indexes/interestrateindex.py")

TODAY = ql.Date(16, ql.June, 2025)
TARGET = {"type": "TARGET"}
UK = {"type": "UnitedKingdom", "market": int(ql.UnitedKingdom.Exchange)}
A365 = {"type": "Actual365Fixed"}
A360 = {"type": "Actual360"}


def index_spec(kind="Euribor6M", forward=0.04):
    return {"type": kind, "curve": irx.curve_spec(TODAY, forward)}


def make_index(spec):
    return {"Euribor6M": ql.Euribor6M, "Euribor3M": ql.Euribor3M}[spec["type"]](irx.handle(spec["curve"]))


def spec_of(dates, strikes, rows, settlement_days=2, cal=TARGET, convention=ql.Following, idx=None, dc=A365,
            vol_type=ql.ShiftedLognormal, displacement=0.0):
    return {"settlementDays": settlement_days, "calendar": cal, "convention": int(convention),
            "index": idx or index_spec(), "optionletDates": v.enc(dates), "strikes": list(strikes),
            "volatilities": [list(r) for r in rows], "dayCounter": dc, "type": int(vol_type),
            "displacement": displacement}


def make(spec):
    quotes = [[ql.QuoteHandle(ql.SimpleQuote(x)) for x in row] for row in spec["volatilities"]]
    return ql.StrippedOptionlet(spec["settlementDays"], irx.calendar(spec["calendar"]), spec["convention"],
                                make_index(spec["index"]), [ql.Date(d) for d in spec["optionletDates"]],
                                spec["strikes"], quotes, irx.day_counter(spec["dayCounter"]), spec["type"],
                                spec["displacement"])


def six_monthly(n, start=ql.Date(16, ql.December, 2025)):
    cal = ql.TARGET()
    return [cal.adjust(start + ql.Period(6 * i, ql.Months)) for i in range(n)]


def rows(rnd, n, strikes, base=0.22, normal=False):
    out = []
    for i in range(n):
        row = []
        for k in strikes:
            x = base - 0.004 * i + 0.8 * (k - 0.035) ** 2 + rnd.uniform(-0.003, 0.003)
            row.append(round(x / 25.0 if normal else x, 7))
        out.append(row)
    return out


def optionlets(rnd):
    ks4 = [0.02, 0.03, 0.04, 0.05]
    ks6 = [0.01, 0.02, 0.03, 0.04, 0.05, 0.07]
    return [
        ("three dates, two strikes", spec_of(six_monthly(3), [0.02, 0.03],
                                             [[0.20, 0.19], [0.22, 0.21], [0.25, 0.24]])),
        ("one date", spec_of(six_monthly(1), ks4, rows(rnd, 1, ks4))),
        ("twelve dates, six strikes", spec_of(six_monthly(12), ks6, rows(rnd, 12, ks6))),
        ("no settlement days", spec_of(six_monthly(4), ks4, rows(rnd, 4, ks4), settlement_days=0)),
        ("UK calendar, Actual/360, three settlement days",
         spec_of(six_monthly(5), ks4, rows(rnd, 5, ks4), settlement_days=3, cal=UK, convention=ql.ModifiedFollowing,
                 dc=A360)),
        ("normal volatilities", spec_of(six_monthly(6), ks4, rows(rnd, 6, ks4, normal=True), vol_type=ql.Normal)),
        ("shifted lognormal, displacement 1%", spec_of(six_monthly(6), [-0.005] + ks4, rows(rnd, 6, [-0.005] + ks4),
                                                       displacement=0.01)),
        ("Euribor 3M on a 2% curve", spec_of(six_monthly(8), ks4, rows(rnd, 8, ks4),
                                             idx=index_spec("Euribor3M", 0.02))),
    ]


def refused():
    d1, d2 = ql.Date(16, ql.December, 2025), ql.Date(16, ql.June, 2026)
    two = [[0.2, 0.2], [0.2, 0.2]]
    return [
        ("no dates", spec_of([], [0.02, 0.03], [])),
        ("fewer rows than dates", spec_of([d1, d2], [0.02, 0.03], [[0.2, 0.2]])),
        ("a first date before the evaluation date", spec_of([TODAY - 1, d2], [0.02, 0.03], two)),
        ("a first date on the evaluation date", spec_of([TODAY, d2], [0.02, 0.03], two)),
        ("non increasing dates", spec_of([d2, d1], [0.02, 0.03], two)),
        ("a row longer than the strikes", spec_of([d1, d2], [0.02, 0.03], [[0.2, 0.2], [0.2, 0.2, 0.2]])),
        ("non increasing strikes", spec_of([d1, d2], [0.03, 0.02], two)),
    ]


def optionlet_case(st, name, spec):
    s = make(spec)
    n = s.optionletMaturities()
    return {"name": name, "inputs": {"settings": st, "optionlet": spec},
            "expected": {"built": True, "optionletMaturities": n,
                         "optionletFixingDates": v.enc(list(s.optionletFixingDates())),
                         "optionletFixingTimes": list(s.optionletFixingTimes()),
                         "optionletStrikes": [list(s.optionletStrikes(i)) for i in range(n)],
                         "optionletVolatilities": [list(s.optionletVolatilities(i)) for i in range(n)],
                         "atmOptionletRates": v.attempt(lambda: list(s.atmOptionletRates())),
                         "dayCounter": s.dayCounter().name(), "calendar": s.calendar().name(),
                         "settlementDays": s.settlementDays(), "businessDayConvention": s.businessDayConvention(),
                         "volatilityType": s.volatilityType(), "displacement": s.displacement(),
                         "volatilitiesPastLast": v.attempt(lambda: list(s.optionletVolatilities(n))),
                         "strikesPastLast": v.attempt(lambda: list(s.optionletStrikes(n)))}}


def refused_case(st, name, spec):
    return {"name": f"refused: {name}", "inputs": {"settings": st, "optionlet": spec},
            "expected": {"built": v.attempt(lambda: make(spec) and True)}}


def cases():
    rnd = random.Random(v.SEED)
    out = []
    for name, spec in optionlets(rnd):
        with v.case_settings(TODAY) as st:
            out.append(optionlet_case(st, name, spec))
    for name, spec in refused():
        with v.case_settings(TODAY) as st:
            out.append(refused_case(st, name, spec))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/optionlet/strippedoptionlet.hpp", cases)
