# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's UpperBoundEngine (ql/models/marketmodels/callability/upperboundengine.hpp, .cpp), which the
wheel doesn't expose, each output computed by the C++ harness: the engine's values over a few outer paths of the
predictor-corrector evolver, each with a few inner paths of Mersenne-twister evolvers from each exercise step, read
through the statistics' samples, weight sum, mean and error estimate.

Rate grids: six semi-annual rates and five irregular ones; QuantLib's tested configuration, a receiver swap callable at
every rate time but the last with null rebates, on its naif trigger and on a Longstaff-Schwartz strategy the harness's
regression gives; a payer swap callable at every other rate time.

Left out: hedges with a rebate of value (a Bermudan swaption's exercise value), whose exercise values QuantLib reads
from an adapter done at its first exercise date, and hedges that end before the last exercise date (the S-mmp plan's
MMPR4 (vii), option (1) with (a), tests/NanoQL.Tests/Divergences/UpperBoundEngineTests.cs).

The harness unit is tools/cxx/src/models/marketmodels/callability/callability.cpp (collectnodedata.py's helpers). Read
back by tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/UpperBoundEngineCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "products", "multistep"))

import collectnodedata as cn  # noqa: E402
import cxx  # noqa: E402
import lsstrategy as ls  # noqa: E402
import multistepswap as ms  # noqa: E402
import vectors as v  # noqa: E402

OUTER = 8
INNER = 16


def engine_case(H, st, name, times, underlying, rebate, strategy, exercise_steps):
    n = len(times) - 1
    outer = cn.evolver(times, times[:-1], {"type": "Sobol", "ordering": 2, "seed": 42 + 142})
    inner = [cn.evolver(times, times[:-1], {"type": "MT", "seed": 42 + s}, s) for s in exercise_steps]
    initial = ms.discounts(times)[n]
    r = H.call("UpperBoundEngine.values", outer, inner, underlying, rebate, underlying, rebate, strategy, initial,
               OUTER, INNER)
    return {"name": name,
            "inputs": {"settings": st, "evolver": outer, "innerEvolvers": inner, "underlying": underlying,
                       "rebate": rebate, "hedge": underlying, "hedgeRebate": rebate, "strategy": strategy,
                       "initialNumeraireValue": initial, "outerPaths": OUTER, "innerPaths": INNER},
            "expected": {"samples": r["samples"], "weightSum": v.enc(r["weightSum"]), "mean": v.enc(r["mean"]),
                         "errorEstimate": v.enc(r["errorEstimate"])}}


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(cn.R)
        for label, times in cn.GRIDS:
            n = len(times) - 1
            k = cn.mid(times)
            exercises = times[:-1]
            receiver = cn.swap(times, False)
            null = cn.nothing(times)
            out.append(engine_case(H, st, f"{label}: QuantLib's callable receiver, its naif trigger", times, receiver,
                                   null, cn.trigger(times, [k] * n, exercises), range(n)))
            args = cn.collect_args(times, receiver, cn.basis("SwapBasisSystem", times, exercises), null, null, paths=64)
            coefficients = H.call("GenericLongstaffSchwartzRegression.run", *args)["coefficients"]
            out.append(engine_case(H, st, f"{label}: QuantLib's callable receiver, a Longstaff-Schwartz strategy",
                                   times, receiver, null, ls.ls_spec(times, coefficients, [n] * n, null), range(n)))
            every_other = [t for i, t in enumerate(exercises) if i % 2 == 0]
            out.append(engine_case(H, st, f"{label}: a payer swap callable at every other rate time", times,
                                   cn.swap(times, True), null, cn.trigger(times, [k] * len(every_other), every_other),
                                   [i for i in range(n) if i % 2 == 0]))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/callability/upperboundengine.hpp", cases)
