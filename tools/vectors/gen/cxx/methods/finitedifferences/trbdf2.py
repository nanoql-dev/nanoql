# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the old framework's TRBDF2 and ZeroCondition (ql/methods/finitedifferences/trbdf2.hpp,
zerocondition.hpp), which the wheel doesn't bind, each output computed by the C++ harness: values rolled back through
FiniteDifferenceModel on time-independent tridiagonal operators (the heat equation's on uniform grids, and a
convection-diffusion operator with discounting), from 1 to 0 and from 2 to 0.5 in 1 to 50 steps, with the zero condition
and without. A time-dependent operator, whose trapezoidal stage nanoql reads at the stage's own time
(tests/NanoQL.Tests/Divergences/TRBDF2Tests.cs), isn't among them. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/FiniteDifferences/TRBDF2CxxVectorTests.cs."""
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


def heat(n, dx, c):
    k = c / (dx * dx)
    return [-k] * (n - 1), [2.0 * k] * n, [-k] * (n - 1)


def convection(n, dx, sigma, mu, r):
    a = 0.5 * sigma * sigma / (dx * dx)
    b = mu / (2.0 * dx)
    return [-(a - b)] * (n - 1), [2.0 * a + r] * n, [-(a + b)] * (n - 1)


def operators():
    return [("heat, 11 nodes", heat(11, 0.1, 0.5)), ("heat, 41 nodes", heat(41, 0.05, 0.1)),
            ("convection-diffusion, 21 nodes", convection(21, 0.15, 0.3, 0.02, 0.05))]


def cases():
    rng = random.Random(v.SEED)
    out = []
    with cxx.connect() as H:
        for name, (low, mid, high) in operators():
            n = len(mid)
            for vname, values in (("a hump", [math.sin(math.pi * i / (n - 1)) for i in range(n)]),
                                  ("a put's payoff", [max(1.0 - 0.1 * i, 0.0) - 0.3 for i in range(n)]),
                                  ("random", [rng.uniform(-1.0, 1.0) for _ in range(n)])):
                for start, end in ((1.0, 0.0), (2.0, 0.5)):
                    for steps in (1, 7, 50):
                        for zero in (False, True):
                            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
                            out.append({"name": f"{name}, {vname}, {start} to {end} in {steps} steps"
                                                + (", zero condition" if zero else ""),
                                        "inputs": {"settings": st, "low": v.enc(low), "mid": v.enc(mid),
                                                   "high": v.enc(high), "values": v.enc(values), "from": start,
                                                   "to": end, "steps": steps, "zeroCondition": zero},
                                        "expected": {"values": v.enc(H.call("TRBDF2::rollback", low, mid, high, values,
                                                                            start, end, steps, zero))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/trbdf2.hpp", cases)
