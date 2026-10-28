# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for GaussLaguerreCosinePolynomial and GaussLaguerreSinePolynomial
(ql/math/integrals/gausslaguerrecosinepolynomial.hpp) on MomentBasedGaussianPolynomial
(ql/math/integrals/momentbasedgaussianpolynomial.hpp), which the wheel doesn't bind, each output computed by the C++
harness: at u of 0.05 to 5, the first 12 moments and recurrence coefficients, the weight function at points from 0 to
20, and the Gaussian quadrature's nodes and weights at orders 2, 5, 8 and 12. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Integrals/GaussLaguerreCosinePolynomialCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

US = [0.05, 0.2, 1.0, 2.5, 5.0]
ORDERS = [2, 5, 8, 12]
MOMENTS = 12
XS = [0.0, 0.1, 1.0, 3.7, 20.0]
FIELDS = ("moment", "alpha", "beta", "w", "nodes", "weights")


def cases():
    out = []
    with cxx.connect() as H:
        for kind in ("Cosine", "Sine"):
            for u in US:
                for order in ORDERS:
                    st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
                    r = H.call("GaussLaguerreTrigonometricPolynomial", kind, u, MOMENTS, order, XS)
                    out.append({"name": f"{kind}, u {u}, order {order}",
                                "inputs": {"settings": st, "kind": kind, "u": u, "moments": MOMENTS, "order": order,
                                           "x": XS},
                                "expected": {k: v.enc(r[k]) for k in FIELDS}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/integrals/gausslaguerrecosinepolynomial.hpp", cases)
