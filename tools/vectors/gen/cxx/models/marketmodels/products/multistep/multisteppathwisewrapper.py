# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MultiProductPathwiseWrapper (ql/models/marketmodels/products/multistep/
multisteppathwisewrapper.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness: a
pathwise product wrapped as a multi-step product, stepped on LMM curve states the harness sets, read per step, with the
product's counts and times; and its values through the accounting engine over a few paths (multistepswap.py's
helpers), QuantLib's testInverseFloater route.

Rate grids: six semi-annual rates and five irregular ones; the wrapped pathwise caplets, inverse floater (payer and
receiver, QuantLib's test's terms), swap, cash rebate and coterminal swaptions, every one an undeflated product.

Left out: wrapped deflated products (the deflated caplets and caps), whose amounts QuantLib hands the engine deflated
for it to discount again (the S-mmp plan's MMPR4 (xvii), tests/NanoQL.Tests/Divergences/
MultiProductPathwiseWrapperDeflatedTests.cs).

The harness units are tools/cxx/src/models/marketmodels/products/products.cpp and
tools/cxx/src/models/marketmodels/accountingengine.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MultiProductPathwiseWrapperCxxVectorTests.cs."""
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
import multistepinversefloater as mif  # noqa: E402
import multistepswap as ms  # noqa: E402
import vectors as v  # noqa: E402


def wrapped(inner):
    return {"type": "MultiProductPathwiseWrapper", "inner": inner}


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(ms.R)
        for label, times in ms.GRIDS:
            n = len(times) - 1
            f0 = mf.forwards_of(times)
            mid = sum(f0) / n
            caplets = {"type": "MarketModelPathwiseMultiCaplet", "rateTimes": times, "accruals": mf.accruals_of(times),
                       "paymentTimes": times[1:], "strikes": f0}
            out += ms.steps_and_engine(H, st, rnd, f"{label}: caplets at the forwards", wrapped(caplets), times)
            for payer in (True, False):
                side = "payer" if payer else "receiver"
                floater = mif.spec("MarketModelPathwiseInverseFloater", times, 0.15, 2.0, 0.0, payer)
                out += ms.steps_and_engine(H, st, rnd, f"{label}: a {side} inverse floater, QuantLib's test's terms",
                                           wrapped(floater), times)
            swap = {"type": "MarketModelPathwiseSwap", "rateTimes": times, "accruals": mf.accruals_of(times),
                    "strikes": [mid] * n, "multiplier": 1.0}
            out += ms.steps_and_engine(H, st, rnd, f"{label}: a payer swap", wrapped(swap), times)
            rebate = {"type": "MarketModelPathwiseCashRebate", "rateTimes": times, "evolutionTimes": times[:-1],
                      "paymentTimes": times[1:], "amounts": [[0.01 * (j + 1) for j in range(n)]],
                      "numberOfProducts": 1}
            out += ms.steps_and_engine(H, st, rnd, f"{label}: a cash rebate", wrapped(rebate), times, engine=False)
            swaptions = {"type": "MarketModelPathwiseCoterminalSwaptionsDeflated", "rateTimes": times,
                         "strikes": [mid] * n}
            out += ms.steps_and_engine(H, st, rnd, f"{label}: coterminal swaptions", wrapped(swaptions), times)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/multistep/multisteppathwisewrapper.hpp", cases)
