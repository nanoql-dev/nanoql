# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MultiStepRatchet (ql/models/marketmodels/products/multistep/multistepratchet.hpp, .cpp),
which the wheel doesn't expose, each output computed by the C++ harness: the ratchet stepped on LMM curve states the
harness sets, read per step, with the product's counts and times; and its values through the accounting engine over a
few paths (multistepswap.py's helpers).

Rate grids: six semi-annual rates and five irregular ones; payer and receiver; a floor that ratchets on the rates
(gearings 1, spreads 0), one geared and spread, and an initial floor above every rate; accruals off the rate taus and
payment times off the rate times.

Left out (nanoql refuses them, QuantLib reading past its inputs; tests/NanoQL.Tests/Conventions/
MarketModelProductTests.cs): accruals or payment times not one a rate.

The harness units are tools/cxx/src/models/marketmodels/products/products.cpp and
tools/cxx/src/models/marketmodels/accountingengine.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MultiStepRatchetCxxVectorTests.cs."""
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


def spec(times, floor_gearing, fixing_gearing, floor_spread, fixing_spread, initial, payer, accruals=None,
         payment_times=None):
    return {"type": "MultiStepRatchet", "rateTimes": times, "accruals": accruals or mf.accruals_of(times),
            "paymentTimes": payment_times or times[1:], "gearingOfFloor": floor_gearing,
            "gearingOfFixing": fixing_gearing, "spreadOfFloor": floor_spread, "spreadOfFixing": fixing_spread,
            "initialFloor": initial, "payer": payer}


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(ms.R)
        for label, times in ms.GRIDS:
            for payer in (True, False):
                side = "payer" if payer else "receiver"
                out += ms.steps_and_engine(H, st, rnd, f"{label}: {side}, a floor ratcheting on the rates",
                                           spec(times, 1.0, 1.0, 0.0, 0.0, 0.03, payer), times)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: geared and spread",
                                       spec(times, 0.9, 1.1, 0.001, -0.002, 0.03, True), times, engine=False)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: an initial floor above the rates",
                                       spec(times, 1.0, 1.0, 0.0, 0.0, 0.08, True), times, engine=False)
            accruals = [a * 365.0 / 360.0 for a in mf.accruals_of(times)]
            pays = [t - 0.1 for t in times[1:]]
            out += ms.steps_and_engine(H, st, rnd, f"{label}: own accruals and payment times",
                                       spec(times, 1.0, 1.0, 0.0, 0.0, 0.03, False, accruals, pays), times,
                                       engine=False)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/multistep/multistepratchet.hpp", cases)
