# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's pseudoSqrt (ql/math/matrixutilities/pseudosqrt.hpp, .cpp): symmetricschurdecomposition.py's
matrices under each salvaging algorithm (None, Spectral, Hypersphere, LowerDiagonal, Higham, Principal), each the
pseudo square root's rows or QuantLib's refusal (None and Principal of a matrix with a negative eigenvalue). Then
the refusals of a matrix with no rows and of one that isn't square.

The hypersphere and lower-diagonal algorithms' results are left out where the matrix has a negative eigenvalue:
there they optimize with ConjugateGradient, whose returned point the port corrects to the last one its line search
accepted (Divergences/LineSearchReturnTests.cs); elsewhere they are the spectral root, kept. rankReducedSqrt, which
the wheel doesn't bind, is the harness's (cxx/math/matrixutilities/pseudosqrt). The wheel's pseudoSqrt(Matrix,
SalvagingAlgorithm) is the C++ function, SalvagingAlgorithm.NoAlgorithm its None. Read back by
tests/NanoQL.Tests/Vectors/Numerics/MatrixUtilities/PseudoSqrtVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import QuantLib as ql  # noqa: E402

import symmetricschurdecomposition as ssd  # noqa: E402
import vectors as v  # noqa: E402

# QuantLib's SalvagingAlgorithm::Type, by its numeric value
ALGORITHMS = [("None", ql.SalvagingAlgorithm.NoAlgorithm), ("Spectral", ql.SalvagingAlgorithm.Spectral),
              ("Hypersphere", ql.SalvagingAlgorithm.Hypersphere), ("LowerDiagonal", ql.SalvagingAlgorithm.LowerDiagonal),
              ("Higham", ql.SalvagingAlgorithm.Higham), ("Principal", ql.SalvagingAlgorithm.Principal)]


def cases():
    st = v.settings(ql.Date(22, ql.October, 2026))
    rnd = random.Random(v.SEED)
    out = []
    for name, m in ssd.matrices(rnd):
        negative = any(e < 0.0 for e in ql.SymmetricSchurDecomposition(ssd.qlm(m)).eigenvalues())
        for algo, sa in ALGORITHMS:
            if negative and algo in ("Hypersphere", "LowerDiagonal"):
                continue
            out.append({"name": f"{name}, {algo}", "inputs": {"settings": st, "matrix": m, "algorithm": int(sa)},
                        "expected": {"sqrt": v.attempt(lambda m=m, sa=sa: ssd.rows(ql.pseudoSqrt(ssd.qlm(m), sa)))}})
    for name, m in (("no rows", []), ("not square", [[1.0, 0.5, 0.2], [0.5, 1.0, 0.1]])):
        for algo, sa in ALGORITHMS:
            out.append({"name": f"{name}, {algo}", "inputs": {"settings": st, "matrix": m, "algorithm": int(sa)},
                        "expected": {"sqrt": v.attempt(lambda m=m, sa=sa: ssd.rows(ql.pseudoSqrt(ssd.qlm(m), sa)))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/pseudosqrt.hpp", cases)
