# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SVD (ql/math/matrixutilities/svd.hpp, .cpp): the members the wheel doesn't expose, each
output computed by the C++ harness (docs/porting-playbook.md §9.4). On the matrices of the wheel's generator
(tools/vectors/gen/math/matrixutilities/svd.py): norm2, cond, rank, and solveFor on two right-hand sides of the matrix's
rows. A matrix is the list of its rows (tools/cxx/src/math/matrixutilities/svd.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/MatrixUtilities/SVDCxxVectorTests.cs."""
import importlib.util
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("svd", os.path.join(v.GEN_ROOT, "math", "matrixutilities", "svd.py"))
svd = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(svd)


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        rnd = random.Random(v.SEED)
        matrices = svd.matrices(rnd)
        rnd = random.Random(v.SEED + 1)
        out = []
        for name, rows in matrices:
            bs = [[rnd.uniform(-3.0, 3.0) for _ in range(len(rows))] for _ in range(2)]
            out.append({"name": name,
                        "inputs": {"settings": st, "matrix": rows, "b": bs},
                        "expected": {"norm2": v.enc(H.call("SVD::norm2", rows)),
                                     "cond": v.enc(H.call("SVD::cond", rows)),
                                     "rank": H.call("SVD::rank", rows),
                                     "solveFor": [v.enc(H.call("SVD::solveFor", rows, b)) for b in bs]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/svd.hpp", cases)
