# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CapFloorTermVolCurve (ql/termstructures/volatility/capfloor/capfloortermvolcurve.hpp), built
on its volatility constructors, on the evaluation date 15 January 2025: 10 curves, each read at about 30 points by
time, by date and by tenor, and 6 curves QuantLib refuses to build.

The curves are on fixed reference dates (the settlement-date constructor) and moving ones (the settlement-days
constructor, its reference date the given business days after the evaluation date), with option tenors in months and
years, from two tenors to sixteen (the at-the-money curve of QuantLib's stripper tests), a flat curve, a day counter and
a calendar and convention other than the defaults, and extrapolation enabled. Each is read at its nodes, between them,
before the first option time (inside the structure's range, where the spline extrapolates), past its last time
(refused unless the call or the curve extrapolates), at a negative time and a date before the reference date (refused),
and by tenor at its tenors and others; the strike is any, as the curve ignores it. A refused value is {"throws": true}.
The curve's own outputs are its reference date, maximum date and time, minimum and maximum strikes and business-day
convention (its option tenors, dates and times aren't bound: the harness reads them). The refused curves are QuantLib's
checks (an empty tenor vector, fewer volatilities than tenors, a zero first tenor, non-increasing tenors) and the
spline's (one tenor), each "built": {"throws": true}.

A curve is {"settlementDays" or "settlementDate", "calendar", "convention", "optionTenors", "volatilities",
"dayCounter", "enableExtrapolation"}: the tenors as [length, units], the calendar and day counter as specs of
indexes/interestrateindex.py. A point is [time, strike, extrapolate], [date, strike, extrapolate] or [[length, units],
strike, extrapolate]. The wheel's CapFloorTermVolCurve is the proxy of the C++ class's constructors on quotes and on
values (QuantLib-SWIG 1.44, SWIG/old_volatility.i); the generator calls the ones on values, which the quote
constructors equal once a quote is a value. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/CapFloorTermVolCurveVectorTests.cs."""
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

TODAY = ql.Date(15, ql.January, 2025)
TARGET = {"type": "TARGET"}
UK = {"type": "UnitedKingdom", "market": int(ql.UnitedKingdom.Exchange)}
A365 = {"type": "Actual365Fixed"}
A360 = {"type": "Actual360"}
D, M, Y = int(ql.Days), int(ql.Months), int(ql.Years)


def curve_spec(tenors, vols, settlement_days=None, settlement_date=None, cal=TARGET, convention=ql.Following, dc=A365,
               extrapolate=False):
    out = {"settlementDays": settlement_days} if settlement_date is None else {"settlementDate": v.enc(settlement_date)}
    out.update({"calendar": cal, "convention": int(convention), "optionTenors": [list(t) for t in tenors],
                "volatilities": list(vols), "dayCounter": dc, "enableExtrapolation": extrapolate})
    return out


def make(spec):
    tenors = [ql.Period(n, u) for n, u in spec["optionTenors"]]
    first = spec["settlementDays"] if "settlementDays" in spec else ql.Date(spec["settlementDate"])
    c = ql.CapFloorTermVolCurve(first, irx.calendar(spec["calendar"]), spec["convention"], tenors,
                                spec["volatilities"], irx.day_counter(spec["dayCounter"]))
    if spec["enableExtrapolation"]:
        c.enableExtrapolation()
    return c


def vols(rnd, tenors):
    return [round(0.09 + 0.08 * (1.0 - 1.0 / (1.0 + i)) + rnd.uniform(-0.004, 0.004), 6) for i, _ in enumerate(tenors)]


# The at-the-money curve of QuantLib's stripper tests (optionletstripper.cpp:211-249).
MARKET_TENORS = [(1, Y), (18, M), (2, Y), (3, Y), (4, Y), (5, Y), (6, Y), (7, Y), (8, Y), (9, Y), (10, Y), (12, Y),
                 (15, Y), (20, Y), (25, Y), (30, Y)]
MARKET_VOLS = [0.090304, 0.12180, 0.13077, 0.14832, 0.15570, 0.15816, 0.15932, 0.16035, 0.15951, 0.15855, 0.15754,
               0.15459, 0.15163, 0.14575, 0.14175, 0.13889]


def curves(rnd):
    """(name, spec) for the 10 curves QuantLib builds."""
    years = [(1, Y), (2, Y), (3, Y), (5, Y), (7, Y), (10, Y), (15, Y), (20, Y)]
    months = [(3, M), (6, M), (9, M), (1, Y), (18, M), (2, Y), (3, Y), (5, Y)]
    return [
        ("eight years, fixed", curve_spec(years, vols(rnd, years), settlement_date=ql.Date(17, 1, 2025))),
        ("eight years, two settlement days", curve_spec(years, vols(rnd, years), 2)),
        ("the stripper tests' market curve", curve_spec(MARKET_TENORS, MARKET_VOLS, 0)),
        ("months and years", curve_spec(months, vols(rnd, months), 2)),
        ("two tenors", curve_spec([(1, Y), (2, Y)], [0.25, 0.22], settlement_date=TODAY)),
        ("flat", curve_spec(years[:4], [0.18] * 4, 2)),
        ("Actual/360", curve_spec(years[:6], vols(rnd, years[:6]), 2, dc=A360)),
        ("UK calendar, modified following", curve_spec(months[:6], vols(rnd, months[:6]), 1, cal=UK,
                                                        convention=ql.ModifiedFollowing)),
        ("unadjusted dates, fixed", curve_spec(years[:5], vols(rnd, years[:5]), settlement_date=ql.Date(18, 1, 2025),
                                               convention=ql.Unadjusted)),
        ("extrapolation enabled", curve_spec(years[:5], vols(rnd, years[:5]), 2, extrapolate=True)),
    ]


REFUSED = [
    ("an empty tenor vector", [], []),
    ("fewer volatilities than tenors", [(1, Y), (2, Y)], [0.2]),
    ("a zero first tenor", [(0, D), (1, Y)], [0.2, 0.21]),
    ("non increasing tenors", [(1, Y), (1, Y)], [0.2, 0.21]),
    ("an undecidable pair of tenors", [(1, M), (30, D)], [0.2, 0.21]),
    ("one tenor", [(1, Y)], [0.2]),
]


def points(rnd, c, spec):
    """About thirty points by time, by date and by tenor, with their extrapolation flags."""
    ref = c.referenceDate()
    dc = irx.day_counter(spec["dayCounter"])
    tenors = [ql.Period(n, u) for n, u in spec["optionTenors"]]
    cal = irx.calendar(spec["calendar"])
    dates = [cal.advance(ref, t, spec["convention"]) for t in tenors]  # optionDateFromTenor, which the wheel doesn't bind
    times = [dc.yearFraction(ref, d) for d in dates]
    t0, t1 = times[0], times[-1]
    k = rnd.uniform(0.01, 0.06)
    by_time = [[t, k, False] for t in times]
    by_time += [[rnd.uniform(t0, t1), rnd.uniform(-0.01, 0.1), False] for _ in range(8)]
    by_time += [[0.5 * t0, k, False], [0.0, k, False], [t1 + 1.0, k, False], [t1 + 1.0, k, True], [-0.1, k, True]]
    by_date = [[v.enc(x), k, False] for x in dates]
    by_date += [[v.enc(ref + int(rnd.uniform(1, (dates[-1] - ref)))), k, False] for _ in range(4)]
    by_date += [[v.enc(dates[-1] + 400), k, False], [v.enc(dates[-1] + 400), k, True], [v.enc(ref - 1), k, True]]
    by_tenor = [[list(t), k, False] for t in spec["optionTenors"]]
    by_tenor += [[[4, M], k, False], [[40, Y], k, False], [[40, Y], k, True]]
    return by_time, by_date, by_tenor


def volatility(c, point):
    x, k, extrapolate = point
    if isinstance(x, list):
        return v.attempt(lambda: c.volatility(ql.Period(x[0], x[1]), k, extrapolate))
    if isinstance(x, int):
        return v.attempt(lambda: c.volatility(ql.Date(x), k, extrapolate))
    return v.attempt(lambda: c.volatility(x, k, extrapolate))


def curve_case(rnd, st, name, spec):
    c = make(spec)
    by_time, by_date, by_tenor = points(rnd, c, spec)
    return {"name": name,
            "inputs": {"settings": st, "curve": spec, "byTime": by_time, "byDate": by_date, "byTenor": by_tenor},
            "expected": {"built": True, "referenceDate": v.enc(c.referenceDate()), "maxDate": v.enc(c.maxDate()),
                         "maxTime": c.maxTime(), "minStrike": c.minStrike(), "maxStrike": c.maxStrike(),
                         "businessDayConvention": c.businessDayConvention(),
                         "volatilityByTime": [volatility(c, p) for p in by_time],
                         "volatilityByDate": [volatility(c, p) for p in by_date],
                         "volatilityByTenor": [volatility(c, p) for p in by_tenor]}}


def refused_case(st, name, tenors, vols_):
    spec = curve_spec(tenors, vols_, 2)
    return {"name": f"refused: {name}", "inputs": {"settings": st, "curve": spec},
            "expected": {"built": v.attempt(lambda: make(spec) and True)}}


def cases():
    rnd = random.Random(v.SEED)
    out = []
    for name, spec in curves(rnd):
        with v.case_settings(TODAY) as st:
            out.append(curve_case(rnd, st, name, spec))
    for row in REFUSED:
        with v.case_settings(TODAY) as st:
            out.append(refused_case(st, *row))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/capfloor/capfloortermvolcurve.hpp", cases)
