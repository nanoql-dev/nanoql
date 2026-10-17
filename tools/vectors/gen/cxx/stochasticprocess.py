# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's StochasticProcess (ql/stochasticprocess.hpp, .cpp) that the wheel doesn't
expose, each output computed by the C++ harness (docs/porting-playbook.md §9.4).

Five Ornstein-Uhlenbeck processes, each through a StochasticProcess reference: apply(x0, dx) at 10 points, which
StochasticProcess1D implements on arrays of size 1, and time(d), the base's default, which refuses. Four of the
harness's LinearProcess, of 1 to 4 dimensions, whose expectation, standard deviation and covariance are hard-coded
so that the base class's own defaults run on it: size, factors (the default, size()), evolve(t0, x0, dt, dw) at 10
points (expectation plus standard deviation times dw, through apply) and apply(x0, dx) at 10 points, both the
base's, and time(d); then the refusals of apply with a change of another size and of evolve with increments of another
size.

A process argument is the harness's spec (tools/cxx/src/stochasticprocess.cpp): {"type": "OrnsteinUhlenbeckProcess",
"speed", "volatility", "x0", "level"}, or {"type": "LinearProcess", "x0", "a", "sigma"}, the harness's own process,
which tests/NanoQL.Tests/Vectors/Cxx/StochasticProcessCxxVectorTests.cs, reading this file back, defines too."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 16, 10, 2026))
        date = H.call("Date(Day,Month,Year)", 17, 10, 2026)
        rnd = random.Random(v.SEED)

        def call(method, *params):
            return v.attempt(lambda: H.call(method, *params))

        out = []
        for k in range(5):
            process = {"type": "OrnsteinUhlenbeckProcess", "speed": rnd.uniform(-0.5, 2.0),
                       "volatility": rnd.uniform(0.0, 0.5), "x0": rnd.uniform(-1.0, 1.0), "level": rnd.uniform(-0.5, 0.5)}
            points = [[[rnd.uniform(-2.0, 2.0)], [rnd.uniform(-0.5, 0.5)]] for _ in range(10)]
            out.append({"name": f"OrnsteinUhlenbeckProcess {k}",
                        "inputs": {"settings": st, "process": process, "applyPoints": points, "date": date},
                        "expected": {"apply": [H.call("StochasticProcess::apply", process, x0, dx) for x0, dx in points],
                                     "time": call("StochasticProcess::time", process, date)}})

        for n in (1, 2, 3, 4):
            process = {"type": "LinearProcess", "x0": [rnd.uniform(0.5, 2.0) for _ in range(n)],
                       "a": [rnd.uniform(-0.3, 0.3) for _ in range(n)],
                       "sigma": [[rnd.uniform(-0.4, 0.4) for _ in range(n)] for _ in range(n)]}
            evolve = [[rnd.uniform(0.0, 5.0), [rnd.uniform(0.0, 3.0) for _ in range(n)], rnd.uniform(0.0, 1.0),
                       [rnd.gauss(0.0, 1.0) for _ in range(n)]] for _ in range(10)]
            apply = [[[rnd.uniform(-2.0, 2.0) for _ in range(n)], [rnd.uniform(-0.5, 0.5) for _ in range(n)]]
                     for _ in range(10)]
            t0, x0, dt, dw = evolve[0]
            out.append({"name": f"LinearProcess {n}-D",
                        "inputs": {"settings": st, "process": process, "evolvePoints": evolve, "applyPoints": apply,
                                   "date": date},
                        "expected": {
                            "size": H.call("StochasticProcess::size", process),
                            "factors": H.call("StochasticProcess::factors", process),
                            "evolve": [H.call("StochasticProcess::evolve", process, *p) for p in evolve],
                            "apply": [H.call("StochasticProcess::apply", process, x, dx) for x, dx in apply],
                            "time": call("StochasticProcess::time", process, date),
                            "applyWrongSize": call("StochasticProcess::apply", process, apply[0][0],
                                                   apply[0][1] + [0.1]),
                            "evolveWrongSize": call("StochasticProcess::evolve", process, t0, x0, dt, dw + [0.3])}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/stochasticprocess.hpp", cases)
