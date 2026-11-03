# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's PiecewiseSpreadYieldCurve (ql/termstructures/yield/piecewisespreadyieldcurve.hpp, on
spreadbootstraptraits.hpp's SpreadTraits<Discount>), and the InterpolatedSpreadDiscountCurve and SpreadDiscountCurve
of ql/termstructures/yield/spreaddiscountcurve.hpp it derives from: spread discount factors bootstrapped over a base
curve, and the spread discount curve rebuilt from their nodes.

The six originals of tools/vectors/gen/termstructures/yield/zerospreadedtermstructure.py, under its evaluation date
Monday 16 June 2025, each a base under two of four interpolators by rotation (log-linear, the wheel's
PiecewiseLogLinearSpreadDiscount; log-cubic with the spline's derivative, monotonic and not, and log-mixed-linear-cubic,
the wheel's PiecewiseLogCubicSpreadDiscount and PiecewiseLogMixedLinearCubicSpreadDiscount on their factories), on a
six-month deposit and swaps to 1, 2, 3, 5 and 7 years against Euribor 6M, at rates from 3.1% to 3.6%, with QuantLib's
default iterative bootstrap.

A case is one base under one interpolator. Its outputs are whether the curve builds, its nodes' dates and data, its
reference date, day counter (its name), calendar (the base's name, or null), maximum date and time, and whether it
allows extrapolation (the base's flag); at the base's 30 query dates
(the zero-spreaded generator's) its discount, Actual/365 (Fixed) continuous zero rate, semiannual forward rate over the
six months after the date and instantaneous continuous forward rate; and the discount at those dates of the spread
discount curve on the same interpolator rebuilt from the nodes over the base (the wheel's SpreadDiscountCurve,
LogCubicSpreadDiscountCurve and LogMixedLinearCubicSpreadDiscountCurve), its extrapolation enabled. A refused value is
{"throws": true}.

Left out: the rebuilt curve's reads past a base that doesn't extrapolate, where QuantLib's spread discount curve reads
the base without the extrapolation flag and throws, and nanoql's reads it with the flag
(tests/NanoQL.Tests/Divergences/DerivedCurveExtrapolationTests.cs).

Each case's inputs hold its settings, the "base" (the zero-spreaded generator's spec), the "interpolator" (as
tests/NanoQL.Tests/Vectors/BootstrapInputs.cs reads it), the "helpers" (tools/vectors/gen/termstructures/yield/
ratehelpers.py's specs), the "dates" and the rebuilt curve's "rawDates". Read back by
tests/NanoQL.Tests/Vectors/TermStructures/Yield/PiecewiseSpreadYieldCurveVectorTests.cs."""
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
rh = _load("wheel_ratehelpers", "termstructures/yield/ratehelpers.py")
CI = ql.CubicInterpolation


def log_cubic(monotonic):
    return {"type": "LogCubic", "derivativeApprox": int(CI.Spline), "monotonic": monotonic,
            "leftCondition": int(CI.SecondDerivative), "leftConditionValue": 0.0,
            "rightCondition": int(CI.SecondDerivative), "rightConditionValue": 0.0}


LOG_MIXED = {"type": "LogMixedLinearCubic", "n": 2, "behavior": int(ql.MixedInterpolation.ShareRanges),
             "derivativeApprox": int(CI.Spline), "monotonic": True, "leftCondition": int(CI.SecondDerivative),
             "leftConditionValue": 0.0, "rightCondition": int(CI.SecondDerivative), "rightConditionValue": 0.0}
INTERPOLATORS = [{"type": "LogLinear"}, log_cubic(True), log_cubic(False), LOG_MIXED]

HELPERS = ([rh.deposit_tenor(0.031, (6, ql.Months), 2, rh.TARGET, ql.ModifiedFollowing, False, rh.A360)]
           + [rh.swap_tenor(rate, (years, ql.Years)) for years, rate in
              [(1, 0.0315), (2, 0.032), (3, 0.033), (5, 0.0345), (7, 0.036)]])


def factory(spec):
    t = spec["type"]
    if t == "LogLinear":
        return ql.LogLinear()
    args = (spec["derivativeApprox"], spec["monotonic"], spec["leftCondition"], spec["leftConditionValue"],
            spec["rightCondition"], spec["rightConditionValue"])
    if t == "LogCubic":
        return ql.LogCubic(*args)
    return ql.LogMixedLinearCubic(spec["n"], spec["behavior"], *args)


def curves(spec, h, helpers):
    """The bootstrapped spread curve and the class of its rebuilt spread discount curve."""
    t = spec["type"]
    if t == "LogLinear":
        return ql.PiecewiseLogLinearSpreadDiscount(h, helpers), ql.SpreadDiscountCurve
    if t == "LogCubic":
        return ql.PiecewiseLogCubicSpreadDiscount(h, helpers, factory(spec)), ql.LogCubicSpreadDiscountCurve
    return (ql.PiecewiseLogMixedLinearCubicSpreadDiscount(h, helpers, factory(spec)),
            ql.LogMixedLinearCubicSpreadDiscountCurve)


def cases():
    out = []
    for k, (name, spec) in enumerate(zs.ORIGINALS):
        for i in range(2):
            interpolator = INTERPOLATORS[(2 * k + i) % len(INTERPOLATORS)]
            with v.case_settings(zs.TODAY) as st:
                base = zs.original(spec)
                h = ql.YieldTermStructureHandle(base)
                helpers = [rh.make_helper(x) for x in HELPERS]
                curve, raw_class = curves(interpolator, h, helpers)
                dates = zs.query_dates(base, spec)
                expected = {"built": v.attempt(lambda: curve.nodes() and True)}
                if expected["built"] is True:
                    nodes = curve.nodes()
                    expected["nodeDates"] = v.enc([d for d, _ in nodes])
                    expected["nodeData"] = [x for _, x in nodes]
                    expected.update(fs.curve_outputs(curve, dates))
                    raw = raw_class(h, [d for d, _ in nodes], [x for _, x in nodes], factory(interpolator))
                    raw.enableExtrapolation()
                    # past a base that doesn't extrapolate QuantLib's spread discount curve throws, reading the base
                    # without the flag, where nanoql's reads it with: left out
                    raw_dates = [d for d in dates if base.allowsExtrapolation() or d <= base.maxDate()]
                    expected["rawDiscount"] = [v.attempt(lambda d=d: raw.discount(d)) for d in raw_dates]
                out.append({"name": f"{name} / {interpolator['type']}"
                                    + (f" {interpolator['monotonic']}" if "monotonic" in interpolator else ""),
                            "inputs": {"settings": st, "base": spec, "interpolator": interpolator,
                                       "helpers": HELPERS, "dates": v.enc(dates), "rawDates": v.enc(raw_dates)},
                            "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/piecewisespreadyieldcurve.hpp", cases)
