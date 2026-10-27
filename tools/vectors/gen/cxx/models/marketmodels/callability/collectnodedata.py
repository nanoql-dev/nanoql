# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's collectNodeData (ql/models/marketmodels/callability/collectnodedata.hpp, .cpp), which the
wheel doesn't expose, each output computed by the C++ harness: the node data a few paths of the predictor-corrector
evolver give, each exercise's exercise value, cash flows to the next exercise, the provider's values, the control's
value and validity, row 0 the cash flows before the first exercise.

Rate grids: six semi-annual rates and five irregular ones; a receiver swap at the forwards' mean, exercised at every
rate time but the last, into nothing and into a Bermudan swaption's exercise value, its data a swap basis system, a
swap-forward basis system or a triggered swap exercise's variable; a payer swap exercised at every other rate time.

This file holds the shared helpers of the callability vectors, which the other callability generators import.

Left out (nanoql refuses them, QuantLib reading past them or dropping an exercise; tests/NanoQL.Tests/Conventions/
MarketModelCallabilityTests.cs): an evolver whose steps aren't the product's, a provider, rebate or control off the
product's times, a provider whose exercise times aren't the rebate's, and a control whose evolution misses one.

The harness unit is tools/cxx/src/models/marketmodels/callability/callability.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/CollectNodeDataCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "products", "multistep"))

import cxx  # noqa: E402
import multistepforwards as mf  # noqa: E402
import multistepswap as ms  # noqa: E402
import vectors as v  # noqa: E402

R = 46322
PATHS = 16
GRIDS = mf.GRIDS


def mid(times):
    f0 = mf.forwards_of(times)
    return sum(f0) / len(f0)


def swap(times, payer, rate=None):
    return ms.swap_spec("MultiStepSwap", times, mid(times) if rate is None else rate, payer=payer)


def nothing(times, flags=None):
    return {"type": "NothingExerciseValue", "rateTimes": times, "isExerciseTime": flags or []}


def bermudan(times, option_type, strike):
    return {"type": "BermudanSwaptionExerciseValue", "rateTimes": times,
            "payoffs": [[option_type, strike]] * (len(times) - 1)}


def basis(kind, times, exercise_times):
    return {"type": kind, "rateTimes": times, "exerciseTimes": exercise_times}


def triggered(times, exercise_times, strikes):
    return {"type": "TriggeredSwapExercise", "rateTimes": times, "exerciseTimes": exercise_times, "strikes": strikes}


def trigger(times, triggers, exercise_times):
    return {"type": "SwapRateTrigger", "rateTimes": times, "swapTriggers": triggers, "exerciseTimes": exercise_times}


def evolver(times, evolution_times, generator, initial_step=0):
    """The predictor-corrector evolver on FlatVol at 20%, in the terminal measure."""
    n = len(times) - 1
    return ["LogNormalFwdRatePc", ms.model(times, evolution_times), generator, [n] * len(evolution_times),
            initial_step]


def collect_args(times, product, provider, rebate, control, paths=PATHS, generator=None):
    return [evolver(times, times[:-1], generator or {"type": "MT", "seed": 42}), product, provider, rebate, control,
            paths]


def node_outputs(rows):
    return {"exerciseValues": [[v.enc(n[0]) for n in r] for r in rows],
            "cumulatedCashFlows": [[v.enc(n[1]) for n in r] for r in rows],
            "values": [[v.enc(n[2]) for n in r] for r in rows],
            "controlValues": [[v.enc(n[3]) for n in r] for r in rows],
            "isValid": [[n[4] for n in r] for r in rows]}


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in GRIDS:
            exercises = times[:-1]
            k = mid(times)
            for blabel, provider in (("a swap basis system", basis("SwapBasisSystem", times, exercises)),
                                     ("a swap-forward basis system", basis("SwapForwardBasisSystem", times, exercises)),
                                     ("a triggered swap exercise", triggered(times, exercises, [k] * len(exercises)))):
                for rlabel, rebate in (("into nothing", nothing(times)), ("into a Bermudan payer", bermudan(times, 1, k))):
                    args = collect_args(times, swap(times, False), provider, rebate, nothing(times))
                    r = H.call("CollectNodeData.collect", *args)
                    out.append({"name": f"{label}: a receiver swap {rlabel}, {blabel}",
                                "inputs": {"settings": st, "evolver": args[0], "product": args[1], "provider": args[2],
                                           "rebate": args[3], "control": args[4], "paths": args[5]},
                                "expected": node_outputs(r)})
            every_other = [t for i, t in enumerate(exercises) if i % 2 == 0]
            flags = [i % 2 == 0 for i in range(len(exercises))]
            args = collect_args(times, swap(times, True), basis("SwapBasisSystem", times, every_other),
                                nothing(times, flags), nothing(times), generator={"type": "Sobol", "ordering": 2,
                                                                                 "seed": 1234})
            r = H.call("CollectNodeData.collect", *args)
            out.append({"name": f"{label}: a payer swap exercised at every other rate time, Sobol",
                        "inputs": {"settings": st, "evolver": args[0], "product": args[1], "provider": args[2],
                                   "rebate": args[3], "control": args[4], "paths": args[5]},
                        "expected": node_outputs(r)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/callability/collectnodedata.hpp", cases)
