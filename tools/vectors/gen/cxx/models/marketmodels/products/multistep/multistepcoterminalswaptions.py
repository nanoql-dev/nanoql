# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MultiStepCoterminalSwaptions (ql/models/marketmodels/products/multistep/
multistepcoterminalswaptions.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness: the
coterminal swaptions stepped on LMM curve states the harness sets, read per step, with the product's counts and times;
and their values through the accounting engine over a few paths (multistepswap.py's helpers).

Rate grids: six semi-annual rates and five irregular ones; calls and puts at the forwards' mean, strikes rising along
the curve, and payment times off the rate times.

Left out (nanoql refuses them, QuantLib reading past its inputs; tests/NanoQL.Tests/Conventions/
MarketModelProductTests.cs): payment times or payoffs not one a rate.

The harness units are tools/cxx/src/models/marketmodels/products/products.cpp and
tools/cxx/src/models/marketmodels/accountingengine.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MultiStepCoterminalSwaptionsCxxVectorTests.cs."""
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
import multistepswap as ms  # noqa: E402
import vectors as v  # noqa: E402


def spec(times, payoffs, payment_times=None):
    return {"type": "MultiStepCoterminalSwaptions", "rateTimes": times, "paymentTimes": payment_times or times[:-1],
            "payoffs": payoffs}


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(ms.R)
        for label, times in ms.GRIDS:
            n = len(times) - 1
            mid = sum(mf.forwards_of(times)) / n
            out += ms.steps_and_engine(H, st, rnd, f"{label}: calls at the forwards' mean",
                                       spec(times, [[1, mid]] * n), times)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: puts at the forwards' mean",
                                       spec(times, [[-1, mid]] * n), times)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: calls, strikes rising",
                                       spec(times, [[1, 0.025 + 0.003 * i] for i in range(n)]), times, engine=False)
            pays = [t + 0.1 for t in times[:-1]]
            out += ms.steps_and_engine(H, st, rnd, f"{label}: calls paid off the rate times",
                                       spec(times, [[1, mid]] * n, pays), times, engine=False)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/multistep/multistepcoterminalswaptions.hpp", cases)
