# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LongstaffSchwartzExerciseStrategy (ql/models/marketmodels/callability/lsstrategy.hpp,
.cpp), which the wheel doesn't expose, each output computed by the C++ harness: the strategy stepped on LMM curve
states the harness sets, its decision at each exercise time, with its exercise and relevant times
(swapratetrigger.py's helper); its coefficients the harness's regression on collected node data.

Rate grids: six semi-annual rates and five irregular ones; a receiver swap's regression on a swap basis system into
nothing and into a Bermudan payer, in the terminal measure, and the same coefficients in the money-market measure;
states drawn around the grid's forwards.

The harness unit is tools/cxx/src/models/marketmodels/callability/callability.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/LongstaffSchwartzExerciseStrategyCxxVectorTests.cs."""
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
import swapratetrigger as sr  # noqa: E402
import vectors as v  # noqa: E402


def ls_spec(times, coefficients, numeraires, rebate):
    exercises = times[:-1]
    return {"type": "LongstaffSchwartzExerciseStrategy", "basisSystem": cn.basis("SwapBasisSystem", times, exercises),
            "basisCoefficients": coefficients, "rateTimes": times, "evolutionTimes": exercises,
            "numeraires": numeraires, "exercise": rebate, "control": cn.nothing(times)}


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(cn.R)
        for label, times in cn.GRIDS:
            n = len(times) - 1
            exercises = times[:-1]
            for rlabel, rebate in (("into nothing", cn.nothing(times)), ("into a Bermudan payer",
                                                                         cn.bermudan(times, 1, cn.mid(times)))):
                args = cn.collect_args(times, cn.swap(times, False), cn.basis("SwapBasisSystem", times, exercises),
                                       rebate, cn.nothing(times), paths=64)
                coefficients = H.call("GenericLongstaffSchwartzRegression.run", *args)["coefficients"]
                for mlabel, numeraires in (("terminal", [n] * n), ("money market", list(range(1, n + 1)))):
                    out.append(sr.strategy_case(H, st, f"{label}: {rlabel}, {mlabel}",
                                                ls_spec(times, coefficients, numeraires, rebate), times,
                                                mf.states(rnd, times, n)))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/callability/lsstrategy.hpp", cases)
