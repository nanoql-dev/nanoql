# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CotSwapFromFwdCorrelation (ql/models/marketmodels/correlations/cotswapfromfwdcorrelation.hpp,
.cpp), which the wheel doesn't expose, each output computed by the C++ harness.

The coterminal swap rates' correlation from forward correlations (marketmodelspec.hpp) on eight semi-annual and six
irregular rates: exponential (exponent 1, its default times; exponent 0.5 on finer times) and time-homogeneous; on flat
and rising forwards; undisplaced and displaced 1%. The reads: the times, rate times, number of rates and each period's
matrix. Throws: a curve of another number of rates.

The harness unit is tools/cxx/src/models/marketmodels/correlations/correlations.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/CotSwapFromFwdCorrelationCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
GRIDS = [
    ("eight semi-annual rates", [0.5 * (i + 1) for i in range(9)]),
    ("six irregular rates", [0.3, 0.8, 1.0, 1.75, 2.5, 4.0, 4.6]),
]


def correlations(H, times):
    fix = times[:-1]
    finer = sorted(set(fix) | {(a + b) / 2 for a, b in zip([0.0] + fix[:-1], fix)})
    yield "exponential", {"type": "Exponential", "rateTimes": times, "longTermCorr": 0.5, "beta": 0.2, "gamma": 1.0,
                          "times": []}
    yield "exponential at 0.5 on finer times", {"type": "Exponential", "rateTimes": times, "longTermCorr": 0.3,
                                                "beta": 0.4, "gamma": 0.5, "times": finer}
    yield "time-homogeneous", {"type": "TimeHomogeneous", "rateTimes": times,
                               "fwdCorrelation": H.call("exponentialCorrelations", times, 0.2, 0.1, 1.0, 0.0)}


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in GRIDS:
            n = len(times) - 1
            for clabel, corr in correlations(H, times):
                for flabel, fwds in (("flat forwards", [0.04] * n), ("rising forwards",
                                                                      [0.03 + 0.003 * i for i in range(n)])):
                    for d in (0.0, 0.01):
                        out.append({"name": f"{label}, {clabel}, {flabel}, displaced {d}",
                                    "inputs": {"settings": st, "call": "correlation", "corr": corr,
                                               "rateTimes": times, "forwards": fwds, "displacement": d},
                                    "expected": v.enc(H.call("CotSwapFromFwdCorrelation", corr, times, fwds, d))})
            corr = next(correlations(H, times))[1]
            short = times[:-1]
            out.append({"name": f"{label}, refused curve",
                        "inputs": {"settings": st, "call": "refused", "corr": corr, "rateTimes": short,
                                   "forwards": [0.04] * (n - 1), "displacement": 0.0},
                        "expected": {"refused": v.attempt(lambda: H.call("CotSwapFromFwdCorrelation", corr, short,
                                                                         [0.04] * (n - 1), 0.0))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/correlations/cotswapfromfwdcorrelation.hpp", cases)
