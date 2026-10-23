# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FlatExtrapolator (ql/math/interpolations/flatextrapolation.hpp), which the wheel doesn't
expose, each output computed by the C++ harness (docs/porting-playbook.md §9.4).

On the nine interpolations of tools/vectors/gen/cxx/math/interpolation.py (the linear, backward-flat on one point and
four, log-linear, cubic and log-cubic families), each decorated: the value, derivative, second derivative and primitive
at up to 30 points with QuantLib's allowExtrapolation argument (every node; per interval its middle and a random point; just
inside and just beyond each end, within and past QuantLib's close(); outside the range near and far, without and with
the argument; NaN and the infinities, with the argument), the log families' primitive refused, as QuantLib doesn't
implement it; xMin, xMax, xValues and yValues; isInRange at the points; and the value at the points read without the
argument after enableExtrapolation() on the decorator.

A refused value is {"throws": true}. Each case's input "interpolation" is the decorated interpolation's spec
(tools/cxx/src/math/interpolationspec.hpp), {"type", "x", "y"} with the cubic families' parameters. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Interpolations/FlatExtrapolatorCxxVectorTests.cs."""
import importlib.util
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

_spec = importlib.util.spec_from_file_location(
    "cxx_interpolation", os.path.join(v.GEN_ROOT, "cxx", "math", "interpolation.py"))
base = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(base)

EPS = 2.220446049250313e-16
POINTS = 30
MEMBERS = {"value": "FlatExtrapolator::operator()", "derivative": "FlatExtrapolator::derivative",
           "secondDerivative": "FlatExtrapolator::secondDerivative", "primitive": "FlatExtrapolator::primitive"}


def points(rnd, xs):
    """POINTS points and their allowExtrapolation arguments: False inside the range, and outside it each point once
    without and once with the argument, the infinities and NaN with it."""
    lo, hi = xs[0], xs[-1]
    span = hi - lo if hi > lo else 1.0
    inside = list(xs)
    for i in range(len(xs) - 1):
        inside += [0.5 * (xs[i] + xs[i + 1]), rnd.uniform(xs[i], xs[i + 1])]
    near = []
    for end, sign in ((lo, -1.0), (hi, 1.0)):
        scale = abs(end) if end != 0.0 else 1.0
        # within and beyond close()'s 42 epsilons (to 0, a difference below 42 epsilons squared)
        near += [end + sign * 10 * EPS * scale if end != 0.0 else sign * 1e-30,
                 end + sign * 100 * EPS * scale if end != 0.0 else sign * 1e-20]
    outside = [lo - 0.01 * span, lo - 2.0 * span, hi + 0.01 * span, hi + 2.0 * span]
    out = [(p, a) for p in near + outside for a in (False, True)]
    out += [(p, True) for p in (math.inf, -math.inf, math.nan)]
    room = POINTS - len(out)
    rnd.shuffle(inside)
    return [(p, False) for p in inside[:room]] + out


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        rnd = random.Random(v.SEED)
        out = []
        for name, spec in base.SPECS:
            pa = points(rnd, spec["x"])
            expected = {k: [v.attempt(lambda p=p, a=a, m=m: H.call(m, spec, p, a)) for p, a in pa]
                        for k, m in MEMBERS.items()}
            for member in ("xMin", "xMax", "xValues", "yValues"):
                expected[member] = H.call(f"FlatExtrapolator::{member}", spec)
            expected["isInRange"] = [H.call("FlatExtrapolator::isInRange", spec, p) for p, _ in pa]
            expected["enableExtrapolation"] = [
                v.attempt(lambda p=p: H.call("FlatExtrapolator::enableExtrapolation", spec, p)) for p, _ in pa]
            out.append({"name": f"FlatExtrapolator on {name}",
                        "inputs": {"settings": st, "interpolation": v.enc(spec), "points": v.enc([p for p, _ in pa]),
                                   "allowExtrapolation": [a for _, a in pa]},
                        "expected": expected})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/flatextrapolation.hpp", cases)
