# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MultiStepSwaption (ql/models/marketmodels/products/multistep/multistepswaption.hpp, .cpp),
which the wheel doesn't expose, each output computed by the C++ harness: the swaption stepped on LMM curve states the
harness sets, read per step, with the product's counts and times; and its values through the accounting engine over a
few paths (multistepswap.py's helpers).

Rate grids: six semi-annual rates and five irregular ones; swaptions on the whole curve, starting at the second rate,
and ending before the last; calls and puts at the forwards' mean and away from it.

Left out (nanoql and QuantLib refuse them): a start index not before the end index, an end index past the rates.

The harness units are tools/cxx/src/models/marketmodels/products/products.cpp and
tools/cxx/src/models/marketmodels/accountingengine.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MultiStepSwaptionCxxVectorTests.cs."""
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


def spec(times, start, end, option_type, strike):
    return {"type": "MultiStepSwaption", "rateTimes": times, "startIndex": start, "endIndex": end,
            "payoff": [option_type, strike]}


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(ms.R)
        for label, times in ms.GRIDS:
            n = len(times) - 1
            mid = sum(mf.forwards_of(times)) / n
            out += ms.steps_and_engine(H, st, rnd, f"{label}: a call on the whole curve", spec(times, 0, n, 1, mid),
                                       times)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: a put from the second rate",
                                       spec(times, 1, n, -1, mid), times)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: a call ending before the last rate, 1% out",
                                       spec(times, 1, n - 1, 1, mid + 0.01), times, engine=False)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: a put on one period, 1% in",
                                       spec(times, 2, 3, -1, mid + 0.01), times, engine=False)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/multistep/multistepswaption.hpp", cases)
