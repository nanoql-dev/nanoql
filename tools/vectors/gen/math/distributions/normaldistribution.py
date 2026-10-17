# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the normal distributions the wheel exposes (ql/math/distributions/normaldistribution.hpp): for three
(average, sigma) pairs, the density and its derivative (NormalDistribution's operator() and derivative) and the
cumulative distribution and its derivative (CumulativeNormalDistribution's) at 200 points, and the inverses
(InverseCumulativeNormal's and MoroInverseCumulativeNormal's operator()) at 200 probabilities; and each constructor's
refusal of a sigma of 0, -1 and NaN. The points cover the cumulative's switch to its asymptotic lower tail (a
probability of 1e-8, about 5.61 standard deviations below the average), the density's cut-off (an exponent of -690,
about 37.15 standard deviations away), the infinities, NaN and the largest doubles; the probabilities cover Acklam's
tails (below 0.02425 and above 0.97575) and their recovery near 0 and 1, Moro's switch at 0.08 and 0.92, the
probabilities both refuse, and NaN. The wheel's four classes are SWIG wrappers of the C++ classes of the same names,
with no logic of their own. Read back by tests/NanoQL.Tests/Vectors/Numerics/Distributions/NormalDistributionVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

INF = float("inf")
NAN = float("nan")
EPS = 2.220446049250313e-16
MAX = 1.7976931348623157e308
PARAMETERS = [(0.0, 1.0), (1.0, 2.0), (-3.25, 0.4)]
# Standard deviations from the average: a grid of 160, then 34 points in the tails and at the switches.
GRID = [-9.0 + 18.0 * i / 159 for i in range(160)]
TAILS = [-40.0, -38.0, -37.2, -37.1, -30.0, -25.0, -20.0, -15.0, -12.0, -10.0, -8.5, -7.0, -6.5, -6.0, -5.7, -5.62,
         -5.61, -5.6, -5.5, -5.0, 0.0, 5.0, 5.5, 6.0, 7.0, 8.5, 9.5, 10.0, 20.0, 37.1, 37.2, 38.0, 40.0, 1e-300]
# Six points given directly: the infinities, NaN, the largest doubles and -0.
SPECIAL = [INF, -INF, NAN, MAX, -MAX, -0.0]
# 200 probabilities: 40 at the edges, the tails and the switches, and a grid of 160.
PROBABILITIES = ([0.0, 1.0, -0.5 * EPS, 0.5 * EPS, -2.0 * EPS, 1.0 + EPS, 1.0 + 100.0 * EPS, -1e-8, 1.0 + 1e-4, NAN,
                  5e-324, 1e-300, 1e-100, 1e-20, 1e-10, 1e-6, 1e-3, 0.01, 0.02, 0.0242499, 0.02425, 0.0242501, 0.05,
                  0.08, 0.080001, 0.1, 0.5, 0.9, 0.919999, 0.92, 0.95, 0.9757499, 0.97575, 0.9757501, 0.98, 0.99,
                  1.0 - 1e-6, 1.0 - 1e-10, 1.0 - 1e-15, 1.0 - EPS / 2])
PROBABILITIES += [(i + 0.5) / 160 for i in range(160)]
REFUSED_SIGMAS = [0.0, -1.0, NAN]


def points(average, sigma):
    return [average + sigma * t for t in GRID + TAILS] + SPECIAL


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    assert len(points(0.0, 1.0)) == 200 and len(PROBABILITIES) == 200
    out = []
    for average, sigma in PARAMETERS:
        xs = points(average, sigma)
        normal = ql.NormalDistribution(average, sigma)
        cumulative = ql.CumulativeNormalDistribution(average, sigma)
        inverse = ql.InverseCumulativeNormal(average, sigma)
        moro = ql.MoroInverseCumulativeNormal(average, sigma)
        out.append({"name": f"average {average!r} sigma {sigma!r}",
                    "inputs": {"settings": st, "average": average, "sigma": sigma, "x": v.enc(xs),
                               "p": v.enc(PROBABILITIES)},
                    "expected": {
                        "density": [v.attempt(lambda x=x: normal(x)) for x in xs],
                        "densityDerivative": [v.attempt(lambda x=x: normal.derivative(x)) for x in xs],
                        "cumulative": [v.attempt(lambda x=x: cumulative(x)) for x in xs],
                        "cumulativeDerivative": [v.attempt(lambda x=x: cumulative.derivative(x)) for x in xs],
                        "inverse": [v.attempt(lambda p=p: inverse(p)) for p in PROBABILITIES],
                        "moroInverse": [v.attempt(lambda p=p: moro(p)) for p in PROBABILITIES]}})
    # Each constructor's refusal, evaluated at 0.5, which all four take.
    out.append({"name": "refused sigmas",
                "inputs": {"settings": st, "average": 0.0, "sigmas": v.enc(REFUSED_SIGMAS), "x": 0.5},
                "expected": {
                    "density": [v.attempt(lambda s=s: ql.NormalDistribution(0.0, s)(0.5)) for s in REFUSED_SIGMAS],
                    "cumulative": [v.attempt(lambda s=s: ql.CumulativeNormalDistribution(0.0, s)(0.5))
                                   for s in REFUSED_SIGMAS],
                    "inverse": [v.attempt(lambda s=s: ql.InverseCumulativeNormal(0.0, s)(0.5)) for s in REFUSED_SIGMAS],
                    "moroInverse": [v.attempt(lambda s=s: ql.MoroInverseCumulativeNormal(0.0, s)(0.5))
                                    for s in REFUSED_SIGMAS]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/distributions/normaldistribution.hpp", cases)
