# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's genericLongstaffSchwartzRegression (ql/methods/montecarlo/genericlsregression.hpp, .cpp),
which the wheel doesn't expose, each output computed by the C++ harness: the regression on the node data collectNodeData
gives on a few paths of the predictor-corrector evolver, its biased estimate, each exercise's coefficients and the
paths' cash flows before the first exercise after it.

Rate grids: six semi-annual rates and five irregular ones; a receiver swap exercised at every rate time but the last
into nothing and into a Bermudan payer, on a swap basis system and a swap-forward basis system; a payer swap exercised at
every other rate time; Mersenne-twister and Sobol paths.

Left out: node data with an invalid first path, from which QuantLib sizes the regression
(tests/NanoQL.Tests/Divergences/GenericLSRegressionBasisSizeTests.cs); no product here ends before an exercise.

The harness unit is tools/cxx/src/models/marketmodels/callability/callability.cpp (the callability generators'
helpers). Read back by tests/NanoQL.Tests/Vectors/Cxx/Methods/MonteCarlo/GenericLSRegressionCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.join(_dir, "gen", "cxx", "models", "marketmodels", "callability"))
sys.path.insert(0, os.path.join(_dir, "gen", "cxx", "models", "marketmodels", "products", "multistep"))

import collectnodedata as cn  # noqa: E402
import cxx  # noqa: E402
import vectors as v  # noqa: E402

PATHS = 64


def run_case(H, st, method, name, args, extra=()):
    r = H.call(method, *args, *extra)
    inputs = {"settings": st, "evolver": args[0], "product": args[1], "provider": args[2], "rebate": args[3],
              "control": args[4], "paths": args[5]}
    if extra:
        inputs["simplexLambda"] = extra[0]
        inputs["endCriteria"] = extra[1]
    key = "coefficients" if "coefficients" in r else "parameters"
    return {"name": name, "inputs": inputs,
            "expected": {"estimate": v.enc(r["estimate"]), key: v.enc(r[key]),
                         "cumulatedCashFlows": v.enc(r["cumulatedCashFlows"])}}


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(cn.R)
        for label, times in cn.GRIDS:
            exercises = times[:-1]
            for blabel, kind in (("a swap basis system", "SwapBasisSystem"),
                                 ("a swap-forward basis system", "SwapForwardBasisSystem")):
                for rlabel, rebate in (("into nothing", cn.nothing(times)),
                                       ("into a Bermudan payer", cn.bermudan(times, 1, cn.mid(times)))):
                    args = cn.collect_args(times, cn.swap(times, False), cn.basis(kind, times, exercises), rebate,
                                           cn.nothing(times), paths=PATHS)
                    out.append(run_case(H, st, "GenericLongstaffSchwartzRegression.run",
                                        f"{label}: a receiver swap {rlabel}, {blabel}", args))
            every_other = [t for i, t in enumerate(exercises) if i % 2 == 0]
            flags = [i % 2 == 0 for i in range(len(exercises))]
            args = cn.collect_args(times, cn.swap(times, True), cn.basis("SwapBasisSystem", times, every_other),
                                   cn.nothing(times, flags), cn.nothing(times), paths=PATHS,
                                   generator={"type": "Sobol", "ordering": 2, "seed": 1234})
            out.append(run_case(H, st, "GenericLongstaffSchwartzRegression.run",
                                f"{label}: a payer swap exercised at every other rate time, Sobol", args))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/montecarlo/genericlsregression.hpp", cases)
