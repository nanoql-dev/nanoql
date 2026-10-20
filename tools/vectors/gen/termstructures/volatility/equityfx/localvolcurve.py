# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LocalVolCurve (ql/termstructures/volatility/equityfx/localvolcurve.hpp), which the wheel
doesn't expose: through a Black-Scholes process on a BlackVarianceCurve, whose local volatility QuantLib's process
builds as a LocalVolCurve, on the evaluation date 15 October 2026.

Six curves (one to six dates, the three time extrapolations, linear and cubic interpolation, a variance flat between
two dates): the local volatility's reference and maximum dates, minimum and maximum strikes and day counter's name,
which are the Black curve's, and at 12 (time, level) points, at and between the dates, past the last and at level
1e6 (which the curve ignores), the local volatility read through the process's localVolatility() and the process's
diffusion. A curve whose variance falls is left out: QuantLib returns the square root of a negative derivative, NaN,
where nanoql refuses (tests/NanoQL.Tests/Divergences/LocalVolCurveRefusalTests.cs).

The specs are tools/vectors/gen/processes/blackscholesprocess.py's. The wheel's calls, by QuantLib-SWIG 1.44's
stochasticprocess.i: BlackScholesMertonProcess's constructor, localVolatility(), whose handle holds the
LocalVolCurve, and diffusion. Read back by tests/NanoQL.Tests/Vectors/TermStructures/LocalVolCurveVectorTests.cs."""
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
X = ql.BlackVolTimeExtrapolation
CURVES = [
    ("one date", bvts.vol_spec_curve(R, [365], [0.2])),
    ("six dates", bvts.vol_spec_curve(R, [30, 91, 182, 365, 730, 1826], [0.15, 0.17, 0.19, 0.21, 0.23, 0.25])),
    ("use interpolator", bvts.vol_spec_curve(R, [91, 365, 730], [0.22, 0.24, 0.25], extrapolation=X.UseInterpolator)),
    ("linear variance, Actual/360",
     bvts.vol_spec_curve(R, [91, 365, 730], [0.22, 0.24, 0.25], bvts.A360, extrapolation=X.LinearVariance)),
    ("cubic", bvts.vol_spec_curve(R, [30, 91, 182, 365, 730, 1826], [0.15, 0.17, 0.19, 0.21, 0.23, 0.25],
                                  interpolation="cubic")),
    ("flat variance between two dates", bvts.vol_spec_curve(R, [182, 365], [0.20, 0.2 * (182 / 365) ** 0.5],
                                                             force=False)),
]
POINTS = [(0.0, 100.0), (0.05, 90.0), (0.0821917808219178, 100.0), (0.2, 110.0), (0.5, 100.0), (0.75, 1e6),
          (1.0, 100.0), (1.5, 80.0), (2.0, 100.0), (3.0, 120.0), (5.0027397260274, 100.0), (8.0, 100.0)]


def cases():
    out = []
    st = v.settings(R)
    for name, vol in CURVES:
        spec = bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, vol, bsp.ZERO_Q)
        p = bsp.process(spec)
        lv = p.localVolatility()
        out.append({
            "name": name,
            "inputs": {"settings": st, "process": spec, "points": [list(x) for x in POINTS]},
            "expected": v.enc({
                "referenceDate": lv.referenceDate(),
                "maxDate": lv.maxDate(),
                "minStrike": lv.minStrike(),
                "maxStrike": lv.maxStrike(),
                "dayCounter": lv.dayCounter().name(),
                "localVol": [lv.localVol(t, x, True) for t, x in POINTS],
                "diffusion": [p.diffusion(t, x) for t, x in POINTS]})})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/localvolcurve.hpp", cases)
