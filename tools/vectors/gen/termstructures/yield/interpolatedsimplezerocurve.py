# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's InterpolatedSimpleZeroCurve (ql/termstructures/yield/interpolatedsimplezerocurve.hpp), on
linear interpolation, the one the wheel names (SimpleZeroCurve): 10 curves, a case each, and 5 refusals.

- 8 node sets, a case each: 2 nodes; 4 within a year; 8 to 10 years; 12 to 30 years on Actual/360; 6 of negative and
  positive rates; 7 a day apart; 5 whose rates fall so far that R·t < −1 between the last nodes and past them, where
  the discount 1 / (1 + R·t) is negative, which QuantLib computes without a check; on TARGET, the null calendar and
  QuantLib's empty calendar; and 2 cases of jumps, two on given dates, one of them before the reference date, which
  QuantLib's discount skips, and three on QuantLib's default dates, 31 December of the reference date's year and the
  years after it.
- 5 refusals, in QuantLib's checks: too few dates; dates and rates in different numbers; unsorted dates; two equal
  dates; jumps and jump dates in different numbers. An empty list of dates is left out: QuantLib's first failure there
  is dates.at(0)'s std::out_of_range, which the wheel raises as IndexError.

Each curve's outputs are whether it builds, QuantLib's refusals recorded as its throw; its dates, times, data and zero
rates (its data again, by the curve's own inspector), and nodes (each [date, value]); its maximum date, its last
node's; and its discount at 60 dates, 50 from its reference date to its last node and 10 past it, extrapolated; as
arrays of one element, a refused curve's empty (its maximum date null). Each case's inputs hold its settings (the
evaluation date is the reference date; the curve doesn't read it), "dates", "rates", "dayCounter", "calendar" (null for
QuantLib's empty one), "jumps" and "jumpDates" (null for none; null jump dates are QuantLib's default), "classes" (the
named class nanoql builds the curve through, SimpleZeroCurve, or null for the generic InterpolatedSimpleZeroCurve on
Linear: the cases alternate), "discountDates" and "extrapolate". The wheel's curve is built from (dates, rates, day
counter, calendar, interpolator), or with jumps (…, calendar, jumps, jump dates, interpolator), QuantLib's empty
calendar being ql.Schedule().calendar(). Read back by
tests/NanoQL.Tests/Vectors/TermStructures/Yield/InterpolatedSimpleZeroCurveVectorTests.cs."""
import importlib.util
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
irx = pw.irx

D = pw.D
TARGET, NULL_CALENDAR = irx.TARGET, irx.NULL_CALENDAR
A360, A365 = irx.A360, irx.A365


def make_curve(dates, rates, dc, cal, jumps=None, jump_dates=None):
    """The wheel's curve; jumps or their dates given as None are none."""
    head = ([ql.Date(x) for x in dates], rates, irx.day_counter(dc),
            ql.Schedule().calendar() if cal is None else irx.calendar(cal))
    if jumps is None and jump_dates is None:
        return ql.SimpleZeroCurve(*head, ql.Linear())
    return ql.SimpleZeroCurve(*head, [ql.QuoteHandle(ql.SimpleQuote(x)) for x in jumps or []],
                              [ql.Date(x) for x in jump_dates or []], ql.Linear())


def node_sets():
    """(name, dates, rates, day counter, calendar) for the 8 node sets."""
    r1, r2, r3 = D(15, 1, 2024), D(29, 2, 2024), D(31, 12, 2025)
    out = [
        ("2 nodes", [r1, r1 + 366], [0.031, 0.034], A365, TARGET),
        ("4 nodes within a year", [r2, r2 + 31, r2 + 182, r2 + 365], [0.0405, 0.041, 0.0398, 0.0386], A360,
         NULL_CALENDAR),
        ("8 nodes to 10 years", [r3 + n for n in (0, 91, 182, 365, 730, 1095, 1826, 3652)],
         [0.0312, 0.0318, 0.0321, 0.0333, 0.0347, 0.0358, 0.0371, 0.0392], A365, None),
        ("12 nodes to 30 years", [r1 + n for n in (0, 30, 91, 182, 365, 730, 1095, 1826, 2557, 3653, 5479, 10958)],
         [0.0436, 0.0436, 0.0442, 0.0447, 0.0445, 0.0438, 0.0444, 0.0450, 0.0457, 0.0468, 0.0471, 0.0465], A360,
         TARGET),
        ("6 nodes of negative and positive rates", [r3 + n for n in (0, 90, 181, 365, 547, 730)],
         [-0.0045, -0.0051, -0.0032, 0.0008, 0.0026, 0.0041], A365, TARGET),
        ("7 nodes a day apart", [r2 + n for n in (0, 1, 2, 3, 4, 31, 32)],
         [0.0521, 0.0521, 0.0523, 0.0522, 0.0524, 0.0530, 0.0531], A360, None),
        ("5 nodes falling to R t below -1", [r1 + n for n in (0, 365, 730, 1095, 1461)],
         [0.02, -0.1, -0.35, -0.42, -0.45], A365, NULL_CALENDAR),
        ("9 nodes over leap years", [D(28, 2, 2027) + n for n in (0, 366, 367, 731, 1096, 1461, 1462, 2192, 3653)],
         [0.0301, 0.0312, 0.0313, 0.0322, 0.0335, 0.0342, 0.0343, 0.0361, 0.0389],
         {"type": "ActualActual", "convention": int(ql.ActualActual.ISDA)}, TARGET),
    ]
    assert len(out) == 8
    return out


