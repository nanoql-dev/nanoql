# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's TriggeredSwapExercise (ql/models/marketmodels/callability/triggeredswapexercise.hpp, .cpp),
which the wheel doesn't expose, each output computed by the C++ harness: the parametric exercise stepped on LMM curve
states the harness sets, its variable at each exercise and its decision there on its guess, with its counts, its
variables' and parameters' counts and each exercise's guess (swapbasissystem.py's helper).

Rate grids: six semi-annual rates and five irregular ones; exercises at every rate time but the last, at every other,
and off the rate times; strikes at the forwards' mean and rising; states drawn around the grid's forwards.

The harness unit is tools/cxx/src/models/marketmodels/callability/callability.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/TriggeredSwapExerciseCxxVectorTests.cs."""
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


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(cn.R)
        for label, times in cn.GRIDS:
            k = cn.mid(times)
            for what, exercises in sb.exercise_sets(times):
                for slabel, strikes in (("at the forwards' mean", [k] * len(exercises)),
                                        ("rising", [k - 0.004 + 0.002 * i for i in range(len(exercises))])):
                    out.append(sb.provider_case(H, st, f"{label}: exercised at {what}, strikes {slabel}",
                                                cn.triggered(times, exercises, strikes),
                                                mf.states(rnd, times, len(exercises))))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/callability/triggeredswapexercise.hpp", cases)
