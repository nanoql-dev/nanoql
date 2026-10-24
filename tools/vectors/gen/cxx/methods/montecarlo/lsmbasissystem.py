# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LsmBasisSystem (ql/methods/montecarlo/lsmbasissystem.hpp, .cpp): the Longstaff-Schwartz
basis systems, whose functions the wheel doesn't expose (its LsmBasisSystem holds only the polynomial types), each
output computed by the C++ harness (docs/porting-playbook.md §9.4).

The one-factor systems of every polynomial type to orders 0 to 4, each function's values at points inside and
outside [-1, 1] (the Chebyshev bases' weights are infinite at ±1 and NaN beyond); the multi-factor systems of one to
three factors to orders 0 to 3 on the monomials and the Hermite polynomials, at random points; and the refusals of an
unknown type and of no factors. The harness's methods are tools/cxx/src/methods/montecarlo/lsmbasissystem.cpp's. Read
back by tests/NanoQL.Tests/Vectors/Cxx/Methods/MonteCarlo/LsmBasisSystemCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TYPES = ["Monomial", "Laguerre", "Hermite", "Hyperbolic", "Legendre", "Chebyshev", "Chebyshev2nd"]
XS = [-2.5, -1.0, -0.9, -0.3, 0.0, 0.2, 0.7, 0.99, 1.0, 1.4, 2.5, 5.0]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        rnd = random.Random(v.SEED)
        out = []
        for k, name in enumerate(TYPES):
            for order in range(5):
                out.append({"name": f"path {name} {order}",
                            "inputs": {"settings": st, "order": order, "polynomialType": k, "xs": XS},
                            "expected": {"values": v.enc(H.call("LsmBasisSystem::pathBasisSystem", order, k, XS))}})
        for dim in (1, 2, 3):
            points = [[rnd.uniform(-1.5, 1.5) for _ in range(dim)] for _ in range(6)]
            for k in (0, 2):
                for order in range(4):
                    out.append({"name": f"multi-path {TYPES[k]} {dim} factors {order}",
                                "inputs": {"settings": st, "dim": dim, "order": order, "polynomialType": k,
                                           "points": points},
                                "expected": {"values": v.enc(H.call("LsmBasisSystem::multiPathBasisSystem", dim,
                                                                    order, k, points))}})
        out.append({"name": "unknown type",
                    "inputs": {"settings": st, "order": 2, "polynomialType": 7, "xs": XS},
                    "expected": {"values": v.attempt(lambda: H.call("LsmBasisSystem::pathBasisSystem", 2, 7, XS))}})
        out.append({"name": "no factors",
                    "inputs": {"settings": st, "dim": 0, "order": 2, "polynomialType": 0, "points": []},
                    "expected": {"values": v.attempt(
                        lambda: H.call("LsmBasisSystem::multiPathBasisSystem", 0, 2, 0, []))}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/montecarlo/lsmbasissystem.hpp", cases)
