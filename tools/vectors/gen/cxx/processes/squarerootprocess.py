# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SquareRootProcess (ql/processes/squarerootprocess.hpp, .cpp), dx = a(b − x) dt + σ√x dW on
its default Euler discretization, which the wheel doesn't expose, each output computed by the C++ harness: 8
processes (with and without the defaulted x0; a zero, a large speed, a zero volatility), each with its x0, a, b and
sigma, and at 20 points (t, x, dt, dw) drawn with x negative at some, its drift, diffusion, Euler expectation,
standard deviation, variance and evolved state. The points at a negative state are generated and left out: QuantLib's
diffusion σ√x is NaN there, nanoql's the partial truncation's σ√max(x, 0)
(tests/NanoQL.Tests/Divergences/SquareRootProcessTruncationTests.cs). The draws all come before, so the other points are
kept. tests/NanoQL.Tests/Vectors/Cxx/Processes/SquareRootProcessCxxVectorTests.cs reads it back."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

# (b, a, sigma, x0); None leaves x0 to its default
PROCESSES = [(0.1, 0.1, 0.20, 10.0), (0.04, 0.5, 0.1, None), (0.05, 0.0, 0.15, 0.03), (0.02, 5.0, 0.3, 0.5),
             (1.0, 1.0, 0.0, 2.0), (0.06, 0.25, 0.05, 0.06), (0.3, 2.0, 1.0, 0.0), (0.01, 0.05, 0.02, 0.2)]
POINT_MEMBERS = ("drift", "diffusion")
STEP_MEMBERS = ("expectation", "stdDeviation", "variance")


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 22, 10, 2026))
        rnd = random.Random(v.SEED)
        out = []
        for k, (b, a, sigma, x0) in enumerate(PROCESSES):
            points = [[rnd.uniform(0.0, 10.0), rnd.choice([rnd.uniform(0.0, 2.0), rnd.uniform(-0.5, 0.0), 0.0]),
                       rnd.choice([0.0, 1e-6, 0.01, 0.25, 1.0]), rnd.gauss(0.0, 1.0)] for _ in range(20)]
            points = [p for p in points if not p[1] < 0.0]

            def call(m, t=0.0, x=0.0, dt=0.0, dw=0.0):
                return v.attempt(lambda: H.call("SquareRootProcess", b, a, sigma, x0, m, t, x, dt, dw))

            expected = {m: call(m) for m in ("x0", "a", "b", "sigma")}
            for m in POINT_MEMBERS:
                expected[m] = [call(m, t, x) for t, x, _, _ in points]
            for m in STEP_MEMBERS:
                expected[m] = [call(m, t, x, dt) for t, x, dt, _ in points]
            expected["evolve"] = [call("evolve", t, x, dt, dw) for t, x, dt, dw in points]
            inputs = {"settings": st, "b": b, "a": a, "sigma": sigma, "points": points}
            if x0 is not None:
                inputs["x0"] = x0
            out.append({"name": f"{k} b {b!r} a {a!r} sigma {sigma!r}", "inputs": inputs, "expected": expected})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/squarerootprocess.hpp", cases)
