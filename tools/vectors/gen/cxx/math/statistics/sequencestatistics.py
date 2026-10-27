# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GenericSequenceStatistics (ql/math/statistics/sequencestatistics.hpp): the lifted members
the wheel doesn't bind, each output computed by the C++ harness. On QuantLib's SequenceStatistics, its Statistics
components' Gaussian members (gaussianPercentile, gaussianPotentialUpside, gaussianValueAtRisk,
gaussianExpectedShortfall, gaussianShortfall, gaussianAverageShortfall) and empirical ones (percentile, potentialUpside,
valueAtRisk, expectedShortfall, regret, shortfall, averageShortfall, semiVariance, semiDeviation, downsideVariance,
downsideDeviation); on SequenceStatisticsInc, IncrementalStatistics' own downsideVariance and downsideDeviation.

Correlated Gaussian samples of 1 to 3 components, unit and random weights, at percentiles 0.9, 0.95 and 0.99 and
targets below, at and above the means; and small sets (2 and 3 samples), where members needing two samples below a
level refuse.

Left out: expectedShortfall where QuantLib refuses it, no sample lying below the value-at-risk's level, where nanoql
reads the level itself (a documented divergence; tests/NanoQL.Tests/Divergences/ExpectedShortfallNoLossTests.cs).

Not a twin of math/statistics/sequencestatistics.json, the wheel's, which reads the members the wheel binds. The
harness unit is tools/cxx/src/math/statistics/sequencestatistics.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Statistics/GenericSequenceStatisticsCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46322


def sets(rnd):
    out = []
    for dim, n in ((1, 300), (3, 500), (2, 2), (2, 3)):
        samples = []
        for _ in range(n):
            z = [rnd.gauss(0.0, 1.0) for _ in range(dim)]
            samples.append([0.02 * k + 0.3 * (z[k] + 0.5 * z[0]) for k in range(dim)])
        out.append((f"{dim} components, {n} samples, unit weights", samples, [1.0] * n))
        out.append((f"{dim} components, {n} samples, random weights", samples,
                    [rnd.choice((0.5, 1.0, 2.0)) for _ in samples]))
    return out


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(R)
        for name, samples, weights in sets(rnd):
            for p, t in ((0.9, -0.2), (0.95, 0.0), (0.99, 0.15)):
                for stats in ("Statistics", "IncrementalStatistics"):
                    r = H.call("GenericSequenceStatistics.members", stats, samples, weights, p, t)
                    if r.get("expectedShortfall") == {"throws": True}:
                        r.pop("expectedShortfall")
                    out.append({"name": f"{stats}: {name}, percentile {p}, target {t}",
                                "inputs": {"settings": st, "statistics": stats, "samples": samples,
                                           "weights": weights, "percentile": p, "target": t},
                                "expected": {k: v.enc(x) for k, x in r.items()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/statistics/sequencestatistics.hpp", cases)
