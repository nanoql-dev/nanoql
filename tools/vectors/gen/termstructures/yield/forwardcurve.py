# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's InterpolatedForwardCurve (ql/termstructures/yield/forwardcurve.hpp), on its base
ZeroYieldStructure (ql/termstructures/yield/zeroyieldstructure.hpp): 32 curves in 8 cases, and 6 refusals.

- 6 node sets, a case each, each on 4 interpolators: backward-flat (the wheel's ForwardCurve), linear
  (LinearForwardCurve), cubic with Kruger's derivatives (CubicForwardCurve) and the cubic spline
  (NaturalCubicForwardCurve, on the wheel's SplineCubic, QuantLib's Cubic with the spline and no monotonicity). The
  node sets: a single node, which only backward-flat interpolation takes, the others refusing too few dates; 3 nodes;
  5 within a year; 9 to 20 years on Actual/360; 6 of negative and positive forwards; 7 a day apart; on TARGET, the
  null calendar and QuantLib's empty calendar.
- 2 cases with jumps: two on given dates, one of them before the reference date, which QuantLib's discount
  skips; and three on QuantLib's default dates, 31 December of the reference date's year and the years after it.
- 6 refusals, in QuantLib's checks: too few dates; dates and forwards in different numbers; unsorted dates; two equal
  dates; jumps and jump dates in different numbers; and a pair of these, whose first check refuses (the order nanoql's
  tests pin, since a refusal here is recorded only as a throw). An empty list of dates is left out: QuantLib's first
  failure there is dates.at(0)'s std::out_of_range, which the wheel raises as IndexError.

Each curve's outputs are whether it builds, QuantLib's refusals recorded as its throw; its dates, times, forwards (its
data) and nodes (each [date, value]); its maximum date; and, at 30 dates, 25 from its reference date to its last node
and 5 past it, extrapolated, its continuously compounded zero rate (Actual/365 (Fixed), annual), its instantaneous
forward rate (continuous, by QuantLib's finite difference) and its discount; as arrays of one element per curve, a
refused curve's empty (its maximum date null). The wheel binds no times() for the forward curves: each node's time is
the curve's own timeFromReference, the day counter's year fraction from the first node, which is what QuantLib's
setupTimes stores. Each case's inputs hold its settings (the evaluation date is the reference date; the curves don't
read it), "dates", "forwards", "dayCounter", "calendar" (null for QuantLib's empty one), "jumps" and "jumpDates" (null
for none; null jump dates are QuantLib's default), "interpolators" (the interpolator specs of
termstructures/yield/piecewiseyieldcurve.py), "classes" (for each curve, the named class nanoql builds it through,
ForwardCurve, LinearForwardCurve or CubicForwardCurve, or null for the generic InterpolatedForwardCurve),
"discountDates" and "extrapolate". The wheel's curves are built from (dates, forwards, day counter, calendar,
interpolator), or with jumps (…, calendar, jumps, jump dates, interpolator), QuantLib's empty calendar being
ql.Schedule().calendar(). Read back by tests/NanoQL.Tests/Vectors/TermStructures/Yield/ForwardCurveVectorTests.cs."""
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

INTERPOLATORS = [pw.BACKWARD_FLAT, pw.LINEAR, pw.KRUGER_CUBIC, pw.SPLINE_CUBIC]
# The nanoql class each curve is built through: the named class of its closed generic, or the generic itself.
CLASSES = ["ForwardCurve", "LinearForwardCurve", "CubicForwardCurve", None]
# The wheel's names nanoql's INTERPOLATORS don't cover, each with its class and interpolator, by the nanoql class.
FORWARD_FLAT = {"type": "ForwardFlat"}
NAMED_WHEEL = {"ForwardFlatForwardCurve": (ql.ForwardFlatForwardCurve, lambda: ql.ForwardFlat())}
# The wheel's class and interpolator for each of INTERPOLATORS.
WHEEL = [(ql.ForwardCurve, lambda: ql.BackwardFlat()),
         (ql.LinearForwardCurve, lambda: ql.Linear()),
         (ql.CubicForwardCurve, lambda: pw.make_interpolator("ForwardRate", pw.KRUGER_CUBIC)),
         (ql.NaturalCubicForwardCurve, lambda: ql.SplineCubic())]


def make_curve(dates, forwards, dc, cal, interpolator, jumps=None, jump_dates=None, named=None):
    """The wheel's curve; jumps or their dates given as None are none."""
    cls, make = NAMED_WHEEL[named] if named in NAMED_WHEEL else WHEEL[INTERPOLATORS.index(interpolator)]
    head = ([ql.Date(x) for x in dates], forwards, irx.day_counter(dc),
            ql.Schedule().calendar() if cal is None else irx.calendar(cal))
    if jumps is None and jump_dates is None:
        return cls(*head, make())
    return cls(*head, [ql.QuoteHandle(ql.SimpleQuote(x)) for x in jumps or []],
               [ql.Date(x) for x in jump_dates or []], make())


