# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BlackVarianceSurface (ql/termstructures/volatility/equityfx/blackvariancesurface.hpp,
.cpp), on the evaluation date 15 October 2026.

- Surfaces: six surfaces (two to five strikes, one to four dates, bilinear and,
  through setInterpolation, bicubic interpolation, each strike extrapolation on either side, two calendars and two
  day counters): the reference and maximum dates, the minimum and maximum strikes, the calendar's and day counter's
  names, and the variance and volatility at a grid of times (0, within, at and past the dates) and strikes (within,
  at and past both ends), extrapolated. Where QuantLib's variance is negative, the strike extrapolation's defect, the
  outputs are left out, and so are the volatilities at time 0 that read it at QuantLib's shifted time of 0.00001: nanoql floors the variance at zero
  (tests/NanoQL.Tests/Divergences/BlackVarianceSurfaceFloorTests.cs).
- Refusals: a matrix whose columns don't match the dates or whose rows don't match the strikes, a first date before
  the reference date, dates out of order, a first date on it (its time equals the reference's), one strike (too few for the interpolation), and a strike outside the
  surface without extrapolation. A surface with no dates is left out: QuantLib reads dates.back() of an empty list
  (tests/NanoQL.Tests/Divergences/BlackVarianceEmptyRefusalTests.cs).

The structure specs are tools/vectors/gen/termstructures/volatility/equityfx/blackvoltermstructure.py's. The wheel's
calls, by QuantLib-SWIG 1.44's volatilities.i: BlackVarianceSurface(referenceDate, calendar, dates, strikes, matrix,
dayCounter, lower, upper), the C++ constructor with the default interpolator, and setInterpolation("bicubic"), which
calls setInterpolation<Bicubic>(). nanoql's SetInterpolation returns a copy, which the test reads. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/BlackVarianceSurfaceVectorTests.cs."""
import importlib.util
import math
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
R = bvts.EVALUATION_DATE
CONST = ql.BlackVarianceSurface.ConstantExtrapolation
DEFAULT = ql.BlackVarianceSurface.InterpolatorDefaultExtrapolation
SURFACES = [
    ("two by two", bvts.vol_spec_surface(R, [182, 365], [90.0, 110.0], [[0.30, 0.28], [0.20, 0.21]])),
    ("one date", bvts.vol_spec_surface(R, [365], [80.0, 100.0, 120.0], [[0.3], [0.25], [0.24]], bvts.A360,
                                       bvts.NULL_CALENDAR)),
    ("three by three, Actual/360",
     bvts.vol_spec_surface(R, [30, 182, 365], [90.0, 100.0, 110.0], [[0.27, 0.25, 0.24], [0.23, 0.22, 0.21],
                                                                    [0.22, 0.21, 0.205]], bvts.A360)),
    ("constant both sides",
     bvts.vol_spec_surface(R, [91, 365, 730, 1826], [70.0, 85.0, 100.0, 115.0, 130.0],
                           [[0.32, 0.30, 0.28, 0.27], [0.26, 0.25, 0.24, 0.235], [0.22, 0.215, 0.21, 0.205],
                            [0.21, 0.205, 0.20, 0.198], [0.23, 0.22, 0.215, 0.21]], lower=CONST, upper=CONST)),
    ("constant below, interpolator above",
     bvts.vol_spec_surface(R, [91, 365, 730], [80.0, 100.0, 120.0], [[0.30, 0.28, 0.27], [0.22, 0.21, 0.205],
                                                                   [0.21, 0.205, 0.20]], lower=CONST)),
    ("bicubic", bvts.vol_spec_surface(R, [91, 182, 365, 730], [70.0, 90.0, 100.0, 110.0, 130.0],
                                      [[0.34, 0.32, 0.31, 0.30], [0.27, 0.26, 0.255, 0.25],
                                       [0.24, 0.235, 0.23, 0.228], [0.23, 0.225, 0.222, 0.22],
                                       [0.26, 0.25, 0.245, 0.24]], bvts.ACT_ISDA, interpolation="bicubic")),
]
TIMES = [0.0, 0.1, 0.25, 0.4986301369863014, 0.75, 1.0, 1.6, 2.0, 3.5, 6.0]
STRIKES = [50.0, 70.0, 80.0, 85.0, 90.0, 95.0, 100.0, 107.5, 110.0, 120.0, 130.0, 150.0]


def cases():
    out = []
    st = v.settings(R)
    for name, spec in SURFACES:
        s = bvts.black_vol(spec)
        # QuantLib's negative variances, which nanoql floors, are left out
        points = [[t, k] for t in TIMES for k in STRIKES
                  if s.blackVariance(t, k, True) >= 0.0 and not math.isnan(s.blackVol(t, k, True))]
        left_out = len(TIMES) * len(STRIKES) - len(points)
        print(f"{name}: {left_out} negative variances left out")
        out.append({
            "name": name,
            "inputs": {"settings": st, "structure": spec, "points": points},
            "expected": v.enc({
                "referenceDate": s.referenceDate(),
                "maxDate": s.maxDate(),
                "minStrike": s.minStrike(),
                "maxStrike": s.maxStrike(),
                "calendar": s.calendar().name(),
                "dayCounter": s.dayCounter().name(),
                "blackVariance": [s.blackVariance(t, k, True) for t, k in points],
                "blackVol": [s.blackVol(t, k, True) for t, k in points],
                "outsideStrikes": v.attempt(lambda: s.blackVol(0.5, 1e4))})})
    refusals = [
        ("columns don't match the dates", [182, 365], [90.0, 110.0], [[0.3], [0.2]]),
        ("rows don't match the strikes", [182, 365], [90.0, 100.0, 110.0], [[0.3, 0.28], [0.2, 0.21]]),
        ("first date before the reference date", [-1, 365], [90.0, 110.0], [[0.3, 0.28], [0.2, 0.21]]),
        ("dates out of order", [365, 182], [90.0, 110.0], [[0.3, 0.28], [0.2, 0.21]]),
        ("first date on the reference date", [0, 365], [90.0, 110.0], [[0.3, 0.28], [0.2, 0.21]]),
        ("one strike", [182, 365], [100.0], [[0.3, 0.28]]),
    ]
    for name, offsets, strikes, vols in refusals:
        spec = bvts.vol_spec_surface(R, offsets, strikes, vols)
        out.append({"name": name, "inputs": {"settings": st, "structure": spec},
                    "expected": {"build": v.attempt(lambda: bvts.black_vol(spec).blackVariance(0.1, 100.0))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/blackvariancesurface.hpp", cases)
