# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SwapBasisSystem (ql/models/marketmodels/callability/swapbasissystem.hpp, .cpp), which the
wheel doesn't expose, each output computed by the C++ harness: the basis system stepped on LMM curve states the harness
sets, its functions' values at each exercise, with its counts and times.

Rate grids: six semi-annual rates and five irregular ones; exercises at every rate time but the last, at every other,
and off the rate times; states drawn around the grid's forwards.

This file holds the provider vectors' shared helper, which the swap-forward basis system's and the triggered swap
exercise's generators import.

Left out: two exercises in the last rate period, where QuantLib counts 3 functions for the first and its values give 2
(the S-mmp plan's MMPR4 (xv), tests/NanoQL.Tests/Divergences/BasisSystemFunctionCountTests.cs).

The harness unit is tools/cxx/src/models/marketmodels/callability/callability.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/SwapBasisSystemCxxVectorTests.cs."""
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


def provider_case(H, st, name, spec, forwards, counts_agree=True):
    """A provider stepped on the states; its counts left out where QuantLib's differ from its values' (MMPR4 (xv))."""
    r = H.call("MarketModelNodeDataProvider.steps", spec, forwards)
    expected = {"numberOfExercises": r["numberOfExercises"], "evolutionTimes": r["evolutionTimes"],
                "isExerciseTime": r["isExerciseTime"], "values": [v.enc(s) for s in r["steps"]]}
    if counts_agree:
        expected["numberOfData"] = r["numberOfData"]
    for key in ("numberOfVariables", "numberOfParameters", "guesses", "decisions"):
        if key in r:
            expected[key] = v.enc(r[key])
    return {"name": name, "inputs": {"settings": st, "provider": spec, "stepForwards": forwards}, "expected": expected}


def exercise_sets(times):
    exercises = times[:-1]
    return [("every rate time but the last", exercises),
            ("every other rate time", [t for i, t in enumerate(exercises) if i % 2 == 0]),
            ("off the rate times", [t + 0.1 for t in exercises[:-1]])]


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(cn.R)
        for label, times in cn.GRIDS:
            for what, exercises in exercise_sets(times):
                out.append(provider_case(H, st, f"{label}: exercised at {what}",
                                         cn.basis("SwapBasisSystem", times, exercises),
                                         mf.states(rnd, times, len(exercises))))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/callability/swapbasissystem.hpp", cases)
