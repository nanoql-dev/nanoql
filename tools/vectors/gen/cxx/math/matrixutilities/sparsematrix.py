# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SparseMatrix += (ql/math/matrixutilities/sparsematrix.hpp, uBLAS's compressed_matrix), which
the wheel doesn't bind, each output computed by the C++ harness (docs/porting-playbook.md §9.4): a += b on matrices
built element by element, the first matrix's storage after it (filled1, the rows' starts, the stored elements' columns
and values): disjoint and overlapping elements, a sum that cancels to zero, explicit zeros, empty matrices, elements
written out of order, and a seeded 8 x 6 pair.

The harness's call is tools/cxx/src/math/matrixutilities/sparsematrix.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/MatrixUtilities/SparseMatrixCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def seeded(rng, size1, size2, n):
    return [[rng.randrange(size1), rng.randrange(size2), rng.uniform(-2.0, 2.0)] for _ in range(n)]


def cases():
    rng = random.Random(20261101)
    pairs = [
        ("disjoint elements", 3, 3, [[0, 0, 1.0], [2, 1, 2.0]], [[1, 1, 3.0], [2, 2, 4.0]]),
        ("overlapping elements", 3, 4, [[0, 1, 1.5], [1, 2, -2.0]], [[0, 1, 0.5], [1, 0, 1.0], [2, 3, 7.0]]),
        ("a sum that cancels", 2, 2, [[0, 0, 1.0], [1, 1, 2.0]], [[0, 0, -1.0]]),
        ("explicit zeros", 3, 3, [[0, 0, 0.0], [1, 2, 5.0]], [[2, 2, 0.0]]),
        ("an empty first matrix", 3, 3, [], [[1, 0, 2.5], [2, 2, -1.0]]),
        ("both empty", 4, 2, [], []),
        ("elements out of order", 4, 4, [[3, 3, 1.0], [0, 2, 2.0], [2, 0, 3.0], [0, 1, 4.0]], [[1, 1, 5.0], [0, 0, 6.0]]),
        ("seeded 8 x 6", 8, 6, seeded(rng, 8, 6, 14), seeded(rng, 8, 6, 10)),
    ]
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []
        for name, size1, size2, a, b in pairs:
            r = H.call("SparseMatrix::plusAssign", size1, size2, a, b)
            out.append({"name": name, "inputs": {"settings": st, "size1": size1, "size2": size2, "a": a, "b": b},
                        "expected": {"filled1": r["filled1"], "index1": r["index1"], "index2": r["index2"],
                                     "values": r["values"]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/sparsematrix.hpp", cases)
