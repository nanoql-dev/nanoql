# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MarketModelPathwiseSwap (ql/models/marketmodels/products/pathwise/pathwiseproductswap.hpp,
.cpp), which the wheel doesn't expose, each output computed by the C++ harness: the pathwise swap stepped on LMM curve
states the harness sets, read per step with its amounts' derivatives, with the product's counts and times
(pathwiseproductcaplet.py's helpers).

Rate grids: six semi-annual rates and five irregular ones; payer (multiplier 1) and receiver (-1); strikes at the
forwards, one strike repeated for every rate, strikes rising along the curve; accruals off the rate taus.

Left out: one strike for every rate, which QuantLib refuses where its message offers it and nanoql takes (the S-mmp
plan's MMPR4 (xi), tests/NanoQL.Tests/Divergences/MarketModelPathwiseSwapStrikeTests.cs); and, nanoql refusing them,
QuantLib reading past its inputs, accruals or strikes not one a rate (tests/NanoQL.Tests/Conventions/
MarketModelProductTests.cs).

The harness unit is tools/cxx/src/models/marketmodels/products/products.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MarketModelPathwiseSwapCxxVectorTests.cs."""
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
import multistepforwards as mf  # noqa: E402
import pathwiseproductcaplet as pc  # noqa: E402
import vectors as v  # noqa: E402


def spec(times, strikes, multiplier, accruals=None):
    return {"type": "MarketModelPathwiseSwap", "rateTimes": times, "accruals": accruals or mf.accruals_of(times),
            "strikes": strikes, "multiplier": multiplier}


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(pc.R)
        for label, times in pc.GRIDS:
            n = len(times) - 1
            f0 = mf.forwards_of(times)
            mid = sum(f0) / n
            out += pc.stepped(H, st, rnd, f"{label}: payer at the forwards", spec(times, f0, 1.0), times)
            out += pc.stepped(H, st, rnd, f"{label}: receiver at the forwards", spec(times, f0, -1.0), times)
            out += pc.stepped(H, st, rnd, f"{label}: payer, one strike repeated", spec(times, [mid] * n, 1.0), times)
            accruals = [a * 365.0 / 360.0 for a in mf.accruals_of(times)]
            out += pc.stepped(H, st, rnd, f"{label}: receiver, strikes rising, own accruals",
                              spec(times, [0.025 + 0.003 * i for i in range(n)], -1.0, accruals), times)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/pathwise/pathwiseproductswap.hpp", cases)
