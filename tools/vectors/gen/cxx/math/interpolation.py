# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's Interpolation (ql/math/interpolation.hpp) and of the interpolation factories
that the wheel doesn't expose, each output computed by the C++ harness (docs/porting-playbook.md §9.4).

For nine interpolations of the linear, backward-flat (one point and four), log-linear, cubic and log-cubic families:
xMin, xMax, xValues and yValues; isInRange at 40 points (inside, at the ends, within and just beyond QuantLib's close()
of each end, outside, and NaN and the infinities); and extrapolation set on the object, read without the call's flag at
six points outside the range: enableExtrapolation(true) and (false), and disableExtrapolation(true) and (false) after
enableExtrapolation(). Then the constructors' refusals: too few points for each family, Akima's scheme and a Lagrange
condition on 3 points, the periodic condition and FourthOrder under the spline and on 3 points, a y value of 0, -1 or
NaN for the log families, with the cases QuantLib accepts beside them (Lagrange conditions on 4 points, FourthOrder on
2 points, a periodic condition the Kruger scheme ignores), each read at its first node and at a point inside each
interval, where an accepted case shows its interpolation. Then each factory's interpolate on two node sets, read back through the interpolation's
values at 40 points, extrapolating where needed: Linear, BackwardFlat, LogLinear, Cubic (QuantLib's Cubic() and three
parameter sets), LogCubic (with its derivative approximation alone, so its defaults, and with all six parameters, and
the presets MonotonicLogCubic and KrugerLog copied into a LogCubic), MonotonicLogCubic and KrugerLog; with a refusal
through two of them. Then each factory's global and requiredPoints.

