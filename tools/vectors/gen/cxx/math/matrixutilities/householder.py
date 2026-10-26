# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's HouseholderTransformation and HouseholderReflection (ql/math/matrixutilities/householder.hpp,
.cpp), which the wheel doesn't expose, each output computed by the C++ harness (docs/porting-playbook.md §9.4): the
transformation of seeded vectors by seeded vectors in one to six dimensions (and by the zero vector, the identity) and
its matrix; the reflection onto unit vectors (the axes and seeded ones) of seeded vectors on e's side, of vectors near e
(the series branch), of multiples of e (the zero vector) and of the zero vector (refused), its value and its vector.

The zero vector's matrix isn't asked, NaN in QuantLib and the identity in nanoql
(tests/NanoQL.Tests/Divergences/HouseholderZeroVectorTests.cs), nor the reflection of a vector on the side opposite e,
which QuantLib's near-parallel branches don't map to |a|e (tests/NanoQL.Tests/Divergences/HouseholderOppositeTests.cs).

The harness's calls are tools/cxx/src/math/matrixutilities/householder.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Math/MatrixUtilities/HouseholderCxxVectorTests.cs."""
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


def unit(x):
    n = math.sqrt(sum(xi * xi for xi in x))
    return [xi / n for xi in x]


def cases():
    rng = random.Random(20261025)
    out = []
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        for n in range(1, 7):
            vec = [rng.uniform(-0.5, 0.5) for _ in range(n)]
            x = [rng.uniform(-1.0, 1.0) for _ in range(n)]
            out.append({"name": f"transformation, {n} dimensions", "inputs": {"settings": st, "v": vec, "x": x},
                        "expected": {"value": H.call("HouseholderTransformation::operator()", vec, x),
                                     "matrix": H.call("HouseholderTransformation::getMatrix", vec)}})
        out.append({"name": "transformation by the zero vector",
                    "inputs": {"settings": st, "v": [0.0, 0.0, 0.0], "x": [0.3, -0.2, 0.9]},
                    "expected": {"value": H.call("HouseholderTransformation::operator()", [0.0, 0.0, 0.0],
                                                 [0.3, -0.2, 0.9])}})
        reflections = []
        for n in (2, 3, 5):
            for axis in range(n):
                e = [1.0 if i == axis else 0.0 for i in range(n)]
                a = [rng.uniform(-1.0, 1.0) for _ in range(n)]
                a[axis] = abs(a[axis]) + 0.1
                reflections.append((f"axis {axis} of {n}, seeded", e, a))
            e = unit([rng.uniform(-1.0, 1.0) for _ in range(n)])
            a = [ei + rng.uniform(-0.3, 0.3) for ei in e]
            reflections.append((f"seeded unit vector of {n}", e, a))
            reflections.append((f"near e, {n} dimensions", e, [3.0 * ei + 1e-3 * (i + 1) for i, ei in enumerate(e)]))
            reflections.append((f"a multiple of e, {n} dimensions", e, [2.5 * ei for ei in e]))
        for name, e, a in reflections:
            out.append({"name": "reflection, " + name, "inputs": {"settings": st, "e": e, "a": a},
                        "expected": {"value": H.call("HouseholderReflection::operator()", e, a),
                                     "reflectionVector": H.call("HouseholderReflection::reflectionVector", e, a)}})
        out.append({"name": "reflection of the zero vector (refused)",
                    "inputs": {"settings": st, "e": [1.0, 0.0], "a": [0.0, 0.0]},
                    "expected": {"value": v.attempt(lambda: H.call("HouseholderReflection::operator()", [1.0, 0.0],
                                                                   [0.0, 0.0])),
                                 "reflectionVector": v.attempt(lambda: H.call(
                                     "HouseholderReflection::reflectionVector", [1.0, 0.0], [0.0, 0.0]))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/householder.hpp", cases)
