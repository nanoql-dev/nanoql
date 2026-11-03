# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GaussianQuadMultidimIntegrator and MultidimIntegral
(ql/experimental/math/multidimquadrature.hpp, .cpp, multidimintegrator.hpp, .cpp), which the wheel doesn't expose, each
output computed by the C++ harness (tools/cxx/src/experimental/math/multidimquadrature.cpp) on the integrands of
tools/cxx/src/experimental/math/latentmodelspec.hpp: the Gauss-Hermite rule's scalar integrals in one to three
dimensions at orders 5 to 30 and two weight parameters, with its order; and the trapezoid rule's on boxes in one and
two dimensions at two accuracies, its failures to converge where they come. The vector integration loses its first
node's weight in QuantLib (held in Divergences/VectorQuadratureWeightTests.cs), so it isn't vectored; dimensions of 0
and above 15 read outside QuantLib's entries. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Math/MultidimQuadratureCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 38960
GH = [(1, 5, 0.0), (1, 25, 0.0), (1, 25, 0.5), (2, 10, 0.0), (2, 25, 0.0), (3, 8, 0.0)]
TRAPEZOID = [(1, -5.0, 5.0, 1e-4, 20), (1, -35.0, 35.0, 1e-6, 25), (2, -3.0, 3.0, 1e-3, 12), (2, 0.0, 1.0, 1e-5, 6)]
FUNCTIONS = ["one", "m0", "m0sq", "exp", "cos"]


def cases():
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        st = H.settings(R)
        return [{"name": "integrals",
                 "inputs": {"settings": st, "gaussHermite": [list(g) for g in GH],
                            "trapezoid": [list(t) for t in TRAPEZOID], "functions": FUNCTIONS},
                 "expected": {
                     "gaussHermite": [call("GaussianQuadMultidimIntegrator::integrate", d, o, mu, f)
                                      for d, o, mu in GH for f in FUNCTIONS],
                     "gaussHermiteM0M1": [call("GaussianQuadMultidimIntegrator::integrate", d, o, mu, "m0m1")
                                          for d, o, mu in GH if d > 1],
                     "trapezoid": [call("MultidimIntegral::integrate", d, a, b, acc, it, f)
                                   for d, a, b, acc, it in TRAPEZOID for f in FUNCTIONS]}}]


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/math/multidimquadrature.hpp", cases)
