# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ParametricExerciseAdapter (ql/models/marketmodels/callability/parametricexerciseadapter.hpp,
.cpp), which the wheel doesn't expose, each output computed by the C++ harness: the strategy stepped on LMM curve
states the harness sets, its decision at each exercise time, with its exercise and relevant times
(swapratetrigger.py's helper); its exercise a triggered swap exercise.

Rate grids: six semi-annual rates and five irregular ones; exercises at every rate time but the last and at every
other; parameters at the forwards' mean, rising, and the harness's optimisation's on collected node data; states drawn
around the grid's forwards.

The harness unit is tools/cxx/src/models/marketmodels/callability/callability.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/ParametricExerciseAdapterCxxVectorTests.cs."""
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

END_CRITERIA = [1000, 100, 1e-8, 1e-16, 1e-8]


def adapter_spec(times, exercises, strikes, parameters):
    return {"type": "ParametricExerciseAdapter", "exercise": cn.triggered(times, exercises, strikes),
            "parameters": parameters}


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(cn.R)
        for label, times in cn.GRIDS:
            k = cn.mid(times)
            exercises = times[:-1]
            every_other = [t for i, t in enumerate(exercises) if i % 2 == 0]
            for what, ex in (("every rate time but the last", exercises), ("every other rate time", every_other)):
                strikes = [k] * len(ex)
                for plabel, parameters in (("at the forwards' mean", [[k]] * len(ex)),
                                           ("rising", [[k - 0.004 + 0.002 * i] for i in range(len(ex))])):
                    out.append(sr.strategy_case(H, st, f"{label}: exercised at {what}, parameters {plabel}",
                                                adapter_spec(times, ex, strikes, parameters), times,
                                                mf.states(rnd, times, len(ex))))
            args = cn.collect_args(times, cn.swap(times, False), cn.triggered(times, exercises, [k] * len(exercises)),
                                   cn.nothing(times), cn.nothing(times), paths=64)
            parameters = H.call("GenericEarlyExerciseOptimization.run", *args, 0.01, END_CRITERIA)["parameters"]
            out.append(sr.strategy_case(H, st, f"{label}: the optimised parameters",
                                        adapter_spec(times, exercises, [k] * len(exercises), parameters), times,
                                        mf.states(rnd, times, len(exercises))))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/callability/parametricexerciseadapter.hpp", cases)
