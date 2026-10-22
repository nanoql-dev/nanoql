# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's GeneralStatistics (ql/math/statistics/generalstatistics.hpp, .cpp) the
wheel's Statistics doesn't expose, each output computed by the C++ harness: percentile and topPercentile at 10
percentiles, their refusals at 0 and above 1 among them, and the data in the set's order and after sort, on
generalstatistics.py's data sets (the empty set's refusal included) and on sets of weighted ties.

The set with a NaN sample is left out of the percentiles and the sort: QuantLib sorts with a comparison NaN leaves
undefined, and the port refuses (Divergences/StatisticsNaNSortTests.cs). tests/NanoQL.Tests/Vectors/Cxx/Numerics/
Statistics/GeneralStatisticsCxxVectorTests.cs reads it back."""
import math
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.join(_dir, "gen", "math", "statistics"))

import cxx  # noqa: E402
import generalstatistics  # noqa: E402
import vectors as v  # noqa: E402

PERCENTILES = [0.01, 0.05, 0.1, 0.25, 0.5, 0.75, 0.9, 0.99, 1.0, 0.0, 1.5]
SMALL = 50


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 22, 10, 2026))
        rnd = random.Random(v.SEED)
        sets = [s for s in generalstatistics.data_sets(rnd) if not any(math.isnan(x) for x in s[1])]
        sets.append(("weighted ties", [3.0, 1.0, 2.0, 1.0, 3.0, 2.0, 1.0], [0.5, 2.0, 1.0, 1.0, 0.25, 3.0, 0.0]))
        sets.append(("one heavy sample", [5.0, 1.0, 9.0, 3.0], [0.1, 0.1, 10.0, 0.1]))
        out = []
        for name, data, weights in sets:
            expected = {
                "percentile": [v.attempt(lambda y=y: H.call("GeneralStatistics::percentile", data, weights, y))
                               for y in PERCENTILES],
                "topPercentile": [v.attempt(lambda y=y: H.call("GeneralStatistics::topPercentile", data, weights, y))
                                  for y in PERCENTILES]}
            if len(data) <= SMALL:
                expected["data"] = v.enc(H.call("GeneralStatistics::data", data, weights, False))
                expected["sortedData"] = v.enc(H.call("GeneralStatistics::data", data, weights, True))
            out.append({"name": name, "inputs": {"settings": st, "data": [v.enc(x) for x in data], "weights": weights,
                                                 "percentiles": PERCENTILES},
                        "expected": expected})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/statistics/generalstatistics.hpp", cases)
