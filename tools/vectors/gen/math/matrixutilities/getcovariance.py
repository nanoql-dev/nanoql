# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's getCovariance (ql/math/matrixutilities/getcovariance.hpp, .cpp), on the evaluation date 15
October 2026, which nothing reads: the covariance matrix from standard deviations and a correlation matrix, its
symmetric part, at the default tolerance of 1e-12, in one to five dimensions; a correlation off symmetry within the
tolerance (its two halves averaged); and the refusals of a size mismatch, a non-square matrix, an asymmetry beyond the
tolerance and a diagonal other than 1.

The wheel's call, by QuantLib-SWIG 1.44's linearalgebra.i: getCovariance(volatilities, correlations), the C++ template
on the Array's iterators with its default tolerance. The covariance decomposition, which the wheel doesn't expose, is
tools/vectors/gen/cxx/math/matrixutilities/getcovariance.py's. Read back by
tests/NanoQL.Tests/Vectors/Numerics/MatrixUtilities/GetCovarianceVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def correlation(n, rng):
    """A random correlation matrix: the normalized Gram matrix of random vectors."""
    rows = [[rng.uniform(-1.0, 1.0) for _ in range(n)] for _ in range(n)]
    g = [[sum(a * b for a, b in zip(rows[i], rows[j])) for j in range(n)] for i in range(n)]
    return [[g[i][j] / (g[i][i] * g[j][j]) ** 0.5 if i != j else 1.0 for j in range(n)] for i in range(n)]


def covariance(vols, corr):
    m = ql.getCovariance(ql.Array(vols), ql.Matrix(corr))
    return v.enc([m[i][j] for i in range(m.rows()) for j in range(m.columns())])


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    rng = random.Random(20261025)
    out = []
    for n in range(1, 6):
        vols = [rng.uniform(0.05, 0.6) for _ in range(n)]
        corr = correlation(n, rng)
        out.append({"name": f"{n} dimensions", "inputs": {"settings": st, "stdDev": vols, "corr": corr},
                    "expected": {"covariance": covariance(vols, corr)}})
    vols = [0.2, 0.3, 0.4]
    corr = [[1.0, 0.5 + 4e-13, -0.2], [0.5, 1.0, 0.1], [-0.2, 0.1 - 6e-13, 1.0 + 5e-13]]
    out.append({"name": "asymmetry and diagonal within the tolerance",
                "inputs": {"settings": st, "stdDev": vols, "corr": corr},
                "expected": {"covariance": covariance(vols, corr)}})
    refused = [("size mismatch (refused)", [0.2, 0.3], [[1.0, 0.5, 0.1], [0.5, 1.0, 0.2], [0.1, 0.2, 1.0]]),
               ("not square (refused)", [0.2, 0.3], [[1.0, 0.5, 0.1], [0.5, 1.0, 0.2]]),
               ("not symmetric (refused)", [0.2, 0.3], [[1.0, 0.5], [0.4, 1.0]]),
               ("diagonal not 1 (refused)", [0.2, 0.3], [[1.0, 0.5], [0.5, 0.99]])]
    for name, vols, corr in refused:
        out.append({"name": name, "inputs": {"settings": st, "stdDev": vols, "corr": corr},
                    "expected": {"covariance": v.attempt(lambda: covariance(vols, corr))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/getcovariance.hpp", cases)
