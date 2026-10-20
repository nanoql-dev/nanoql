# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BlackVolTimeExtrapolation (ql/termstructures/volatility/equityfx/
blackvoltimeextrapolation.hpp, .cpp), each output computed by the C++ harness: its two extrapolatedVariance overloads
called directly, the surface one (which no QuantLib class calls) and the curve one, on the harness's own variances: a
surface w(t, k) = t s(k)^2 + c t^2 with s(k) = sigma0 + slope (k - 100) / 100, and a curve w(t) = t sigma0^2 + c t^2.

For each of the three strategies, on three variance shapes (increasing, flat in strike, and one whose curvature c is
negative, so it falls at long times), at four times past each of three time grids (one time, two, four) and three
strikes: the extrapolated variance, or QuantLib's refusals (the linear strategy's falling variances and its times out
of order). The linear strategy on a grid of one time is left out of the surface overload: QuantLib reads times[N-2]
with N - 2 wrapped (tests/NanoQL.Tests/Divergences/BlackVolTimeExtrapolationRefusalTests.cs); the curve overload
refuses it itself ("at least two times required for volatility extrapolation"), as nanoql's two do.

Read back by tests/NanoQL.Tests/Vectors/Cxx/TermStructures/BlackVolTimeExtrapolationCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

FLAT, INTERPOLATOR, LINEAR = 0, 1, 2
STRATEGIES = [("flat volatility", FLAT), ("use interpolator", INTERPOLATOR), ("linear variance", LINEAR)]
SHAPES = [("increasing", [0.2, 0.05, 0.001]), ("flat in strike", [0.25, 0.0, 0.0]),
          ("falling at long times", [0.3, 0.1, -0.02])]
GRIDS = [("one time", [1.0]), ("two times", [0.5, 2.0]), ("four times", [0.25, 1.0, 2.0, 3.0]),
         ("out of order", [2.0, 1.0])]
PAST = [1e-7, 0.5, 2.0, 7.0]
STRIKES = [80.0, 100.0, 130.0]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(46310)
        for sname, strategy in STRATEGIES:
            for shape, (sigma0, slope, c) in SHAPES:
                for gname, times in GRIDS:
                    ts = [times[-1] + d for d in PAST]
                    expected = {
                        "curve": [v.attempt(lambda t=t: H.call("BlackVolTimeExtrapolation::extrapolatedVarianceCurve",
                                                               strategy, t, times, [sigma0, c])) for t in ts]}
                    if not (strategy == LINEAR and len(times) < 2):
                        expected["surface"] = [
                            [v.attempt(lambda t=t, k=k: H.call("BlackVolTimeExtrapolation::extrapolatedVariance",
                                                               strategy, t, k, times, [sigma0, slope, c]))
                             for k in STRIKES] for t in ts]
                    out.append({"name": f"{sname}, {shape}, {gname}",
                                "inputs": {"settings": st, "type": strategy, "times": times, "ts": ts,
                                           "strikes": STRIKES, "sigma0": sigma0, "slope": slope, "c": c},
                                "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/blackvoltimeextrapolation.hpp", cases)
