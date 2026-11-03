# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the triangular angles parametrizations and the Frobenius cost
(ql/math/matrixutilities/tapcorrelations.hpp, .cpp), which the wheel doesn't bind, each output computed by the C++
harness: the general parametrization at sizes 2 to 5 and ranks 1 to the size + 1 (whose count and loops are rank
size's), the LMM form, both on unconstrained parameters, the rank-three spiral and its vectorial form, and the Frobenius
cost of each parametrization against a correlation matrix. Ranks outside 1 to the size + 1, size 0 and short LMM angles,
where QuantLib reads or writes outside its arrays and nanoql refuses, are left out. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/MatrixUtilities/TapCorrelationsCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TARGET = [[1.0, 0.8, 0.6, 0.4], [0.8, 1.0, 0.7, 0.5], [0.6, 0.7, 1.0, 0.6], [0.4, 0.5, 0.6, 1.0]]


def cases():
    rng = random.Random(v.SEED)
    out = []
    with cxx.connect() as H:
        def case(name, method, args, inputs):
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            r = v.enc(H.call(method, *args))
            out.append({"name": name, "inputs": dict({"settings": st, "method": method}, **inputs),
                        "expected": r if method == "FrobeniusCostFunction" else {"result": r}})

        for n in range(2, 6):
            for rank in range(1, n + 2):
                count = (rank - 1) * (2 * n - rank) // 2
                angles = [rng.uniform(0.1, 3.0) for _ in range(count)]
                x = [rng.uniform(-2.0, 2.0) for _ in range(count)]
                for method, values in (("triangularAnglesParametrization", angles),
                                       ("triangularAnglesParametrizationUnconstrained", x)):
                    case(f"{method}, size {n}, rank {rank}", method, [values, n, rank],
                         {"values": v.enc(values), "matrixSize": n, "rank": rank})
            lmm = [rng.uniform(0.1, 3.0) for _ in range(n - 1)]
            for method in ("lmmTriangularAnglesParametrization", "lmmTriangularAnglesParametrizationUnconstrained"):
                case(f"{method}, size {n}", method, [lmm, n, n], {"values": v.enc(lmm), "matrixSize": n, "rank": n})
        for alpha, t0, epsilon, rows in ((0.5, 1.2, -0.3, 4), (2.0, 0.5, 0.1, 6)):
            case(f"rank three, {rows} rows", "triangularAnglesParametrizationRankThree", [alpha, t0, epsilon, rows],
                 {"alpha": alpha, "t0": t0, "epsilon": epsilon, "nbRows": rows})
            case(f"rank three vectorial, {rows} rows", "triangularAnglesParametrizationRankThreeVectorial",
                 [[alpha, t0, epsilon], rows], {"values": [alpha, t0, epsilon], "nbRows": rows})
        for f, rank, count in (("triangularAnglesParametrization", 4, 6), ("triangularAnglesParametrization", 2, 3),
                               ("triangularAnglesParametrizationUnconstrained", 3, 5),
                               ("lmmTriangularAnglesParametrization", 4, 3)):
            x = [rng.uniform(0.2, 2.5) for _ in range(count)]
            case(f"Frobenius cost, {f}, rank {rank}", "FrobeniusCostFunction", [TARGET, f, 4, rank, x],
                 {"target": TARGET, "parametrization": f, "matrixSize": 4, "rank": rank, "values": v.enc(x)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/matrixutilities/tapcorrelations.hpp", cases)
