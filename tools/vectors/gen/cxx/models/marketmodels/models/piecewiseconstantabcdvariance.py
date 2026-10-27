# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's PiecewiseConstantAbcdVariance and the PiecewiseConstantVariance base's reads
(ql/models/marketmodels/models/piecewiseconstantabcdvariance.hpp, .cpp, piecewiseconstantvariance.hpp, .cpp), which the
wheel doesn't expose, each output computed by the C++ harness.

Three abcd functions (QuantLib's test's -0.0597, 0.1677, 0.5403, 0.1710; a flat 0, 0, 1, 0.2; a humped 0.02, 0.4, 1.2,
0.1) on ten semi-annual rate times, five irregular ones and two: every reset index. The reads: each step's variance and
volatility, the rate times, variance(i), volatility(i), totalVariance(i), totalVolatility(i) and getABCD. Throws: one
rate time, a reset index at the last rate time, decreasing times.

The harness unit is tools/cxx/src/models/marketmodels/models/variances.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/PiecewiseConstantAbcdVarianceCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
ABCDS = [("QuantLib's", [-0.0597, 0.1677, 0.5403, 0.1710]), ("flat", [0.0, 0.0, 1.0, 0.2]),
         ("humped", [0.02, 0.4, 1.2, 0.1])]
TIMES = [("ten semi-annual times", [0.5 * (i + 1) for i in range(10)]),
         ("five irregular times", [0.3, 0.8, 1.0, 1.75, 2.5]), ("two times", [1.0, 2.0])]


def spec(abcd, reset, times):
    a, b, c, d = abcd
    return {"a": a, "b": b, "c": c, "d": d, "resetIndex": reset, "rateTimes": times}


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for alabel, abcd in ABCDS:
            for tlabel, times in TIMES:
                for reset in range(len(times) - 1):
                    s = spec(abcd, reset, times)
                    out.append({"name": f"{alabel} abcd, {tlabel}, reset {reset}",
                                "inputs": {"settings": st, "call": "variance", "variance": s},
                                "expected": v.enc(H.call("PiecewiseConstantAbcdVariance", s))})
        abcd = ABCDS[0][1]
        refusals = {"oneTime": spec(abcd, 0, [1.0]), "resetAtLastTime": spec(abcd, 4, TIMES[1][1]),
                    "decreasingTimes": spec(abcd, 0, [1.0, 0.5, 2.0])}
        out.append({"name": "refused variances",
                    "inputs": {"settings": st, "call": "refused", "variances": refusals},
                    "expected": {k: v.attempt(lambda s=s: H.call("PiecewiseConstantAbcdVariance", s))
                                 for k, s in refusals.items()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/models/piecewiseconstantabcdvariance.hpp", cases)
