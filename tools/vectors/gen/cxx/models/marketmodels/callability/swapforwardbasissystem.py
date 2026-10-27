# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SwapForwardBasisSystem (ql/models/marketmodels/callability/swapforwardbasissystem.hpp,
.cpp), which the wheel doesn't expose, each output computed by the C++ harness: the basis system stepped on LMM curve
states the harness sets, its functions' values at each exercise, with its counts and times (swapbasissystem.py's
helper).

Rate grids: six semi-annual rates and five irregular ones; exercises at every rate time but the last, at every other,
and off the rate times; states drawn around the grid's forwards.

Left out: the counts where an exercise before the last sits in the last two periods, which QuantLib counts 10 where
its values give 6 or 3 (the S-mmp plan's MMPR4 (xv), tests/NanoQL.Tests/Divergences/BasisSystemFunctionCountTests.cs);
the values stay, QuantLib's.

The harness unit is tools/cxx/src/models/marketmodels/callability/callability.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/SwapForwardBasisSystemCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "products", "multistep"))

import collectnodedata as cn  # noqa: E402
import cxx  # noqa: E402
import multistepforwards as mf  # noqa: E402
import swapbasissystem as sb  # noqa: E402
import vectors as v  # noqa: E402


def counts_agree(times, exercises):
    """Whether QuantLib's counts, the last exercise's alone adjusted, are each exercise's own."""
    size = len(times)
    index = []
    j = 0
    for t in exercises:
        while j < size and times[j] < t:
            j += 1
        index.append(j)
    own = [10 if r < size - 3 else 6 if r == size - 3 else 3 for r in index]
    quantlib = [10] * len(exercises)
    quantlib[-1] = own[-1]
    return own == quantlib


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(cn.R)
        for label, times in cn.GRIDS:
            for what, exercises in sb.exercise_sets(times):
                out.append(sb.provider_case(H, st, f"{label}: exercised at {what}",
                                            cn.basis("SwapForwardBasisSystem", times, exercises),
                                            mf.states(rnd, times, len(exercises)), counts_agree(times, exercises)))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/callability/swapforwardbasissystem.hpp", cases)