def discount_dates(dates):
    """50 dates from the first node to the last, closer together early on, then 10 past the last."""
    first, last = ql.Date(dates[0]), ql.Date(dates[-1])
    span = last - first
    inside = [first + int(round(span * (k / 49.0) ** 1.5)) for k in range(50)]
    past = [last + n for n in (1, 2, 7, 30, 91, 182, 365, 730, 1826, 3653)]
    return v.enc(inside + past), [False] * 50 + [True] * 10


def case(name, dates, rates, dc, cal, named, jumps=None, jump_dates=None):
    """A case of one curve, built through the named class or the generic."""
    dates = v.enc(dates)
    jump_dates = v.enc(jump_dates)
    when, flags = discount_dates(dates)
    try:
        curve = make_curve(dates, rates, dc, cal, jumps, jump_dates)
    except RuntimeError:
        expected = {"built": [dict(v.THROWS)], "dates": [[]], "times": [[]], "data": [[]], "zeroRates": [[]],
                    "nodes": [[]], "maxDate": [None], "discountAt": [[]]}
    else:
        expected = {"built": [True], "dates": [v.enc(list(curve.dates()))], "times": [list(curve.times())],
                    "data": [list(curve.data())], "zeroRates": [list(curve.zeroRates())],
                    "nodes": [[[d.serialNumber(), x] for d, x in curve.nodes()]],
                    "maxDate": [v.enc(curve.maxDate())],
                    "discountAt": [[v.attempt(lambda d=d, x=x: curve.discount(ql.Date(d), x))
                                    for d, x in zip(when, flags)]]}
    with v.case_settings(ql.Date(dates[0])) as st:
        inputs = {"settings": st, "dates": dates, "rates": rates, "dayCounter": dc, "calendar": cal, "jumps": jumps,
                  "jumpDates": jump_dates, "classes": [named], "discountDates": when, "extrapolate": flags}
    return {"name": name, "inputs": inputs, "expected": expected}


def refusals():
    """(name, dates, rates, jumps, jump dates) for the 5 refusals."""
    r = D(15, 1, 2024)
    three = [r, r + 182, r + 365]
    return [
        ("one date", [r], [0.03], None, None),
        ("three dates, two rates", three, [0.03, 0.031], None, None),
        ("unsorted dates", [r, r + 365, r + 182], [0.03, 0.031, 0.032], None, None),
        ("two equal dates", [r, r + 182, r + 182], [0.03, 0.031, 0.032], None, None),
        ("two jumps, one jump date", three, [0.03, 0.031, 0.032], [0.999, 0.998], [r + 90]),
    ]


def cases():
    out = []
    for k, (name, dates, rates, dc, cal) in enumerate(node_sets()):
        out.append(case(name, dates, rates, dc, cal, "SimpleZeroCurve" if k % 2 == 0 else None))
    sets = {s[0]: s for s in node_sets()}
    _, dates, rates, dc, cal = sets["12 nodes to 30 years"]
    out.append(case("2 jumps on given dates", dates, rates, dc, cal, "SimpleZeroCurve", [0.998, 0.9995],
                    [D(10, 1, 2024), D(30, 6, 2031)]))
    out.append(case("3 jumps on default dates", dates, rates, dc, cal, None, [0.999, 0.9985, 1.0005]))
    for k, (name, dates, rates, jumps, jump_dates) in enumerate(refusals()):
        out.append(case(f"refused: {name}", dates, rates, A365, TARGET, "SimpleZeroCurve" if k % 2 == 0 else None,
                        jumps, jump_dates))
    check_rows(out)
    return out


def check_rows(out):
    for c in out:
        built = c["expected"]["built"]
        assert built == ([v.THROWS] if c["name"].startswith("refused: ") else [True]), c["name"]
    falling = next(c for c in out if c["name"] == "5 nodes falling to R t below -1")
    assert any(isinstance(x, float) and x < 0.0 for x in falling["expected"]["discountAt"][0]), falling["name"]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/interpolatedsimplezerocurve.hpp", cases)
