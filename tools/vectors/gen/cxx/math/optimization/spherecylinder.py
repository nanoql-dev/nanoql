# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SphereCylinderOptimizer and sphereCylinderOptimizerClosest
(ql/math/optimization/spherecylinder.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++
harness: QuantLib's two cases (marketmodel_smmcaplethomocalibration.cpp's testSphereCylinder), seeded sphere-cylinder
pairs that meet, at weights 1, 0.5 and 3 on the third coordinate and at 0 and 25 iterations, a cylinder of radius 0 and
pairs that don't meet: whether they meet, the closest point, the projection; the function's closest point, and its throw
where they don't meet.

Negative cylinder radii are left out: QuantLib's constructor clamps its parameter after storing the unclamped one, so
its solves read a radius its intersection test doesn't (tests/NanoQL.Tests/Divergences/SphereCylinderClampTests.cs).

The harness unit is tools/cxx/src/math/optimization/spherecylinder.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Math/Optimization/SphereCylinderCxxVectorTests.cs."""
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


def cases():
    rng = random.Random(20261027)
    rows = [("QuantLib's first case", 1.0, 0.5, 1.5, 1.0 / 3 ** 0.5, 1.0 / 3 ** 0.5, 1.0 / 3 ** 0.5, 1.0, 100, 1e-8),
            ("QuantLib's second case", 5.0, 1.0, 1.0, 1.0, 2.0, 20.0 ** 0.5, 1.0, 100, 1e-8),
            ("a cylinder of radius 0", 1.0, 0.0, 0.5, 0.6, 0.1, 0.5, 1.0, 100, 1e-8),
            ("disjoint, cylinder outside", 1.0, 0.2, 3.0, 0.5, 0.5, 0.5, 1.0, 100, 1e-8),
            ("disjoint, cylinder inside", 3.0, 0.5, 0.2, 1.0, 1.0, 1.0, 1.0, 100, 1e-8)]
    while len(rows) < 25:
        r = rng.uniform(0.5, 3.0)
        s = rng.uniform(0.05, 0.9) * r
        alpha = rng.uniform(r - s + 0.01, r + s - 0.01)
        z = [rng.uniform(-r, r) for _ in range(3)]
        weight = (1.0, 0.5, 3.0)[len(rows) % 3]
        iterations = 0 if len(rows) % 5 == 0 else 25 if len(rows) % 5 == 1 else 100
        rows.append((f"seeded {len(rows) - 5}, weight {weight}, {iterations} iterations", r, s, alpha, z[0], z[1], z[2],
                     weight, iterations, 1e-10))
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for name, r, s, alpha, z1, z2, z3, w, n, tol in rows:
            args = [r, s, alpha, z1, z2, z3, w, n, tol]
            out.append({"name": name,
                        "inputs": {"settings": st, "r": r, "s": s, "alpha": alpha, "z1": z1, "z2": z2, "z3": z3,
                                   "zweight": w, "maxIterations": n, "tolerance": tol},
                        "expected": {**v.enc(H.call("SphereCylinderOptimizer", *args)),
                                     "function": v.attempt(lambda a=args: H.call("sphereCylinderOptimizerClosest",
                                                                                 *a))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/optimization/spherecylinder.hpp", cases)
