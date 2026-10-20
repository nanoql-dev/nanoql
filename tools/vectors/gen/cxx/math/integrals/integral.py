# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the integrators' settings (ql/math/integrals/integral.hpp, .cpp), which the wheel doesn't expose, each
output computed by the C++ harness: absoluteAccuracy and maxEvaluations as constructed and after setAbsoluteAccuracy
and setMaxEvaluations, for Simpson, both trapezoid rules and Gauss-Lobatto at three settings and three new ones (a new
accuracy of zero among them, which QuantLib's setter takes unchecked), and for the Gauss-Legendre, Gauss-Chebyshev and
second-kind Gauss-Chebyshev integrators, which pass Null<Real>() as their accuracy and their order as their maximum;
and the refusal of an accuracy not above machine epsilon. A case's accuracy is null for the Gaussian integrators, which
take none. Read back by tests/NanoQL.Tests/Vectors/Cxx/Numerics/Integrals/IntegralCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

ADAPTIVE = ["Simpson", "TrapezoidDefault", "TrapezoidMidPoint", "GaussLobatto"]
GAUSSIAN = ["GaussLegendre", "GaussChebyshev", "GaussChebyshev2nd"]
# (accuracy, maxEvaluations, new accuracy, new maxEvaluations)
SETTINGS = [(1e-6, 100, 1e-8, 50), (1e-10, 7, 1e-4, 1000), (0.5, 0, 0.0, 3)]
REFUSED = [(1e-17, 100, 1e-6, 100), (0.0, 100, 1e-6, 100), (-1e-6, 100, 1e-6, 100)]
ORDERS = [(1, 5), (8, 20), (64, 0)]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def row(kind, s):
            return v.attempt(lambda: H.call("Integrator::settings", kind, *s))

        out = []
        for kind in ADAPTIVE:
            rows = SETTINGS + REFUSED
            out.append({"name": kind,
                        "inputs": {"settings": st, "kind": kind, "accuracy": [s[0] for s in rows],
                                   "maxEvaluations": [s[1] for s in rows], "newAccuracy": [s[2] for s in rows],
                                   "newMaxEvaluations": [s[3] for s in rows]},
                        "expected": {"settings": [row(kind, s) for s in rows]}})
        for kind in GAUSSIAN:
            rows = [(None, n, 1e-6, m) for n, m in ORDERS]
            out.append({"name": kind,
                        "inputs": {"settings": st, "kind": kind, "accuracy": None,
                                   "maxEvaluations": [s[1] for s in rows], "newAccuracy": [s[2] for s in rows],
                                   "newMaxEvaluations": [s[3] for s in rows]},
                        "expected": {"settings": [row(kind, s) for s in rows]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/integrals/integral.hpp", cases)
