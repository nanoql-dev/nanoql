# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's rankReducedSqrt (ql/math/matrixutilities/pseudosqrt.hpp, .cpp), which the wheel doesn't
expose, each output computed by the C++ harness: symmetricschurdecomposition.py's matrices under the None, Spectral
and Higham salvaging algorithms, at maximum ranks 1, 3 and 100 and retained percentages 0.5, 0.9 and 1.0 (QuantLib's
1.1 factor), each the result's rows or QuantLib's refusal (None of a matrix with a negative eigenvalue);
then the refusals of the other algorithms, of a percentage of 0 or above 1, of a rank of 0, of a matrix with no rows
and of one that isn't square.

Left out, generated: the three roots whose retained eigenvalues include a negative one (Higham's adjusted matrices of
the "negative eigenvalue" matrix at ranks 3 and 100 and of Higham's M5 at rank 100, all at 1.0), where QuantLib takes
its square root, a NaN column, and nanoql floors it at 0
(tests/NanoQL.Tests/Divergences/RankReducedSqrtNegativeEigenvalueTests.cs). Added for that check: the "negative
eigenvalue" matrix under Higham at rank 2 and 1.0, the rank that keeps its non-negative eigenvalues.
tests/NanoQL.Tests/Vectors/Cxx/Numerics/MatrixUtilities/PseudoSqrtCxxVectorTests.cs reads it back."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.join(_dir, "gen", "math", "matrixutilities"))

import cxx  # noqa: E402
import symmetricschurdecomposition as ssd  # noqa: E402
import vectors as v  # noqa: E402

# SalvagingAlgorithm::Type
NONE, SPECTRAL, HYPERSPHERE, LOWERDIAGONAL, HIGHAM, PRINCIPAL = range(6)
RANKS = [1, 3, 100]
PERCENTAGES = [0.5, 0.9, 1.0]
# The roots with a negative retained eigenvalue, generated and left out.
LEFT_OUT = {"negative eigenvalue, Higham, rank 3, 1.0", "negative eigenvalue, Higham, rank 100, 1.0",
            "Higham's M5, Higham, rank 100, 1.0"}


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 22, 10, 2026))
        rnd = random.Random(v.SEED)
        out = []

        def case(name, m, rank, pct, sa):
            return {"name": name,
                    "inputs": {"settings": st, "matrix": m, "maxRank": rank, "percentage": pct, "algorithm": sa},
                    "expected": {"sqrt": v.attempt(lambda: H.call("rankReducedSqrt", m, rank, pct, sa))}}

        for name, m in ssd.matrices(rnd):
            for sa, algo in ((NONE, "None"), (SPECTRAL, "Spectral"), (HIGHAM, "Higham")):
                for rank in RANKS:
                    for pct in PERCENTAGES:
                        c = case(f"{name}, {algo}, rank {rank}, {pct!r}", m, rank, pct, sa)
                        if c["name"] not in LEFT_OUT:
                            out.append(c)
                if name == "negative eigenvalue" and sa == HIGHAM:
                    out.append(case(f"{name}, {algo}, rank 2, 1.0", m, 2, 1.0, sa))
        m = ssd.correlation(3)
        for sa, algo in ((HYPERSPHERE, "Hypersphere"), (LOWERDIAGONAL, "LowerDiagonal"), (PRINCIPAL, "Principal")):
            out.append(case(f"{algo} refused", m, 2, 0.9, sa))
        out.append(case("no percentage", m, 2, 0.0, SPECTRAL))
        out.append(case("percentage above 1", m, 2, 1.5, SPECTRAL))
        out.append(case("rank 0", m, 0, 0.9, SPECTRAL))
        out.append(case("no rows", [], 2, 0.9, SPECTRAL))
        out.append(case("not square", [[1.0, 0.5, 0.2], [0.5, 1.0, 0.1]], 2, 0.9, SPECTRAL))
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/pseudosqrt.hpp", cases)
