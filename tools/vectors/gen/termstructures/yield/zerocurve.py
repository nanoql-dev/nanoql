# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's InterpolatedZeroCurve (ql/termstructures/yield/zerocurve.hpp) and its base
ZeroYieldStructure (ql/termstructures/yield/zeroyieldstructure.hpp): 110 curves in 22 cases, and 14 refusals.

- 6 node sets x 3 compoundings, a case each, each on 5 interpolators: linear (the wheel's ZeroCurve), cubic with
  Kruger's derivatives (CubicZeroCurve), the cubic spline (NaturalCubicZeroCurve, on the wheel's SplineCubic,
  QuantLib's Cubic with the spline and no monotonicity), the monotonic cubic spline (MonotonicCubicZeroCurve, on the
  wheel's MonotonicCubic, QuantLib's Cubic with the spline, monotonic) and log-linear (_LogLinearZeroCurve, the target
  of the wheel's LogLinearZeroCurve shim). The node sets: 2 nodes; 4 within a year; 8 to 10 years; 12 to 30 years on
  Actual/360; 6 of negative and positive rates; 7 a day apart; on TARGET, the null calendar and QuantLib's empty
  calendar. The compoundings: continuous; compounded semiannually; simple. And 2 cases on continuous rates through
  the wheel's deprecated names' classes, _LogLinearZeroCurve and _LogCubicZeroCurve (QuantLib's DefaultLogCubic,
  Kruger's derivatives, monotonic), each through nanoql's LogLinearZeroCurve and LogCubicZeroCurve. Given rates are converted to their
  continuous equivalents, at about a day (1/365) for the first node, so the curve's data differ from them.
- 2 cases on QuantLib's continuous compounding, where the frequency is never read: with NoFrequency, and with 7, no
  member of Frequency; and 2 with jumps: two on given dates, one of them before the reference date, which QuantLib's
  discount skips, on quarterly compounded rates; and three on QuantLib's default dates, 31 December of the reference
  date's year and the years after it.
- 14 refusals, in QuantLib's checks: too few dates; dates and rates in different numbers; unsorted dates; two equal
  dates; two dates of the same time under 30/360; jumps and jump dates in different numbers; compounded rates with
  NoFrequency and with Once; SimpleThenCompounded, which QuantLib can't convert directly; a simple rate whose compound
  factor isn't positive; a negative rate under log-linear interpolation; and pairs of these, whose first check
  refuses (the order nanoql's tests pin, since a refusal here is recorded only as a throw). An empty list of dates is
  left out: QuantLib's first failure there is dates.at(0)'s std::out_of_range, which the wheel raises as IndexError.

Each curve's outputs are whether it builds, QuantLib's refusals recorded as its throw; its dates, times, data and zero
rates (its data again, by the zero curve's own inspector), and nodes (each [date, value]); its maximum date; and, at 30
dates, 25 from its reference date to its last node and 5 past it, extrapolated, its continuously compounded zero rate
(Actual/365 (Fixed), annual) and its discount; as arrays of one element per curve, a refused curve's empty (its
maximum date null). Each case's inputs hold its settings (the evaluation date is the reference date; the curves don't
read it), "dates", "rates", "dayCounter", "calendar" (null for QuantLib's empty one), "jumps" and "jumpDates" (null for
none; null jump dates are QuantLib's default), "compounding" and "frequency" (QuantLib's integers), "interpolators"
(the interpolator specs of termstructures/yield/piecewiseyieldcurve.py), "classes" (for each curve, the named class
nanoql builds it through, ZeroCurve or CubicZeroCurve, or null for the generic InterpolatedZeroCurve),
"discountDates" and "extrapolate". The wheel's curves are built from (dates, rates, day counter, calendar,
interpolator, compounding, frequency), or with jumps (…, calendar, jumps, jump dates, interpolator, compounding,
frequency), QuantLib's empty calendar being ql.Schedule().calendar(). Read back by
tests/NanoQL.Tests/Vectors/TermStructures/Yield/ZeroCurveVectorTests.cs."""
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
irx, CI = pw.irx, pw.CI

D = pw.D
TARGET, NULL_CALENDAR = irx.TARGET, irx.NULL_CALENDAR
A360, A365, THIRTY360_BB = irx.A360, irx.A365, irx.THIRTY360_BB

LINEAR, LOG_LINEAR = pw.LINEAR, pw.LOG_LINEAR
KRUGER_CUBIC, SPLINE_CUBIC, MONOTONIC_CUBIC = pw.KRUGER_CUBIC, pw.SPLINE_CUBIC, pw.cubic(CI.Spline, monotonic=True)
INTERPOLATORS = [LINEAR, KRUGER_CUBIC, SPLINE_CUBIC, MONOTONIC_CUBIC, LOG_LINEAR]
# The nanoql class each curve is built through: the named class of its closed generic, or the generic itself.
CLASSES = ["ZeroCurve", "CubicZeroCurve", None, "CubicZeroCurve", None]

COMPOUNDINGS = [("continuous", int(ql.Continuous), int(ql.Annual)),
                ("compounded semiannually", int(ql.Compounded), int(ql.Semiannual)),
                ("simple", int(ql.Simple), int(ql.Annual))]


# The wheel's deprecated names, each with the class its shim builds and its interpolator, by the nanoql class.
LOG_CUBIC = pw.cubic(CI.Kruger, True, log=True)
NAMED_WHEEL = {"LogLinearZeroCurve": (ql.QuantLib._LogLinearZeroCurve, lambda: ql.LogLinear()),
               "LogCubicZeroCurve": (ql.QuantLib._LogCubicZeroCurve, lambda: ql.DefaultLogCubic())}

# The wheel's class and interpolator for each of INTERPOLATORS.
WHEEL = [(ql.ZeroCurve, lambda: ql.Linear()),
         (ql.CubicZeroCurve, lambda: pw.make_interpolator("ZeroYield", KRUGER_CUBIC)),
         (ql.NaturalCubicZeroCurve, lambda: ql.SplineCubic()),
         (ql.MonotonicCubicZeroCurve, lambda: ql.MonotonicCubic()),
         (ql.QuantLib._LogLinearZeroCurve, lambda: ql.LogLinear())]


def make_curve(dates, rates, dc, cal, interpolator, compounding, frequency, jumps=None, jump_dates=None, named=None):
    """The wheel's curve; jumps or their dates given as None are none."""
    cls, make = NAMED_WHEEL[named] if named in NAMED_WHEEL else WHEEL[INTERPOLATORS.index(interpolator)]
    head = ([ql.Date(x) for x in dates], rates, irx.day_counter(dc),
            ql.Schedule().calendar() if cal is None else irx.calendar(cal))
    if jumps is None and jump_dates is None:
        return cls(*head, make(), compounding, frequency)
    return cls(*head, [ql.QuoteHandle(ql.SimpleQuote(x)) for x in jumps or []],
               [ql.Date(x) for x in jump_dates or []], make(), compounding, frequency)


def node_sets():
    """(name, dates, rates, day counter, calendar) for the 6 node sets."""
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
    ]
    assert len(out) == 6
    return out


