# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LagrangeInterpolation's value(y, x) (ql/math/interpolations/lagrangeinterpolation.hpp), the
interpolation of other y values on the same nodes, which the wheel doesn't expose, each output computed by the C++
harness: tools/vectors/gen/math/interpolations/lagrangeinterpolation.py's node sets, each with a second set of y
values, read at its nodes, near them, between them and past its ends (value(y, x) checks no range); on the evaluation
date 15 October 2026.

The outputs nanoql fixes are left out (the S-van plan's VR4 (xxviii)): x = 0 where 0 is a node, which QuantLib's node
test misses (tests/NanoQL.Tests/Divergences/LagrangeZeroNodeTests.cs). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Interpolations/LagrangeInterpolationCxxVectorTests.cs."""
import importlib.util
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

_spec = importlib.util.spec_from_file_location(
    "wheel_lagrangeinterpolation", os.path.join(v.GEN_ROOT, "math", "interpolations", "lagrangeinterpolation.py"))
wheel = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(wheel)

R = 46310


def cases():
    rnd = random.Random(v.SEED)
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for name, xs, ys in wheel.node_sets(rnd):
            other = [math.cos(3.0 * x) + 0.5 * x for x in xs]
            points = [x for x, _ in wheel.sample(rnd, xs)]
            out.append({"name": name,
                        "inputs": {"settings": st, "x": xs, "y": ys, "otherY": other, "points": points},
                        "expected": {"value": v.attempt(lambda: H.call("LagrangeInterpolation::value", xs, ys, other,
                                                                       points))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/lagrangeinterpolation.hpp", cases)
