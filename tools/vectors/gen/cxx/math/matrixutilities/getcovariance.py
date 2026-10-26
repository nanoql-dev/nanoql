# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CovarianceDecomposition (ql/math/matrixutilities/getcovariance.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness (docs/porting-playbook.md §9.4): the variances, standard
deviations and correlation matrix of covariances in one to five dimensions (getCovariance's of seeded deviations and
correlations), of one off symmetry within the tolerance, and the refusals of a non-square matrix and of an asymmetry
beyond the tolerance.

The harness's call is tools/cxx/src/math/matrixutilities/getcovariance.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Math/MatrixUtilities/GetCovarianceCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def covariance(n, rng):
    rows = [[rng.uniform(-1.0, 1.0) for _ in range(n)] for _ in range(n)]
    return [[sum(a * b for a, b in zip(rows[i], rows[j])) for j in range(n)] for i in range(n)]


def cases():
    rng = random.Random(20261025)
    out = []
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        setups = [(f"{n} dimensions", covariance(n, rng), 1e-12) for n in range(1, 6)]
        setups.append(("off symmetry within the tolerance", [[0.04, 0.012 + 3e-13], [0.012, 0.09]], 1e-12))
        setups.append(("a wider tolerance", [[0.04, 0.0125], [0.012, 0.09]], 1e-3))
        setups.append(("not square (refused)", [[0.04, 0.01, 0.0], [0.01, 0.09, 0.0]], 1e-12))
        setups.append(("not symmetric (refused)", [[0.04, 0.0125], [0.012, 0.09]], 1e-12))
        for name, cov, tol in setups:
            res = v.attempt(lambda: H.call("CovarianceDecomposition", cov, tol))
            expected = res
            if res == v.THROWS:
                expected = {"variances": v.THROWS, "standardDeviations": v.THROWS, "correlationMatrix": v.THROWS}
            out.append({"name": name, "inputs": {"settings": st, "covariance": cov, "tolerance": tol},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/getcovariance.hpp", cases)
