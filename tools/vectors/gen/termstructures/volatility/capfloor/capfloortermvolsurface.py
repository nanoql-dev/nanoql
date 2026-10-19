# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CapFloorTermVolSurface (ql/termstructures/volatility/capfloor/capfloortermvolsurface.hpp),
built on its matrix constructors, on the evaluation date 15 January 2025: 13 surfaces, each read at about 40 points by
time, by date and by tenor, and 10 surfaces QuantLib refuses to build.

The surfaces are on fixed reference dates (the settlement-date constructor) and moving ones (the settlement-days
constructor, its reference date the given TARGET business days after the evaluation date, which the case's settings
set), with option tenors in months and years, from two strikes and two tenors to twelve by ten, irregular strikes, a
flat surface, a day counter and a calendar and convention other than the defaults (Actual/365 (Fixed), TARGET,
Following), and extrapolation enabled. Each is read at its nodes, between them, before the first option time (inside
the structure's range, where the spline extrapolates), past its last time and outside its strikes (refused unless the
call or the structure extrapolates), at a negative time and a date before the reference date (refused), and, by tenor,
at the option tenors and others; a refused value is {"throws": true}. The surface's own outputs are its reference
date, option tenors, strikes, option dates and times, maximum date and time, minimum and maximum strikes and business-day convention. The
refused surfaces are QuantLib's checks (an empty tenor vector, a row or column count other than the grid's, a zero first
tenor, non-increasing tenors or strikes, an undecidable pair of tenors, an empty matrix) and the interpolation's (one
strike, one tenor), each built whole: "built" is {"throws": true}.

