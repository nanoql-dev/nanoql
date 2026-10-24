# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Boost.Math's modified Bessel function of the first kind (boost/math/special_functions/bessel.hpp, Boost
1.90) for double under the default policy, as Boost's non-central chi-squared density calls it, each output computed by
the C++ harness: cyl_bessel_i over orders from -10.5 to 200, among them 0, 1/2 and 1 (Boost's special cases) and the
non-central chi-squared's k/2 - 1, at arguments from 0 to 1e4, which reach the small-argument series, Temme's series
(x <= 2) and Steed's continued fraction above it, the large-argument expansion, I0's and I1's three ranges, the
reflection of a negative order, and the overflow; negative arguments with integer and non-integer orders. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Boost/BesselCxxVectorTests.cs through nanoql's BesselFunctions."""
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

ORDERS = [-10.5, -3.0, -2.5, -1.75, -1.0, -0.5, -0.25, -1e-10, 0.0, 1e-10, 0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 2.0,
          2.75, 3.5, 5.0, 7.25, 10.0, 20.5, 50.0, 100.0, 169.5, 170.0, 171.5, 200.0]
ARGUMENTS = [0.0, 1e-300, 1e-10, 1e-5, 0.01, 0.1, 0.5, 1.0, 1.5, 1.99, 2.0, 2.01, 3.0, 5.0, 7.7, 7.8, 10.0, 25.0,
             50.0, 100.0, 100.5, 150.0, 400.0, 499.0, 500.0, 501.0, 700.0, 709.0, 710.0, 713.0, 750.0, 1000.0, 1e4]
NEGATIVE = [(0.0, -1.0), (1.0, -2.5), (2.0, -0.3), (3.0, -10.0), (0.5, -1.0), (1.5, -2.0), (-2.0, -3.0)]
ODD = [(NAN, 1.0), (1.0, NAN), (1.0, INF), (INF, 1.0)]


def pairs():
    return [(o, x) for o in ORDERS for x in ARGUMENTS] + NEGATIVE + ODD


def cases():
    with cxx.connect() as H:
        st = H.settings(R)
        ps = pairs()
        return [{"name": "cyl_bessel_i",
                 "inputs": {"settings": st, "v": v.enc([o for o, _ in ps]), "x": v.enc([x for _, x in ps])},
                 "expected": {"cylBesselI": [v.attempt(lambda o=o, x=x: H.call("bessel::cyl_bessel_i", o, x))
                                             for o, x in ps]}}]


if __name__ == "__main__":
    v.main(__file__, "boost/math/special_functions/bessel.hpp", cases)
