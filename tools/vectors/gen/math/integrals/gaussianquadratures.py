# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the Gaussian quadratures the wheel exposes (ql/math/integrals/gaussianquadratures.hpp, .cpp, and through
them ql/math/integrals/gaussianorthogonalpolynomial.hpp, .cpp and ql/math/matrixutilities/tqreigendecomposition.hpp,
.cpp): for the Laguerre, Hermite, Jacobi, hyperbolic, Legendre, Chebyshev, Chebyshev second-kind and Gegenbauer
integrations over orders from 1 to 256 and their parameters, the order, the nodes (x), the weights and the weighted
sum (operator()) of three integrands per family; and the refusals of the polynomials' parameters and of order 0, where
QuantLib allocates an array of n - 1 elements (std::bad_array_new_length, which the wheel raises as RuntimeError).

The integrands are named here and in the reader: on [0, inf) exp(-x), x exp(-x) and 1/((1 + x)(1 + x)); on the real
line exp(-x x), x x exp(-x x) and 1/(1 + x x); for the hyperbolic weight 1/cosh(x) and x x/cosh(x); on [-1, 1] 1, x x
and cos(x). The weights carry 1/w(x), so each sum approximates the integrand's integral. The wheel's classes are SWIG
wrappers of the C++ classes of the same names, with no logic of their own; the integrators over [a, b]
(GaussLegendreIntegrator and its two siblings), MultiDimGaussianIntegration and TabulatedGaussLegendre aren't exposed.
Read back by tests/NanoQL.Tests/Vectors/Numerics/Integrals/GaussianQuadraturesVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

INTEGRANDS = {
    "exp(-x)": lambda x: math.exp(-x),
    "x exp(-x)": lambda x: x * math.exp(-x),
    "1/((1+x)(1+x))": lambda x: 1.0 / ((1.0 + x) * (1.0 + x)),
    "exp(-x x)": lambda x: math.exp(-x * x),
    "x x exp(-x x)": lambda x: x * x * math.exp(-x * x),
    "1/(1+x x)": lambda x: 1.0 / (1.0 + x * x),
    "1/cosh(x)": lambda x: 1.0 / math.cosh(x),
    "x x/cosh(x)": lambda x: x * x / math.cosh(x),
    "1": lambda x: 1.0,
    "x x": lambda x: x * x,
    "cos(x)": math.cos,
}
FAMILIES = {
    "Laguerre": ["exp(-x)", "x exp(-x)", "1/((1+x)(1+x))"],
    "Hermite": ["exp(-x x)", "x x exp(-x x)", "1/(1+x x)"],
    "Hyperbolic": ["1/cosh(x)", "x x/cosh(x)"],
    "Jacobi": ["1", "x x", "cos(x)"],
}


def quadrature(kind, n, params):
    return {"Laguerre": lambda: ql.GaussLaguerreIntegration(n, *params),
            "Hermite": lambda: ql.GaussHermiteIntegration(n, *params),
            "Jacobi": lambda: ql.GaussJacobiIntegration(n, *params),
            "Hyperbolic": lambda: ql.GaussHyperbolicIntegration(n),
            "Legendre": lambda: ql.GaussLegendreIntegration(n),
            "Chebyshev": lambda: ql.GaussChebyshevIntegration(n),
            "Chebyshev2nd": lambda: ql.GaussChebyshev2ndIntegration(n),
            "Gegenbauer": lambda: ql.GaussGegenbauerIntegration(n, *params)}[kind]()


def family(kind):
    return {"Laguerre": "Laguerre", "Hermite": "Hermite", "Hyperbolic": "Hyperbolic"}.get(kind, "Jacobi")


SPECS = ([("Laguerre", n, [0.0]) for n in (1, 2, 3, 8, 16, 64, 150, 192, 256)]
         + [("Laguerre", n, [s]) for n in (1, 3, 16) for s in (-0.5, 0.01, 1.0, 2.5)]
         + [("Hermite", n, [0.0]) for n in (1, 2, 3, 8, 16, 64, 200)]
         + [("Hermite", n, [mu]) for n in (1, 3, 16) for mu in (-0.25, 0.5, 0.9)]
         + [("Jacobi", n, [a, b]) for n in (1, 2, 5, 16, 50)
            for a, b in ((0.0, 0.0), (0.5, -0.5), (-0.9, 2.0), (1.5, 1.5))]
         + [("Hyperbolic", n, []) for n in (1, 2, 5, 16, 64, 200)]
         + [("Legendre", n, []) for n in (1, 2, 5, 16, 64, 130)]
         + [("Chebyshev", n, []) for n in (1, 2, 5, 16, 130)]
         + [("Chebyshev2nd", n, []) for n in (1, 2, 5, 16, 130)]
         + [("Gegenbauer", n, [lam]) for n in (1, 5, 50) for lam in (0.55, 1.0, 2.5)])
REFUSED = [("Laguerre", 4, [-1.0]), ("Hermite", 4, [-0.5]), ("Jacobi", 4, [-1.0, 0.5]), ("Jacobi", 4, [0.5, -1.0]),
           ("Jacobi", 4, [-0.99, -1.01]), ("Gegenbauer", 4, [-0.5]),
           ("Laguerre", 0, [0.0]), ("Hermite", 0, [0.0]), ("Jacobi", 0, [0.0, 0.0]), ("Hyperbolic", 0, []),
           ("Legendre", 0, []), ("Chebyshev", 0, []), ("Chebyshev2nd", 0, []), ("Gegenbauer", 0, [1.0])]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for kind, n, params in SPECS:
        q = quadrature(kind, n, params)
        names = FAMILIES[family(kind)]
        out.append({"name": f"{kind}({n}{''.join(', ' + repr(p) for p in params)})",
                    "inputs": {"settings": st, "kind": kind, "n": n, "params": params, "integrands": names},
                    "expected": {"order": q.order(), "x": list(q.x()), "weights": list(q.weights()),
                                 "sum": [q(INTEGRANDS[name]) for name in names]}})
    for kind, n, params in REFUSED:
        out.append({"name": f"refused {kind}({n}{''.join(', ' + repr(p) for p in params)})",
                    "inputs": {"settings": st, "kind": kind, "n": n, "params": params},
                    "expected": {"order": v.attempt(lambda: quadrature(kind, n, params).order())}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/integrals/gaussianquadratures.hpp", cases)
