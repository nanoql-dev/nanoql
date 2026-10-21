# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LocalVolSurface (ql/termstructures/volatility/equityfx/localvolsurface.hpp, .cpp), on the
evaluation date 15 October 2026: Dupire's local volatility from a Black volatility and two curves.

Five surfaces, on a constant volatility, a variance curve and three variance surfaces (with a smile, with a skew and
flat), over flat curves and linear zero curves on the Black structure's day counter, at a spot of 100 (and 50): on
another day counter QuantLib reads the curves at the structure's times, where nanoql reads them at their instants
(tests/NanoQL.Tests/Divergences/LocalVolSurfaceMismatchedTimesTests.cs). The outputs are the reference and maximum
dates, the minimum and maximum strikes and the day counter's name, which are the Black structure's, the calendar
(QuantLib's empty one) and the local volatility at 14 (time, level) points, the time 0 branch and the flat-smile
shortcut (a zero strike derivative) among them, extrapolated. A Black structure whose variance falls in time, or whose
smile is too steep, gives QuantLib's refusals: "decreasing variance at strike …" and "negative local vol^2 at strike …".

The specs are tools/vectors/gen/termstructures/volatility/equityfx/blackvoltermstructure.py's and
tools/vectors/gen/processes/blackscholesprocess.py's. The wheel's calls, by QuantLib-SWIG 1.44's volatilities.i:
LocalVolSurface(blackTS, riskFreeTS, dividendTS, underlying), the C++ constructor taking a number, and localVol, the
base's. Read back by tests/NanoQL.Tests/Vectors/TermStructures/LocalVolSurfaceVectorTests.cs."""
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


bvts = _load("blackvoltermstructure", "termstructures/volatility/equityfx/blackvoltermstructure.py")
bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
R = bvts.EVALUATION_DATE
SMILE = bvts.vol_spec_surface(R, [91, 365, 730, 1826], [70.0, 85.0, 100.0, 115.0, 130.0],
                              [[0.32, 0.30, 0.28, 0.27], [0.26, 0.25, 0.24, 0.235], [0.22, 0.215, 0.21, 0.205],
                               [0.21, 0.205, 0.20, 0.198], [0.23, 0.22, 0.215, 0.21]])
SKEW = bvts.vol_spec_surface(R, [182, 365, 1095], [60.0, 80.0, 100.0, 120.0, 140.0],
                             [[0.35, 0.33, 0.31], [0.29, 0.28, 0.27], [0.24, 0.235, 0.23], [0.21, 0.205, 0.20],
                              [0.19, 0.185, 0.18]], bvts.A360)
FLAT = bvts.vol_spec_surface(R, [182, 365, 1095], [80.0, 100.0, 120.0],
                             [[0.2, 0.2, 0.2], [0.2, 0.2, 0.2], [0.2, 0.2, 0.2]])
CASES = [
    ("constant volatility, flat curves", bvts.vol_spec_constant(R, 0.25), bsp.FLAT_R, bsp.FLAT_Q, 100.0),
    ("variance curve, zero curves", bsp.CURVE, bsp.ZERO_R, bsp.ZERO_Q, 100.0),
    ("smile, zero curves", SMILE, bsp.ZERO_R, bsp.ZERO_Q, 100.0),
    ("skew, flat curves, spot 50", SKEW, bsp.flat_spec(R, 0.04, bvts.A360), bsp.flat_spec(R, 0.02, bvts.A360), 50.0),
    ("flat surface, zero curves", FLAT, bsp.ZERO_R, bsp.ZERO_Q, 100.0),
]
POINTS = [(0.0, 100.0), (0.0, 80.0), (0.05, 100.0), (0.25, 90.0), (0.5, 100.0), (0.5, 120.0), (1.0, 75.0),
          (1.0, 100.0), (1.5, 110.0), (2.0, 95.0), (3.0, 100.0), (4.0, 130.0), (6.0, 100.0), (0.5, 100.000001)]
FALLING = bvts.vol_spec_curve(R, [182, 365], [0.40, 0.20], force=False)
STEEP = bvts.vol_spec_surface(R, [182, 365], [95.0, 100.0, 105.0], [[0.60, 0.60], [0.10, 0.10], [0.60, 0.60]])


def surface(vol, rf, q, spot):
    return ql.LocalVolSurface(ql.BlackVolTermStructureHandle(bvts.black_vol(vol)),
                              ql.YieldTermStructureHandle(bsp.curve(rf)), ql.YieldTermStructureHandle(bsp.curve(q)),
                              spot)


def cases():
    out = []
    st = v.settings(R)
    for name, vol, rf, q, spot in CASES:
        s = surface(vol, rf, q, spot)
        pts = [[t, x * spot / 100.0] for t, x in POINTS]
        out.append({
            "name": name,
            "inputs": {"settings": st, "blackVolatility": vol, "riskFree": rf, "dividend": q, "underlying": spot,
                       "points": pts},
            "expected": v.enc({
                "referenceDate": s.referenceDate(),
                "maxDate": s.maxDate(),
                "minStrike": s.minStrike(),
                "maxStrike": s.maxStrike(),
                "dayCounter": s.dayCounter().name(),
                "calendarEmpty": s.calendar().empty(),
                "localVol": [v.attempt(lambda: s.localVol(t, x, True)) for t, x in pts]})})
    for name, vol in (("a variance falling in time", FALLING), ("a smile too steep", STEEP)):
        s = surface(vol, bsp.FLAT_R, bsp.FLAT_Q, 100.0)
        pts = [[0.0, 100.0], [0.3, 100.0], [0.75, 100.0], [0.3, 97.5]]
        out.append({
            "name": name,
            "inputs": {"settings": st, "blackVolatility": vol, "riskFree": bsp.FLAT_R, "dividend": bsp.FLAT_Q,
                       "underlying": 100.0, "points": pts},
            "expected": v.enc({"localVol": [v.attempt(lambda: s.localVol(t, x, True)) for t, x in pts]})})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/localvolsurface.hpp", cases)
