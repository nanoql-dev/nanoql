# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CoxIngersollRossProcess (ql/processes/coxingersollrossprocess.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness: 8 processes (with and without the defaulted x0 and level;
speeds from 0.01 to 5, a zero volatility, a zero state), each with its x0, speed, volatility and level, and at 20
points (t, x, dt, dw) drawn with x non-negative and dt positive, its drift, its expectation level + (x - level)e^(-k dt)
and its quadratic-exponential evolve, on both of its branches (psi at most 1.5, and above it, where the draw's
cumulative normal picks a zero or an exponential state).

Generated and left out: the diffusion, the variance and the standard deviation. QuantLib's diffusion is the constant
sigma, its variance reads the constructor's x0 where the step's state is given and its level term is twice evolve's, and
its standard deviation is the variance's root; nanoql's diffusion is sigma sqrt(x), the header's SDE, and its variance
evolve's own s2 at the state (tests/NanoQL.Tests/Divergences/CirProcessMomentsTests.cs). nanoql writes s2's (1 - e^(-k
dt))/k as dt (1 - e^(-x))/x, x = k dt, the cancellation-free form of the Ornstein-Uhlenbeck process, which moves
evolve's last bits only at these speeds.
tests/NanoQL.Tests/Vectors/Cxx/Processes/CoxIngersollRossProcessCxxVectorTests.cs reads it back."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

# (speed, volatility, x0, level); None leaves x0, then the level, to its default
PROCESSES = [(0.3, 0.1, 0.04, 0.04), (0.5, 0.2, 0.03, None), (1.0, 0.15, None, None), (5.0, 0.3, 0.5, 0.02),
             (0.01, 0.05, 0.06, 0.08), (0.2, 0.0, 0.05, 0.04), (2.0, 0.6, 0.0, 0.05), (0.1, 0.02, 0.2, 0.01)]
INSPECTORS = ("x0", "speed", "volatility", "level")
LEFT_OUT = ("diffusion", "variance", "stdDeviation")


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 22, 10, 2026))
        rnd = random.Random(v.SEED)
        out = []
        for k, (speed, vol, x0, level) in enumerate(PROCESSES):
            points = [[rnd.uniform(0.0, 10.0), rnd.choice([rnd.uniform(0.0, 0.2), rnd.uniform(0.0, 0.01), 0.0]),
                       rnd.choice([1e-6, 0.01, 0.25, 1.0]), rnd.gauss(0.0, 1.0)] for _ in range(20)]

            def call(m, t=0.0, x=0.0, dt=0.0, dw=0.0):
                return v.attempt(lambda: H.call("CoxIngersollRossProcess", speed, vol, x0, level, m, t, x, dt, dw))

            expected = {m: call(m) for m in INSPECTORS}
            expected["drift"] = [call("drift", t, x) for t, x, _, _ in points]
            expected["expectation"] = [call("expectation", t, x, dt) for t, x, dt, _ in points]
            expected["evolve"] = [call("evolve", t, x, dt, dw) for t, x, dt, dw in points]
            for m in LEFT_OUT:
                [call(m, t, x, dt) for t, x, dt, _ in points]
            inputs = {"settings": st, "speed": speed, "volatility": vol, "points": points}
            if x0 is not None:
                inputs["x0"] = x0
            if level is not None:
                inputs["level"] = level
            out.append({"name": f"{k} speed {speed!r} volatility {vol!r}", "inputs": inputs, "expected": expected})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/coxingersollrossprocess.hpp", cases)
