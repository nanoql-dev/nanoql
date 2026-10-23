# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's InterpolatedPiecewiseZeroSpreadedTermStructure and its PiecewiseZeroSpreadedTermStructure
(ql/termstructures/yield/piecewisezerospreadedtermstructure.hpp): spreads at dates, interpolated, added to an
original curve's zero rates in a given compounding and frequency.

The six originals of tools/vectors/gen/termstructures/yield/zerospreadedtermstructure.py, under its evaluation date
Monday 16 June 2025, each under the three spread schedules (1% at a year and 1.5% at ten; -0.5%, 0.2% and 1% at three
months, a year and twenty; and 0.4%, 0.6%, 0 and 1.2% at the reference date, six months, two years and thirty), from
the curve's reference date, with two interpolators by rotation and one setting of compounding and frequency by
rotation; and each original under the second schedule through the named, linear class at QuantLib's default rule. The
interpolators are linear and backward-flat, and the cubic one with Kruger's, the spline's and the parabolic
derivative, monotonic or not, the wheel's SpreadedLinearZeroInterpolatedTermStructure,
SpreadedBackwardFlatZeroInterpolatedTermStructure and SpreadedCubicZeroInterpolatedTermStructure, the last on its
Cubic factory; the settings continuous, annually compounded and simple.

A case is one original under one schedule, interpolator and setting. Its outputs are the curve's reference date, day
counter (its name), calendar (its name, or null), maximum date (the earlier of the original's and the last spread's)
and time, and whether it allows extrapolation (the original's flag); and at the zero-spreaded generator's 30 dates its
discount, Actual/365 (Fixed) continuous zero rate, semiannual forward rate over the six months after the date and
instantaneous continuous forward rate. A refused value is {"throws": true}.

Each case's inputs hold its settings, the "original" (the zero-spreaded generator's spec), the "spreads", the
"spreadDates", the "interpolator" ({"type": "Linear" | "BackwardFlat"}, or {"type": "Cubic", "derivativeApprox",
"monotonic", "leftCondition", "leftConditionValue", "rightCondition", "rightConditionValue"}, as
tests/NanoQL.Tests/Vectors/BootstrapInputs.cs reads them), "named" for the linear class, and the "compounding" and
"frequency" as QuantLib's integers. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/Yield/PiecewiseZeroSpreadedTermStructureVectorTests.cs."""
import importlib.util
import os
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


zs = _load("zerospreadedtermstructure", "termstructures/yield/zerospreadedtermstructure.py")
fs = _load("forwardspreadedtermstructure", "termstructures/yield/forwardspreadedtermstructure.py")
CI = ql.CubicInterpolation

SCHEDULES = [
    [(ql.Period(1, ql.Years), 0.01), (ql.Period(10, ql.Years), 0.015)],
    [(ql.Period(3, ql.Months), -0.005), (ql.Period(1, ql.Years), 0.002), (ql.Period(20, ql.Years), 0.01)],
    [(ql.Period(0, ql.Days), 0.004), (ql.Period(6, ql.Months), 0.006), (ql.Period(2, ql.Years), 0.0),
     (ql.Period(30, ql.Years), 0.012)],
]


def cubic(da, monotonic):
    return {"type": "Cubic", "derivativeApprox": int(da), "monotonic": monotonic,
            "leftCondition": int(CI.SecondDerivative), "leftConditionValue": 0.0,
            "rightCondition": int(CI.SecondDerivative), "rightConditionValue": 0.0}


INTERPOLATORS = [{"type": "Linear"}, {"type": "BackwardFlat"}, cubic(CI.Kruger, False), cubic(CI.Spline, False),
                 cubic(CI.Parabolic, False), cubic(CI.Parabolic, True), cubic(CI.Kruger, True)]
SETTINGS = [(ql.Continuous, ql.NoFrequency), (ql.Compounded, ql.Annual), (ql.Simple, ql.Annual)]


def make_curve(base, spreads, dates, interpolator, comp, freq, named):
    h = ql.YieldTermStructureHandle(base)
    quotes = [ql.QuoteHandle(ql.SimpleQuote(s)) for s in spreads]
    if named:
        return ql.PiecewiseZeroSpreadedTermStructure(h, quotes, dates, comp, freq)
    t = interpolator["type"]
    if t == "Linear":
        return ql.SpreadedLinearZeroInterpolatedTermStructure(h, quotes, dates, comp, freq)
    if t == "BackwardFlat":
        return ql.SpreadedBackwardFlatZeroInterpolatedTermStructure(h, quotes, dates, comp, freq)
    factory = ql.Cubic(interpolator["derivativeApprox"], interpolator["monotonic"], interpolator["leftCondition"],
                       interpolator["leftConditionValue"], interpolator["rightCondition"],
                       interpolator["rightConditionValue"])
    # the deprecated day counter before the factory, which QuantLib ignores: the original's
    return ql.SpreadedCubicZeroInterpolatedTermStructure(h, quotes, dates, comp, freq, base.dayCounter(), factory)


def case(name, spec, schedule, interpolator, comp, freq, named):
    with v.case_settings(zs.TODAY) as st:
        base = zs.original(spec)
        reference = base.referenceDate()
        dates = [reference + p for p, _ in schedule]
        spreads = [s for _, s in schedule]
        curve = make_curve(base, spreads, dates, interpolator, comp, freq, named)
        query = zs.query_dates(base, spec)
        label = "named" if named else interpolator["type"] + "".join(
            f" {interpolator[k]}" for k in ("derivativeApprox", "monotonic") if k in interpolator)
        return {"name": f"{name} / {len(schedule)} spreads / {label} {comp} {freq}",
                "inputs": {"settings": st, "original": spec, "spreads": spreads, "spreadDates": v.enc(dates),
                           "interpolator": interpolator, "named": named, "compounding": int(comp),
                           "frequency": int(freq), "dates": v.enc(query)},
                "expected": fs.curve_outputs(curve, query)}


def cases():
    out = []
    for k, (name, spec) in enumerate(zs.ORIGINALS):
        for j, schedule in enumerate(SCHEDULES):
            for i in range(2):
                interpolator = INTERPOLATORS[(2 * (3 * k + j) + i) % len(INTERPOLATORS)]
                comp, freq = SETTINGS[(k + j + i) % len(SETTINGS)]
                out.append(case(name, spec, schedule, interpolator, comp, freq, False))
        out.append(case(name, spec, SCHEDULES[1], {"type": "Linear"}, ql.Continuous, ql.NoFrequency, True))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/piecewisezerospreadedtermstructure.hpp", cases)
