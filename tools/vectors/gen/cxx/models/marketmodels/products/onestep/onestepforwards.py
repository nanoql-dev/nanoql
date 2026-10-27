# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's OneStepForwards (ql/models/marketmodels/products/onestep/onestepforwards.hpp, .cpp), which
the wheel doesn't expose, each output computed by the C++ harness: the forwards stepped once on an LMM curve state the
harness sets, every cash flow paid at that step, with the product's counts and times; and their values through the
accounting engine over a few paths of its one step (multistep/multistepswap.py's helpers).

Rate grids: six semi-annual rates and five irregular ones; strikes at, above and below the forwards; accruals off the
rate taus and payment times off the rate times.

Left out (nanoql refuses them, QuantLib reading past its inputs; tests/NanoQL.Tests/Conventions/
MarketModelProductTests.cs): accruals, payment times or strikes not one a rate.

The harness units are tools/cxx/src/models/marketmodels/products/products.cpp and
tools/cxx/src/models/marketmodels/accountingengine.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/OneStepForwardsCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "multistep"))

import cxx  # noqa: E402
import multistepforwards as mf  # noqa: E402
import multistepswap as ms  # noqa: E402
import vectors as v  # noqa: E402


def spec(times, strikes, accruals=None, payment_times=None):
    return {"type": "OneStepForwards", "rateTimes": times, "accruals": accruals or mf.accruals_of(times),
            "paymentTimes": payment_times or times[1:], "strikes": strikes}


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(ms.R)
        for label, times in ms.GRIDS:
            f0 = mf.forwards_of(times)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: at the forwards", spec(times, f0), times)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: 1% above", spec(times, [f + 0.01 for f in f0]), times)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: 1% below", spec(times, [f - 0.01 for f in f0]), times,
                                       engine=False)
            accruals = [a * 365.0 / 360.0 for a in mf.accruals_of(times)]
            pays = [t - 0.1 for t in times[1:]]
            out += ms.steps_and_engine(H, st, rnd, f"{label}: own accruals and payment times",
                                       spec(times, f0, accruals, pays), times, engine=False)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/onestep/onestepforwards.hpp", cases)
