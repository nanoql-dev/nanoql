# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MarketModelPathwiseInverseFloater (ql/models/marketmodels/products/pathwise/
pathwiseproductinversefloater.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness: the
pathwise inverse floater stepped on LMM curve states the harness sets, read per step with its amounts' derivatives,
with the product's counts and times (pathwiseproductcaplet.py's helpers).

The multi-step floater's cases (multistep/multistepinversefloater.py): payer and receiver on QuantLib's
testInverseFloater terms, coupons floored at zero, a spread with accruals off the rate taus and payment times off the
rate times.

Left out (nanoql refuses them, QuantLib reading past its inputs; tests/NanoQL.Tests/Conventions/
MarketModelProductTests.cs): accruals, strikes, multipliers, spreads or payment times not one a rate.

The harness unit is tools/cxx/src/models/marketmodels/products/products.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MarketModelPathwiseInverseFloaterCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "multistep"))

import cxx  # noqa: E402
import multistepinversefloater as mif  # noqa: E402
import pathwiseproductcaplet as pc  # noqa: E402
import vectors as v  # noqa: E402


def cases():
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(pc.R)
        return mif.floater_cases(H, st, rnd, "MarketModelPathwiseInverseFloater",
                                 lambda H, st, rnd, label, s, times, e: pc.stepped(H, st, rnd, label, s, times))


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/pathwise/pathwiseproductinversefloater.hpp", cases)
