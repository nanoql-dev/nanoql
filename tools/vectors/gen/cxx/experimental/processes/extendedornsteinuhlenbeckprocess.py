# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ExtendedOrnsteinUhlenbeckProcess (ql/experimental/processes/extendedornsteinuhlenbeckprocess.hpp,
.cpp) on the rules the wheel doesn't bind, each output computed by the C++ harness: the trapezoidal and Gauss-Lobatto
rules (at QuantLib's integration accuracy of 1e-4 and at 1e-6), constant and linear levels, speeds from 0.5 to 7; each
process's expectation and standard deviation over steps, and its evolution on draws.

The outputs nanoql fixes are left out: every trapezoidal step has a·dt of 1e-2 or more, where nanoql's rule is
QuantLib's quotient (below it a series); every Gauss-Lobatto step ends at a(t0 + dt) of 30 or less, where nanoql
integrates QuantLib's integrand (past it the bounded one). Nothing is fitted, so no output is filtered.

The processes are tools/cxx/src/experimental/processes/extouspec.hpp's specs; the harness's method is
tools/cxx/src/experimental/processes/extendedornsteinuhlenbeckprocess.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Processes/ExtendedOrnsteinUhlenbeckProcessCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 40895  # 18 December 2011
MIDPOINT, TRAPEZOIDAL, GAUSSLOBATTO = 0, 1, 2
DRAWS = [-1.3, 0.0, 0.7, 2.1]


def process(speed, sigma, x0, level, discretization, int_eps=1e-4):
    return {"speed": speed, "sigma": sigma, "x0": x0, "level": level, "discretization": discretization,
            "intEps": int_eps}


def cases():
    out = []
    constant = {"type": "constant", "b": 3.0}
    linear = {"type": "linear", "c0": 1.0, "c1": 0.5}
    falling = {"type": "linear", "c0": 2.0, "c1": -0.3}
    with cxx.connect() as H:
        st = H.settings(R)
        setups = [
            ("trapezoidal, a constant level", process(1.0, 2.0, 3.0, constant, TRAPEZOIDAL),
             [[0.0, 3.0, 0.02], [0.5, 2.0, 0.25], [1.0, -0.5, 1.0], [3.0, 4.0, 0.5]]),
            ("trapezoidal, a linear level", process(2.5, 0.7, 0.0, linear, TRAPEZOIDAL),
             [[0.0, 0.5, 0.004], [0.5, 2.0, 0.1], [2.0, 1.0, 1.5], [5.0, 3.0, 0.01]]),
            ("trapezoidal, a falling level, a fast speed", process(7.0, 1.4, 0.2, falling, TRAPEZOIDAL),
             [[0.0, 0.2, 0.002], [0.25, 1.0, 0.05], [1.0, 0.0, 0.5]]),
            ("Gauss-Lobatto, a constant level", process(1.0, 2.0, 3.0, constant, GAUSSLOBATTO),
             [[0.0, 3.0, 0.01], [0.5, 2.0, 0.25], [1.0, -0.5, 1.0], [10.0, 4.0, 2.0]]),
            ("Gauss-Lobatto, a linear level", process(2.5, 0.7, 0.0, linear, GAUSSLOBATTO),
             [[0.0, 0.5, 0.004], [0.5, 2.0, 0.1], [2.0, 1.0, 1.5], [8.0, 3.0, 4.0]]),
            ("Gauss-Lobatto at 1e-6, a falling level", process(0.5, 0.3, 1.5, falling, GAUSSLOBATTO, 1e-6),
             [[0.0, 1.5, 0.1], [5.0, 1.0, 5.0], [20.0, 0.5, 10.0]]),
            ("Gauss-Lobatto, a fast speed below the cut", process(7.0, 1.4, 0.2, falling, GAUSSLOBATTO),
             [[0.0, 0.2, 0.002], [1.0, 1.0, 1.0], [3.0, 0.0, 1.2]]),
        ]
        for name, spec, steps in setups:
            for t0, _, dt in steps:
                a = spec["speed"]
                if spec["discretization"] == TRAPEZOIDAL:
                    assert a * dt >= 1e-2, name
                else:
                    assert a * (t0 + dt) <= 30.0, name
            expected = H.call("ExtendedOrnsteinUhlenbeckProcess::values", spec, steps, DRAWS)
            out.append({"name": name, "inputs": {"settings": st, "process": spec, "steps": steps, "draws": DRAWS},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/processes/extendedornsteinuhlenbeckprocess.hpp", cases)
