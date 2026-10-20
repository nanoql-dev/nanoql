# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for TqrEigenDecomposition (ql/math/matrixutilities/tqreigendecomposition.hpp, .cpp), which the wheel doesn't
expose, each output computed by the C++ harness: the eigenvalues, the eigenvectors and the iteration count of seeded
random symmetric tridiagonal matrices of sizes 1 to 12, of a matrix already split by a zero off-diagonal, of one with a
repeated eigenvalue, of one whose first QR step takes the underflow recovery (an off-diagonal element whose square
underflows) and of the Gauss-Legendre and Gauss-Hermite Jacobi matrices of order 8, under each eigenvector calculation
(WithEigenVector, WithoutEigenVector, OnlyFirstRowEigenVector) and each shift strategy (NoShift, Overrelaxation,
CloseEigenValue), the enums as QuantLib's integers, except these three under NoShift, where QuantLib's unshifted
iteration never returns: the Jacobi matrices' eigenvalues come in pairs of opposite sign, and the underflow recovery
leaves the state as it was; and the refusal of an off-diagonal whose size isn't the diagonal's less one ("Wrong
dimensions"), an empty diagonal among them. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/MatrixUtilities/TqrEigenDecompositionCxxVectorTests.cs."""
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

SIZES = [1, 2, 3, 4, 6, 9, 12]


def matrices():
    """(name, diagonal, off-diagonal)."""
    rng = random.Random(20261020)
    out = []
    for n in SIZES:
        out.append((f"random {n}", [rng.uniform(-3.0, 3.0) for _ in range(n)],
                    [rng.uniform(-1.0, 1.0) for _ in range(n - 1)]))
    out.append(("split", [1.0, 2.0, 3.0, 4.0, 5.0], [0.5, 0.0, 0.7, -0.2]))
    out.append(("repeated eigenvalue", [2.0, 2.0, 2.0, 2.0], [0.0, 1.0, 0.0]))
    out.append(("underflow", [0.0, 0.0, 1e-160], [1e-170, 3.0]))
    out.append(("Gauss-Legendre 8", [0.0] * 8, [i / math.sqrt(4.0 * i * i - 1.0) for i in range(1, 8)]))
    out.append(("Gauss-Hermite 8", [0.0] * 8, [math.sqrt(i / 2.0) for i in range(1, 8)]))
    return out


REFUSED = [("empty", [], []), ("off-diagonal too long", [1.0, 2.0, 3.0], [0.5, 0.5, 0.5]),
           ("off-diagonal too short", [1.0, 2.0, 3.0], [0.5])]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []
        for name, diag, sub in matrices() + REFUSED:
            # QuantLib's unshifted iteration never returns on the Jacobi matrices' paired eigenvalues, nor where the
            # underflow recovery leaves the state as it was
            strategies = range(1, 3) if name.startswith("Gauss-") or name == "underflow" else range(3)
            combos = [(calc, strategy) for calc in range(3) for strategy in strategies]
            out.append({"name": name,
                        "inputs": {"settings": st, "diag": diag, "sub": sub, "calc": [c for c, _ in combos],
                                   "strategy": [s for _, s in combos]},
                        "expected": {"result": [v.attempt(lambda c=c, s=s: H.call(
                            "TqrEigenDecomposition", diag, sub, c, s)) for c, s in combos]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/tqreigendecomposition.hpp", cases)
