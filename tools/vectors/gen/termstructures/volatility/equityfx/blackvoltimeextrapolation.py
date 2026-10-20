# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BlackVolTimeExtrapolation (ql/termstructures/volatility/equityfx/
blackvoltimeextrapolation.hpp, .cpp): the time extrapolation of a curve's variance past its last date, through
BlackVarianceCurve, which calls the curve overload of extrapolatedVariance with its times and its interpolated
variance, on the evaluation date 15 October 2026.

For each of the three strategies (FlatVolatility, UseInterpolator, LinearVariance) and five curves (one date, two,
four; increasing variance; and, with the forced monotone variance off, a variance falling between the last two dates
and one falling below zero when the linear interpolation is extended), the variance and volatility at eight times
past the last date: the flat volatility of the last date, the interpolator's own linear extension (floored at zero),
and the line through the last two variances, which QuantLib refuses where they decrease ("variances must be
non-decreasing"). The surface overload, which no QuantLib class calls, is vectored through the C++ harness
(tools/vectors/gen/cxx/termstructures/volatility/equityfx/blackvoltimeextrapolation.py).

The structure specs are tools/vectors/gen/termstructures/volatility/equityfx/blackvoltermstructure.py's. The wheel's
calls, by QuantLib-SWIG 1.44's volatilities.i: the BlackVolTimeExtrapolation enum and BlackVarianceCurve's constructor
taking it. Read back by tests/NanoQL.Tests/Vectors/TermStructures/BlackVolTimeExtrapolationVectorTests.cs."""
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
STRATEGIES = [("flat volatility", X.FlatVolatility), ("use interpolator", X.UseInterpolator),
              ("linear variance", X.LinearVariance)]
CURVES = [
    ("one date", [365], [0.2], True),
    ("two dates", [182, 730], [0.25, 0.22], True),
    ("four dates", [91, 365, 730, 1095], [0.18, 0.20, 0.21, 0.215], True),
    ("variance falling at the end", [182, 365, 730], [0.25, 0.30, 0.20], False),
    ("variance extended below zero", [365, 730], [0.40, 0.10], False),
]
PAST = [1e-7, 0.25, 0.5, 1.0, 2.0, 5.0, 10.0, 28.0]


def cases():
    out = []
    st = v.settings(R)
    for strategy_name, strategy in STRATEGIES:
        for curve_name, offsets, vols, force in CURVES:
            spec = bvts.vol_spec_curve(R, offsets, vols, force=force, extrapolation=strategy)
            s = bvts.black_vol(spec)
            times = [s.maxTime() + d for d in PAST]
            out.append({
                "name": f"{strategy_name}, {curve_name}",
                "inputs": {"settings": st, "structure": spec, "times": times},
                "expected": v.enc({
                    "blackVariance": [v.attempt(lambda: s.blackVariance(t, 100.0, True)) for t in times],
                    "blackVol": [v.attempt(lambda: s.blackVol(t, 100.0, True)) for t in times]})})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/blackvoltimeextrapolation.hpp", cases)
