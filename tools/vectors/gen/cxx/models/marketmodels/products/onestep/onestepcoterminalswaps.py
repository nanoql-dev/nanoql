# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's OneStepCoterminalSwaps (ql/models/marketmodels/products/onestep/onestepcoterminalswaps.hpp,
.cpp), which the wheel doesn't expose, each output computed by the C++ harness: the coterminal swaps stepped once on an
LMM curve state the harness sets, with the product's counts and times; and their values through the accounting engine
over a few paths of its one step (multistep/multistepswap.py's helpers).

Rate grids: six semi-annual rates and five irregular ones; fixed rates at the forwards' mean and 1% either side;
fixed accruals off the rate taus and payment times off the rate times.

Left out: the values through the engine of payments inside the rates' periods, which QuantLib's discounter
brackets by the period after (the S-mmp plan's MMPR4 (ix), tests/NanoQL.Tests/Divergences/
MarketModelDiscounterBracketTests.cs); and, nanoql refusing them, QuantLib reading past its inputs
(tests/NanoQL.Tests/Conventions/MarketModelProductTests.cs), accruals or payment times not one a rate.

The harness units are tools/cxx/src/models/marketmodels/products/products.cpp and
tools/cxx/src/models/marketmodels/accountingengine.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/OneStepCoterminalSwapsCxxVectorTests.cs."""
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

KIND = "OneStepCoterminalSwaps"


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(ms.R)
        for label, times in ms.GRIDS:
            mid = sum(mf.forwards_of(times)) / (len(times) - 1)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: at the forwards' mean", ms.swap_spec(KIND, times, mid),
                                       times)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: 1% above", ms.swap_spec(KIND, times, mid + 0.01), times,
                                       engine=False)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: 1% below", ms.swap_spec(KIND, times, mid - 0.01), times,
                                       engine=False)
            fixed = [a * 365.0 / 360.0 for a in mf.accruals_of(times)]
            pays = [t - 0.1 for t in times[1:]]
            out += ms.steps_and_engine(H, st, rnd, f"{label}: own fixed accruals and payment times",
                                       ms.swap_spec(KIND, times, mid, fixed_accruals=fixed, payment_times=pays), times,
                                       engine=False)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/onestep/onestepcoterminalswaps.hpp", cases)
