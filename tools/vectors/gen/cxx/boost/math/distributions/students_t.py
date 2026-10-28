# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Boost.Math's Student's t quantile, cumulative distribution and density (boost/math/distributions/students_t.hpp
and detail/t_distribution_inv.hpp, Boost 1.90) for double under the default policy, as QuantLib's InverseCumulativeStudent
and its T copula policy call them, each output computed by the C++ harness. The quantile: integer degrees of freedom
from 1 to 2147483647, the only ones QuantLib passes (its closed forms at 1, 2, 4 and 6, Hill's approximation and the
tail series with the Halley step on the incomplete beta, and above 2^28 the normal's), at probabilities from 1e-300 to
1 - 1e-15; and the refused arguments (degrees of freedom not positive, probabilities outside (0, 1)). The cdf and the
pdf: degrees of freedom from 1 to 1e16, past 1/epsilon where the normal answers, the T policy's odd orders among them,
at variates from -1e300 to 1e300 and the infinities, on both sides of the incomplete beta's two forms (df above or
below twice the square, the density's log1p and pow forms either side of x^2/df = 0.125); and the refused arguments
(degrees of freedom not positive or NaN, a NaN variate). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Boost/StudentsTCxxVectorTests.cs through nanoql's StudentsTFunctions."""
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

DEGREES = list(range(1, 31)) + [40, 50, 60, 100, 1000, 10**4, 10**6, 268435456, 268435457, 10**9, 2147483647]
PROBABILITIES = [1e-300, 1e-200, 1e-151, 1e-149, 1e-100, 1e-50, 1e-20, 1e-10, 1e-6, 1e-3, 0.01, 0.05, 0.1, 0.25, 0.4,
                 0.5, 0.6, 0.75, 0.9, 0.95, 0.99, 0.999, 1 - 1e-6, 1 - 1e-10, 1 - 1e-15]
REFUSED = [(0.0, 0.5), (-1.0, 0.5), (NAN, 0.5), (5.0, 0.0), (5.0, 1.0), (5.0, -0.1), (5.0, 1.1), (5.0, NAN)]
INF = float("inf")
CDF_DEGREES = [1, 2, 3, 4, 5, 6, 7, 9, 15, 29, 45, 99, 171, 173, 1001, 1e5, 4503599627370496.0, 1e16]
VARIATES = [-1e300, -1e6, -50.0, -10.0, -3.0, -1.0, -0.5, -1e-3, -1e-10, 0.0, 1e-10, 1e-3, 0.3, 0.5, 1.0, 1.5, 2.5, 3.0,
            10.0, 50.0, 1e6, 1e300, INF, -INF]
CDF_REFUSED = [(0.0, 1.0), (-1.0, 1.0), (NAN, 1.0), (5.0, NAN)]


def cases():
    with cxx.connect() as H:
        st = H.settings(R)
        pairs = [(float(df), p) for df in DEGREES for p in PROBABILITIES] + REFUSED
        out = [{"name": "the quantile",
                "inputs": {"settings": st, "df": v.enc([d for d, _ in pairs]), "p": v.enc([p for _, p in pairs])},
                "expected": {"quantile": [v.attempt(lambda d=d, p=p: H.call("students_t::quantile", d, p))
                                          for d, p in pairs]}}]
        points = [(float(df), x) for df in CDF_DEGREES for x in VARIATES] + CDF_REFUSED
        for member in ("cdf", "pdf"):
            out.append({"name": f"the {member}",
                        "inputs": {"settings": st, "df": v.enc([d for d, _ in points]),
                                   "x": v.enc([x for _, x in points])},
                        "expected": {member: [v.attempt(lambda d=d, x=x: H.call(f"students_t::{member}", d, x))
                                              for d, x in points]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "boost/math/distributions/students_t.hpp", cases)
