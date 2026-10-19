# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Interpolation2D (ql/math/interpolations/interpolation2d.hpp) and FlatExtrapolator2D
(ql/math/interpolations/flatextrapolation2d.hpp), each output computed by the C++ harness: neither the base's
inspectors nor the flat extrapolator are exposed by the wheel.

On 5 grids of tools/vectors/gen/math/interpolations/bilinearinterpolation.py, whose functions build them (a larger
matrix than the grid among them), 5 interpolations each: the BilinearInterpolation as built and with extrapolation
enabled, and a FlatExtrapolator2D decorating it, as built, with extrapolation enabled, and with extrapolation enabled
on both. Each case gives xMin, xMax, yMin, yMax, xValues, yValues and zData (the matrix's rows); locateX and locateY
at 9 coordinates (below, at and between the nodes, above, NaN); isInRange at 12 points; and the value at 20 points
inside and outside the grid, and at NaN coordinates, with and without the allowExtrapolation argument. The flat
extrapolator moves a point outside the grid onto its edges and reads the decorated interpolation there without
extrapolating, so a NaN coordinate, which no move puts in range, is refused unless the decorated interpolation
extrapolates.

A refused value is {"throws": true}. Each case's input "interpolation" is the spec
tools/cxx/src/math/interpolations/interpolation2d.cpp reads: {"type", "x", "y", "z", "enableExtrapolation"}, and
"decoratedExtrapolation" for a FlatExtrapolator2D. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Interpolations/Interpolation2DCxxVectorTests.cs."""
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
METHOD = "Interpolation2D::"
GRIDS = ["three by two", "two by four", "option by swap", "irregular three by seven", "extra columns"]
# (type, decorated extrapolation, extrapolation)
KINDS = [("BilinearInterpolation", None, False), ("BilinearInterpolation", None, True),
         ("FlatExtrapolator2D", False, False), ("FlatExtrapolator2D", False, True),
         ("FlatExtrapolator2D", True, True)]


def spec_of(xs, ys, z, kind):
    t, decorated, extrapolate = kind
    spec = {"type": t, "x": xs, "y": ys, "z": z, "enableExtrapolation": extrapolate}
    if decorated is not None:
        spec["decoratedExtrapolation"] = decorated
    return spec


def coordinates(rnd, ns):
    """9 coordinates: below, at the first, last and a middle node, between two, at the last, above, NaN."""
    lo, hi = ns[0], ns[-1]
    span = hi - lo
    return [lo - 0.5 * span, lo, rnd.uniform(lo, ns[1]), ns[len(ns) // 2], rnd.uniform(ns[-2], hi), hi,
            hi + 10 * bil.EPS * abs(hi) if hi != 0.0 else 1e-300, hi + span, NAN]


def points(rnd, xs, ys):
    """20 points with their allowExtrapolation arguments: inside, on the edges, outside on each side and at a corner,
    and at NaN coordinates, each outside one without and with the argument."""
    x0, x1, y0, y1 = xs[0], xs[-1], ys[0], ys[-1]
    sx, sy = x1 - x0, y1 - y0
    inside = [(rnd.uniform(x0, x1), rnd.uniform(y0, y1)) for _ in range(4)] + [(x0, y1), (x1, rnd.uniform(y0, y1))]
    outside = [(x0 - 0.5 * sx, rnd.uniform(y0, y1)), (rnd.uniform(x0, x1), y1 + sy), (x1 + sx, y0 - sy),
               (NAN, rnd.uniform(y0, y1)), (rnd.uniform(x0, x1), NAN), (NAN, NAN), (x1 + sx, NAN)]
    out = [(p, False) for p in inside]
    for p in outside:
        out += [(p, False), (p, True)]
    return out


def cases():
    rnd = random.Random(v.SEED)
    grids = {name: (xs, ys, z) for name, xs, ys, z in bil.grids(random.Random(v.SEED))}
    out = []
    with cxx.connect() as H:
        for g in GRIDS:
            xs, ys, z = grids[g]
            xc, yc = coordinates(rnd, xs), coordinates(rnd, ys)
            ranged = [(x, y) for x in (xc[0], xc[1], xc[4], xc[6]) for y in (yc[0], yc[3], yc[7])]
            pts = points(rnd, xs, ys)
            for k, kind in enumerate(KINDS):
                spec = spec_of(xs, ys, z, kind)
                with H.case_settings(v.enc(ql.Date(15, ql.October, 2026))) as st:
                    expected = {m: v.attempt(lambda m=m: H.call(METHOD + m, spec))
                                for m in ("xMin", "xMax", "yMin", "yMax", "xValues", "yValues", "zData")}
                    expected["locateX"] = [v.attempt(lambda c=c: H.call(METHOD + "locateX", spec, c)) for c in xc]
                    expected["locateY"] = [v.attempt(lambda c=c: H.call(METHOD + "locateY", spec, c)) for c in yc]
                    expected["isInRange"] = [v.attempt(lambda p=p: H.call(METHOD + "isInRange", spec, *p))
                                             for p in ranged]
                    expected["value"] = [v.attempt(lambda p=p, a=a: H.call(METHOD + "operator()", spec, *p, a))
                                         for p, a in pts]
                    out.append({"name": f"{g} {kind[0]} {kind[1]} {kind[2]} / {k}",
                                "inputs": {"settings": st, "interpolation": v.enc(spec), "xCoordinates": v.enc(xc),
                                           "yCoordinates": v.enc(yc), "inRange": v.enc([list(p) for p in ranged]),
                                           "points": v.enc([list(p) for p, _ in pts]),
                                           "allowExtrapolation": [a for _, a in pts]},
                                "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/interpolation2d.hpp", cases)
