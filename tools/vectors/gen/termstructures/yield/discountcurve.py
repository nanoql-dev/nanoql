# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's InterpolatedDiscountCurve (ql/termstructures/yield/discountcurve.hpp), on the data its base
InterpolatedCurve (ql/termstructures/interpolatedcurve.hpp) checks and interpolates: 70 curves in 12 cases, and 14
refusals.

- 10 node sets, a case each, each on 6 interpolators: log-linear (the wheel's DiscountCurve), monotonic log-cubic and
  Kruger's log-cubic, the log-cubic spline and the log-cubic with parabolic derivatives (LogCubicDiscountCurve and
  KrugerLogDiscountCurve), and the cubic spline (_NaturalCubicDiscountCurve, the target of the wheel's
  NaturalCubicDiscountCurve shim, on the wheel's SplineCubic, QuantLib's Cubic with the spline and no monotonicity).
  The node sets: 2 nodes; 3; 5 within a year; 8 to 10 years; 12 to 30 years on Actual/360; 20 to 50 years on 30/360;
  6 with a hump, a negative forward rate between two of them; 5 with negative rates, discounts above 1; 7 with nodes a
  day apart; 9 over leap years on Actual/Actual (ISDA); on TARGET, the null calendar and QuantLib's empty calendar.
- 2 cases with jumps, each on 5 of the interpolators: two jumps on given dates (one before the reference date, which
  QuantLib's discount skips), and three on QuantLib's default dates, 31 December of the reference date's year and the
  years after it.
- 14 refusals, in QuantLib's checks: too few dates for the interpolation; dates and discounts in different numbers; a
  first discount other than 1; a zero and a negative discount; unsorted dates; two equal dates; two dates of the same
  time under 30/360; jumps and jump dates in different numbers; and pairs of these, whose first check refuses (the
  order nanoql's tests pin, since a refusal here is recorded only as a throw); and a negative jump, which QuantLib
  builds and whose discounts past it it refuses. An empty list of dates is left out: QuantLib's first failure there is
  dates.at(0)'s std::out_of_range, which the wheel raises as IndexError.

Each curve's outputs are whether it builds, QuantLib's refusals recorded as its throw; its dates, times, data,
discounts (its data again, by the discount curve's own inspector) and nodes (each [date, value]); its maximum date;
and its discount at 60 dates, 50 from its reference date to its last node and 10 past it, extrapolated; as arrays of one
element per curve, a refused curve's empty (its maximum date null). Each case's inputs hold its settings (the
evaluation date is the reference date; the curves don't read it), "dates", "discounts", "dayCounter", "calendar"
(null for QuantLib's empty one), "jumps" and "jumpDates" (null for none; null jump dates are QuantLib's default),
"interpolators" (the interpolator specs of termstructures/yield/piecewiseyieldcurve.py), "discountDates" and
"extrapolate". The wheel's curves are built from (dates, discounts, day counter, calendar, interpolator), or with jumps
(…, calendar, jumps, jump dates, interpolator), QuantLib's empty calendar being ql.Schedule().calendar(). Read back by
tests/NanoQL.Tests/Vectors/TermStructures/Yield/DiscountCurveVectorTests.cs."""
import importlib.util
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_piecewiseyieldcurve", os.path.join(v.GEN_ROOT, "termstructures", "yield", "piecewiseyieldcurve.py"))
pw = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(pw)
irx, CI = pw.irx, pw.CI

D = pw.D
TARGET, NULL_CALENDAR = irx.TARGET, irx.NULL_CALENDAR
A360, A365, THIRTY360_BB = irx.A360, irx.A365, irx.THIRTY360_BB
ACT_ISDA = {"type": "ActualActual", "convention": int(ql.ActualActual.ISDA)}

INTERPOLATORS = [pw.LOG_LINEAR, pw.MONOTONIC_LOG_CUBIC, pw.KRUGER_LOG, pw.cubic(CI.Spline, log=True),
                 pw.cubic(CI.Parabolic, log=True), pw.SPLINE_CUBIC]

# The wheel's curve for each interpolator type.
WHEEL = {"LogLinear": ql.DiscountCurve, "MonotonicLogCubic": ql.LogCubicDiscountCurve,
         "KrugerLog": ql.KrugerLogDiscountCurve, "LogCubic": ql.LogCubicDiscountCurve,
         "Cubic": ql.QuantLib._NaturalCubicDiscountCurve}


def flat(reference, rate, offsets, dc=ql.Actual365Fixed()):
    """Dates at the given day offsets from the reference date, with the discounts of a continuous rate."""
    dates = [reference + n for n in offsets]
    return dates, [1.0] + [math.exp(-rate * dc.yearFraction(reference, d)) for d in dates[1:]]


def node_sets():
    """(name, dates, discounts, day counter, calendar) for the 10 node sets."""
    r1, r2, r3 = D(15, 1, 2024), D(29, 2, 2024), D(31, 12, 2025)
    out = []
    dates, dfs = flat(r1, 0.03, [0, 366])
    out.append(("2 nodes", dates, dfs, A365, TARGET))
    out.append(("3 nodes", [r2, D(29, 8, 2024), D(28, 2, 2026)], [1.0, 0.985, 0.94], A365, NULL_CALENDAR))
    dates, dfs = flat(r1, 0.041, [0, 7, 31, 92, 182, 365])
    out.append(("5 nodes within a year", dates, dfs, A360, None))
    out.append(("8 nodes to 10 years", [r3 + n for n in (0, 91, 182, 365, 730, 1095, 1826, 3652)],
                [1.0, 0.9921, 0.9840, 0.9672, 0.9351, 0.9030, 0.8402, 0.7108], A365, TARGET))
    out.append(("12 nodes to 30 years", [r1 + n for n in (0, 30, 91, 182, 365, 730, 1095, 1826, 2557, 3653, 5479,
                                                          10958)],
                [1.0, 0.99637, 0.98894, 0.97790, 0.95650, 0.91560, 0.87500, 0.79730, 0.72640, 0.63910, 0.49870,
                 0.21650], A360, TARGET))
    dates, dfs = flat(r2, 0.0275, [0] + [int(round(n * 365.25 / 12)) for n in
                                         (1, 3, 6, 9, 12, 18, 24, 36, 48, 60, 84, 120, 144, 180, 240, 300, 360, 480,
                                          600)])
    out.append(("20 nodes to 50 years on 30/360", dates, dfs, THIRTY360_BB, NULL_CALENDAR))
    out.append(("6 nodes with a hump", [r3 + n for n in (0, 90, 181, 365, 547, 730)],
                [1.0, 0.990, 0.982, 0.984, 0.975, 0.960], A365, TARGET))
    out.append(("5 nodes of negative rates", [r1 + n for n in (0, 91, 365, 1096, 1826)],
                [1.0, 1.0012, 1.0051, 1.0034, 0.9910], A360, None))
    out.append(("7 nodes a day apart", [r2 + n for n in (0, 1, 2, 3, 4, 31, 32)],
                [1.0, 0.99990, 0.99979, 0.99970, 0.99958, 0.99660, 0.99649], A360, TARGET))
    out.append(("9 nodes over leap years", [D(28, 2, 2027) + n for n in (0, 366, 367, 731, 1096, 1461, 1462, 2192,
                                                                          3653)],
                [1.0, 0.968, 0.9679, 0.937, 0.906, 0.876, 0.8759, 0.818, 0.712], ACT_ISDA, NULL_CALENDAR))
    assert len(out) == 10
    return out


def make_curve(dates, dfs, dc, cal, interpolator, jumps=None, jump_dates=None):
    """The wheel's curve; jumps or their dates given as None are none."""
    cls = WHEEL[interpolator["type"]]
    if interpolator["type"] == "Cubic":
        assert interpolator == pw.SPLINE_CUBIC, interpolator
    head = ([ql.Date(x) for x in dates], dfs, irx.day_counter(dc),
            ql.Schedule().calendar() if cal is None else irx.calendar(cal))
    i = pw.make_interpolator("Discount", interpolator)
    if jumps is None and jump_dates is None:
        return cls(*head, i)
    return cls(*head, [ql.QuoteHandle(ql.SimpleQuote(x)) for x in jumps or []],
               [ql.Date(x) for x in jump_dates or []], i)


def discount_dates(dates):
    """50 dates from the first node to the last, closer together early on, then 10 past the last."""
    first, last = ql.Date(dates[0]), ql.Date(dates[-1])
    span = last - first
    inside = [first + int(round(span * (k / 49.0) ** 1.5)) for k in range(50)]
    past = [last + n for n in (1, 2, 7, 30, 91, 182, 365, 730, 1826, 3653)]
    return v.enc(inside + past), [False] * 50 + [True] * 10


def case(name, dates, dfs, dc, cal, interpolators, jumps=None, jump_dates=None):
    """A case of curves on the same nodes, one per interpolator."""
    dates = v.enc(dates)
    jump_dates = v.enc(jump_dates)
    when, flags = discount_dates(dates)
    keys = ["built", "dates", "times", "data", "discounts", "nodes", "maxDate", "discountAt"]
    expected = {k: [] for k in keys}
    for interpolator in interpolators:
        try:
            curve = make_curve(dates, dfs, dc, cal, interpolator, jumps, jump_dates)
        except RuntimeError:
            one = {"built": dict(v.THROWS), "dates": [], "times": [], "data": [], "discounts": [], "nodes": [],
                   "maxDate": None, "discountAt": []}
        else:
            one = {"built": True, "dates": v.enc(list(curve.dates())), "times": list(curve.times()),
                   "data": list(curve.data()), "discounts": list(curve.discounts()),
                   "nodes": [[d.serialNumber(), x] for d, x in curve.nodes()], "maxDate": v.enc(curve.maxDate()),
                   "discountAt": [v.attempt(lambda d=d, x=x: curve.discount(ql.Date(d), x))
                                  for d, x in zip(when, flags)]}
        for k in keys:
            expected[k].append(one[k])
    with v.case_settings(ql.Date(dates[0]) if dates else ql.Date(15, 1, 2024)) as st:
        inputs = {"settings": st, "dates": dates, "discounts": dfs, "dayCounter": dc, "calendar": cal,
                  "jumps": jumps, "jumpDates": jump_dates, "interpolators": interpolators,
                  "discountDates": when, "extrapolate": flags}
    return {"name": name, "inputs": inputs, "expected": expected}


def refusals():
    """(name, dates, discounts, day counter, jumps, jump dates) for the 14 refusals, on log-linear interpolation."""
    r = D(15, 1, 2024)
    three = [r, r + 182, r + 365]
    return [
        ("one date", [r], [1.0], A365, None, None),
        ("three dates, two discounts", three, [1.0, 0.98], A365, None, None),
        ("a first discount of 0.99", three, [0.99, 0.98, 0.96], A365, None, None),
        ("a zero discount", three, [1.0, 0.0, 0.96], A365, None, None),
        ("a negative discount", three, [1.0, 0.98, -0.96], A365, None, None),
        ("unsorted dates", [r, r + 365, r + 182], [1.0, 0.98, 0.96], A365, None, None),
        ("two equal dates", [r, r + 182, r + 182], [1.0, 0.98, 0.96], A365, None, None),
        ("the same time under 30/360", [D(30, 1, 2024), D(30, 1, 2025), D(31, 1, 2025)], [1.0, 0.97, 0.969],
         THIRTY360_BB, None, None),
        ("two jumps, one jump date", three, [1.0, 0.98, 0.96], A365, [0.999, 0.998], [r + 90]),
        ("two discounts and a first discount of 0.99", three, [0.99, 0.98], A365, None, None),
        ("a first discount of 0.99 and unsorted dates", [r, r + 365, r + 182], [0.99, 0.98, 0.96], A365, None, None),
        ("a negative discount and unsorted dates", [r, r + 365, r + 182], [1.0, -0.98, 0.96], A365, None, None),
        ("one jump, two jump dates and one date", [r], [1.0], A365, [0.999], [r + 30, r + 60]),
        ("a negative jump", three, [1.0, 0.98, 0.96], A365, [-0.5], [r + 90]),
    ]


def cases():
    out = []
    for name, dates, dfs, dc, cal in node_sets():
        out.append(case(name, dates, dfs, dc, cal, INTERPOLATORS))
    sets = {s[0]: s for s in node_sets()}
    _, dates, dfs, dc, cal = sets["12 nodes to 30 years"]
    out.append(case("2 jumps on given dates", dates, dfs, dc, cal, INTERPOLATORS[:5], [0.998, 0.9995],
                    [D(10, 1, 2024), D(30, 6, 2031)]))
    _, dates, dfs, dc, cal = sets["8 nodes to 10 years"]
    out.append(case("3 jumps on default dates", dates, dfs, dc, cal, INTERPOLATORS[:5], [0.999, 0.9985, 1.0005]))
    for name, dates, dfs, dc, jumps, jump_dates in refusals():
        out.append(case(f"refused: {name}", dates, dfs, dc, TARGET, [pw.LOG_LINEAR], jumps, jump_dates))
    check_rows(out)
    return out


def check_rows(out):
    for c in out:
        built = c["expected"]["built"]
        if c["name"] == "refused: a negative jump":
            assert built == [True] and v.THROWS in c["expected"]["discountAt"][0], c["name"]
        elif c["name"].startswith("refused: "):
            assert built == [v.THROWS], c["name"]
        else:
            assert all(b is True for b in built), c["name"]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/discountcurve.hpp", cases)
