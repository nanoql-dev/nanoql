# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's market-model time utilities (ql/models/marketmodels/utilities.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness: mergeTimes, isInSubset and the two time checks.

mergeTimes on sets with shared, repeated and disjoint times (and an empty set): the merged times and each set's
presence over them. isInSubset on sets and subsets equal, interleaved, disjoint above and below, empty, and larger
(a throw). The checks on increasing positive times, one time, a first time at or below zero, equal and decreasing
times (throws); the taus of each accepted.

Left out (nanoql refuses them, its sort and binary search undefined in QuantLib;
tests/NanoQL.Tests/Divergences/MarketModelUndefinedReadsTests.cs): a set holding a NaN or out of order.

The harness unit is tools/cxx/src/models/marketmodels/utilities.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/UtilitiesCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
MERGES = [
    ("two sets sharing times", [[0.5, 1.0, 1.5, 2.0], [1.0, 2.0, 3.0]]),
    ("three sets, one repeating a time", [[0.25, 0.5, 0.75], [0.5, 0.5, 1.0], [2.0]]),
    ("disjoint sets", [[3.0, 4.0], [0.1, 0.2]]),
    ("an empty set", [[], [1.0, 2.0]]),
    ("one set", [[0.3, 0.6, 0.9, 1.2]]),
    ("no sets", []),
]
SUBSETS = [
    ("equal", [1.0, 2.0, 3.0], [1.0, 2.0, 3.0]),
    ("interleaved", [0.5, 1.0, 1.5, 2.0, 2.5, 3.0], [1.0, 2.0, 3.0]),
    ("disjoint above", [float(i) for i in range(100)], [100.0 + i for i in range(100)]),
    ("disjoint below", [10.0, 11.0], [1.0]),
    ("an empty subset", [1.0, 2.0], []),
    ("off the set", [1.0, 2.0, 3.0, 4.0], [1.5, 3.0]),
    ("a larger subset", [1.0], [1.0, 2.0]),
]
TIMES = [
    ("increasing", [0.5, 1.0, 1.75, 3.0]),
    ("one time", [2.0]),
    ("a first time at zero", [0.0, 1.0]),
    ("a negative first time", [-0.5, 1.0]),
    ("equal times", [0.5, 1.0, 1.0]),
    ("decreasing times", [0.5, 2.0, 1.0]),
    ("no times", []),
]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in MERGES:
            r = H.call("mergeTimes", times)
            out.append({"name": f"merge: {label}", "inputs": {"settings": st, "call": "merge", "times": times},
                        "expected": v.enc(r)})
        for label, s, sub in SUBSETS:
            out.append({"name": f"subset: {label}",
                        "inputs": {"settings": st, "call": "subset", "set": s, "subset": sub},
                        "expected": {"isInSubset": v.attempt(lambda: H.call("isInSubset", s, sub))}})
        for label, times in TIMES:
            out.append({"name": f"times: {label}", "inputs": {"settings": st, "call": "times", "times": times},
                        "expected": {
                            "checkIncreasingTimes": v.attempt(
                                lambda: H.call("checkIncreasingTimes", times) is None or None),
                            "taus": v.attempt(lambda: H.call("checkIncreasingTimesAndCalculateTaus", times))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/utilities.hpp", cases)
