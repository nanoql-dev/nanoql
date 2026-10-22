# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GeneralStatistics (ql/math/statistics/generalstatistics.hpp, .cpp), through the wheel's
Statistics, QuantLib's Statistics typedef (RiskStatistics, a GeneralStatistics with the Gaussian and empirical risk
measures): data sets of 0 to 4 samples, where the members refuse one by one; Gaussian samples with unit weights,
with random weights (a fifth of them 0), and around a large mean; ties, a signed zero and infinities, whose minimum
and maximum read the pairs' operator<; and a NaN sample. For each set, after adding every sample: samples, weightSum,
mean, variance, standardDeviation, errorEstimate, skewness, kurtosis, min and max; then, after reset, samples and
mean. Then add's refusal of a negative and of a NaN weight.

Sets whose samples weigh 0 in all are left out: QuantLib's members divide 0 by 0 there and the port refuses
(Divergences/StatisticsZeroWeightTests.cs). The wheel's Statistics binds add, reset and those members, each the C++
class's; percentile, topPercentile, data and expectationValue aren't bound (the harness's
cxx/math/statistics/generalstatistics and QuantLib's riskstats case cover them). Read back by
tests/NanoQL.Tests/Vectors/Numerics/Statistics/GeneralStatisticsVectorTests.cs."""
import math
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

MEMBERS = ("samples", "weightSum", "mean", "variance", "standardDeviation", "errorEstimate", "skewness", "kurtosis",
           "min", "max")


def data_sets(rnd):
    """(name, samples, weights) for every set the statistics vectors read; shared with incrementalstatistics.py."""
    sets = [("empty", [], []), ("one", [2.5], [1.0]), ("two", [2.5, -1.0], [1.0, 3.0]),
            ("three", [2.5, -1.0, 4.0], [0.5, 1.0, 2.0]), ("four", [2.5, -1.0, 4.0, 0.25], [1.0, 1.0, 2.0, 0.5])]
    normal = [rnd.gauss(0.01, 0.3) for _ in range(2000)]
    sets.append(("Gaussian, unit weights", normal, [1.0] * len(normal)))
    sets.append(("Gaussian, random weights", normal,
                 [rnd.choice((0.0, 0.5, 1.0, 2.0, 3.25)) for _ in normal]))
    large = [1e8 + rnd.gauss(0.0, 0.1) for _ in range(2000)]
    sets.append(("large mean, unit weights", large, [1.0] * len(large)))
    sets.append(("large mean, random weights", large, [rnd.uniform(0.0, 2.0) for _ in large]))
    sets.append(("ties", [1.0, 2.0, 2.0, 1.0, 3.0, 3.0, 1.0], [2.0, 1.0, 1.0, 0.5, 0.0, 4.0, 2.0]))
    sets.append(("signed zero", [0.0, -0.0, 1.5, -0.0, 0.0], [1.0, 2.0, 1.0, 1.0, 0.5]))
    sets.append(("infinities", [1.0, math.inf, -2.0, -math.inf, 0.5], [1.0, 1.0, 1.0, 1.0, 1.0]))
    sets.append(("NaN sample", [1.0, math.nan, 2.0, 3.0, 0.5], [1.0, 1.0, 2.0, 1.0, 1.0]))
    return sets


def readings(s):
    return {m: v.attempt(lambda m=m: getattr(s, m)()) for m in MEMBERS}


def cases_for(cls):
    st = v.settings(ql.Date(22, ql.October, 2026))
    rnd = random.Random(v.SEED)
    out = []
    for name, data, weights in data_sets(rnd):
        s = cls()
        for x, w in zip(data, weights):
            s.add(x, w)
        expected = readings(s)
        s.reset()
        expected["samplesAfterReset"] = s.samples()
        expected["meanAfterReset"] = v.attempt(lambda: s.mean())
        out.append({"name": name, "inputs": {"settings": st, "data": [v.enc(x) for x in data], "weights": weights},
                    "expected": expected})
    for name, w in (("negative weight", -1.0), ("NaN weight", math.nan)):
        out.append({"name": name, "inputs": {"settings": st, "data": [1.0], "weights": [v.enc(w)]},
                    "expected": {"add": v.attempt(lambda w=w: cls().add(1.0, w))}})
    return out


def cases():
    return cases_for(ql.Statistics)


if __name__ == "__main__":
    v.main(__file__, "ql/math/statistics/generalstatistics.hpp", cases)
