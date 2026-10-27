# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SquareRootAndersen (ql/models/marketmodels/evolvers/volprocesses/squarerootandersen.hpp,
.cpp), which the wheel doesn't expose, each output computed by the C++ harness: paths of given variates, step by step.

QuantLib leaves the first evolution step's e^(-k dt) at 0 (the S-mm plan's MR3 (xvii)), so its first step reverts in
full where nanoql's takes e^(-k dt); the two agree where e^(-k dt) underflows to 0 itself, k dt past 745. So: mean level
0.04, reversion speed 4000, the first evolution time 1 (k dt 4,000, 2,000 and 1,000 at 1, 2 and 4 sub-steps), then
1.0002, 1.0006, 1.0016 and 1.004 (k dt from 0.2 to 9.6, past nanoql's expm1 cut of 0.01, MR3 (xviii)); vol of variance
5, 25 and 60, so each sub-step's psi falls on either side of the cut point; v0 0.01 and 0.16; weights 0.5 and 0.5 and
the cut point 1.5, and on 2 sub-steps and vol of variance 25 the weights 1 and 0 and 0.25 and 0.75 and the cut points 1,
2 and 2.5 (nanoql clamps a cut to [1, 2], where each form holds; 2.5's psi never fall in (2, 2.5]). Each of three paths
of seeded Gaussian variates: each step's weight, step standard deviation and state; and the process's variates per step
and state variables. numberSteps is left out: QuantLib's counts every sub-step again per sub-step (MR3 (xiii)).

The harness call is tools/cxx/src/models/marketmodels/evolvers/evolvers.cpp's "SquareRootAndersen.paths". Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/SquareRootAndersenCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
THETA = 0.04
KAPPA = 4000.0
TIMES = [1.0, 1.0002, 1.0006, 1.0016, 1.004]
PATHS = 3


def specs():
    out = []
    for sub in (1, 2, 4):
        for eps in (5.0, 25.0, 60.0):
            for v0 in (0.01, 0.16):
                out.append((sub, eps, v0, 0.5, 0.5, 1.5))
    out += [(2, 25.0, 0.16, 1.0, 0.0, 1.5), (2, 25.0, 0.16, 0.25, 0.75, 1.5), (2, 25.0, 0.01, 0.5, 0.5, 2.5),
            (2, 25.0, 0.16, 0.5, 0.5, 1.0), (2, 25.0, 0.16, 0.5, 0.5, 2.0)]
    return out


def cases():
    rng = random.Random(20261027)
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for sub, eps, v0, w1, w2, cut in specs():
            variates = [[[rng.gauss(0.0, 1.0) for _ in range(sub)] for _ in TIMES] for _ in range(PATHS)]
            r = H.call("SquareRootAndersen.paths", THETA, KAPPA, eps, v0, TIMES, sub, w1, w2, cut, variates)
            out.append({"name": f"{sub} sub-steps, vol of variance {eps}, v0 {v0}, weights {w1} and {w2}, cut {cut}",
                        "inputs": {"settings": st, "meanLevel": THETA, "reversionSpeed": KAPPA, "volVar": eps,
                                   "v0": v0, "evolutionTimes": TIMES, "numberSubSteps": sub, "w1": w1, "w2": w2,
                                   "cutPoint": cut, "variates": variates},
                        "expected": {"variatesPerStep": r["variatesPerStep"],
                                     "numberStateVariables": r["numberStateVariables"],
                                     **{k: [[s[k] for s in p] for p in r["paths"]]
                                        for k in ("weight", "stepSd", "state")}}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/evolvers/volprocesses/squarerootandersen.hpp", cases)
