# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for DiscrepancyStatistics (ql/math/statistics/discrepancystatistics.hpp, .cpp), which the wheel doesn't bind,
each output computed by the C++ harness: statistics of a dimension given, 2, 3, 5 and 10 (QuantLib's lazy dimension 0
and its percentile reads are nanoql's fixes, left out), and the discrepancy after each of 200 samples, uniform in the
unit cube from a fixed random sample, with a few exact repeats and coordinates at 0. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Statistics/DiscrepancyStatisticsCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

DIMENSIONS = [2, 3, 5, 10]
SAMPLES = 200


def samples(dim):
    rng = random.Random(v.SEED + dim)
    out = [[rng.random() for _ in range(dim)] for _ in range(SAMPLES)]
    out[10] = list(out[3])
    out[20] = [0.0] + out[20][1:]
    return out


def cases():
    out = []
    with cxx.connect() as H:
        for dim in DIMENSIONS:
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            s = samples(dim)
            out.append({"name": f"{dim} dimensions",
                        "inputs": {"settings": st, "dimension": dim, "samples": [v.enc(x) for x in s]},
                        "expected": {"discrepancy": v.enc(H.call("DiscrepancyStatistics::discrepancy", dim, s))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/statistics/discrepancystatistics.hpp", cases)
