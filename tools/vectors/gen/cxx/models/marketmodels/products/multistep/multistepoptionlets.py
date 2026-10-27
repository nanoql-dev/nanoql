# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MultiStepOptionlets (ql/models/marketmodels/products/multistep/multistepoptionlets.hpp,
.cpp), which the wheel doesn't expose, each output computed by the C++ harness: the optionlets stepped on set LMM
curve states, read per step as multistepforwards.py reads the forwards.

Calls and puts at, above and below the forwards on multistepforwards.py's grids, fewer payoffs than rates, and accruals
and payment times off the grid; each payoff a PlainVanillaPayoff.

Left out (nanoql refuses them, QuantLib reading past its inputs; tests/NanoQL.Tests/Conventions/
MarketModelProductTests.cs): no payoff, more payoffs than rates, fewer accruals or payment times than payoffs.

The harness unit is tools/cxx/src/models/marketmodels/products/products.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MultiStepOptionletsCxxVectorTests.cs."""
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
            f0 = mf.forwards_of(times)
            fs = mf.states(rnd, times, n)
            for kind, name in ((1, "calls"), (-1, "puts")):
                for shift, where in ((0.0, "at the money"), (0.004, "40 bp up"), (-0.004, "40 bp down")):
                    payoffs = [[kind, f + shift] for f in f0]
                    out.append(mf.product_case(H, st, f"{label}: {name} {where}", mf.optionlets_spec(times, payoffs), fs))
            out.append(mf.product_case(H, st, f"{label}: three calls", mf.optionlets_spec(times, [[1, f] for f in f0[:3]]), fs))
            accruals = [a * 365.0 / 360.0 for a in mf.accruals_of(times)]
            pays = [t - 0.05 for t in times[1:]]
            out.append(mf.product_case(H, st, f"{label}: own accruals and payment times",
                                       mf.optionlets_spec(times, [[1, f] for f in f0], accruals, pays), fs))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/multistep/multistepoptionlets.hpp", cases)
