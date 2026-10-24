# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Boost.Math's gamma family (boost/math/special_functions/gamma.hpp and detail/igamma_inverse.hpp, Boost
1.90) for double under the default policy, as QuantLib's CEV engine and density calculator call it, each output
computed by the C++ harness: tgamma, lgamma and tgamma1pm1 over the poles' neighbourhoods, the small-argument and
Lanczos regions and past the overflow; gamma_p, gamma_q and gamma_p_derivative over shapes from 1e-5 to 1e6 at points
relative to the shape, which reach each of the incomplete gamma's evaluation methods (the series, the continued
fraction, Temme's large-shape expansion, the small-shape and integer and half-integer forms); gamma_p_inv and
gamma_q_inv over the same shapes at probabilities from 1e-300 to 1; and the refused arguments. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Boost/GammaCxxVectorTests.cs through nanoql's GammaFunctions and
IgammaInverseFunctions."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
NAN = float("nan")
INF = float("inf")

# tgamma, lgamma: the poles and their neighbours, the small region, the Lanczos region, the overflow and beyond
Z = ([-170.5, -150.25, -100.5, -20.5, -20.000001, -19.999999, -10.5, -3.5, -2.5, -2.0, -1.5, -1.0, -0.999999, -0.5,
      -1e-10, -1e-300, 0.0, 1e-300, 1e-200, 1e-20, 1e-10, 1e-5, 0.001, 0.01, 0.1, 0.25, 0.5, 0.75, 0.9999, 1.0,
      1.0000001, 1.5, 1.9999999, 2.0, 2.5, 3.0, 3.5, 4.0, 5.5, 7.0, 9.999, 10.0, 12.5, 20.0, 34.0, 35.0, 50.5, 99.9,
      100.0, 100.1, 150.0, 170.0, 170.5, 171.0, 171.6, 171.7, 172.0, 200.0, 1000.0, 1e5, 1e10, 1e20, 1e300, INF, -INF,
      NAN]
     + [i / 8 for i in range(-80, 81)])
# tgamma1pm1: around 0, where it is accurate, and its two other ranges
ZPM1 = [-1.0, -0.999, -0.75, -0.5, -0.4999, -0.25, -1e-5, -1e-10, -1e-300, 0.0, 1e-300, 1e-10, 1e-5, 0.25, 0.5,
        0.75, 1.0, 1.5, 1.9999, 2.0, 2.0001, 3.0, 10.0, 50.0, 100.0, 170.0, 171.0, NAN]
SHAPES = [1e-5, 0.001, 0.01, 0.1, 0.2, 0.5, 0.75, 0.9, 1.0, 1.25, 1.5, 2.0, 2.5, 3.0, 4.5, 5.0, 7.0, 10.0, 11.5, 12.0,
          19.5, 20.0, 25.0, 30.0, 33.3, 50.0, 100.0, 170.0, 171.0, 200.0, 500.0, 1000.0, 1e4, 1e5, 1e6]
RELATIVE = [1e-10, 0.01, 0.1, 0.3, 0.5, 0.8, 0.9, 0.99, 1.0, 1.01, 1.1, 1.25, 1.5, 2.0, 3.0, 5.0, 10.0]
ABSOLUTE = [0.0, 1e-300, 1e-20, 1e-5, 0.1, 0.5, 0.75, 1.1, 1.5, 2.0, 5.0, 20.0, 100.0, 1000.0]
PROBABILITIES = [0.0, 1e-300, 1e-100, 1e-30, 1e-15, 1e-10, 1e-5, 1e-3, 0.01, 0.05, 0.1, 0.3, 0.5, 0.7, 0.9, 0.95,
                 0.99, 0.999, 1 - 1e-6, 1 - 1e-12, 1.0]
REFUSED_AX = [(0.0, 1.0), (-1.0, 1.0), (NAN, 1.0), (2.0, -1.0), (2.0, NAN), (-1.0, 0.0), (INF, 1.0), (1.0, INF)]
REFUSED_AP = [(0.0, 0.5), (-1.0, 0.5), (2.0, -0.1), (2.0, 1.1), (NAN, 0.5), (2.0, NAN)]


def pairs_ax():
    out = []
    for a in SHAPES:
        out += [(a, a * r) for r in RELATIVE] + [(a, x) for x in ABSOLUTE] + [(a, a + 1.0), (a, a - 1.0)]
    return [(a, x) for a, x in out if x >= 0] + REFUSED_AX


def pairs_ap():
    return [(a, p) for a in SHAPES for p in PROBABILITIES] + REFUSED_AP


def cases():
    with cxx.connect() as H:
        st = H.settings(R)

        def one(method, zs):
            return [v.attempt(lambda z=z: H.call(method, z)) for z in zs]

        def two(method, pairs):
            return [v.attempt(lambda a=a, x=x: H.call(method, a, x)) for a, x in pairs]

        ax = pairs_ax()
        ap = pairs_ap()
        return [
            {"name": "tgamma and lgamma",
             "inputs": {"settings": st, "z": v.enc(Z)},
             "expected": {"tgamma": one("gamma::tgamma", Z), "lgamma": one("gamma::lgamma", Z)}},
            {"name": "tgamma1pm1",
             "inputs": {"settings": st, "z": v.enc(ZPM1)},
             "expected": {"tgamma1pm1": one("gamma::tgamma1pm1", ZPM1)}},
            {"name": "the incomplete gamma",
             "inputs": {"settings": st, "a": v.enc([a for a, _ in ax]), "x": v.enc([x for _, x in ax])},
             "expected": {"gammaP": two("gamma::gamma_p", ax), "gammaQ": two("gamma::gamma_q", ax),
                          "gammaPDerivative": two("gamma::gamma_p_derivative", ax)}},
            {"name": "the inverses",
             "inputs": {"settings": st, "a": v.enc([a for a, _ in ap]), "p": v.enc([p for _, p in ap])},
             "expected": {"gammaPInv": two("gamma::gamma_p_inv", ap), "gammaQInv": two("gamma::gamma_q_inv", ap)}},
        ]


if __name__ == "__main__":
    v.main(__file__, "boost/math/special_functions/gamma.hpp", cases)
