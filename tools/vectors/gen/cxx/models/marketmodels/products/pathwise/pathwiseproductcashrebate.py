# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MarketModelPathwiseCashRebate (ql/models/marketmodels/products/pathwise/
pathwiseproductcashrebate.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness: the
pathwise rebate stepped on LMM curve states the harness sets, read per step with its amounts' derivatives, with the
product's counts and times (pathwiseproductcaplet.py's helpers).

Rate grids: six semi-annual rates and five irregular ones; one and two products, rebates at every step and at a
coarse evolution's steps, amounts per product and step, payment times off the rate times.

Left out (QuantLib and nanoql refuse them): amounts not one a product and payment time, payment times not one an
evolution time.

The harness unit is tools/cxx/src/models/marketmodels/products/products.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MarketModelPathwiseCashRebateCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import cxx  # noqa: E402
import pathwiseproductcaplet as pc  # noqa: E402
import vectors as v  # noqa: E402


def spec(times, evolution_times, payment_times, amounts):
    return {"type": "MarketModelPathwiseCashRebate", "rateTimes": times, "evolutionTimes": evolution_times,
            "paymentTimes": payment_times, "amounts": amounts, "numberOfProducts": len(amounts)}


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(pc.R)
        for label, times in pc.GRIDS:
            n = len(times) - 1
            every = times[:-1]
            out += pc.stepped(H, st, rnd, f"{label}: one rebate a step",
                              spec(times, every, times[1:], [[0.01 * (j + 1) for j in range(n)]]), times)
            out += pc.stepped(H, st, rnd, f"{label}: two products, paid off the rate times",
                              spec(times, every, [t + 0.1 for t in every],
                                   [[0.01] * n, [0.02 * (j + 1) for j in range(n)]]), times)
            coarse = [times[1], times[3]]
            out += pc.stepped(H, st, rnd, f"{label}: a coarse evolution",
                              spec(times, coarse, [times[2], times[4]], [[0.01, 0.03], [0.02, 0.0]]), times)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/pathwise/pathwiseproductcashrebate.hpp", cases)
