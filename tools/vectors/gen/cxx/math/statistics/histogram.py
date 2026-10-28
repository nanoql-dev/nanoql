# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Histogram (ql/math/statistics/histogram.hpp, .cpp), which the wheel doesn't bind, each output computed by
the C++ harness: data sets from a fixed random sample (normal, uniform, and integers with ties), each binned by a number
of breaks (0, 1, 5 and 20), by Sturges' and Scott's rules, and by given breaks, sorted and unsorted, the bins, breaks,
algorithm, counts and frequencies. The Freedman-Diaconis rule's quantiles and given breaks that repeat are CR7 (v) to
(vii)'s, left out. Read back by tests/NanoQL.Tests/Vectors/Cxx/Numerics/Statistics/HistogramCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

FIELDS = ("bins", "breaks", "algorithm", "empty", "counts", "frequency")


def data_sets():
    rng = random.Random(v.SEED)
    return [("normal", [rng.gauss(0.0, 1.0) for _ in range(100)]),
            ("uniform", [rng.random() * 10.0 for _ in range(1000)]),
            ("integers with ties", [float(rng.randrange(10)) for _ in range(57)])]


def cases():
    out = []
    with cxx.connect() as H:
        for name, data in data_sets():
            ways = ([(f"{k} breaks", {"breaks": k}) for k in (0, 1, 5, 20)]
                    + [(a, {"algorithm": a}) for a in ("Sturges", "Scott")]
                    + [("breaks given, sorted", {"breaksGiven": [-1.0, 0.5, 2.0, 4.5, 7.0]}),
                       ("breaks given, unsorted", {"breaksGiven": [4.5, -1.0, 7.0, 0.5, 2.0]})])
            for way, how in ways:
                st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
                r = H.call("Histogram", data, how)
                out.append({"name": f"{name}, {way}", "inputs": {"settings": st, "data": v.enc(data), "how": how},
                            "expected": {k: v.enc(r[k]) for k in FIELDS}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/statistics/histogram.hpp", cases)
