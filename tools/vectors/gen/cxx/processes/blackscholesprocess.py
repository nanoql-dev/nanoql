# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GeneralizedBlackScholesProcess (ql/processes/blackscholesprocess.hpp, .cpp), each output
computed by the C++ harness: the forceDiscretization argument, which the wheel doesn't expose. Processes over flat
forwards and a constant volatility, with Euler's discretization and the argument false and true, at 12 points (t0,
x0, dt, dw): the expectation (exact without the argument, QuantLib's "not implemented" with it), the standard
deviation and variance (exact, or the discretization's) and the evolution.

Read back by tests/NanoQL.Tests/Vectors/Cxx/Processes/BlackScholesProcessCxxVectorTests.cs."""
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
A365 = {"type": "Actual365Fixed"}
A360 = {"type": "Actual360"}
PROCESSES = [(100.0, 0.04, 0.02, 0.2, A365), (50.0, -0.01, 0.01, 0.35, A360), (100.0, 0.0, 0.0, 0.0, A365)]
MEMBERS = ["expectation", "stdDeviation", "variance", "evolve"]


def cases():
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(R)
        for k, (spot, r, q, vol, dc) in enumerate(PROCESSES):
            pts = [[rnd.choice([0.0, rnd.uniform(0.0, 3.0)]), rnd.uniform(0.5, 1.5) * spot,
                    rnd.choice([0.01, 0.5, rnd.uniform(0.0, 2.0)]), rnd.uniform(-2.5, 2.5)] for _ in range(12)]
            for force in (False, True):
                out.append({"name": f"process {k}, forced {force}",
                            "inputs": {"settings": st, "spot": spot, "riskFreeRate": r, "dividendYield": q,
                                       "volatility": vol, "referenceDate": R, "dayCounter": dc,
                                       "forceDiscretization": force, "points": pts},
                            "expected": {m: [v.attempt(lambda p=p, m=m: H.call(
                                "GeneralizedBlackScholesProcess::forced", m, spot, r, q, vol, R, dc, force, *p))
                                for p in pts] for m in MEMBERS}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/blackscholesprocess.hpp", cases)
