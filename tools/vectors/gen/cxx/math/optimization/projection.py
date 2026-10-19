# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Projection (ql/math/optimization/projection.hpp, .cpp), which no wheel class exposes, each output
computed by the C++ harness (docs/porting-playbook.md §9.4): project and include for 26 projections of one to six
parameters, with the flags QuantLib's default leaves empty (all free), all free given explicitly, and every mix with at
least one free parameter, each at four parameter vectors drawn at random, a NaN among them; then the refusals, in
QuantLib's order: flags of another size than the values, no free parameter (every one fixed, or no values), and
parameters or projected parameters of another size than the call takes.

Each case's inputs hold the projection's values and flags ("parameterValues", "fixParameters") and the vectors given
to project ("parameters") and to include ("projectedParameters"). tests/NanoQL.Tests/Vectors/Cxx/Numerics/
Optimization/ProjectionCxxVectorTests.cs reads the file back."""
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

VECTORS = 4


def projections(rnd):
    """(name, values, flags) for the 26 projections."""
    out = []
    for n in range(1, 7):
        values = [rnd.uniform(-2.0, 2.0) for _ in range(n)]
        out.append((f"{n} parameters, default flags", values, []))
        out.append((f"{n} parameters, all free", values, [False] * n))
        if n > 1:
            out.append((f"{n} parameters, first fixed", values, [True] + [False] * (n - 1)))
            out.append((f"{n} parameters, last free", values, [True] * (n - 1) + [False]))
    out.append(("4 parameters, alternate", [0.1, 0.2, 0.3, 0.4], [True, False, True, False]))
    out.append(("5 parameters, middle free", [1.0, 2.0, 3.0, 4.0, 5.0], [True, True, False, True, True]))
    out.append(("Hull-White's FixedReversion", [0.05, 0.01], [True, False]))
    out.append(("Vasicek, b and lambda fixed", [0.1, 0.05, 0.01, 0.0], [False, True, False, True]))
    assert len(out) == 26, len(out)
    return out


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        rnd = random.Random(v.SEED)

        def call(method, *params):
            return v.attempt(lambda: H.call(method, *params))

        out = []
        for name, values, flags in projections(rnd):
            n, free = len(values), (len(values) if not flags else flags.count(False))
            parameters = [[rnd.uniform(-5.0, 5.0) for _ in range(n)] for _ in range(VECTORS)]
            projected = [[rnd.uniform(-5.0, 5.0) for _ in range(free)] for _ in range(VECTORS)]
            parameters[-1][0] = math.nan
            projected[-1][-1] = math.nan
            out.append({"name": name,
                        "inputs": {"settings": st, "parameterValues": values, "fixParameters": flags,
                                   "parameters": v.enc(parameters), "projectedParameters": v.enc(projected)},
                        "expected": {"project": [call("Projection::project", values, flags, p) for p in parameters],
                                     "include": [call("Projection::include", values, flags, p) for p in projected]}})

        refusals = [
            ("flags of another size", "Projection::project", [[1.0, 2.0], [True], [1.0, 2.0]]),
            ("every parameter fixed", "Projection::project", [[1.0, 2.0], [True, True], [1.0, 2.0]]),
            ("no values", "Projection::include", [[], [], []]),
            ("parameters of another size", "Projection::project", [[1.0, 2.0, 3.0], [False, True, False], [1.0, 2.0]]),
            ("projected parameters of another size", "Projection::include",
             [[1.0, 2.0, 3.0], [False, True, False], [1.0, 2.0, 3.0]]),
            ("projected parameters for every parameter, default flags", "Projection::include",
             [[1.0, 2.0], [], [1.0]]),
        ]
        for name, method, params in refusals:
            out.append({"name": "refused / " + name,
                        "inputs": {"settings": st, "method": method, "params": v.enc(params)},
                        "expected": {"result": call(method, *params)}})
        for c in out[-len(refusals):]:
            assert c["expected"]["result"] == v.THROWS, c["name"]
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/projection.hpp", cases)
