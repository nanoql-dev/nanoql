# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ExerciseAdapter (ql/models/marketmodels/products/multistep/exerciseadapter.hpp, .cpp), which
the wheel doesn't expose, each output computed by the C++ harness: the adapter stepped on LMM curve states the harness
sets, read per step, with its counts and times (multistepswap.py's helpers).

Rate grids: six semi-annual rates and five irregular ones; adapters of a Bermudan swaption's exercise value, payer and
receiver, and of nothing exercised at every other and at the last rate time; one and two products.

The harness unit is tools/cxx/src/models/marketmodels/products/products.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/ExerciseAdapterCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "callability"))

import collectnodedata as cn  # noqa: E402
import cxx  # noqa: E402
import multistepswap as ms  # noqa: E402
import vectors as v  # noqa: E402


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(ms.R)
        for label, times in ms.GRIDS:
            n = len(times) - 1
            k = cn.mid(times)
            for option_type, side in ((1, "payer"), (-1, "receiver")):
                spec = {"type": "ExerciseAdapter", "exercise": cn.bermudan(times, option_type, k), "numberOfProducts": 1}
                out += ms.steps_and_engine(H, st, rnd, f"{label}: a Bermudan {side}", spec, times, engine=False)
            for what, flags in (("every other rate time", [i % 2 == 0 for i in range(n)]),
                                ("the last rate time", [i == n - 1 for i in range(n)])):
                for products in (1, 2):
                    spec = {"type": "ExerciseAdapter", "exercise": cn.nothing(times, flags), "numberOfProducts": products}
                    out += ms.steps_and_engine(H, st, rnd, f"{label}: nothing at {what}, {products} products", spec,
                                               times, engine=False)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/multistep/exerciseadapter.hpp", cases)
