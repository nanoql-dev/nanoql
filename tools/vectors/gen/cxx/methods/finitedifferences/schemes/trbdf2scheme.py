# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's TrBDF2Scheme (ql/methods/finitedifferences/schemes/trbdf2scheme.hpp), which the wheel doesn't
bind, each output computed by the C++ harness (docs/porting-playbook.md §9.4): the scheme on its own,
TrBDF2Scheme<CrankNicolsonScheme> with a Crank-Nicolson trapezoid at theta 0.5 (FdmBackwardSolver's, over a Craig-Sneyd
trapezoid, is tools/vectors/gen/methods/finitedifferences/schemes/trbdf2scheme.py's), on the Black-Scholes operator of
three flat-forward processes on uniform log meshers, at alpha 2 - sqrt(2) (QuantLib's description's) and 0.5, stepped
back ten steps of 0.05 from 1 from a seeded vector: the values after each step and numberOfIterations after the last
(0: the operator has one direction, which the splitting solves), on the evaluation date 15 October 2026. The operator
doesn't depend on time, so nanoql's two stages over the whole step (SEQR9) move only the values' last bits, within R7
(tests/NanoQL.Tests/Divergences/TrBdf2TimeDependentTests.cs).

The harness's call is tools/cxx/src/methods/finitedifferences/schemes/trbdf2scheme.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/FiniteDifferences/Schemes/TrBDF2SchemeCxxVectorTests.cs."""
import math
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

# (name, spot, r, q, volatility, xMin, xMax, size)
PROCESSES = [("at the money", 100.0, 0.04, 0.02, 0.2, math.log(25.0), math.log(400.0), 41),
             ("high volatility", 80.0, 0.01, 0.0, 0.6, math.log(5.0), math.log(1000.0), 31),
             ("negative rate", 120.0, -0.01, 0.03, 0.15, math.log(60.0), math.log(240.0), 21)]
ALPHAS = [2.0 - math.sqrt(2.0), 0.5]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        rng = random.Random(20261031)
        out = []
        for name, spot, r, q, vol, lo, hi, size in PROCESSES:
            for alpha in ALPHAS:
                initial = [rng.uniform(0.0, 50.0) for _ in range(size)]
                spec = {"spot": spot, "riskFreeRate": r, "dividendYield": q, "volatility": vol, "xMin": lo,
                        "xMax": hi, "size": size, "alpha": alpha, "dt": 0.05, "from": 1.0, "steps": 10}
                res = H.call("TrBDF2Scheme::step", spot, r, q, vol, lo, hi, size, alpha, 0.05, 1.0, 10, initial)
                out.append({"name": f"{name}, alpha {alpha}",
                            "inputs": {"settings": st, "scheme": spec, "initial": initial},
                            "expected": {"values": res["values"], "numberOfIterations": res["numberOfIterations"]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/schemes/trbdf2scheme.hpp", cases)
