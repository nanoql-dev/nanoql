# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BasisIncompleteOrdered and OrthogonalProjections
(ql/math/matrixutilities/basisincompleteordered.hpp, .cpp), which the wheel doesn't expose, each output computed by the
C++ harness.

The basis: seeded vectors in one to six dimensions, more vectors than the dimension (the later ones refused), a
dependent vector, a zero vector and a vector of the wrong size (refused): each addVector's result, the basis size and
dimension, and the basis as rows. The projections: seeded rows of five to twelve vectors in up to forty dimensions at
multiplier cut-offs 100, 2 and 1.2 (some vectors then invalid) and tolerance 1e-6, a dependent row (invalid): each
vector's validity, the count valid and each projection (an invalid vector's the last valid one's before it, zeros before
any, QuantLib's design).

The harness unit is tools/cxx/src/math/matrixutilities/basisincompleteordered.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Math/MatrixUtilities/BasisIncompleteOrderedCxxVectorTests.cs."""
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
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for n in range(1, 7):
            vectors = [[rng.uniform(-1.0, 1.0) for _ in range(n)] for _ in range(n + 2)]
            out.append({"name": f"basis, {n} dimensions, {n + 2} vectors",
                        "inputs": {"settings": st, "call": "basis", "dimension": n, "vectors": vectors},
                        "expected": v.enc(H.call("BasisIncompleteOrdered", n, vectors))})
        x = [rng.uniform(-1.0, 1.0) for _ in range(4)]
        y = [rng.uniform(-1.0, 1.0) for _ in range(4)]
        vectors = [x, y, [2.0 * a - 0.5 * b for a, b in zip(x, y)], [0.0] * 4, [rng.uniform(-1.0, 1.0) for _ in range(4)]]
        out.append({"name": "basis, a dependent and a zero vector",
                    "inputs": {"settings": st, "call": "basis", "dimension": 4, "vectors": vectors},
                    "expected": v.enc(H.call("BasisIncompleteOrdered", 4, vectors))})
        out.append({"name": "basis, a vector of the wrong size (refused)",
                    "inputs": {"settings": st, "call": "basis", "dimension": 3, "vectors": [[1.0, 0.0]]},
                    "expected": {"throws": v.attempt(lambda: H.call("BasisIncompleteOrdered", 3, [[1.0, 0.0]]))}})
        for k, (m, d, cut) in enumerate([(5, 8, 100.0), (8, 20, 100.0), (12, 40, 100.0), (6, 6, 2.0), (10, 12, 1.2),
                                         (7, 9, 1.5)]):
            rows = [[rng.uniform(0.0, 1.0) for _ in range(d)] for _ in range(m)]
            out.append({"name": f"projections, {m} vectors in {d} dimensions, cut-off {cut}",
                        "inputs": {"settings": st, "call": "projections", "vectors": rows, "multiplierCutOff": cut,
                                   "tolerance": 1e-6},
                        "expected": v.enc(H.call("OrthogonalProjections", rows, cut, 1e-6))})
        rows = [[rng.uniform(0.0, 1.0) for _ in range(6)] for _ in range(4)]
        rows.insert(2, [a + b for a, b in zip(rows[0], rows[1])])
        out.append({"name": "projections, a dependent row",
                    "inputs": {"settings": st, "call": "projections", "vectors": rows, "multiplierCutOff": 100.0,
                               "tolerance": 1e-6},
                    "expected": v.enc(H.call("OrthogonalProjections", rows, 100.0, 1e-6))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/basisincompleteordered.hpp", cases)
