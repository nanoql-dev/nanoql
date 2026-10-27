# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GenericSequenceStatistics (ql/math/statistics/sequencestatistics.hpp) through the wheel's
two closings of it: SequenceStatistics, whose source reads "Proxy of C++ GenericSequenceStatistics< RiskStatistics >"
(QuantLib's Statistics typedef, so nanoql's SequenceStatistics), and MultipleIncrementalStatistics, "Proxy of C++
GenericSequenceStatistics< IncrementalStatistics >" (nanoql's SequenceStatisticsInc). The wheel's MultipleStatistics,
"Proxy of C++ GenericSequenceStatistics< Statistics >", is the first's class again and isn't called.

Data sets of 1 to 4 components: one sample, two, three weighted, correlated Gaussian samples with unit and random
weights, samples around a large mean, a component of no variance (the correlation's 1 and 0 entries), and a
statistics built without a dimension that its first sample sets. Each read through size, samples, weightSum, mean,
variance, standardDeviation, errorEstimate, skewness, kurtosis, min, max, covariance and correlation (a throw where a
set is too small), then size and samples after reset(), the wheel's, which sets the dimension to 0 and leaves the
components' statistics as they were, so samples still counts them; and add's refusal of a sample of another size.

Read back by tests/NanoQL.Tests/Vectors/Numerics/Statistics/SequenceStatisticsVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

CLASSES = (("SequenceStatistics", ql.SequenceStatistics), ("SequenceStatisticsInc", ql.MultipleIncrementalStatistics))
VECTOR_MEMBERS = ("mean", "variance", "standardDeviation", "errorEstimate", "skewness", "kurtosis", "min", "max")
MATRIX_MEMBERS = ("covariance", "correlation")


def data_sets(rnd):
    """(name, dimension given, samples, weights) for every set the vectors read."""
    sets = [("one sample", 3, [[1.0, 2.0, 3.0]], [1.0]),
            ("two samples", 2, [[2.5, -1.0], [0.5, 4.0]], [1.0, 3.0]),
            ("three weighted", 3, [[2.5, -1.0, 4.0], [0.25, 1.0, -2.0], [1.5, 0.5, 0.0]], [0.5, 1.0, 2.0])]
    gauss = []
    for _ in range(500):
        z = [rnd.gauss(0.0, 1.0) for _ in range(3)]
        gauss.append([0.01 + 0.3 * z[0], -0.02 + 0.2 * (0.6 * z[0] + 0.8 * z[1]), 0.5 * z[2] - 0.3 * z[0]])
    sets.append(("Gaussian, unit weights", 3, gauss, [1.0] * len(gauss)))
    sets.append(("Gaussian, random weights", 3, gauss, [rnd.choice((0.5, 1.0, 2.0, 3.25)) for _ in gauss]))
    large = [[1e8 + rnd.gauss(0.0, 0.1), rnd.gauss(0.0, 1.0)] for _ in range(300)]
    sets.append(("large mean", 2, large, [rnd.uniform(0.1, 2.0) for _ in large]))
    sets.append(("a component of no variance", 4,
                 [[1.0, 2.0, 0.5, 7.0], [3.0, 2.0, -0.5, 7.0], [2.0, 2.0, 1.5, 7.0]], [1.0, 1.0, 1.0, 1.0]))
    sets.append(("dimension from the first sample", 0, [[0.3, -0.7], [1.1, 0.2], [-0.4, 0.9], [0.0, 0.1]],
                 [1.0, 2.0, 1.0, 0.5]))
    return sets


def rows(m):
    return [[m[i][j] for j in range(m.columns())] for i in range(m.rows())]


def readings(s):
    out = {"size": s.size(), "samples": s.samples(), "weightSum": s.weightSum()}
    for m in VECTOR_MEMBERS:
        out[m] = v.attempt(lambda m=m: [v.enc(x) for x in getattr(s, m)()])
    for m in MATRIX_MEMBERS:
        out[m] = v.attempt(lambda m=m: [[v.enc(x) for x in r] for r in rows(getattr(s, m)())])
    return out


def cases():
    st = v.settings(ql.Date(27, ql.October, 2026))
    out = []
    for label, cls in CLASSES:
        rnd = random.Random(v.SEED)
        for name, dimension, samples, weights in data_sets(rnd):
            s = cls(dimension)
            for x, w in zip(samples, weights):
                s.add(x, w)
            expected = readings(s)
            s.reset()
            expected["sizeAfterReset"] = s.size()
            expected["samplesAfterReset"] = s.samples()
            out.append({"name": f"{label}: {name}",
                        "inputs": {"settings": st, "class": label, "dimension": dimension,
                                   "samples": samples, "weights": weights},
                        "expected": expected})
        out.append({"name": f"{label}: a sample of another size",
                    "inputs": {"settings": st, "class": label, "dimension": 3, "samples": [[1.0, 2.0]],
                               "weights": [1.0]},
                    "expected": {"add": v.attempt(lambda cls=cls: cls(3).add([1.0, 2.0], 1.0))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/statistics/sequencestatistics.hpp", cases)