def node_sets():
    """(name, dates, forwards, day counter, calendar) for the 6 node sets."""
    r1, r2, r3 = D(15, 1, 2024), D(29, 2, 2024), D(31, 12, 2025)
    out = [
        ("a single node", [r1], [0.035], A365, TARGET),
        ("3 nodes", [r2, D(29, 8, 2024), D(28, 2, 2026)], [0.0412, 0.0398, 0.0371], A365, NULL_CALENDAR),
        ("5 nodes within a year", [r1 + n for n in (0, 7, 31, 92, 182)], [0.0531, 0.0533, 0.0529, 0.0518, 0.0502],
         A360, None),
        ("9 nodes to 20 years", [r3 + n for n in (0, 182, 365, 730, 1095, 1826, 2557, 3652, 7305)],
         [0.0301, 0.0312, 0.0329, 0.0351, 0.0366, 0.0389, 0.0402, 0.0413, 0.0398], A360, TARGET),
        ("6 nodes of negative and positive forwards", [r3 + n for n in (0, 90, 181, 365, 547, 730)],
         [-0.0052, -0.0038, -0.0011, 0.0015, 0.0034, 0.0049], A365, TARGET),
        ("7 nodes a day apart", [r2 + n for n in (0, 1, 2, 3, 4, 31, 32)],
         [0.0521, 0.0525, 0.0519, 0.0522, 0.0524, 0.0530, 0.0527], A360, None),
    ]
    assert len(out) == 6
    return out


def rate_dates(dates):
    """25 dates from the first node to the last, closer together early on, then 5 past the last; past the reference
    date alone for a single node."""
    first, last = ql.Date(dates[0]), ql.Date(dates[-1])
    span = last - first
    inside = [first + int(round(span * (k / 24.0) ** 1.5)) for k in range(25)]
    past = [last + n for n in (1, 30, 365, 1826, 3653)]
    return v.enc(inside + past), [False] * 25 + [True] * 5