def zero_dates(dates):
    """25 dates from the first node to the last, closer together early on, then 5 past the last."""
    first, last = ql.Date(dates[0]), ql.Date(dates[-1])
    span = last - first
    inside = [first + int(round(span * (k / 24.0) ** 1.5)) for k in range(25)]
    past = [last + n for n in (1, 30, 365, 1826, 3653)]
    return v.enc(inside + past), [False] * 25 + [True] * 5


def case(name, dates, rates, dc, cal, compounding, frequency, interpolators=None, classes=None, jumps=None,
         jump_dates=None):
    """A case of curves on the same nodes, one per interpolator."""
    interpolators = INTERPOLATORS if interpolators is None else interpolators
    classes = CLASSES if classes is None else classes
    dates = v.enc(dates)
    jump_dates = v.enc(jump_dates)
    when, flags = zero_dates(dates)
    keys = ["built", "dates", "times", "data", "zeroRates", "nodes", "maxDate", "zeroRateAt", "discountAt"]
    expected = {k: [] for k in keys}
    result_dc = ql.Actual365Fixed()
    for interpolator, named in zip(interpolators, classes):
        try:
            curve = make_curve(dates, rates, dc, cal, interpolator, compounding, frequency, jumps, jump_dates, named)
        except RuntimeError:
            one = {"built": dict(v.THROWS), "dates": [], "times": [], "data": [], "zeroRates": [], "nodes": [],
                   "maxDate": None, "zeroRateAt": [], "discountAt": []}
        else:
            pairs = list(zip(when, flags))
            one = {"built": True, "dates": v.enc(list(curve.dates())), "times": list(curve.times()),
                   "data": list(curve.data()), "zeroRates": list(curve.zeroRates()),
                   "nodes": [[d.serialNumber(), x] for d, x in curve.nodes()], "maxDate": v.enc(curve.maxDate()),
                   "zeroRateAt": [v.attempt(lambda d=d, x=x: curve.zeroRate(ql.Date(d), result_dc, ql.Continuous,
                                                                           ql.Annual, x).rate())
                                  for d, x in pairs],
                   "discountAt": [v.attempt(lambda d=d, x=x: curve.discount(ql.Date(d), x)) for d, x in pairs]}
        for k in keys:
            expected[k].append(one[k])
    with v.case_settings(ql.Date(dates[0])) as st:
        inputs = {"settings": st, "dates": dates, "rates": rates, "dayCounter": dc, "calendar": cal, "jumps": jumps,
                  "jumpDates": jump_dates, "compounding": compounding, "frequency": frequency,
                  "interpolators": interpolators, "classes": classes, "discountDates": when, "extrapolate": flags}
    return {"name": name, "inputs": inputs, "expected": expected}


