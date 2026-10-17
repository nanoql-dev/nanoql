# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the normal distributions' members the wheel doesn't expose (ql/math/distributions/normaldistribution.hpp,
docs/porting-playbook.md §9.4), each output computed by the C++ harness: MaddockCumulativeNormal::operator() and
MaddockInverseCumulativeNormal::operator(), which call Boost.Math's normal cdf and quantile, and
InverseCumulativeNormal::standard_value.

For three (average, sigma) pairs, (0, 1), (3, 2) and (0.3, 2.5), the cdf at points x = average - z·sigma·√2 for z on
every interval of Boost's erf_imp (zero, below 1e-10, 0.5, 1.5, 2.5, 4.5 and 28, and beyond), of both signs, with
±0, ±∞ and NaN (refused); and the quantile at probabilities p for which q = 2p, or 2 - 2p above one half, reaches
every branch of Boost's erf_inv_imp (p from 0.25 to 0.75; q from 0.25; and x = sqrt(-log q) below 3, 6, 18 and 44,
q down to the smallest double), with test-suite/distributions.cpp's testMaddockNormalDistributions probabilities, 0
and 1 (refused, an overflow), and probabilities outside [0, 1], infinite or NaN (refused); each with an even grid of
20 ordinary points and probabilities. Then the refusals of the parameters: a sigma of 0, -1 or NaN, which
QuantLib's constructors refuse, and an infinite sigma or an infinite or NaN average, which Boost refuses on
evaluation. Then standard_value over Acklam's central region and tails, their recovery near 0 and 1, and the
probabilities it refuses.

tests/NanoQL.Tests/Vectors/Cxx/Numerics/Distributions/NormalDistributionCxxVectorTests.cs reads it back and prints
how many of its real outputs are bit-identical to nanoql's."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

INF = math.inf
NAN = math.nan
EPS = 2.220446049250313e-16
PARAMETERS = [(0.0, 1.0), (3.0, 2.0), (0.3, 2.5)]
# erfc's argument z = -(x - average)/(sigma·√2), per erf_imp interval, positive; the cases take both signs.
Z = ([5e-324, 1e-300, 1e-200, 1e-100, 1e-20, 1e-15, 1e-11, 9.999999999999999e-11]  # below 1e-10
     + [1e-10, 1e-8, 1e-5, 0.001, 0.1, 0.25, 0.4, 0.49999999999999994]  # below 0.5
     + [0.5, 0.6, 0.84375, 1.0, 1.25, 1.4999999999999998]  # below 1.5
     + [1.5, 1.75, 2.0, 2.25, 2.4999999999999996]  # below 2.5
     + [2.5, 3.0, 3.5, 4.0, 4.499999999999999]  # below 4.5
     + [4.5, 5.0, 5.93, 6.0, 8.0, 10.0, 15.0, 20.0, 26.0, 27.999999999999996]  # below 28
     + [28.0, 30.0, 38.0, 100.0, 1e10, 1e300])  # 28 and beyond
X_SPECIAL = [0.0, -0.0, INF, -INF, NAN]
# testMaddockNormalDistributions' nine probabilities.
TEST_SUITE = [1.0e-10, 1.0e-6, 0.01, 0.1, 0.5, 0.9, 0.99, 1.0 - 1.0e-6, 1.0 - 1.0e-10]
P = ([0.0, 1.0, -0.0, -0.1, 1.1, NAN, INF, -INF]  # refused
     + [5e-324, 1e-320, 1e-310, 1e-300, 1e-250, 1e-200, 1e-160, 1e-150]  # x in [18, 44)
     + [1e-141, 1e-100, 1e-50, 1e-30, 1e-20, 1e-17, 1e-16]  # x in [6, 18)
     + [1e-15, 1e-10, 1e-6, 1e-5, 6e-5]  # x in [3, 6)
     + [7e-5, 1e-4, 0.001, 0.01, 0.05, 0.1, 0.12, 0.1249999]  # x below 3
     + [0.125, 0.15, 0.2, 0.24, 0.2499999999999999]  # q from 0.25, lower
     + [0.25, 0.3, 0.4, 0.5, 0.6, 0.7, 0.75]  # p up to 0.5
     + [0.7500000000000001, 0.8, 0.85, 0.875]  # q from 0.25, upper
     + [0.9, 0.95, 0.99, 0.999, 0.99993, 0.99995, 0.999999, 1.0 - 1e-10, 1.0 - 1e-15, 1.0 - EPS / 2])  # upper tail
P += [p for p in TEST_SUITE if p not in P]
# An even grid of ordinary probabilities.
P += [(i + 0.5) / 20 for i in range(20)]
# (average, sigma) pairs refused by QuantLib's constructors, then by Boost on evaluation.
REFUSED = [(0.0, 0.0), (0.0, -1.0), (0.0, NAN), (0.0, INF), (INF, 1.0), (-INF, 1.0), (NAN, 1.0)]
# standard_value: Acklam's lower tail, central region and upper tail, and the recovery near 0 and 1.
STANDARD = ([0.0, 1.0, -0.5 * EPS, 0.5 * EPS, -2.0 * EPS, 1.0 + EPS, 1.0 + 100.0 * EPS, -1e-8, 1.0 + 1e-4, NAN,
             -INF, INF]
            + [5e-324, 1e-300, 1e-100, 1e-20, 1e-10, 1e-6, 1e-4, 1e-3, 0.01, 0.02, 0.0242499, 0.02425]
            + [0.02425 + (0.97575 - 0.02425) * i / 59 for i in range(60)]
            + [0.97575, 0.9757501, 0.98, 0.99, 0.999, 1.0 - 1e-6, 1.0 - 1e-10, 1.0 - 1e-15, 1.0 - EPS / 2])


def cdf_points(average, sigma):
    zs = Z + [-z for z in Z]
    grid = [average + sigma * (-8.0 + 16.0 * i / 19) for i in range(20)]
    return [average - z * sigma * math.sqrt(2.0) for z in zs] + grid + X_SPECIAL


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []
        for average, sigma in PARAMETERS:
            spec = {"average": average, "sigma": sigma}
            xs = cdf_points(average, sigma)
            out.append({"name": f"average {average!r} sigma {sigma!r}",
                        "inputs": {"settings": st, "average": average, "sigma": sigma, "x": v.enc(xs),
                                   "p": v.enc(P)},
                        "expected": {
                            "cumulative": [v.attempt(lambda x=x: H.call("MaddockCumulativeNormal::operator()", spec, x))
                                           for x in xs],
                            "inverse": [v.attempt(lambda p=p: H.call("MaddockInverseCumulativeNormal::operator()",
                                                                     spec, p)) for p in P]}})
        specs = [{"average": a, "sigma": s} for a, s in REFUSED]
        out.append({"name": "refused parameters",
                    "inputs": {"settings": st, "averages": v.enc([a for a, _ in REFUSED]),
                               "sigmas": v.enc([s for _, s in REFUSED]), "x": 0.5, "p": 0.5},
                    "expected": {
                        "cumulative": [v.attempt(lambda s=s: H.call("MaddockCumulativeNormal::operator()", s, 0.5))
                                       for s in specs],
                        "inverse": [v.attempt(lambda s=s: H.call("MaddockInverseCumulativeNormal::operator()", s, 0.5))
                                    for s in specs]}})
        out.append({"name": "standard value", "inputs": {"settings": st, "x": v.enc(STANDARD)},
                    "expected": {"standardValue": [v.attempt(lambda x=x: H.call(
                        "InverseCumulativeNormal::standard_value", x)) for x in STANDARD]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/distributions/normaldistribution.hpp", cases)
