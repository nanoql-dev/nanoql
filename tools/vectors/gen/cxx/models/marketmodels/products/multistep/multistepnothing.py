# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MultiStepNothing (ql/models/marketmodels/products/multistep/multistepnothing.hpp, .cpp), which
the wheel doesn't expose, each output computed by the C++ harness: the product of nothing stepped on set LMM curve
states, read per step as multistepforwards.py reads the forwards: its path finished at its done index (the first step
for 0 and 1), its counts 0, its evolution the rate times' whatever the evolution given.

The harness unit is tools/cxx/src/models/marketmodels/products/products.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MultiStepNothingCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import cxx  # noqa: E402
import multistepforwards as mf  # noqa: E402
import vectors as v  # noqa: E402

R = 46322


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in mf.GRIDS:
            n = len(times) - 1
            fs = mf.states(rnd, times, n)
            for products, done, evolution in ((1, 0, times[:-1]), (1, 1, times[:-1]), (3, 3, times[:-1]),
                                              (0, n, [times[1]]), (2, n + 2, times[:-1])):
                spec = {"type": "MultiStepNothing", "rateTimes": times, "evolutionTimes": evolution,
                        "numberOfProducts": products, "doneIndex": done}
                out.append(mf.product_case(H, st, f"{label}: {products} products, done at {done}", spec, fs))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/multistep/multistepnothing.hpp", cases)
