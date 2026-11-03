# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmExtendedOrnsteinUhlenbeckOp, FdmExtOUJumpOp and FdmKlugeExtOUOp
(ql/experimental/finitedifferences/fdmextendedornsteinuhlenbeckop.hpp, fdmextoujumpop.hpp, fdmklugeextouop.hpp, .cpp
each), which the wheel doesn't expose, each output computed by the C++ harness on meshers built as QuantLib's engines
build them: the extended OU operator on constant and linear levels; the jump operator on Kluge's swing process and on
frequent small jumps, at Gauss-Laguerre orders 8 and 32; the Kluge-OU operator on the VPP case's process and the spread
case's, at order 16; each discounted at 0 and 5%. For [t1, t2] of [0, 0.1] and [0.5, 0.75], after setTime: apply,
apply_mixed, apply_direction and solve_splitting at s = -0.01 in each direction, and the preconditioner at s, on a
seeded vector.

No output is left out: the operators read their processes' drifts and volatilities, which no fix touches. Nothing is
fitted, so no output is filtered.

The processes are tools/cxx/src/experimental/processes/extouspec.hpp's specs; the methods
tools/cxx/src/experimental/finitedifferences/fdmklugeextouop.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/FiniteDifferences/FdmKlugeExtOUOpCxxVectorTests.cs."""
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

R = 40895  # 18 December 2011
TIMES = [[0.0, 0.1], [0.5, 0.75]]
S = -0.01


def ou(speed, sigma, x0, level):
    return {"speed": speed, "sigma": sigma, "x0": x0, "level": level, "discretization": 0, "intEps": 1e-4}


def vector(n, seed):
    rnd = random.Random(seed)
    return [rnd.uniform(-1.0, 1.0) for _ in range(n)]


def cases():
    out = []
    constant = {"type": "constant", "b": 3.0}
    linear = {"type": "linear", "c0": 1.0, "c1": 0.5}
    zero = {"type": "constant", "b": 0.0}
    swing = {"ou": ou(1.0, 2.0, 3.0, constant), "y0": 0.0, "beta": 5.0, "jumpIntensity": 1.0, "eta": 2.0}
    small = {"ou": ou(4.0, 0.8, 3.3, linear), "y0": 0.1, "beta": 50.0, "jumpIntensity": 6.0, "eta": 8.0}
    vpp = {"kluge": {"ou": ou(7.0, 1.4, 0.0, zero), "y0": 0.0, "beta": 200.0, "jumpIntensity": 4.0, "eta": 5.0},
           "gas": ou(4.45, math.sqrt(1.3), 0.0, zero), "rho": 0.7}
    spread = {"kluge": swing, "gas": ou(1.0, math.sqrt(1.4), 3.0, {"type": "linear", "c0": 3.0, "c1": 0.0}),
              "rho": 0.5}
    with cxx.connect() as H:
        st = H.settings(R)
        for name, spec, rate in [("extended OU, a constant level", ou(1.0, 2.0, 3.0, constant), 0.0),
                                 ("extended OU, a linear level", ou(2.5, 0.7, 0.0, linear), 0.05)]:
            r = vector(12, 1)
            out.append({"name": name, "inputs": {"settings": st, "op": "extended", "process": spec, "grids": [12],
                                                 "maturity": 1.0, "rate": rate, "order": 0, "times": TIMES, "s": S,
                                                 "r": r},
                        "expected": H.call("FdmExtendedOrnsteinUhlenbeckOp::values", spec, 12, 1.0, rate, TIMES, S,
                                           r)})
        for name, spec, rate, order in [("jumps, Kluge's swing process, order 32", swing, 0.05, 32),
                                        ("jumps, frequent small jumps, order 8", small, 0.0, 8)]:
            r = vector(10 * 6, 2)
            out.append({"name": name, "inputs": {"settings": st, "op": "jumps", "process": spec, "grids": [10, 6],
                                                 "maturity": 1.0, "rate": rate, "order": order, "times": TIMES,
                                                 "s": S, "r": r},
                        "expected": H.call("FdmExtOUJumpOp::values", spec, [10, 6], 1.0, rate, order, TIMES, S, r)})
        for name, spec, rate in [("Kluge-OU, the VPP case, order 16", vpp, 0.0),
                                 ("Kluge-OU, the spread case, order 16", spread, 0.05)]:
            r = vector(8 * 5 * 6, 3)
            out.append({"name": name, "inputs": {"settings": st, "op": "kluge", "process": spec, "grids": [8, 5, 6],
                                                 "maturity": 1.0, "rate": rate, "order": 16, "times": TIMES, "s": S,
                                                 "r": r},
                        "expected": H.call("FdmKlugeExtOUOp::values", spec, [8, 5, 6], 1.0, rate, 16, TIMES, S, r)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/finitedifferences/fdmklugeextouop.hpp", cases)
