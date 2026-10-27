# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MultiStepPeriodCapletSwaptions (ql/models/marketmodels/products/multistep/
multistepperiodcapletswaptions.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness:
the period caplets and swaptions stepped on LMM curve states the harness sets, read per step, with the product's counts
and times; and their values through the accounting engine over a few paths (multistepswap.py's helpers).

Rate grids: six semi-annual rates and five irregular ones; periods of 2 and 3 rates, with and without an offset; calls
and puts at the forwards' mean; caplets paid at their periods' ends, swaptions at their starts.

Left out (QuantLib and nanoql refuse them): an offset not below the period, no period after the offset, payment
times or payoffs not one a period.

The harness units are tools/cxx/src/models/marketmodels/products/products.cpp and
tools/cxx/src/models/marketmodels/accountingengine.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MultiStepPeriodCapletSwaptionsCxxVectorTests.cs."""
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


def spec(times, period, offset, option_type, strike):
    big = (len(times) - 1 - offset) // period
    return {"type": "MultiStepPeriodCapletSwaptions", "rateTimes": times,
            "forwardOptionPaymentTimes": [times[offset + (k + 1) * period] for k in range(big)],
            "swaptionPaymentTimes": [times[offset + k * period] for k in range(big)],
            "forwardPayOffs": [[option_type, strike]] * big, "swapPayOffs": [[option_type, strike]] * big,
            "period": period, "offset": offset}


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(ms.R)
        for label, times in ms.GRIDS:
            mid = sum(mf.forwards_of(times)) / (len(times) - 1)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: calls on periods of 2", spec(times, 2, 0, 1, mid), times)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: puts on periods of 2 after one rate",
                                       spec(times, 2, 1, -1, mid), times)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: calls on periods of 3", spec(times, 3, 0, 1, mid), times,
                                       engine=False)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/multistep/multistepperiodcapletswaptions.hpp", cases)
