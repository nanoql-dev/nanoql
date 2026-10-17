# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CubicInterpolation (ql/math/interpolations/cubicinterpolation.hpp) where the wheel has no
binding, each output computed by the C++ harness (docs/porting-playbook.md §9.4).

The constructor, on three node sets of 3, 6 and 11 points: the nine derivative approximations, monotonic or not, with
the default second-derivative boundary conditions of 0; and the 25 pairs of boundary conditions under the spline,
not monotonic, each condition with a value of its own (the conditions that ignore their value get one too, so that a
port reading it would show). FourthOrder (with more than 2 points) and Periodic throw, as do Akima and Lagrange on 3
points. Each interpolation is read at 24 points, inside the range, at its ends, just past them and outside it, with
the extrapolation flag set outside the range except at the two farthest points, where QuantLib refuses: the value,
derivative, second derivative and primitive (Interpolation's members); and the five coefficient accessors. Then
updateLeftConditionValue and updateRightConditionValue, each followed by update() on an interpolation with
extrapolation enabled, read at the 24 points without the flag. Then the named classes the wheel lacks,
AkimaCubicInterpolation, HarmonicCubic and the two overshooting minimizations (whose matrices go through uBLAS's LU), on
seven node sets of 2 to 13 points, among them slopes that change sign, which clamp the harmonic scheme's end
derivatives, and two equal pairs of slopes, a case of Akima's.

Then the edges: every configuration, the nine derivative approximations, monotonic or not, by the 25 pairs of boundary
conditions (each with its value of its own, as above, so that the rows reading them matter), on 2 points and on 4
points (Akima's and Lagrange's minimum); and the monotonic spline's 25 pairs on 8 points, with flat stretches, where
the filter works. Each configuration is read at two interior points (the value, derivative, second derivative and
primitive) with its five coefficient accessors; one case holds a size's approximation and filter, its outputs one
entry per condition pair. Left out, as QuantLib's behaviour is undefined there: on 2 points, the spline with a
not-a-knot end, whose rows read dx_[1] and dx_[n_-3] past a one-element dx_ (cubicinterpolation.hpp:428-431, 460-463),
unless a refusal comes first, the constructor's Lagrange check or a left Periodic end, which throws before the right
end is read. Every configuration kept is defined: each throw is one of QuantLib's refusals.

An interpolation is the spec {"type", "x", "y"}, with CubicInterpolation's six parameters ("derivativeApprox",
"monotonic", "leftCondition", "leftConditionValue", "rightCondition", "rightConditionValue") for that type
(tools/cxx/src/math/interpolationspec.hpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Interpolations/CubicInterpolationCxxVectorTests.cs."""
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
POINTS = 24
# CubicInterpolation::DerivativeApprox and BoundaryCondition, by QuantLib's values.
SPLINE, KRUGER = 0, 7
APPROXIMATIONS = range(9)
NOT_A_KNOT, FIRST_DERIVATIVE, SECOND_DERIVATIVE, PERIODIC, LAGRANGE = 0, 1, 2, 3, 4
CONDITIONS = range(5)
# The edges' node sets, each with its two interior points.
EDGE_SETS = {"two points": ([1.0, 2.5], [0.3, -0.4], [1.4, 2.2]),
             "four points": ([0.0, 0.8, 1.9, 3.0], [1.0, 2.0, 0.5, 1.5], [0.3, 2.6]),
             "eight points": ([0.0, 0.5, 1.5, 2.0, 3.5, 4.0, 6.0, 7.0], [1.0, 1.0, 2.0, 2.0, 2.0, -0.5, -0.5, 3.0],
                              [1.2, 4.7])}
LEFT_VALUES = [0.6, 0.3, 1.1, 0.2, -0.9]
RIGHT_VALUES = [-0.6, -0.7, -0.4, 0.5, 0.8]
NAMED = ["AkimaCubicInterpolation", "HarmonicCubic", "CubicSplineOvershootingMinimization1",
         "CubicSplineOvershootingMinimization2"]
ACCESSORS = ["primitiveConstants", "aCoefficients", "bCoefficients", "cCoefficients", "monotonicityAdjustments"]
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
    """24 points and their extrapolation flags: the flag is set outside the range, but not at the two farthest
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
    xs = distinct_sorted(rnd, 11, -2.0, 6.0)
    ys = [0.4, 0.9, 1.3, 1.3, 2.0, 1.1, -0.3, -0.2, 0.8, 0.85, 3.0]
    return [("three points", [0.0, 0.7, 2.0], [1.0, 0.2, 1.5]),
            ("six points", [-2.0, -1.2, -0.1, 0.4, 1.9, 3.0], [0.5, 0.8, 0.8, 1.6, 1.2, 2.5]),
            ("eleven points", xs, ys)]


def named_sets(rnd):
    xs = distinct_sorted(rnd, 13, 0.0, 20.0)
    return [("two points", [1.0, 2.5], [0.3, -0.4]),
            ("three points", [0.0, 0.7, 2.0], [1.0, 0.2, 1.5]),
            ("four points", [0.0, 1.0, 2.0, 3.0], [1.0, 2.0, 0.5, 1.5]),
            # slopes 1, -4 and 1: the harmonic scheme's end derivatives are clamped to three times the end slopes
            ("slopes changing sign", [0.0, 1.0, 2.0, 3.0], [0.0, 1.0, -3.0, -2.0]),
            # slopes 1, 1, 2, 2 and 1: Akima's case of two equal pairs of slopes
            ("equal pairs of slopes", [0.0, 1.0, 2.0, 3.0, 4.0, 5.0], [0.0, 1.0, 2.0, 4.0, 6.0, 7.0]),
            ("eight points", [0.0, 0.5, 1.5, 2.0, 3.5, 4.0, 6.0, 7.0], [1.0, 1.0, 2.0, 2.0, 2.0, -0.5, -0.5, 3.0]),
            ("thirteen points", xs, [rnd.uniform(-2.0, 2.0) for _ in xs])]


def cubic(xs, ys, da, monotonic, left, left_value, right, right_value):
    return {"type": "CubicInterpolation", "x": xs, "y": ys, "derivativeApprox": da, "monotonic": monotonic,
            "leftCondition": left, "leftConditionValue": left_value, "rightCondition": right,
            "rightConditionValue": right_value}


def defined(n, da, left, right):
    """Whether QuantLib's CubicInterpolation is defined on n points with these settings. Only the spline reads its
    boundary rows; on 2 points its not-a-knot rows read dx_[1] and dx_[n_-3] of a one-element dx_
    (cubicinterpolation.hpp:428-431, 460-463), unless a refusal comes first: the constructor's Lagrange check, or a left
    Periodic end, which throws before the right end is read."""
    if n != 2 or da != SPLINE or LAGRANGE in (left, right):
        return True
    if left == NOT_A_KNOT:
        return False
    if left == PERIODIC:
        return True
    return right != NOT_A_KNOT


def settings():
    """(name, da, monotonic, left, leftValue, right, rightValue): the approximations with QuantLib's default
    boundaries, then the 25 boundary pairs under the spline."""
    out = [(f"da {da}{' monotonic' if mono else ''}", da, mono, SECOND_DERIVATIVE, 0.0, SECOND_DERIVATIVE, 0.0)
           for da in APPROXIMATIONS for mono in (False, True)]
    out += [(f"spline left {left} right {right}", SPLINE, False, left, LEFT_VALUES[left], right, RIGHT_VALUES[right])
            for left in CONDITIONS for right in CONDITIONS]
    return out


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        rnd = random.Random(v.SEED)
        out = []

        def evaluated(spec, points, allow):
            pa = list(zip(points, allow))
            return {name: [v.attempt(lambda p=p, a=a, m=method: H.call(m, spec, p, a)) for p, a in pa]
                    for name, method in MEMBERS.items()}

        sets = [(name, xs, ys) + sample(rnd, xs) for name, xs, ys in node_sets(rnd)]
        for name, xs, ys, points, allow in sets:
            for label, da, mono, left, lv, right, rv in settings():
                spec = cubic(xs, ys, da, mono, left, lv, right, rv)
                inputs = {"settings": st, "interpolation": v.enc(spec), "points": v.enc(points),
                          "allowExtrapolation": allow}
                try:
                    accessors = {a: v.enc(H.call("CubicInterpolation::" + a, spec)) for a in ACCESSORS}
                except RuntimeError:
                    # The constructor throws: so does every member.
                    expected = {k: dict(v.THROWS) for k in list(MEMBERS) + ACCESSORS}
                else:
                    expected = evaluated(spec, points, allow)
                    expected.update(accessors)
                out.append({"name": f"{name} {label}", "inputs": inputs, "expected": expected})

        # The boundary values' updates, each followed by update(), with extrapolation enabled first.
        updates = [("spline first and second derivative", SPLINE, False, FIRST_DERIVATIVE, 0.3, SECOND_DERIVATIVE, -0.4),
                   ("monotonic spline second and first derivative", SPLINE, True, SECOND_DERIVATIVE, 1.1,
                    FIRST_DERIVATIVE, -0.7),
                   ("Kruger, which ignores the values", KRUGER, False, SECOND_DERIVATIVE, 0.0, SECOND_DERIVATIVE, 0.0)]
        for name, xs, ys, points, _ in sets:
            for label, da, mono, left, lv, right, rv in updates:
                spec = cubic(xs, ys, da, mono, left, lv, right, rv)
                for side, value in (("Left", 1.25), ("Right", -2.5)):
                    method = f"CubicInterpolation::update{side}ConditionValue"
                    try:
                        r = H.call(method, spec, value, points)
                        expected = {k: v.enc(r[k]) for k in MEMBERS}
                    except RuntimeError:
                        expected = {k: dict(v.THROWS) for k in MEMBERS}
                    out.append({"name": f"{name} {label} update{side}ConditionValue",
                                "inputs": {"settings": st, "interpolation": v.enc(spec), "update": side.lower(),
                                           "value": value, "points": v.enc(points)},
                                "expected": expected})

        # The named classes the wheel lacks.
        named = [(name, xs, ys) + sample(rnd, xs) for name, xs, ys in named_sets(rnd)]
        for cls in NAMED:
            for name, xs, ys, points, allow in named:
                spec = {"type": cls, "x": xs, "y": ys}
                inputs = {"settings": st, "interpolation": v.enc(spec), "points": v.enc(points),
                          "allowExtrapolation": allow}
                try:
                    H.call("Interpolation::xMin", spec)
                except RuntimeError:
                    expected = {k: dict(v.THROWS) for k in MEMBERS}
                else:
                    expected = evaluated(spec, points, allow)
                out.append({"name": f"{cls} {name}", "inputs": inputs, "expected": expected})

        # The edges: one case per size, approximation and filter, one entry per defined condition pair.
        edges = [("two points", da, mono) for da in APPROXIMATIONS for mono in (False, True)]
        edges += [("four points", da, mono) for da in APPROXIMATIONS for mono in (False, True)]
        edges += [("eight points", SPLINE, True)]
        for name, da, mono in edges:
            xs, ys, points = EDGE_SETS[name]
            conditions = [[left, LEFT_VALUES[left], right, RIGHT_VALUES[right]]
                          for left in CONDITIONS for right in CONDITIONS if defined(len(xs), da, left, right)]
            expected = {k: [] for k in list(MEMBERS) + ACCESSORS}
            for left, lv, right, rv in conditions:
                spec = cubic(xs, ys, da, mono, left, lv, right, rv)
                try:
                    accessors = {a: v.enc(H.call("CubicInterpolation::" + a, spec)) for a in ACCESSORS}
                except RuntimeError:
                    # The constructor throws: so does every member.
                    for k in expected:
                        expected[k].append(dict(v.THROWS))
                    continue
                for k, method in MEMBERS.items():
                    expected[k].append([v.attempt(lambda p=p, m=method: H.call(m, spec, p, False)) for p in points])
                for a in ACCESSORS:
                    expected[a].append(accessors[a])
            out.append({"name": f"edges {name} da {da}{' monotonic' if mono else ''}",
                        "inputs": {"settings": st, "x": xs, "y": ys, "derivativeApprox": da, "monotonic": mono,
                                   "conditions": conditions, "points": points},
                        "expected": expected})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/cubicinterpolation.hpp", cases)