def case(name, dates, forwards, dc, cal, interpolators=None, classes=None, jumps=None, jump_dates=None):
    """A case of curves on the same nodes, one per interpolator."""
    interpolators = INTERPOLATORS if interpolators is None else interpolators
    classes = CLASSES if classes is None else classes
    dates = v.enc(dates)
    jump_dates = v.enc(jump_dates)
    when, flags = rate_dates(dates)
    keys = ["built", "dates", "times", "forwards", "nodes", "maxDate", "zeroRateAt", "forwardAt", "discountAt"]
    expected = {k: [] for k in keys}
    result_dc = ql.Actual365Fixed()
    for interpolator, named in zip(interpolators, classes):
        try:
            curve = make_curve(dates, forwards, dc, cal, interpolator, jumps, jump_dates, named)
        except RuntimeError:
            one = {"built": dict(v.THROWS), "dates": [], "times": [], "forwards": [], "nodes": [], "maxDate": None,
                   "zeroRateAt": [], "forwardAt": [], "discountAt": []}
        else:
            pairs = list(zip(when, flags))
            one = {"built": True, "dates": v.enc(list(curve.dates())),
                   "times": [curve.timeFromReference(d) for d in curve.dates()],
                   "forwards": list(curve.forwards()),
                   "nodes": [[d.serialNumber(), x] for d, x in curve.nodes()], "maxDate": v.enc(curve.maxDate()),
                   "zeroRateAt": [v.attempt(lambda d=d, x=x: curve.zeroRate(ql.Date(d), result_dc, ql.Continuous,
                                                                           ql.Annual, x).rate())
                                  for d, x in pairs],
                   "forwardAt": [v.attempt(lambda d=d, x=x: curve.forwardRate(ql.Date(d), ql.Date(d), result_dc,
                                                                             ql.Continuous, ql.Annual, x).rate())
                                 for d, x in pairs],
                   "discountAt": [v.attempt(lambda d=d, x=x: curve.discount(ql.Date(d), x)) for d, x in pairs]}
        for k in keys:
            expected[k].append(one[k])
    with v.case_settings(ql.Date(dates[0])) as st:
        inputs = {"settings": st, "dates": dates, "forwards": forwards, "dayCounter": dc, "calendar": cal,
                  "jumps": jumps, "jumpDates": jump_dates, "interpolators": interpolators, "classes": classes,
                  "discountDates": when, "extrapolate": flags}
    return {"name": name, "inputs": inputs, "expected": expected}


def refusals():
    """(name, dates, forwards, jumps, jump dates) for the 6 refusals, on linear interpolation."""
    r = D(15, 1, 2024)
    three = [r, r + 182, r + 365]
    return [
        ("one date", [r], [0.03], None, None),
        ("three dates, two forwards", three, [0.03, 0.031], None, None),
        ("unsorted dates", [r, r + 365, r + 182], [0.03, 0.031, 0.032], None, None),
        ("two equal dates", [r, r + 182, r + 182], [0.03, 0.031, 0.032], None, None),
        ("two jumps, one jump date", three, [0.03, 0.031, 0.032], [0.999, 0.998], [r + 90]),
        ("two forwards and unsorted dates", [r, r + 365, r + 182], [0.03, 0.031], None, None),
    ]


def cases():
    out = []
    for name, dates, forwards, dc, cal in node_sets():
        out.append(case(name, dates, forwards, dc, cal))
    sets = {s[0]: s for s in node_sets()}
    _, dates, forwards, dc, cal = sets["9 nodes to 20 years"]
    out.append(case("2 jumps on given dates", dates, forwards, dc, cal,
                    jumps=[0.998, 0.9995], jump_dates=[D(10, 1, 2024), D(30, 6, 2031)]))
    out.append(case("3 jumps on default dates", dates, forwards, dc, cal, jumps=[0.999, 0.9985, 1.0005]))
    for name, dates, forwards, jumps, jump_dates in refusals():
        out.append(case(f"refused: {name}", dates, forwards, A365, TARGET, [pw.LINEAR], ["LinearForwardCurve"],
                        jumps, jump_dates))
    for set_name in ("5 nodes within a year", "9 nodes to 20 years", "6 nodes of negative and positive forwards"):
        _, dates, forwards, dc, cal = sets[set_name]
        out.append(case(f"ForwardFlatForwardCurve on {set_name}", dates, forwards, dc, cal, [FORWARD_FLAT],
                        ["ForwardFlatForwardCurve"]))
    out.append(case("ForwardFlatForwardCurve with 2 jumps on given dates", *sets["9 nodes to 20 years"][1:],
                    [FORWARD_FLAT], ["ForwardFlatForwardCurve"], [0.998, 0.9995], [D(10, 1, 2024), D(30, 6, 2031)]))
    check_rows(out)
    return out


def check_rows(out):
    for c in out:
        built = c["expected"]["built"]
        if c["name"].startswith("refused: "):
            assert built == [v.THROWS], c["name"]
        elif c["name"] == "a single node":
            # only backward-flat interpolation takes one point
            assert built == [True] + [v.THROWS] * 3, c["name"]
        else:
            assert all(b is True for b in built), c["name"]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/forwardcurve.hpp", cases)
