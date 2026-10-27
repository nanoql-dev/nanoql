# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BermudanSwaptionExerciseValue (ql/models/marketmodels/callability/
bermudanswaptionexercisevalue.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness: the
exercise value stepped on LMM curve states the harness sets, its cash flow at each step, with its counts and times.

Rate grids: six semi-annual rates and five irregular ones; payer and receiver payoffs at the forwards' mean and 1%
either side; states drawn around the grid's forwards.

Left out (nanoql refuses them, QuantLib reading past them): fewer payoffs than exercises.

The harness unit is tools/cxx/src/models/marketmodels/callability/callability.cpp (collectnodedata.py's helpers). Read
back by tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/BermudanSwaptionExerciseValueCxxVectorTests.cs."""
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
import vectors as v  # noqa: E402


def exercise_case(H, st, name, spec, forwards):
    r = H.call("MarketModelExerciseValue.steps", spec, forwards)
    return {"name": name, "inputs": {"settings": st, "exerciseValue": spec, "stepForwards": forwards},
            "expected": {"numberOfExercises": r["numberOfExercises"],
                         "possibleCashFlowTimes": r["possibleCashFlowTimes"], "evolutionTimes": r["evolutionTimes"],
                         "isExerciseTime": r["isExerciseTime"], "timeIndices": [s[0] for s in r["steps"]],
                         "amounts": [v.enc(s[1]) for s in r["steps"]]}}


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(cn.R)
        for label, times in cn.GRIDS:
            k = cn.mid(times)
            for option_type, side in ((1, "payer"), (-1, "receiver")):
                for strike, what in ((k, "at the forwards' mean"), (k + 0.01, "1% above"), (k - 0.01, "1% below")):
                    out.append(exercise_case(H, st, f"{label}: {side} {what}", cn.bermudan(times, option_type, strike),
                                             mf.states(rnd, times, len(times) - 1)))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/callability/bermudanswaptionexercisevalue.hpp", cases)