A surface is {"settlementDays" or "settlementDate", "calendar", "convention", "optionTenors", "strikes",
"volatilities", "dayCounter", "enableExtrapolation"}: the tenors as [length, units], the volatilities as the matrix's
rows (a row per option tenor, a column per strike), the calendar and day counter as specs of
indexes/interestrateindex.py, and enableExtrapolation whether enableExtrapolation() is called on the built surface. A
point is [time, strike, extrapolate], [date, strike, extrapolate] or [[length, units], strike, extrapolate]. The
wheel's CapFloorTermVolSurface is the proxy of the C++ class's four constructors, positional (QuantLib-SWIG 1.44,
SWIG/old_volatility.i); the generator calls the two on a Matrix, which the quote constructors equal once a quote is a
value, and its volatility(...) overloads, by type. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/CapFloorTermVolSurfaceVectorTests.cs."""
import math
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

TODAY = ql.Date(15, ql.January, 2025)
TARGET = {"type": "TARGET"}
UK = {"type": "UnitedKingdom", "market": int(ql.UnitedKingdom.Exchange)}
A365 = {"type": "Actual365Fixed"}
A360 = {"type": "Actual360"}
D, M, Y = int(ql.Days), int(ql.Months), int(ql.Years)


def calendar(spec):
    if spec["type"] == "TARGET":
        return ql.TARGET()
    return ql.UnitedKingdom(spec["market"])


def day_counter(spec):
    return ql.Actual360() if spec["type"] == "Actual360" else ql.Actual365Fixed()


def surface_spec(tenors, strikes, vols, settlement_days=None, settlement_date=None, cal=TARGET, convention=ql.Following,
                 dc=A365, extrapolate=False):
    out = {"settlementDays": settlement_days} if settlement_date is None else {"settlementDate": v.enc(settlement_date)}
    out.update({"calendar": cal, "convention": int(convention), "optionTenors": [list(t) for t in tenors],
                "strikes": list(strikes), "volatilities": [list(r) for r in vols], "dayCounter": dc,
                "enableExtrapolation": extrapolate})
    return out


def matrix(rows):
    m = ql.Matrix(len(rows), len(rows[0]) if rows else 0)
    for i, row in enumerate(rows):
        for j, x in enumerate(row):
            m[i][j] = x
    return m


def make(spec):
    tenors = [ql.Period(n, u) for n, u in spec["optionTenors"]]
    first = spec["settlementDays"] if "settlementDays" in spec else ql.Date(spec["settlementDate"])
    s = ql.CapFloorTermVolSurface(first, calendar(spec["calendar"]), spec["convention"], tenors, spec["strikes"],
                                  matrix(spec["volatilities"]), day_counter(spec["dayCounter"]))
    if spec["enableExtrapolation"]:
        s.enableExtrapolation()
    return s


def smile(rnd, tenors, strikes, flat=None):
    rows = []
    for i, _ in enumerate(tenors):
        row = []
        for j, k in enumerate(strikes):
            if flat is not None:
                row.append(flat)
            else:
                row.append(round(0.12 + 0.18 * math.exp(-0.25 * i) + 0.9 * (k - 0.035) ** 2 + rnd.uniform(-0.004, 0.004),
                                 6))
        rows.append(row)
    return rows


def surfaces(rnd):
    """(name, spec) for the 13 surfaces QuantLib builds."""
    years = [(1, Y), (2, Y), (3, Y), (5, Y), (7, Y), (10, Y), (15, Y), (20, Y)]
    strikes = [0.01, 0.015, 0.02, 0.025, 0.03, 0.035, 0.04, 0.05, 0.06, 0.1]
    months = [(3, M), (6, M), (9, M), (1, Y), (18, M), (2, Y), (3, Y), (4, Y), (5, Y), (7, Y), (10, Y), (12, Y)]
    irregular = sorted({round(rnd.uniform(0.005, 0.08), 5) for _ in range(7)})
    out = [
        ("eight years by ten strikes, fixed", surface_spec(years, strikes, smile(rnd, years, strikes),
                                                           settlement_date=ql.Date(17, 1, 2025))),
        ("eight years by ten strikes, two settlement days", surface_spec(years, strikes, smile(rnd, years, strikes), 2)),
        ("no settlement days", surface_spec(years[:5], strikes[2:8], smile(rnd, years[:5], strikes[2:8]), 0)),
        ("twelve tenors in months and years", surface_spec(months, strikes, smile(rnd, months, strikes), 2)),
        ("two by two", surface_spec([(1, Y), (2, Y)], [0.02, 0.04], [[0.25, 0.22], [0.23, 0.205]],
                                    settlement_date=TODAY)),
        ("irregular strikes", surface_spec(years[:6], irregular, smile(rnd, years[:6], irregular), 2)),
        ("flat", surface_spec(years[:4], strikes[:5], smile(rnd, years[:4], strikes[:5], 0.2), 2)),
        ("Actual/360", surface_spec(years[:6], strikes[:6], smile(rnd, years[:6], strikes[:6]), 2, dc=A360)),
        ("UK calendar, modified following", surface_spec(months[:8], strikes[:5], smile(rnd, months[:8], strikes[:5]), 1,
                                                          cal=UK, convention=ql.ModifiedFollowing)),
        ("unadjusted dates, fixed", surface_spec(years[:5], strikes[3:9], smile(rnd, years[:5], strikes[3:9]),
                                                 settlement_date=ql.Date(18, 1, 2025), convention=ql.Unadjusted)),
        ("extrapolation enabled", surface_spec(years[:5], strikes[2:8], smile(rnd, years[:5], strikes[2:8]), 2,
                                               extrapolate=True)),
        ("short tenors", surface_spec([(1, M), (2, M), (3, M), (6, M)], [0.02, 0.03, 0.04],
                                      smile(rnd, range(4), [0.02, 0.03, 0.04]), 2)),
        ("long tenors", surface_spec([(10, Y), (20, Y), (30, Y)], [0.01, 0.03, 0.05, 0.07],
                                     smile(rnd, range(3), [0.01, 0.03, 0.05, 0.07]), 2)),
    ]
    return out


REFUSED = [
    ("an empty tenor vector", [], [0.02, 0.03], []),
    ("fewer rows than tenors", [(1, Y), (2, Y)], [0.02, 0.03], [[0.2, 0.21]]),
    ("a zero first tenor", [(0, D), (1, Y)], [0.02, 0.03], [[0.2, 0.21], [0.22, 0.23]]),
    ("non increasing tenors", [(1, Y), (1, Y)], [0.02, 0.03], [[0.2, 0.21], [0.22, 0.23]]),
    ("an undecidable pair of tenors", [(1, M), (30, D)], [0.02, 0.03], [[0.2, 0.21], [0.22, 0.23]]),
    ("fewer columns than strikes", [(1, Y), (2, Y)], [0.02, 0.03, 0.04], [[0.2, 0.21], [0.22, 0.23]]),
    ("non increasing strikes", [(1, Y), (2, Y)], [0.02, 0.04, 0.03], [[0.2, 0.21, 0.22], [0.22, 0.23, 0.24]]),
    ("an empty matrix", [(1, Y), (2, Y)], [0.02, 0.03], []),
    ("one strike", [(1, Y), (2, Y)], [0.02], [[0.2], [0.22]]),
    ("one tenor", [(1, Y)], [0.02, 0.03], [[0.2, 0.21]]),
]


def points(rnd, s, spec):
    """About forty points by time, by date and by tenor, with their extrapolation flags."""
    times = list(s.optionTimes())
    ks = spec["strikes"]
    t0, t1, k0, k1 = times[0], times[-1], ks[0], ks[-1]
    by_time = [[t, k, False] for t, k in ((times[0], ks[0]), (times[-1], ks[-1]), (times[len(times) // 2], ks[1]))]
    by_time += [[rnd.uniform(t0, t1), rnd.uniform(k0, k1), False] for _ in range(14)]
    by_time += [[0.5 * t0, rnd.uniform(k0, k1), False], [0.0, ks[0], False], [t1 + 1.0, rnd.uniform(k0, k1), False],
                [t1 + 1.0, rnd.uniform(k0, k1), True], [rnd.uniform(t0, t1), k1 + 0.01, False],
                [rnd.uniform(t0, t1), k1 + 0.01, True], [rnd.uniform(t0, t1), k0 * 0.5, True], [-0.1, ks[0], True]]
    ref = s.referenceDate()
    dates = list(s.optionDates())
    by_date = [[v.enc(x), rnd.uniform(k0, k1), False] for x in dates]
    by_date += [[v.enc(ref + int(rnd.uniform(1, (dates[-1] - ref)))), rnd.uniform(k0, k1), False] for _ in range(8)]
    by_date += [[v.enc(dates[-1] + 400), ks[1], False], [v.enc(dates[-1] + 400), ks[1], True],
                [v.enc(ref - 1), ks[0], True]]
    tenors = [list(t) for t in spec["optionTenors"]]
    by_tenor = [[t, rnd.uniform(k0, k1), False] for t in tenors]
    by_tenor += [[[4, M], ks[0], False], [[30, Y], ks[-1], False], [[30, Y], ks[-1], True], [[1, D], ks[1], False]]
    return by_time, by_date, by_tenor


def volatility(s, point):
    x, k, extrapolate = point
    if isinstance(x, list):
        return v.attempt(lambda: s.volatility(ql.Period(x[0], x[1]), k, extrapolate))
    if isinstance(x, int):
        return v.attempt(lambda: s.volatility(ql.Date(x), k, extrapolate))
    return v.attempt(lambda: s.volatility(x, k, extrapolate))


def surface_case(rnd, st, name, spec):
    s = make(spec)
    by_time, by_date, by_tenor = points(rnd, s, spec)
    return {"name": name,
            "inputs": {"settings": st, "surface": spec, "byTime": by_time, "byDate": by_date, "byTenor": by_tenor},
            "expected": {"built": True, "referenceDate": v.enc(s.referenceDate()),
                         "optionTenors": [[t.length(), int(t.units())] for t in s.optionTenors()],
                         "strikes": list(s.strikes()), "optionDates": v.enc(list(s.optionDates())),
                         "optionTimes": list(s.optionTimes()), "maxDate": v.enc(s.maxDate()), "maxTime": s.maxTime(),
                         "minStrike": s.minStrike(), "maxStrike": s.maxStrike(),
                         "businessDayConvention": s.businessDayConvention(),
                         "volatilityByTime": [volatility(s, p) for p in by_time],
                         "volatilityByDate": [volatility(s, p) for p in by_date],
                         "volatilityByTenor": [volatility(s, p) for p in by_tenor]}}


def refused_case(st, name, tenors, strikes, rows):
    spec = surface_spec(tenors, strikes, rows, 2)
    return {"name": f"refused: {name}", "inputs": {"settings": st, "surface": spec},
            "expected": {"built": v.attempt(lambda: make(spec) and True)}}


def cases():
    rnd = random.Random(v.SEED)
    out = []
    for name, spec in surfaces(rnd):
        with v.case_settings(TODAY) as st:
            out.append(surface_case(rnd, st, name, spec))
    for row in REFUSED:
        with v.case_settings(TODAY) as st:
            out.append(refused_case(st, *row))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/capfloor/capfloortermvolsurface.hpp", cases)
