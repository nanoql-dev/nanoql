# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for BivariateCumulativeStudentDistribution (ql/math/distributions/bivariatestudenttdistribution.hpp, .cpp),
which the wheel doesn't bind, each output computed by the C++ harness: degrees of freedom from 1 to 10,000, odd and even
(Dunnett and Sobel's two equations), at correlations from -1 to 1, on a 9 x 9 grid of bounds from -8 to 8. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Distributions/BivariateStudentTDistributionCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

DEGREES = [1, 2, 3, 4, 5, 6, 7, 10, 15, 30, 101, 600, 10000]
RHOS = [-1.0, -0.99, -0.5, 0.0, 0.3, 0.5, 0.99, 1.0]
BOUNDS = [-8.0, -3.0, -1.25, -0.5, 0.0, 0.5, 1.25, 3.0, 8.0]


def cases():
    out = []
    xs = [x for x in BOUNDS for _ in BOUNDS]
    ys = [y for _ in BOUNDS for y in BOUNDS]
    with cxx.connect() as H:
        for n in DEGREES:
            for rho in RHOS:
                st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
                out.append({"name": f"n {n}, rho {rho}",
                            "inputs": {"settings": st, "n": n, "rho": v.enc(rho), "x": v.enc(xs), "y": v.enc(ys)},
                            "expected": {"value": v.enc(H.call("BivariateCumulativeStudentDistribution", n, rho, xs,
                                                               ys))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/distributions/bivariatestudenttdistribution.hpp", cases)
