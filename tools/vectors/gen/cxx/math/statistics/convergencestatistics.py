# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ConvergenceStatistics (ql/math/statistics/convergencestatistics.hpp), which the wheel doesn't
expose, each output computed by the C++ harness: the convergence table of the doubling rule (a mean stored at 1, 3,
7, 15, ... samples) over IncrementalStatistics and Statistics, with the samples and mean.

Sequences of 1 to 100 Gaussian samples, unit and random weights, and a reset part way through.

The harness unit is tools/cxx/src/math/statistics/convergencestatistics.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Statistics/ConvergenceStatisticsCxxVectorTests.cs."""
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


def cases():
    out = []
    rnd = random.Random(v.SEED)
    sets = []
    for n in (1, 2, 3, 8, 15, 16, 100):
        xs = [rnd.gauss(0.1, 1.0) for _ in range(n)]
        sets.append((f"{n} samples, unit weights", xs, [1.0] * n, None))
        sets.append((f"{n} samples, random weights", xs, [rnd.choice((0.5, 1.0, 2.0)) for _ in xs], None))
    xs = [rnd.gauss(0.0, 1.0) for _ in range(40)]
    sets.append(("a reset after 10", xs, [1.0] * 40, 10))
    with cxx.connect() as H:
        st = H.settings(R)
        for stats in ("IncrementalStatistics", "Statistics"):
            for name, xs, ws, reset in sets:
                r = H.call("ConvergenceStatistics.table", stats, xs, ws, reset)
                out.append({"name": f"{stats}: {name}",
                            "inputs": {"settings": st, "statistics": stats, "values": xs, "weights": ws,
                                       "resetAfter": reset},
                            "expected": {"table": [[row[0], v.enc(row[1])] for row in r["table"]],
                                         "samples": r["samples"], "mean": v.enc(r["mean"])}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/statistics/convergencestatistics.hpp", cases)
