# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's OneStepOptionlets (ql/models/marketmodels/products/onestep/onestepoptionlets.hpp, .cpp),
which the wheel doesn't expose, each output computed by the C++ harness: the optionlets stepped once on an LMM curve
state the harness sets, with the product's counts and times; and their values through the accounting engine over a
few paths of its one step (multistep/multistepswap.py's helpers).

Rate grids: six semi-annual rates and five irregular ones; calls and puts at the forwards and away from them;
accruals off the rate taus and payment times off the rate times.

Left out (nanoql refuses them, QuantLib reading past its inputs; tests/NanoQL.Tests/Conventions/
MarketModelProductTests.cs): accruals, payment times or payoffs not one a rate.

The harness units are tools/cxx/src/models/marketmodels/products/products.cpp and
tools/cxx/src/models/marketmodels/accountingengine.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/OneStepOptionletsCxxVectorTests.cs."""
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


def spec(times, payoffs, accruals=None, payment_times=None):
    return {"type": "OneStepOptionlets", "rateTimes": times, "accruals": accruals or mf.accruals_of(times),
            "paymentTimes": payment_times or times[1:], "payoffs": payoffs}


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(ms.R)
        for label, times in ms.GRIDS:
            f0 = mf.forwards_of(times)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: calls at the forwards", spec(times, [[1, f] for f in f0]),
                                       times)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: puts at the forwards", spec(times, [[-1, f] for f in f0]),
                                       times)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: calls 50 bp out, puts 50 bp in",
                                       spec(times, [[1 if i % 2 == 0 else -1, f + 0.005] for i, f in enumerate(f0)]),
                                       times, engine=False)
            accruals = [a * 365.0 / 360.0 for a in mf.accruals_of(times)]
            pays = [t - 0.1 for t in times[1:]]
            out += ms.steps_and_engine(H, st, rnd, f"{label}: own accruals and payment times",
                                       spec(times, [[1, f] for f in f0], accruals, pays), times, engine=False)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/onestep/onestepoptionlets.hpp", cases)
