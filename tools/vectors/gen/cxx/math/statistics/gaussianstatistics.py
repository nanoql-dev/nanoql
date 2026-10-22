# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GenericGaussianStatistics (ql/math/statistics/gaussianstatistics.hpp), which the wheel
doesn't expose, each output computed by the C++ harness: the Gaussian risk measures over GeneralStatistics and
IncrementalStatistics on Gaussian sets (losses and gains, with unit and random weights) and on sets of 0, 1 and 2
samples, and over StatsHolder at precomputed means and standard deviations: gaussianDownsideVariance and
gaussianDownsideDeviation; gaussianRegret, gaussianShortfall and gaussianAverageShortfall at five targets;
gaussianPercentile and gaussianTopPercentile at seven percentiles and gaussianPotentialUpside, gaussianValueAtRisk and
gaussianExpectedShortfall at seven, their refusals among them. Where QuantLib builds a distribution from mean() and
standardDeviation() as constructor arguments, MSVC evaluates the standard deviation first, which decides the refusal
of the sets of fewer than two samples. tests/NanoQL.Tests/Vectors/Cxx/Numerics/Statistics/
GaussianStatisticsCxxVectorTests.cs reads it back."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TARGETS = [-1.0, -0.1, 0.0, 0.25, 1.0]
PERCENTILES = [0.01, 0.5, 0.9, 0.95, 0.99, 0.0, 1.0]
NO_ARGUMENT = ("gaussianDownsideVariance", "gaussianDownsideDeviation")
AT_TARGET = ("gaussianRegret", "gaussianShortfall", "gaussianAverageShortfall")
AT_PERCENTILE = ("gaussianPercentile", "gaussianTopPercentile", "gaussianPotentialUpside", "gaussianValueAtRisk",
                 "gaussianExpectedShortfall")


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 22, 10, 2026))
        rnd = random.Random(v.SEED)
        sets = []
        for mean in (-0.5, 0.0, 0.5):
            data = [rnd.gauss(mean, 1.0) for _ in range(500)]
            sets.append((f"Gaussian around {mean!r}, unit weights", data, [1.0] * len(data)))
            sets.append((f"Gaussian around {mean!r}, random weights", data, [rnd.uniform(0.1, 3.0) for _ in data]))
        sets += [("empty", [], []), ("one", [0.3], [1.0]), ("two", [0.3, -0.2], [1.0, 2.0])]
        out = []

        def measures(kind, data, weights):
            e = {m: v.attempt(lambda m=m: H.call("GaussianStatistics", kind, data, weights, m, None)) for m in NO_ARGUMENT}
            for m in AT_TARGET:
                e[m] = [v.attempt(lambda m=m, t=t: H.call("GaussianStatistics", kind, data, weights, m, t))
                        for t in TARGETS]
            for m in AT_PERCENTILE:
                e[m] = [v.attempt(lambda m=m, p=p: H.call("GaussianStatistics", kind, data, weights, m, p))
                        for p in PERCENTILES]
            return e

        for kind in ("general", "incremental"):
            for name, data, weights in sets:
                out.append({"name": f"{kind}, {name}",
                            "inputs": {"settings": st, "kind": kind, "data": data, "weights": weights,
                                       "targets": TARGETS, "percentiles": PERCENTILES},
                            "expected": measures(kind, data, weights)})
        for mean, sd in ((0.0, 1.0), (-0.3, 0.2), (1.5, 2.0)):
            out.append({"name": f"holder, mean {mean!r}, standard deviation {sd!r}",
                        "inputs": {"settings": st, "kind": "holder", "data": [mean, sd], "weights": [],
                                   "targets": TARGETS, "percentiles": PERCENTILES},
                        "expected": measures("holder", [mean, sd], [])})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/statistics/gaussianstatistics.hpp", cases)
