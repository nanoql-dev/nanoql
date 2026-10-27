# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CallSpecifiedPathwiseMultiProduct (ql/models/marketmodels/products/pathwise/
pathwiseproductcallspecified.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness: the
callable pathwise product stepped on LMM curve states the harness sets, read per step with its amounts' derivatives,
with its counts and times (pathwiseproductcaplet.py's helpers).

Rate grids: six semi-annual rates and five irregular ones; a pathwise receiver swap callable at every rate time but the
last on a swap-rate trigger, into QuantLib's default rebate (a pathwise cash rebate of 0) and into a pathwise cash
rebate of 1%; triggers at the forwards' mean and rising.

Left out: exercise or strategy times off the underlying's, where QuantLib writes no counts at an idle step (the S-mmp
plan's MMPR4 (xvi), tests/NanoQL.Tests/Divergences/CallSpecifiedIdleStepTests.cs).

The harness unit is tools/cxx/src/models/marketmodels/products/products.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/CallSpecifiedPathwiseMultiProductCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "callability"))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "multistep"))

import collectnodedata as cn  # noqa: E402
import cxx  # noqa: E402
import multistepforwards as mf  # noqa: E402
import pathwiseproductcaplet as pc  # noqa: E402
import vectors as v  # noqa: E402


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(pc.R)
        for label, times in pc.GRIDS:
            n = len(times) - 1
            k = cn.mid(times)
            exercises = times[:-1]
            underlying = {"type": "MarketModelPathwiseSwap", "rateTimes": times, "accruals": mf.accruals_of(times),
                          "strikes": [k] * n, "multiplier": -1.0}
            cash = {"type": "MarketModelPathwiseCashRebate", "rateTimes": times, "evolutionTimes": exercises,
                    "paymentTimes": exercises, "amounts": [[0.01] * n], "numberOfProducts": 1}
            for tlabel, triggers in (("at the forwards' mean", [k] * n),
                                     ("rising", [k - 0.004 + 0.002 * i for i in range(n)])):
                for rlabel, rebate in (("into QuantLib's default", None), ("into a 1% cash rebate", cash)):
                    spec = {"type": "CallSpecifiedPathwiseMultiProduct", "underlying": underlying,
                            "strategy": cn.trigger(times, triggers, exercises), "rebate": rebate}
                    out += pc.stepped(H, st, rnd, f"{label}: triggers {tlabel}, {rlabel}", spec, times)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/pathwise/pathwiseproductcallspecified.hpp", cases)
