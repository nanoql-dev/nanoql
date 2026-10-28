# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for StudentDistribution, CumulativeStudentDistribution and InverseCumulativeStudent
(ql/math/distributions/studenttdistribution.hpp, .cpp) through the wheel: the density for degrees of freedom from
1 to 300 at points where QuantLib's factors stay finite; the cumulative function above the centre where n/(n + x^2)
is at most 1/2, QuantLib's formula (nearer the centre and below it CR7 (xxiii)'s fixes, left out); and the
inverse, Boost's quantile, for degrees of freedom from 1 to 10^6 at probabilities from 1e-12 to 1 - 1e-12, with
QuantLib's own answers at 0 and 1. Read back by
tests/NanoQL.Tests/Vectors/Numerics/Distributions/StudentTDistributionVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

DENSITY_N = [1, 2, 3, 5, 10, 30, 100, 300]
DENSITY_X = [-10.0, -3.0, -1.5, -0.5, 0.0, 0.25, 1.0, 2.0, 5.0, 10.0]
CDF_N = [1, 2, 3, 4, 5, 6, 10, 30, 100]
CDF_X = [-1000.0, -50.0, -12.0, -10.0, -6.0, -3.0, -2.0, -1.0, 1.0, 2.0, 3.0, 6.0, 10.0, 12.0, 50.0, 1000.0]
INVERSE_N = [1, 2, 3, 4, 5, 6, 7, 10, 19, 20, 30, 100, 1000, 10**6]
INVERSE_P = [0.0, 1e-12, 1e-8, 1e-4, 0.001, 0.01, 0.1, 0.25, 0.4, 0.5, 0.6, 0.75, 0.9, 0.99, 0.999, 1 - 1e-4, 1 - 1e-8,
             1 - 1e-12, 1.0]


def cases():
    out = []
    st = v.settings(ql.Date(15, ql.October, 2026))
    for n in DENSITY_N:
        f = ql.StudentDistribution(n)
        out.append({"name": f"density, n {n}", "inputs": {"settings": st, "n": n, "x": v.enc(DENSITY_X)},
                    "expected": {"density": v.enc([f(x) for x in DENSITY_X])}})
    for n in CDF_N:
        f = ql.CumulativeStudentDistribution(n)
        xs = [x for x in CDF_X if x > 0 and n / (n + x * x) <= 0.5]
        out.append({"name": f"cumulative, n {n}", "inputs": {"settings": st, "n": n, "x": v.enc(xs)},
                    "expected": {"cumulative": v.enc([f(x) for x in xs])}})
    for n in INVERSE_N:
        f = ql.InverseCumulativeStudent(n)
        out.append({"name": f"inverse, n {n}", "inputs": {"settings": st, "n": n, "p": v.enc(INVERSE_P)},
                    "expected": {"inverse": v.enc([f(p) for p in INVERSE_P])}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/distributions/studenttdistribution.hpp", cases)
