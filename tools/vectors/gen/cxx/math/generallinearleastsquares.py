# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GeneralLinearLeastSquares (ql/math/generallinearleastsquares.hpp), which the wheel doesn't
expose, each output computed by the C++ harness (docs/porting-playbook.md §9.4), on the Longstaff-Schwartz basis
systems (ql/methods/montecarlo/lsmbasissystem.hpp).

One-factor fits of noisy polynomials, on the monomials of orders 1 to 4 and the Laguerre and Hermite polynomials,
over 2 to 40 samples, an exact fit (as many samples as functions) and a rank-deficient one among them; two- and
three-factor fits on the multi-factor monomials; each with its coefficients, residuals, modelling uncertainty, size
and dimension, and its standard errors where the basis has two functions. QuantLib's standard errors take n − 2
degrees of freedom whatever the basis, which nanoql replaces by n − p (tests/NanoQL.Tests/Divergences/
LeastSquaresStandardErrorTests.cs), so they are left out for other bases. Then the refusal of fewer samples than
functions. The harness's methods are tools/cxx/src/math/generallinearleastsquares.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/GeneralLinearLeastSquaresCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

MONOMIAL, LAGUERRE, HERMITE = 0, 1, 2
OUTPUTS = ["coefficients", "residuals", "standardErrors", "error", "size", "dim"]


def expected(fit, functions):
    return {k: v.enc(fit[k]) for k in OUTPUTS if k != "standardErrors" or functions == 2}


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        rnd = random.Random(v.SEED)
        out = []
        for name, n, order, kind in [("line, 10 samples", 10, 1, MONOMIAL), ("line, 2 samples", 2, 1, MONOMIAL),
                                     ("line, 40 samples", 40, 1, MONOMIAL), ("quadratic, 7 samples", 7, 2, MONOMIAL),
                                     ("quadratic, 3 samples", 3, 2, MONOMIAL), ("cubic, 30 samples", 30, 3, MONOMIAL),
                                     ("quartic, 25 samples", 25, 4, MONOMIAL), ("Laguerre, 20 samples", 20, 2, LAGUERRE),
                                     ("Hermite, 20 samples", 20, 3, HERMITE), ("Laguerre line", 12, 1, LAGUERRE)]:
            xs = [rnd.uniform(0.0, 3.0) for _ in range(n)]
            c = [rnd.uniform(-2.0, 2.0) for _ in range(order + 1)]
            ys = [sum(c[k] * x ** k for k in range(order + 1)) + rnd.gauss(0.0, 0.1) for x in xs]
            fit = H.call("GeneralLinearLeastSquares(Real)", xs, ys, order, kind)
            out.append({"name": name,
                        "inputs": {"settings": st, "xs": xs, "ys": ys, "order": order, "polynomialType": kind},
                        "expected": expected(fit, order + 1)})
        # rank-deficient: every sample at one point
        xs, ys = [0.7] * 6, [rnd.uniform(0.0, 1.0) for _ in range(6)]
        fit = H.call("GeneralLinearLeastSquares(Real)", xs, ys, 2, MONOMIAL)
        out.append({"name": "rank-deficient", "inputs": {"settings": st, "xs": xs, "ys": ys, "order": 2,
                                                          "polynomialType": MONOMIAL},
                    "expected": expected(fit, 3)})
        for dim, n, order in [(2, 20, 2), (3, 30, 1), (2, 15, 1)]:
            points = [[rnd.uniform(0.5, 1.5) for _ in range(dim)] for _ in range(n)]
            ys = [sum(p) ** 2 + rnd.gauss(0.0, 0.05) for p in points]
            fit = H.call("GeneralLinearLeastSquares(Array)", points, ys, dim, order, MONOMIAL)
            out.append({"name": f"{dim} factors, order {order}, {n} samples",
                        "inputs": {"settings": st, "points": points, "ys": ys, "dim": dim, "order": order,
                                   "polynomialType": MONOMIAL},
                        "expected": expected(fit, fit["dim"])})
        out.append({"name": "too few samples",
                    "inputs": {"settings": st, "xs": [0.5, 1.5], "ys": [1.0, 2.0], "order": 2,
                               "polynomialType": MONOMIAL},
                    "expected": {"coefficients": v.attempt(
                        lambda: H.call("GeneralLinearLeastSquares(Real)", [0.5, 1.5], [1.0, 2.0], 2, MONOMIAL))}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/generallinearleastsquares.hpp", cases)