An interpolation is the spec {"type", "x", "y"}, with CubicInterpolation's and LogCubicInterpolation's six parameters
("derivativeApprox", "monotonic", "leftCondition", "leftConditionValue", "rightCondition", "rightConditionValue") for
those types; a factory taking parameters is the spec {"type"} with them (tools/cxx/src/math/interpolationspec.hpp).
Read back by tests/NanoQL.Tests/Vectors/Cxx/Numerics/InterpolationCxxVectorTests.cs."""
import math
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

EPS = 2.220446049250313e-16
# CubicInterpolation::DerivativeApprox and BoundaryCondition, by QuantLib's values.
SPLINE, FOURTH_ORDER, PARABOLIC, AKIMA, KRUGER = 0, 3, 4, 6, 7
NOT_A_KNOT, FIRST_DERIVATIVE, SECOND_DERIVATIVE, PERIODIC, LAGRANGE = 0, 1, 2, 3, 4
FACTORIES = ["Linear", "BackwardFlat", "LogLinear", "Cubic", "LogCubic", "MonotonicLogCubic", "KrugerLog"]

FIVE = [0.0, 0.7, 2.0, 2.5, 4.0]
FIVE_Y = [1.0, 0.2, 1.5, 1.4, 3.0]
SIX = [-2.0, -1.2, -0.1, 0.4, 1.9, 3.0]
SIX_Y = [0.5, 0.8, 0.8, 1.6, 1.2, 2.5]


def cubic(xs, ys, da, monotonic, left, left_value, right, right_value, family="CubicInterpolation"):
    return {"type": family, "x": xs, "y": ys, "derivativeApprox": da, "monotonic": monotonic, "leftCondition": left,
            "leftConditionValue": left_value, "rightCondition": right, "rightConditionValue": right_value}


def parameters(da, monotonic, left, left_value, right, right_value):
    return {"derivativeApprox": da, "monotonic": monotonic, "leftCondition": left, "leftConditionValue": left_value,
            "rightCondition": right, "rightConditionValue": right_value}


SPECS = [
    ("LinearInterpolation", {"type": "LinearInterpolation", "x": FIVE, "y": FIVE_Y}),
    ("BackwardFlatInterpolation on one point", {"type": "BackwardFlatInterpolation", "x": [1.0], "y": [2.5]}),
    ("BackwardFlatInterpolation", {"type": "BackwardFlatInterpolation", "x": FIVE[:4], "y": FIVE_Y[:4]}),
    ("LogLinearInterpolation", {"type": "LogLinearInterpolation", "x": FIVE, "y": FIVE_Y}),
    ("CubicInterpolation", cubic(SIX, SIX_Y, SPLINE, True, NOT_A_KNOT, 0.6, FIRST_DERIVATIVE, 0.3)),
    ("KrugerCubic", {"type": "KrugerCubic", "x": SIX, "y": SIX_Y}),
    ("CubicNaturalSpline from 0", {"type": "CubicNaturalSpline", "x": [0.0, 1.0, 2.0], "y": [0.0, 1.0, 0.5]}),
    ("LogCubicInterpolation", cubic(FIVE, FIVE_Y, KRUGER, True, SECOND_DERIVATIVE, 0.0, SECOND_DERIVATIVE, 0.0,
                                    "LogCubicInterpolation")),
    ("MonotonicLogParabolic", {"type": "MonotonicLogParabolic", "x": FIVE, "y": FIVE_Y}),
]


def range_points(xs):
    """40 points for isInRange."""
    lo, hi = xs[0], xs[-1]
    span = hi - lo if hi > lo else 1.0
    points = [lo, hi, 0.5 * (lo + hi)]
    for end, sign in ((lo, -1.0), (hi, 1.0)):
        scale = abs(end) if end != 0.0 else 1.0
        if end != 0.0:
            # within and beyond close()'s 42 epsilons
            points += [end + sign * k * EPS * scale for k in (1, 10, 41, 43, 100, 1000)]
        else:
            # close() to 0 is a difference below (42 epsilons) squared
            points += [sign * d for d in (1e-300, 1e-30, 1.7e-29, 1e-28, 1e-20, 1e-15)]
        points += [end + sign * 0.001 * span, end + sign * span]
    points += [math.nan, math.inf, -math.inf]
    rnd = random.Random(v.SEED)
    while len(points) < 40:
        points.append(rnd.uniform(lo - 0.5 * span, hi + 0.5 * span))
    return points


def refusal_points(xs):
    """Where a constructor case is read: its first node, where every interpolation returns y[0], and a point inside
    each interval, so that a case QuantLib accepts shows its interpolation."""
    if not xs:
        return [0.0]
    return [xs[0]] + [xs[i] + 0.37 * (xs[i + 1] - xs[i]) for i in range(len(xs) - 1)]


def outside(xs):
    lo, hi = xs[0], xs[-1]
    span = hi - lo if hi > lo else 1.0
    return [lo - 0.01 * span, lo - 0.5 * span, lo - 3.0 * span, hi + 0.01 * span, hi + 0.5 * span, hi + 3.0 * span]


REFUSALS = [
    ("LinearInterpolation on no point", {"type": "LinearInterpolation", "x": [], "y": []}),
    ("LinearInterpolation on one point", {"type": "LinearInterpolation", "x": [1.0], "y": [2.0]}),
    ("BackwardFlatInterpolation on no point", {"type": "BackwardFlatInterpolation", "x": [], "y": []}),
    ("LogLinearInterpolation on one point", {"type": "LogLinearInterpolation", "x": [1.0], "y": [2.0]}),
    ("LogLinearInterpolation with a y value of 0", {"type": "LogLinearInterpolation", "x": FIVE,
                                                    "y": [1.0, 0.2, 0.0, 1.4, 3.0]}),
    ("LogLinearInterpolation with a y value of -1", {"type": "LogLinearInterpolation", "x": FIVE,
                                                     "y": [1.0, 0.2, 1.5, 1.4, -1.0]}),
    ("LogLinearInterpolation with a y value NaN", {"type": "LogLinearInterpolation", "x": FIVE,
                                                   "y": [math.nan, 0.2, 1.5, 1.4, 3.0]}),
    ("CubicInterpolation on no point", cubic([], [], KRUGER, False, SECOND_DERIVATIVE, 0.0, SECOND_DERIVATIVE, 0.0)),
    ("CubicInterpolation on one point", cubic([1.0], [2.0], KRUGER, False, SECOND_DERIVATIVE, 0.0, SECOND_DERIVATIVE,
                                              0.0)),
    ("Akima on three points", cubic(FIVE[:3], FIVE_Y[:3], AKIMA, False, SECOND_DERIVATIVE, 0.0, SECOND_DERIVATIVE,
                                    0.0)),
    ("AkimaCubicInterpolation on three points", {"type": "AkimaCubicInterpolation", "x": FIVE[:3], "y": FIVE_Y[:3]}),
    ("Lagrange left condition on three points", cubic(FIVE[:3], FIVE_Y[:3], SPLINE, False, LAGRANGE, 0.0,
                                                      SECOND_DERIVATIVE, 0.0)),
    ("Lagrange right condition on three points", cubic(FIVE[:3], FIVE_Y[:3], SPLINE, False, SECOND_DERIVATIVE, 0.0,
                                                       LAGRANGE, 0.0)),
    ("Lagrange conditions on four points", cubic(FIVE[:4], FIVE_Y[:4], SPLINE, False, LAGRANGE, 0.0, LAGRANGE, 0.0)),
    ("periodic left condition", cubic(FIVE, FIVE_Y, SPLINE, False, PERIODIC, 0.0, SECOND_DERIVATIVE, 0.0)),
    ("periodic right condition", cubic(FIVE, FIVE_Y, SPLINE, False, NOT_A_KNOT, 0.0, PERIODIC, 0.0)),
    ("periodic condition under Kruger, which ignores it", cubic(FIVE, FIVE_Y, KRUGER, False, PERIODIC, 0.0, PERIODIC,
                                                                0.0)),
    ("FourthOrder on three points", cubic(FIVE[:3], FIVE_Y[:3], FOURTH_ORDER, False, SECOND_DERIVATIVE, 0.0,
                                          SECOND_DERIVATIVE, 0.0)),
    ("FourthOrder on two points, which QuantLib accepts", cubic(FIVE[:2], FIVE_Y[:2], FOURTH_ORDER, False,
                                                                SECOND_DERIVATIVE, 0.0, SECOND_DERIVATIVE, 0.0)),
    ("LogCubicInterpolation on one point", cubic([1.0], [2.0], SPLINE, False, SECOND_DERIVATIVE, 0.0,
                                                 SECOND_DERIVATIVE, 0.0, "LogCubicInterpolation")),
    ("LogCubicInterpolation with a y value of 0", cubic(FIVE, [1.0, 0.2, 0.0, 1.4, 3.0], SPLINE, False,
                                                        SECOND_DERIVATIVE, 0.0, SECOND_DERIVATIVE, 0.0,
                                                        "LogCubicInterpolation")),
    ("LogCubicInterpolation, Akima on three points with a y value of 0",
     cubic(FIVE[:3], [1.0, 0.0, 1.5], AKIMA, False, SECOND_DERIVATIVE, 0.0, SECOND_DERIVATIVE, 0.0,
           "LogCubicInterpolation")),
    ("LogCubicInterpolation with a periodic condition", cubic(FIVE, FIVE_Y, SPLINE, False, PERIODIC, 0.0,
                                                              SECOND_DERIVATIVE, 0.0, "LogCubicInterpolation")),
]

FACTORY_SETS = [("five points", FIVE, FIVE_Y),
                ("eight points", [0.0, 0.5, 1.5, 2.0, 3.5, 4.0, 6.0, 7.0], [1.0, 1.0, 2.0, 2.0, 2.0, 0.5, 0.5, 3.0])]

# (name, method, factory spec or None)
INTERPOLATES = [
    ("Linear", "Linear::interpolate", None),
    ("BackwardFlat", "BackwardFlat::interpolate", None),
    ("LogLinear", "LogLinear::interpolate", None),
    ("Cubic()", "Cubic::interpolate", {"type": "Cubic"}),
    ("Cubic monotonic spline", "Cubic::interpolate",
     {"type": "Cubic", **parameters(SPLINE, True, NOT_A_KNOT, 0.6, FIRST_DERIVATIVE, -0.7)}),
    ("Cubic parabolic", "Cubic::interpolate",
     {"type": "Cubic", **parameters(PARABOLIC, False, FIRST_DERIVATIVE, 1.0, SECOND_DERIVATIVE, -1.0)}),
    ("Cubic spline", "Cubic::interpolate",
     {"type": "Cubic", **parameters(SPLINE, False, SECOND_DERIVATIVE, 1.1, LAGRANGE, 0.8)}),
    ("LogCubic(Kruger)", "LogCubic::interpolate", {"type": "LogCubic", "derivativeApprox": KRUGER}),
    ("LogCubic spline", "LogCubic::interpolate",
     {"type": "LogCubic", **parameters(SPLINE, False, FIRST_DERIVATIVE, 0.2, NOT_A_KNOT, -0.6)}),
    ("MonotonicLogCubic as a LogCubic", "LogCubic::interpolate", {"type": "MonotonicLogCubic"}),
    ("KrugerLog as a LogCubic", "LogCubic::interpolate", {"type": "KrugerLog"}),
    ("MonotonicLogCubic", "MonotonicLogCubic::interpolate", None),
    ("KrugerLog", "KrugerLog::interpolate", None),
]


def factory_points(xs, rnd):
    lo, hi = xs[0], xs[-1]
    span = hi - lo
    points = list(xs) + [lo - 0.5 * span, hi + 0.5 * span]
    while len(points) < 40:
        points.append(rnd.uniform(lo - 0.2 * span, hi + 0.2 * span))
    return points


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []

        def attempt(method, *params):
            return v.attempt(lambda: H.call(method, *params))

        for name, spec in SPECS:
            points, beyond = range_points(spec["x"]), outside(spec["x"])
            out.append({"name": name,
                        "inputs": {"settings": st, "interpolation": v.enc(spec), "points": v.enc(points),
                                   "outside": v.enc(beyond)},
                        "expected": {
                            "xMin": H.call("Interpolation::xMin", spec),
                            "xMax": H.call("Interpolation::xMax", spec),
                            "xValues": v.enc(H.call("Interpolation::xValues", spec)),
                            "yValues": v.enc(H.call("Interpolation::yValues", spec)),
                            "isInRange": [H.call("Interpolation::isInRange", spec, p) for p in points],
                            "enableExtrapolation": [attempt("Interpolation::enableExtrapolation", spec, True, p)
                                                    for p in beyond],
                            "enableExtrapolationFalse": [attempt("Interpolation::enableExtrapolation", spec, False, p)
                                                         for p in beyond],
                            "disableExtrapolation": [attempt("Interpolation::disableExtrapolation", spec, True, p)
                                                     for p in beyond],
                            "disableExtrapolationFalse": [attempt("Interpolation::disableExtrapolation", spec, False,
                                                                  p) for p in beyond]}})

        for name, spec in REFUSALS:
            at = refusal_points(spec["x"])
            out.append({"name": name,
                        "inputs": {"settings": st, "interpolation": v.enc(spec), "at": at},
                        "expected": {"value": [attempt("Interpolation::operator()", spec, p, True) for p in at]}})

        rnd = random.Random(v.SEED)
        for set_name, xs, ys in FACTORY_SETS:
            points = factory_points(xs, rnd)
            for name, method, factory in INTERPOLATES:
                params = ([factory] if factory is not None else []) + [xs, ys, points]
                inputs = {"settings": st, "method": method, "x": xs, "y": ys, "points": v.enc(points)}
                if factory is not None:
                    inputs["interpolator"] = factory
                out.append({"name": f"{name} {set_name}", "inputs": inputs,
                            "expected": {"values": v.enc(H.call(method, *params))}})
        for name, method, factory in (("Linear on one point", "Linear::interpolate", None),
                                      ("LogCubic with a y value of 0", "LogCubic::interpolate",
                                       {"type": "LogCubic", "derivativeApprox": SPLINE})):
            xs, ys = ([1.0], [2.0]) if factory is None else (FIVE, [1.0, 0.2, 0.0, 1.4, 3.0])
            params = ([factory] if factory is not None else []) + [xs, ys, [0.5]]
            inputs = {"settings": st, "method": method, "x": xs, "y": ys, "points": [0.5]}
            if factory is not None:
                inputs["interpolator"] = factory
            out.append({"name": name, "inputs": inputs, "expected": {"values": attempt(method, *params)}})

        out.append({"name": "traits", "inputs": {"settings": st, "factories": FACTORIES},
                    "expected": {"global": [H.call(f + "::global") for f in FACTORIES],
                                 "requiredPoints": [H.call(f + "::requiredPoints") for f in FACTORIES]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolation.hpp", cases)
