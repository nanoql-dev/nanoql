# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LogCubicInterpolation (ql/math/interpolations/loginterpolation.hpp) and the named log-cubic
class the wheel lacks, HarmonicLogCubic, each output computed by the C++ harness (docs/porting-playbook.md §9.4).

LogCubicInterpolation's constructor, on three node sets of 3, 6 and 11 positive points: the nine derivative
approximations, monotonic or not, with the default second-derivative boundary conditions of 0, and three settings of
the spline with other boundary conditions and their values. FourthOrder (with more than 2 points) throws, as do Akima
and Lagrange on 3 points. Each interpolation is read at 20 points, inside the range, at its ends, just past them and
outside it, with the extrapolation flag set outside the range except at the two farthest points, where QuantLib
refuses: the value, derivative, second derivative and primitive (Interpolation's members), the primitive throwing, as
QuantLib doesn't implement it. Then HarmonicLogCubic on five node sets of 2 to 13 points, and the refusal of a y value
of 0 by both classes.

An interpolation is the spec {"type", "x", "y"}, with LogCubicInterpolation's six parameters ("derivativeApprox",
"monotonic", "leftCondition", "leftConditionValue", "rightCondition", "rightConditionValue") for that type
(tools/cxx/src/math/interpolationspec.hpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Interpolations/LogInterpolationCxxVectorTests.cs."""
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
POINTS = 20
# CubicInterpolation::DerivativeApprox and BoundaryCondition, by QuantLib's values.
SPLINE = 0
APPROXIMATIONS = range(9)
NOT_A_KNOT, FIRST_DERIVATIVE, SECOND_DERIVATIVE, LAGRANGE = 0, 1, 2, 4
MEMBERS = {"value": "Interpolation::operator()", "derivative": "Interpolation::derivative",
           "secondDerivative": "Interpolation::secondDerivative", "primitive": "Interpolation::primitive"}


def close(x, y):
    """QuantLib's close(x, y): within 42 machine epsilons of both, relatively."""
    if x == y:
        return True
    diff, tolerance = abs(x - y), 42 * EPS
    if x == 0.0 or y == 0.0:
        return diff < tolerance * tolerance
    return diff <= tolerance * abs(x) and diff <= tolerance * abs(y)


def in_range(x, xs):
    """QuantLib's isInRange."""
    return (xs[0] <= x <= xs[-1]) or close(x, xs[0]) or close(x, xs[-1])


def distinct_sorted(rnd, n, lo, hi):
    xs = sorted({rnd.uniform(lo, hi) for _ in range(n)})
    assert len(xs) == n
    return xs


def sample(rnd, xs):
    """20 points and their extrapolation flags: the flag is set outside the range, but not at the two farthest
    points, where QuantLib throws."""
    lo, hi = xs[0], xs[-1]
    span = hi - lo
    points = [lo, hi, xs[len(xs) // 2]]
    points += [lo - 10 * EPS * abs(lo) if lo != 0.0 else -1e-300, hi + 10 * EPS * abs(hi) if hi != 0.0 else 1e-300]
    farthest = [lo - 2.0 * span, hi + 2.0 * span]
    points += [lo - 0.05 * span, hi + 0.3 * span] + farthest
    while len(points) < POINTS:
        points.append(rnd.uniform(lo, hi))
    allow = [not in_range(p, xs) and p not in farthest for p in points]
    return points, allow


def node_sets(rnd):
    xs = distinct_sorted(rnd, 11, 0.0, 10.0)
    ys = [1.0, 0.97, 0.93, 0.93, 0.85, 0.9, 0.7, 0.62, 0.66, 0.5, 0.45]
    return [("three points", [0.5, 1.2, 2.0], [0.9, 1.6, 1.1]),
            ("six points", [0.0, 0.8, 1.9, 2.4, 3.9, 5.0], [0.5, 0.8, 0.8, 1.6, 1.2, 2.5]),
            ("eleven points", xs, ys)]


def named_sets(rnd):
    xs = distinct_sorted(rnd, 13, 0.0, 20.0)
    return [("two points", [1.0, 2.5], [0.3, 0.4]),
            ("three points", [0.0, 0.7, 2.0], [1.0, 0.2, 1.5]),
            ("four points", [0.0, 1.0, 2.0, 3.0], [1.0, 2.0, 0.5, 1.5]),
            ("eight points", [0.0, 0.5, 1.5, 2.0, 3.5, 4.0, 6.0, 7.0], [1.0, 1.0, 2.0, 2.0, 2.0, 0.5, 0.5, 3.0]),
            ("thirteen points", xs, [rnd.uniform(0.2, 2.0) for _ in xs])]


def log_cubic(xs, ys, da, monotonic, left, left_value, right, right_value):
    return {"type": "LogCubicInterpolation", "x": xs, "y": ys, "derivativeApprox": da, "monotonic": monotonic,
            "leftCondition": left, "leftConditionValue": left_value, "rightCondition": right,
            "rightConditionValue": right_value}


def settings():
    """(name, da, monotonic, left, leftValue, right, rightValue)."""
    out = [(f"da {da}{' monotonic' if mono else ''}", da, mono, SECOND_DERIVATIVE, 0.0, SECOND_DERIVATIVE, 0.0)
           for da in APPROXIMATIONS for mono in (False, True)]
    out += [("spline first derivative and not-a-knot", SPLINE, False, FIRST_DERIVATIVE, 0.3, NOT_A_KNOT, -0.6),
            ("monotonic spline Lagrange and first derivative", SPLINE, True, LAGRANGE, -0.9, FIRST_DERIVATIVE, -0.2),
            ("spline second derivatives", SPLINE, False, SECOND_DERIVATIVE, 1.1, SECOND_DERIVATIVE, -0.4)]
    return out


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        rnd = random.Random(v.SEED)
        out = []

        def case(name, spec, points, allow):
            inputs = {"settings": st, "interpolation": v.enc(spec), "points": v.enc(points),
                      "allowExtrapolation": allow}
            try:
                H.call("Interpolation::xMin", spec)
            except RuntimeError:
                # The constructor throws: so does every member.
                expected = {k: dict(v.THROWS) for k in MEMBERS}
            else:
                pa = list(zip(points, allow))
                expected = {k: [v.attempt(lambda p=p, a=a, m=m: H.call(m, spec, p, a)) for p, a in pa]
                            for k, m in MEMBERS.items()}
            return {"name": name, "inputs": inputs, "expected": expected}

        sets = [(name, xs, ys) + sample(rnd, xs) for name, xs, ys in node_sets(rnd)]
        for name, xs, ys, points, allow in sets:
            for label, da, mono, left, lv, right, rv in settings():
                out.append(case(f"{name} {label}", log_cubic(xs, ys, da, mono, left, lv, right, rv), points, allow))

        for name, xs, ys, points, allow in [(name, xs, ys) + sample(rnd, xs) for name, xs, ys in named_sets(rnd)]:
            out.append(case(f"HarmonicLogCubic {name}", {"type": "HarmonicLogCubic", "x": xs, "y": ys}, points, allow))

        xs, ys = [0.0, 0.5, 1.0, 2.0], [1.0, 0.5, 0.0, 2.0]
        out.append(case("LogCubicInterpolation refuses a y value of 0",
                        log_cubic(xs, ys, SPLINE, False, SECOND_DERIVATIVE, 0.0, SECOND_DERIVATIVE, 0.0), [0.25],
                        [False]))
        out.append(case("HarmonicLogCubic refuses a y value of 0", {"type": "HarmonicLogCubic", "x": xs, "y": ys},
                        [0.25], [False]))
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/loginterpolation.hpp", cases)
