# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's TimeHomogeneousForwardCorrelation
(ql/models/marketmodels/correlations/timehomogeneousforwardcorrelation.hpp, .cpp), which the wheel doesn't expose,
each output computed by the C++ harness: its times, rate times, number of rates and correlation matrices, and the
static evolvedMatrices.

Correlations at time zero (exponential ones, and symmetric matrices drawn from Python's Mersenne twister, seeded) on
eight semi-annual, six irregular, two and one rates: the correlation's members, and evolvedMatrices of the matrices
and of rectangular ones with one column fewer or more than their rows. Throws: a matrix of the wrong size, refused
times.

Left out (nanoql refuses it, QuantLib reading past it; tests/NanoQL.Tests/Divergences/MarketModelUndefinedReadsTests.cs):
evolvedMatrices of a matrix with fewer columns than its rows less one.

The harness unit is tools/cxx/src/models/marketmodels/correlations/correlations.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/TimeHomogeneousForwardCorrelationCxxVectorTests.cs."""
import os
import random
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
    ("two rates", [1.0, 2.0, 3.0]),
    ("one rate", [1.0, 2.0]),
]


def symmetric(rng, n):
    m = [[0.0] * n for _ in range(n)]
    for i in range(n):
        m[i][i] = 1.0
        for j in range(i):
            m[i][j] = m[j][i] = round(rng.uniform(0.2, 0.95), 6)
    return m


def cases():
    rng = random.Random(v.SEED)
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in GRIDS:
            n = len(times) - 1
            for clabel, m in (("exponential", H.call("exponentialCorrelations", times, 0.5, 0.2, 1.0, 0.0)),
                              ("drawn", symmetric(rng, n))):
                out.append({"name": f"{label}, {clabel}",
                            "inputs": {"settings": st, "call": "correlation", "fwdCorrelation": m, "rateTimes": times},
                            "expected": v.enc(H.call("TimeHomogeneousForwardCorrelation", m, times))})
                out.append({"name": f"{label}, {clabel}, evolved",
                            "inputs": {"settings": st, "call": "evolved", "fwdCorrelation": m},
                            "expected": {"matrices": v.enc(H.call("TimeHomogeneousForwardCorrelation::evolvedMatrices",
                                                                  m))}})
            m = symmetric(rng, n + 1)
            for rlabel, rect in (("one column fewer", [r[:-1] for r in m]), ("one column more", m[:-1])):
                out.append({"name": f"{label}, evolved, {rlabel}",
                            "inputs": {"settings": st, "call": "evolved", "fwdCorrelation": rect},
                            "expected": {"matrices": v.enc(H.call("TimeHomogeneousForwardCorrelation::evolvedMatrices",
                                                                  rect))}})
            for rlabel, bad in (("one row fewer", m[:-2] if n > 1 else [[1.0, 0.5]]),
                                ("one column more", [r + [0.1] for r in symmetric(rng, n)])):
                out.append({"name": f"{label}, refused: {rlabel}",
                            "inputs": {"settings": st, "call": "correlation", "fwdCorrelation": bad, "rateTimes": times},
                            "expected": {"construction": v.attempt(
                                lambda: H.call("TimeHomogeneousForwardCorrelation", bad, times))}})
        out.append({"name": "refused: decreasing times",
                    "inputs": {"settings": st, "call": "correlation", "fwdCorrelation": [[1.0, 0.5], [0.5, 1.0]],
                               "rateTimes": [1.0, 0.5, 2.0]},
                    "expected": {"construction": v.attempt(lambda: H.call(
                        "TimeHomogeneousForwardCorrelation", [[1.0, 0.5], [0.5, 1.0]], [1.0, 0.5, 2.0]))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/correlations/timehomogeneousforwardcorrelation.hpp", cases)
