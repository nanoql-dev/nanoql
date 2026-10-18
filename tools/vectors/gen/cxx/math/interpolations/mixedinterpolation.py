# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's mixed linear/cubic interpolation (ql/math/interpolations/mixedinterpolation.hpp), which the
wheel doesn't expose, each output computed by the C++ harness (docs/porting-playbook.md §9.4).

- The six named mixed interpolations (MixedLinearCubicNaturalSpline, MixedLinearMonotonicCubicNaturalSpline,
  MixedLinearKrugerCubic, MixedLinearFritschButlandCubic, MixedLinearParabolic, MixedLinearMonotonicParabolic), each
  with the switch point n in {0, 1, 2, 4} under both behaviours (ShareRanges, SplitRanges), on two node sets of 5 and
  9 points: under SplitRanges, n = 0 leaves the linear part one point and n = 4 on 5 points the cubic part one, which
  QuantLib refuses, and so does n = 5 on 5 points, past the last point.
- MixedLinearCubicInterpolation with its own parameters, under both behaviours with n = 1 and 3 on the 9 points: the
  spline with a first-derivative and a not-a-knot end; Akima's scheme, whose cubic part needs four points; a Lagrange
  left end; the first-derivative left end with a null value, which matches the cubic's derivative at the switch
  point to the linear part's under SplitRanges and is refused under ShareRanges; and a second-derivative left end with
  a null value, which QuantLib's cubic reads as the number Null<Real>() is.
- Each interpolation read at 20 points (its nodes' ends, the switch point and the points just below and above it,
  points just outside the range and farther, and random ones inside), with the extrapolation flag set outside the
  range but at the two farthest points, where QuantLib refuses: the value at all of them, and the derivative, the
  second derivative and the primitive at the first 8. A refused construction gives each member one throw.
- The MixedLinearCubic factory's interpolate on the 5 and 9 points with three parameter sets, read back through its
  values at 20 points (extrapolating), with one refusal; and its global and requiredPoints.