def refusals():
    """(name, dates, rates, day counter, compounding, frequency, interpolator) for the 14 refusals."""
    r = D(15, 1, 2024)
    three = [r, r + 182, r + 365]
    cont, comp, simple = int(ql.Continuous), int(ql.Compounded), int(ql.Simple)
    annual, none, once = int(ql.Annual), int(ql.NoFrequency), int(ql.Once)
    return [
        ("one date", [r], [0.03], A365, cont, annual, LINEAR),
        ("three dates, two rates", three, [0.03, 0.031], A365, cont, annual, LINEAR),
        ("unsorted dates", [r, r + 365, r + 182], [0.03, 0.031, 0.032], A365, cont, annual, LINEAR),
        ("two equal dates", [r, r + 182, r + 182], [0.03, 0.031, 0.032], A365, cont, annual, LINEAR),
        ("the same time under 30/360", [D(30, 1, 2024), D(30, 1, 2025), D(31, 1, 2025)], [0.03, 0.031, 0.032],
         THIRTY360_BB, cont, annual, LINEAR),
        ("compounded with NoFrequency", three, [0.03, 0.031, 0.032], A365, comp, none, LINEAR),
        ("compounded with Once", three, [0.03, 0.031, 0.032], A365, comp, once, LINEAR),
        ("SimpleThenCompounded", three, [0.03, 0.031, 0.032], A365, int(ql.SimpleThenCompounded), annual, LINEAR),
        ("a simple rate whose compound factor isn't positive", three, [0.03, -2.5, 0.032], A365, simple, annual,
         LINEAR),
        ("a negative rate under log-linear interpolation", three, [0.03, -0.01, 0.032], A365, cont, annual,
         LOG_LINEAR),
        ("two rates and compounded with NoFrequency", three, [0.03, 0.031], A365, comp, none, LINEAR),
        ("unsorted dates and compounded with Once", [r, r + 365, r + 182], [0.03, 0.031, 0.032], A365, comp, once,
         LINEAR),
        ("compounded with NoFrequency and a negative rate under log-linear interpolation", three,
         [0.03, -0.01, 0.032], A365, comp, none, LOG_LINEAR),
    ]


def cases():
    out = []
    for name, dates, rates, dc, cal in node_sets():
        for label, compounding, frequency in COMPOUNDINGS:
            out.append(case(f"{name} {label}", dates, rates, dc, cal, compounding, frequency))
    sets = {s[0]: s for s in node_sets()}
    _, dates, rates, dc, cal = sets["8 nodes to 10 years"]
    out.append(case("continuous with NoFrequency", dates, rates, dc, cal, int(ql.Continuous), int(ql.NoFrequency)))
    out.append(case("continuous with frequency 7, no member of Frequency", dates, rates, dc, cal,
                    int(ql.Continuous), 7))
    _, dates, rates, dc, cal = sets["12 nodes to 30 years"]
    out.append(case("2 jumps on given dates", dates, rates, dc, cal, int(ql.Compounded), int(ql.Quarterly),
                    jumps=[0.998, 0.9995], jump_dates=[D(10, 1, 2024), D(30, 6, 2031)]))
    out.append(case("3 jumps on default dates", dates, rates, dc, cal, int(ql.Continuous), int(ql.Annual),
                    jumps=[0.999, 0.9985, 1.0005]))
    for name, dates, rates, dc, compounding, frequency, interpolator in refusals():
        out.append(case(f"refused: {name}", dates, rates, dc, TARGET, compounding, frequency, [interpolator],
                        [None]))
    r = D(15, 1, 2024)
    out.append(case("refused: one jump, two jump dates", [r, r + 182, r + 365], [0.03, 0.031, 0.032], A365, TARGET,
                    int(ql.Continuous), int(ql.Annual), [LINEAR], ["ZeroCurve"], [0.999], [r + 30, r + 60]))
    for set_name in ("8 nodes to 10 years", "12 nodes to 30 years"):
        _, dates, rates, dc, cal = sets[set_name]
        out.append(case(f"the deprecated named classes on {set_name}", dates, rates, dc, cal, int(ql.Continuous),
                        int(ql.Annual), [LOG_LINEAR, LOG_CUBIC], ["LogLinearZeroCurve", "LogCubicZeroCurve"]))
    check_rows(out)
    return out


def check_rows(out):
    for c in out:
        built = c["expected"]["built"]
        if c["name"].startswith("refused: "):
            assert built == [v.THROWS], c["name"]
        elif c["name"].startswith("6 nodes of negative and positive rates"):
            # log-linear interpolation refuses the negative rates
            assert built == [True] * 4 + [v.THROWS], c["name"]
        else:
            assert all(b is True for b in built), c["name"]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/zerocurve.hpp", cases)
