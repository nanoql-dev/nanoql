# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for BackwardflatLinearInterpolation and its factory BackwardflatLinear
(ql/math/interpolations/backwardflatlinearinterpolation.hpp), each output computed by the C++ harness: the wheel
exposes neither, and reaches the interpolation only through a SABR cube with backward-flat interpolation.

On the 20 grids of tools/vectors/gen/math/interpolations/bilinearinterpolation.py, whose functions build them (larger
matrices than their grids among them), 4 interpolations each: built by the constructor and by the factory, each as
built and with extrapolation enabled. Each case gives the value at 40 points with QuantLib's allowExtrapolation
argument: every node, and per x interval its middle and a random point, each at a node's y and between two y values;
below the first x value, above the last and below and above the y values (each without and with the argument, a
point outside refused without it unless the interpolation extrapolates); and at NaN coordinates. The interpolation is
backward flat in x (between two x values the next one's column, at an x value its own, below the first the first
column and above the last the last) and linear in y, extrapolating linearly outside the y values.

A refused value is {"throws": true}. Each case's input "interpolation" is the spec
tools/cxx/src/math/interpolations/backwardflatlinearinterpolation.cpp reads: {"x", "y", "z", "enableExtrapolation",
"factory"}. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Interpolations/BackwardflatLinearInterpolationCxxVectorTests.cs."""
import importlib.util
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_bilinearinterpolation", os.path.join(v.GEN_ROOT, "math", "interpolations", "bilinearinterpolation.py"))
bil = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bil)

NAN = float("nan")
METHOD = "BackwardflatLinearInterpolation::operator()"
POINTS = 40
# (factory, extrapolation)
KINDS = [(False, False), (False, True), (True, False), (True, True)]


def points(rnd, xs, ys):
    """POINTS points and their allowExtrapolation arguments."""
    x0, x1, y0, y1 = xs[0], xs[-1], ys[0], ys[-1]
    sx, sy = x1 - x0, y1 - y0
    inside = [(x, y) for x in xs for y in ys]
    for i in range(len(xs) - 1):
        mid = 0.5 * (xs[i] + xs[i + 1])
        inside += [(mid, ys[rnd.randrange(len(ys))]), (rnd.uniform(xs[i], xs[i + 1]), rnd.uniform(y0, y1))]
    rnd.shuffle(inside)
    outside = [(x0 - 0.5 * sx, rnd.uniform(y0, y1)), (x1 + sx, rnd.uniform(y0, y1)),
               (rnd.uniform(x0, x1), y0 - 0.5 * sy), (rnd.uniform(x0, x1), y1 + sy), (x1 + sx, y1 + sy)]
    nans = [(NAN, rnd.uniform(y0, y1)), (rnd.uniform(x0, x1), NAN)]
    out = [(p, False) for p in nans]
    for p in outside:
        out += [(p, False), (p, True)]
    room = POINTS - len(out)
    out = [(p, False) for p in inside[:room]] + out
    return out


def cases():
    rnd = random.Random(v.SEED)
    out = []
    with cxx.connect() as H:
        for name, xs, ys, z in bil.grids(random.Random(v.SEED)):
            pts = points(rnd, xs, ys)
            for factory, extrapolate in KINDS:
                spec = {"x": xs, "y": ys, "z": z, "enableExtrapolation": extrapolate, "factory": factory}
                with H.case_settings(v.enc(ql.Date(15, ql.October, 2026))) as st:
                    expected = {"value": [v.attempt(lambda p=p, a=a: H.call(METHOD, spec, *p, a)) for p, a in pts]}
                    out.append({"name": f"{name} / {'factory' if factory else 'constructor'}"
                                        f"{', extrapolating' if extrapolate else ''}",
                                "inputs": {"settings": st, "interpolation": v.enc(spec),
                                           "points": v.enc([list(p) for p, _ in pts]),
                                           "allowExtrapolation": [a for _, a in pts]},
                                "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/backwardflatlinearinterpolation.hpp", cases)
