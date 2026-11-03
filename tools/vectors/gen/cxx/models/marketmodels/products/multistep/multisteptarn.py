# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MultiStepTarn (ql/models/marketmodels/products/multistep/multisteptarn.hpp, .cpp), which
the wheel doesn't expose, each output computed by the C++ harness: the TARN stepped on LMM curve states the harness
sets, read per step, with the product's counts and times; and its values through the accounting engine over a few
paths (multistepswap.py's helpers).

Rate grids: six semi-annual rates and five irregular ones; a total coupon the path reaches early, one it never
reaches (paid up at the last rate), strikes and multipliers per rate, spreads; accruals off the rate taus.

Left out: floating payment times other than the fixed ones, where QuantLib discounts the floating coupons at the fixed
times (tests/NanoQL.Tests/Divergences/MultiStepTarnPaymentTimesTests.cs); and, nanoql refusing them, QuantLib reading
past its inputs (tests/NanoQL.Tests/Conventions/MarketModelProductTests.cs), inputs not one a rate.

The harness units are tools/cxx/src/models/marketmodels/products/products.cpp and
tools/cxx/src/models/marketmodels/accountingengine.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MultiStepTarnCxxVectorTests.cs."""
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


def spec(times, total, strikes, multipliers, spreads, accruals=None):
    a = accruals or mf.accruals_of(times)
    return {"type": "MultiStepTarn", "rateTimes": times, "accruals": a, "accrualsFloating": mf.accruals_of(times),
            "paymentTimes": times[1:], "paymentTimesFloating": times[1:], "totalCoupon": total, "strikes": strikes,
            "multipliers": multipliers, "floatingSpreads": spreads}


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(ms.R)
        for label, times in ms.GRIDS:
            n = len(times) - 1
            out += ms.steps_and_engine(H, st, rnd, f"{label}: a total coupon reached early",
                                       spec(times, 0.04, [0.08] * n, [2.0] * n, [0.0] * n), times)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: a total coupon never reached",
                                       spec(times, 0.5, [0.08] * n, [2.0] * n, [0.0] * n), times)
            out += ms.steps_and_engine(H, st, rnd, f"{label}: strikes and multipliers per rate, spreads",
                                       spec(times, 0.06, [0.07 + 0.002 * i for i in range(n)],
                                            [1.5 + 0.1 * i for i in range(n)], [0.001 * i for i in range(n)]),
                                       times, engine=False)
            accruals = [x * 365.0 / 360.0 for x in mf.accruals_of(times)]
            out += ms.steps_and_engine(H, st, rnd, f"{label}: own fixed accruals",
                                       spec(times, 0.05, [0.08] * n, [2.0] * n, [0.0] * n, accruals), times,
                                       engine=False)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/multistep/multisteptarn.hpp", cases)