An interpolation is the spec {"type", "x", "y", "n", "behavior"}, with MixedLinearCubicInterpolation's six parameters
("derivativeApprox", "monotonic", "leftCondition", "leftConditionValue" (null for Null<Real>()), "rightCondition",
"rightConditionValue") for that type; a factory is {"type": "MixedLinearCubic", "n", "behavior", and the six}
(tools/cxx/src/math/interpolationspec.hpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Interpolations/MixedInterpolationCxxVectorTests.cs."""
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
FIRST = 8
# CubicInterpolation::DerivativeApprox and BoundaryCondition, and MixedInterpolation::Behavior, by QuantLib's values.
SPLINE, PARABOLIC, AKIMA, KRUGER = 0, 4, 6, 7
NOT_A_KNOT, FIRST_DERIVATIVE, SECOND_DERIVATIVE, LAGRANGE = 0, 1, 2, 4
SHARE, SPLIT = 0, 1
BEHAVIORS = [("ShareRanges", SHARE), ("SplitRanges", SPLIT)]
PRESETS = ["MixedLinearCubicNaturalSpline", "MixedLinearMonotonicCubicNaturalSpline", "MixedLinearKrugerCubic",
           "MixedLinearFritschButlandCubic", "MixedLinearParabolic", "MixedLinearMonotonicParabolic"]
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


def node_sets(rnd):
    xs = sorted({round(rnd.uniform(-1.0, 9.0), 6) for _ in range(9)})
    assert len(xs) == 9
    return [("five points", [0.0, 0.6, 1.5, 2.1, 3.0], [1.2, 0.9, 1.4, 1.1, 2.0]),
            ("nine points", xs, [round(rnd.uniform(-1.0, 2.0), 6) for _ in xs])]


def sample(rnd, xs, n, at_switch=True):
    """20 points and their extrapolation flags: the first 8 read every member. The flag is set outside the range, but
    not at the two farthest points, where QuantLib throws. Without at_switch, the switch point itself is replaced by
    the middle of its interval: there a second-derivative left end's value is the second derivative, and a value
    equal to Null<Real>() is encoded as null."""
    lo, hi = xs[0], xs[-1]
    span = hi - lo
    switch = xs[min(n, len(xs) - 1)]
    first = switch if at_switch else (switch + xs[min(n, len(xs) - 1) + 1]) / 2.0
    points = [first, switch - 1e-6 * span, switch + 1e-6 * span, lo - 0.1 * span, hi + 0.2 * span,
              rnd.uniform(lo, hi), rnd.uniform(lo, hi), (lo + switch) / 2.0]
    farthest = [lo - 2.0 * span, hi + 2.0 * span]
    points += [lo, hi, lo - 10 * EPS * max(abs(lo), 1.0), hi + 10 * EPS * max(abs(hi), 1.0)] + farthest
    while len(points) < POINTS:
        points.append(rnd.uniform(lo, hi))
    allow = [not in_range(p, xs) and p not in farthest for p in points]
    return points, allow


def mixed(xs, ys, n, behavior, da, monotonic, left, left_value, right, right_value):
    return {"type": "MixedLinearCubicInterpolation", "x": xs, "y": ys, "n": n, "behavior": behavior,
            "derivativeApprox": da, "monotonic": monotonic, "leftCondition": left, "leftConditionValue": left_value,
            "rightCondition": right, "rightConditionValue": right_value}


# (name, da, monotonic, left, left value, right, right value) for MixedLinearCubicInterpolation's own parameters.
SETTINGS = [
    ("spline first derivative and not-a-knot", SPLINE, False, FIRST_DERIVATIVE, 0.3, NOT_A_KNOT, -0.6),
    ("monotonic Akima", AKIMA, True, SECOND_DERIVATIVE, 0.0, SECOND_DERIVATIVE, 0.0),
    ("spline Lagrange and second derivative", SPLINE, False, LAGRANGE, 0.0, SECOND_DERIVATIVE, 0.4),
    ("spline matching derivatives", SPLINE, False, FIRST_DERIVATIVE, None, SECOND_DERIVATIVE, 0.0),
    ("parabolic matching derivatives", PARABOLIC, False, FIRST_DERIVATIVE, None, FIRST_DERIVATIVE, 0.1),
    ("spline second derivative of Null<Real>()", SPLINE, False, SECOND_DERIVATIVE, None, SECOND_DERIVATIVE, 0.0),
]

FACTORIES = [
    ("Kruger shared", {"type": "MixedLinearCubic", "n": 2, "behavior": SHARE, "derivativeApprox": KRUGER,
                       "monotonic": False, "leftCondition": SECOND_DERIVATIVE, "leftConditionValue": 0.0,
                       "rightCondition": SECOND_DERIVATIVE, "rightConditionValue": 0.0}),
    ("monotonic spline split", {"type": "MixedLinearCubic", "n": 1, "behavior": SPLIT, "derivativeApprox": SPLINE,
                                "monotonic": True, "leftCondition": SECOND_DERIVATIVE, "leftConditionValue": 0.0,
                                "rightCondition": SECOND_DERIVATIVE, "rightConditionValue": 0.0}),
    ("spline matching derivatives", {"type": "MixedLinearCubic", "n": 2, "behavior": SPLIT, "derivativeApprox": SPLINE,
                                     "monotonic": False, "leftCondition": FIRST_DERIVATIVE,
                                     "leftConditionValue": None, "rightCondition": NOT_A_KNOT,
                                     "rightConditionValue": 0.0}),
]


def factory_points(xs, rnd):
    lo, hi = xs[0], xs[-1]
    span = hi - lo
    points = [lo, hi, lo - 0.5 * span, hi + 0.5 * span]
    while len(points) < POINTS:
        points.append(rnd.uniform(lo - 0.2 * span, hi + 0.2 * span))
    return points


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
                expected = {k: [v.attempt(lambda p=p, a=a, m=m: H.call(m, spec, p, a))
                                for p, a in (pa if k == "value" else pa[:FIRST])]
                            for k, m in MEMBERS.items()}
            # no output is Null<Real>() itself, which the encoding writes as null
            assert None not in [x for k in MEMBERS for x in (expected[k] if isinstance(expected[k], list) else [])], name
            return {"name": name, "inputs": inputs, "expected": expected}

        sets = node_sets(rnd)
        for set_name, xs, ys in sets:
            for preset in PRESETS:
                for n in (0, 1, 2, 4):
                    for behavior_name, behavior in BEHAVIORS:
                        spec = {"type": preset, "x": xs, "y": ys, "n": n, "behavior": behavior}
                        out.append(case(f"{preset} n={n} {behavior_name} {set_name}", spec, *sample(rnd, xs, n)))
        set_name, xs, ys = sets[0]
        for behavior_name, behavior in BEHAVIORS:
            spec = {"type": "MixedLinearCubicNaturalSpline", "x": xs, "y": ys, "n": 5, "behavior": behavior}
            out.append(case(f"MixedLinearCubicNaturalSpline n=5 {behavior_name} {set_name}", spec,
                            *sample(rnd, xs, 5)))

        set_name, xs, ys = sets[1]
        for name, da, mono, left, lv, right, rv in SETTINGS:
            for n in (1, 3):
                for behavior_name, behavior in BEHAVIORS:
                    spec = mixed(xs, ys, n, behavior, da, mono, left, lv, right, rv)
                    out.append(case(f"MixedLinearCubicInterpolation {name} n={n} {behavior_name} {set_name}", spec,
                                    *sample(rnd, xs, n, at_switch=lv is not None or left != SECOND_DERIVATIVE)))

        for set_name, xs, ys in sets:
            points = factory_points(xs, rnd)
            for name, factory in FACTORIES:
                out.append({"name": f"MixedLinearCubic {name} {set_name}",
                            "inputs": {"settings": st, "interpolator": factory, "x": xs, "y": ys,
                                       "points": v.enc(points)},
                            "expected": {"values": v.enc(H.call("MixedLinearCubic::interpolate", factory, xs, ys,
                                                                points))}})
        refused = dict(FACTORIES[0][1], n=5)
        set_name, xs, ys = sets[0]
        out.append({"name": f"MixedLinearCubic refuses n=5 on {set_name}",
                    "inputs": {"settings": st, "interpolator": refused, "x": xs, "y": ys, "points": [0.5]},
                    "expected": {"values": v.attempt(lambda: H.call("MixedLinearCubic::interpolate", refused, xs, ys,
                                                                    [0.5]))}})
        out.append({"name": "traits", "inputs": {"settings": st},
                    "expected": {"global": H.call("MixedLinearCubic::global"),
                                 "requiredPoints": H.call("MixedLinearCubic::requiredPoints")}})
        check_rows(out)
        return out


def check_rows(out):
    """The refusals the docstring names, and no others."""
    refused = sorted(c["name"] for c in out if c["expected"].get("value") == v.THROWS
                     or c["expected"].get("values") == v.THROWS)
    expected = []
    for set_name, size in (("five points", 5), ("nine points", 9)):
        for preset in PRESETS:
            expected += [f"{preset} n=0 SplitRanges {set_name}"]
    expected += ["MixedLinearCubicNaturalSpline n=5 ShareRanges five points",
                 "MixedLinearCubicNaturalSpline n=5 SplitRanges five points",
                 "MixedLinearCubic refuses n=5 on five points"]
    expected += [f"{preset} n=4 SplitRanges five points" for preset in PRESETS]
    for n in (1, 3):
        expected += [f"MixedLinearCubicInterpolation spline matching derivatives n={n} ShareRanges nine points",
                     f"MixedLinearCubicInterpolation parabolic matching derivatives n={n} ShareRanges nine points"]
    # Akima's and Lagrange's cubic part needs four points: under SplitRanges with n = 3, six are left.
    assert sorted(expected) == refused, sorted(set(refused) ^ set(expected))


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/mixedinterpolation.hpp", cases)
