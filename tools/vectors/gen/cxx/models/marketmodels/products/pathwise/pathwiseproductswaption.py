# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's pathwise coterminal swaptions (ql/models/marketmodels/products/pathwise/
pathwiseproductswaption.hpp, .cpp): MarketModelPathwiseCoterminalSwaptionsDeflated and
MarketModelPathwiseCoterminalSwaptionsNumericalDeflated, which the wheel doesn't expose, each output computed by the C++
harness: the swaptions stepped on LMM curve states the harness sets, read per step with their amounts' derivatives
(analytic, and by the numerical form's bumps), with the product's counts and times (pathwiseproductcaplet.py's
helpers).

Rate grids: six semi-annual rates and five irregular ones; strikes at the forwards' mean, 1% either side and rising
along the curve; the numerical form with bumps of 1e-6 and 1e-4.

Left out (QuantLib and nanoql refuse them): strikes not one a rate.

The harness unit is tools/cxx/src/models/marketmodels/products/products.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/PathwiseProductSwaptionCxxVectorTests.cs."""
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

ANALYTIC = "MarketModelPathwiseCoterminalSwaptionsDeflated"
NUMERICAL = "MarketModelPathwiseCoterminalSwaptionsNumericalDeflated"


def spec(kind, times, strikes, bump=None):
    s = {"type": kind, "rateTimes": times, "strikes": strikes}
    if bump is not None:
        s["bumpSize"] = bump
    return s


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(pc.R)
        for label, times in pc.GRIDS:
            n = len(times) - 1
            mid = sum(mf.forwards_of(times)) / n
            for strikes, what in (([mid] * n, "at the forwards' mean"), ([mid + 0.01] * n, "1% above"),
                                  ([mid - 0.01] * n, "1% below"),
                                  ([0.025 + 0.003 * i for i in range(n)], "strikes rising")):
                out += pc.stepped(H, st, rnd, f"{label}: analytic, {what}", spec(ANALYTIC, times, strikes), times)
            out += pc.stepped(H, st, rnd, f"{label}: numerical, bumps of 1e-6",
                              spec(NUMERICAL, times, [mid] * n, 1e-6), times)
            out += pc.stepped(H, st, rnd, f"{label}: numerical, bumps of 1e-4, 1% below",
                              spec(NUMERICAL, times, [mid - 0.01] * n, 1e-4), times)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/pathwise/pathwiseproductswaption.hpp", cases)
