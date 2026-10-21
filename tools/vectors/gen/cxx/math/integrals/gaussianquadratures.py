# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the Gaussian quadratures' members the wheel doesn't expose (ql/math/integrals/gaussianquadratures.hpp,
.cpp and gaussianorthogonalpolynomial.hpp, .cpp), each output computed by the C++ harness: the Gauss-Legendre,
Gauss-Chebyshev and second-kind Gauss-Chebyshev integrators over tools/vectors/gen/math/integrals/simpsonintegral.py's
named integrands and intervals, forward, reversed and empty, at orders 1, 2, 3, 5, 10 and 20, each the integral, the
number of evaluations (zero: QuantLib's integrators don't count) and the quadrature's order; and the orthogonal
polynomials' members, mu_0, alpha(n), beta(n), w(x), logW(x), value(n, x) and weightedValue(n, x), for n from 0 to 10 at
five points of each polynomial's domain, among them the members QuantLib refuses (a Jacobi beta(0) whose denominator
vanishes) and the constructors' refusals. A polynomial's parameters are the constructor's arguments, none for Legendre,
Chebyshev, Chebyshev2nd and Hyperbolic. Then the inspectors no QuantLib test reads: TabulatedGaussLegendre's order,
built at 6, 7, 12 and 20 and set to another order (5 and 9 refused), and MultiDimGaussianIntegration's weights and x
over Gauss-Hermite and Gauss-Legendre quadratures in one to three dimensions. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Integrals/GaussianQuadraturesCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


si = _load("simpsonintegral", "math/integrals/simpsonintegral.py")

INTEGRATORS = ["Legendre", "Chebyshev", "Chebyshev2nd"]
ORDERS = [1, 2, 3, 5, 10, 20]
JACOBI_POINTS = [-0.9, -0.3, 0.0, 0.4, 0.95]
# (kind, parameters, points)
POLYNOMIALS = [
    ("Laguerre", [0.0], [0.0, 0.5, 1.0, 3.0, 10.0]),
    ("Laguerre", [0.5], [0.0, 0.5, 1.0, 3.0, 10.0]),
    ("Laguerre", [-0.5], [0.1, 0.5, 1.0, 3.0, 10.0]),
    ("Laguerre", [2.0], [0.0, 0.5, 1.0, 3.0, 10.0]),
    ("Hermite", [0.0], [-2.0, -0.5, 0.0, 0.7, 3.0]),
    ("Hermite", [1.5], [-2.0, -0.5, 0.0, 0.7, 3.0]),
    ("Jacobi", [0.0, 0.0], JACOBI_POINTS),
    ("Jacobi", [0.5, -0.5], JACOBI_POINTS),
    ("Jacobi", [-0.5, -0.5], JACOBI_POINTS),
    ("Jacobi", [1.5, 2.5], JACOBI_POINTS),
    ("Legendre", [], JACOBI_POINTS),
    ("Chebyshev", [], JACOBI_POINTS),
    ("Chebyshev2nd", [], JACOBI_POINTS),
    ("Gegenbauer", [0.5], JACOBI_POINTS),
    ("Gegenbauer", [1.5], JACOBI_POINTS),
    ("Hyperbolic", [], [-3.0, -0.5, 0.0, 1.0, 4.0]),
]
REFUSED = [("Laguerre", [-1.0]), ("Hermite", [-0.5]), ("Jacobi", [-1.0, 0.5]), ("Jacobi", [0.5, -1.0]),
           ("Jacobi", [-0.9, -1.1]), ("Gegenbauer", [-0.5])]
MEMBERS = ["mu_0", "alpha", "beta", "w", "logW", "value", "weightedValue"]
DEGREES = list(range(11))
# TabulatedGaussLegendre built at n, then, where given, set to another order; 5 and 9 aren't tabulated
TABULATED = [(6, None), (7, None), (12, None), (20, None), (20, 7), (6, 12), (5, None), (20, 9)]
# MultiDimGaussianIntegration over each dimension's order
MULTI_DIM = [("Hermite", [3]), ("Hermite", [2, 3]), ("Legendre", [3, 2, 3]), ("Legendre", [1, 4])]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []
        for kind in INTEGRATORS:
            for n in ORDERS:
                rows = si.specs()
                out.append({"name": f"Gauss{kind}Integrator({n})",
                            "inputs": {"settings": st, "kind": kind, "n": n,
                                       "integrals": [{"f": name, "a": a, "b": b} for name, a, b in rows]},
                            "expected": {"result": [v.attempt(lambda name=name, a=a, b=b: H.call(
                                "GaussianQuadratureIntegrator", kind, n, name, a, b)) for name, a, b in rows]}})
        for kind, params, xs in POLYNOMIALS + [(k, p, [0.5]) for k, p in REFUSED]:
            pairs = [(n, x) for n in DEGREES for x in xs]
            out.append({"name": f"{kind} {params!r}",
                        "inputs": {"settings": st, "kind": kind, "parameters": params, "n": [n for n, _ in pairs],
                                   "x": [x for _, x in pairs]},
                        "expected": {m: [v.attempt(lambda m=m, n=n, x=x: H.call(
                            "GaussianOrthogonalPolynomial", kind, params, m, n, x)) for n, x in pairs]
                                     for m in MEMBERS}})
        for n, other in TABULATED:
            name = f"TabulatedGaussLegendre({n})" + ("" if other is None else f".order({other})")
            out.append({"name": name, "inputs": {"settings": st, "kind": "TabulatedGaussLegendre", "n": n,
                                                 "other": other},
                        "expected": {"order": v.attempt(lambda n=n, other=other: H.call(
                            "TabulatedGaussLegendre::order", n, None if other is None else float(other)))}})
        for kind, ns in MULTI_DIM:
            r = H.call("MultiDimGaussianIntegration", kind, ns)
            out.append({"name": f"MultiDimGaussianIntegration, Gauss-{kind}, {ns}",
                        "inputs": {"settings": st, "kind": f"MultiDim{kind}", "ns": ns},
                        "expected": {"weights": r["weights"], "x": r["x"]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/integrals/gaussianquadratures.hpp", cases)
