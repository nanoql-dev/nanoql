# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's genericEarlyExerciseOptimization (ql/methods/montecarlo/parametricexercise.hpp, .cpp),
which the wheel doesn't expose, each output computed by the C++ harness: the optimisation, by QuantLib's simplex, on
the node data collectNodeData gives on a few paths of the predictor-corrector evolver for a triggered swap exercise,
its biased estimate, each exercise's parameters and the paths' cash flows before the first exercise after it
(genericlsregression.py's helper).

Rate grids: six semi-annual rates and five irregular ones; a receiver swap exercised at every rate time but the last
into nothing and into a Bermudan payer, strikes at the forwards' mean and rising, QuantLib's testCallableSwapAnderson
simplex (0.01) and end criteria (1000, 100, 1e-8, 1e-16, 1e-8); a looser simplex (0.05).

The harness unit is tools/cxx/src/models/marketmodels/callability/callability.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/MonteCarlo/ParametricExerciseCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(_dir, "gen", "cxx", "models", "marketmodels", "callability"))
sys.path.insert(0, os.path.join(_dir, "gen", "cxx", "models", "marketmodels", "products", "multistep"))

import collectnodedata as cn  # noqa: E402
import cxx  # noqa: E402
import genericlsregression as gl  # noqa: E402
import vectors as v  # noqa: E402

END_CRITERIA = [1000, 100, 1e-8, 1e-16, 1e-8]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(cn.R)
        for label, times in cn.GRIDS:
            exercises = times[:-1]
            k = cn.mid(times)
            for slabel, strikes in (("strikes at the forwards' mean", [k] * len(exercises)),
                                    ("strikes rising", [k - 0.004 + 0.002 * i for i in range(len(exercises))])):
                for rlabel, rebate in (("into nothing", cn.nothing(times)),
                                       ("into a Bermudan payer", cn.bermudan(times, 1, k))):
                    args = cn.collect_args(times, cn.swap(times, False), cn.triggered(times, exercises, strikes), rebate,
                                           cn.nothing(times), paths=gl.PATHS)
                    out.append(gl.run_case(H, st, "GenericEarlyExerciseOptimization.run",
                                           f"{label}: a receiver swap {rlabel}, {slabel}", args, (0.01, END_CRITERIA)))
            args = cn.collect_args(times, cn.swap(times, False), cn.triggered(times, exercises, [k] * len(exercises)),
                                   cn.nothing(times), cn.nothing(times), paths=gl.PATHS)
            out.append(gl.run_case(H, st, "GenericEarlyExerciseOptimization.run", f"{label}: a looser simplex", args,
                                   (0.05, END_CRITERIA)))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/montecarlo/parametricexercise.hpp", cases)
