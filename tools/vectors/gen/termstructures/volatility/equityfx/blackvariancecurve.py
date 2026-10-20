# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BlackVarianceCurve (ql/termstructures/volatility/equityfx/blackvariancecurve.hpp, .cpp),
on the evaluation date 15 October 2026.

- Curves: nine curves (one to six dates; increasing, flat and decreasing volatilities; the forced monotone variance
  on and off; the three time extrapolations; linear and, through setInterpolation, cubic interpolation; Actual/365
  (Fixed) and Actual/360): the reference and maximum dates, the minimum and maximum strikes, the day counter's name,
  and the variance and volatility at 14 times, within the dates, at them and past the last, extrapolated, at a strike
  of 100 and at one of 1e6 (the curve ignores the strike).
- Refusals: the dates and volatilities differing in number, the first date on the reference date, dates out of order
  or repeated, a variance decreasing under the forced monotone variance, and a time past the maximum without
  extrapolation. A curve with no dates is left out: QuantLib reads dates.back() of an empty list
  (tests/NanoQL.Tests/Divergences/BlackVarianceEmptyRefusalTests.cs).

The structure specs are tools/vectors/gen/termstructures/volatility/equityfx/blackvoltermstructure.py's. The wheel's
calls, by QuantLib-SWIG 1.44's volatilities.i: BlackVarianceCurve(referenceDate, dates, volatilities, dayCounter,
forceMonotoneVariance, timeExtrapolationType), the C++ constructor, and setInterpolation("cubic"), which calls
setInterpolation<Cubic>(). nanoql's SetInterpolation returns a copy, which the test reads. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/BlackVarianceCurveVectorTests.cs."""
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
R = bvts.EVALUATION_DATE
X = ql.BlackVolTimeExtrapolation
CURVES = [
    ("one date", bvts.vol_spec_curve(R, [365], [0.2])),
    ("increasing", bvts.vol_spec_curve(R, [30, 91, 182, 365, 730, 1826], [0.15, 0.17, 0.19, 0.21, 0.23, 0.25])),
    ("decreasing vols, increasing variance",
     bvts.vol_spec_curve(R, [91, 182, 365, 730], [0.30, 0.26, 0.24, 0.23], bvts.A360)),
    ("flat", bvts.vol_spec_curve(R, [182, 365, 1095], [0.2, 0.2, 0.2])),
    ("decreasing variance, not forced", bvts.vol_spec_curve(R, [182, 365, 730], [0.40, 0.25, 0.30], force=False)),
    ("use interpolator", bvts.vol_spec_curve(R, [91, 365, 730], [0.22, 0.24, 0.25], extrapolation=X.UseInterpolator)),
    ("linear variance", bvts.vol_spec_curve(R, [91, 365, 730], [0.22, 0.24, 0.25], extrapolation=X.LinearVariance)),
    ("cubic", bvts.vol_spec_curve(R, [30, 91, 182, 365, 730, 1826], [0.15, 0.17, 0.19, 0.21, 0.23, 0.25],
                                  interpolation="cubic")),
    ("cubic, linear variance", bvts.vol_spec_curve(R, [91, 182, 365, 730], [0.30, 0.26, 0.24, 0.23],
                                                   extrapolation=X.LinearVariance, interpolation="cubic")),
]
TIMES = [0.0, 0.01, 0.08, 0.25, 0.4986301369863014, 0.75, 1.0, 1.3, 2.0, 2.5, 3.0, 5.003, 7.0, 15.0]


def cases():
    out = []
    st = v.settings(R)
    for name, spec in CURVES:
        s = bvts.black_vol(spec)
        out.append({
            "name": name,
            "inputs": {"settings": st, "structure": spec, "times": TIMES},
            "expected": v.enc({
                "referenceDate": s.referenceDate(),
                "maxDate": s.maxDate(),
                "minStrike": s.minStrike(),
                "maxStrike": s.maxStrike(),
                "dayCounter": s.dayCounter().name(),
                "blackVariance": [v.attempt(lambda: s.blackVariance(t, 100.0, True)) for t in TIMES],
                "blackVol": [v.attempt(lambda: s.blackVol(t, 100.0, True)) for t in TIMES],
                "blackVarianceFarStrike": [v.attempt(lambda: s.blackVariance(t, 1e6, True)) for t in TIMES],
                "pastMaxWithoutExtrapolation": v.attempt(lambda: s.blackVariance(15.0, 100.0))})})
    refusals = [
        ("dates and volatilities differ in number", [182, 365], [0.2]),
        ("first date on the reference date", [0, 365], [0.2, 0.2]),
        ("dates out of order", [365, 182], [0.2, 0.2]),
        ("a repeated date", [182, 182], [0.2, 0.2]),
        ("a decreasing variance, forced", [182, 365], [0.40, 0.20]),
    ]
    for name, offsets, vols in refusals:
        spec = bvts.vol_spec_curve(R, offsets, vols)
        out.append({"name": name, "inputs": {"settings": st, "structure": spec},
                    "expected": {"build": v.attempt(lambda: bvts.black_vol(spec).blackVariance(0.1, 100.0))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/blackvariancecurve.hpp", cases)
