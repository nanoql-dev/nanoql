# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's NothingExerciseValue (ql/models/marketmodels/callability/nothingexercisevalue.hpp, .cpp),
which the wheel doesn't expose, each output computed by the C++ harness: the exercise value stepped on LMM curve
states the harness sets, its cash flow at each step, with its counts and times.

Rate grids: six semi-annual rates and five irregular ones; every rate time an exercise time (the default flags), every
other one, and only the last; states drawn around the grid's forwards.

The harness unit is tools/cxx/src/models/marketmodels/callability/callability.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/NothingExerciseValueCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "products", "multistep"))

import bermudanswaptionexercisevalue as bx  # noqa: E402
import collectnodedata as cn  # noqa: E402
import cxx  # noqa: E402
import multistepforwards as mf  # noqa: E402
import vectors as v  # noqa: E402


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(cn.R)
        for label, times in cn.GRIDS:
            n = len(times) - 1
            for what, flags in (("every rate time", None), ("every other rate time", [i % 2 == 0 for i in range(n)]),
                                ("the last rate time", [i == n - 1 for i in range(n)])):
                out.append(bx.exercise_case(H, st, f"{label}: exercised at {what}", cn.nothing(times, flags),
                                            mf.states(rnd, times, n)))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/callability/nothingexercisevalue.hpp", cases)
