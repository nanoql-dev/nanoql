# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Boost.Math's chi-squared distribution (boost/math/distributions/chi_squared.hpp, Boost 1.90) for double
under the default policy, each output computed by the C++ harness: the quantile and its complement, which Boost's
non-central chi-squared quantile takes its first guess from, over degrees of freedom from 0.01 to 1e6 at probabilities
from 0 to 1; the pdf, cdf and complement cdf at points relative to the degrees of freedom; and the refused arguments
(degrees of freedom not positive or not finite, a probability outside [0, 1], a negative or non-finite point, the
density at 0 below 2 degrees of freedom). Read back by tests/NanoQL.Tests/Vectors/Cxx/Boost/ChiSquaredCxxVectorTests.cs
through nanoql's ChiSquaredFunctions."""
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

DF = [0.01, 0.1, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 4.0, 5.0, 7.5, 10.0, 25.0, 50.0, 100.0, 343.0, 1000.0, 1e4, 1e6]
PROBABILITIES = [0.0, 1e-300, 1e-100, 1e-20, 1e-10, 1e-5, 1e-3, 0.01, 0.05, 0.1, 0.25, 0.5, 0.75, 0.9, 0.95, 0.99,
                 0.999, 1 - 1e-8, 1.0]
RELATIVE = [0.0, 1e-10, 0.01, 0.1, 0.5, 0.9, 1.0, 1.1, 1.5, 2.0, 5.0, 20.0]
REFUSED_DF = [(0.0, 0.5), (-1.0, 0.5), (NAN, 0.5), (INF, 0.5)]
REFUSED_P = [(2.0, -0.1), (2.0, 1.1), (2.0, NAN)]
REFUSED_X = [(2.0, -1.0), (2.0, NAN), (2.0, INF)]


def cases():
    with cxx.connect() as H:
        st = H.settings(R)

        def values(method, pairs):
            return [v.attempt(lambda d=d, x=x: H.call(method, d, x)) for d, x in pairs]

        qs = [(d, p) for d in DF for p in PROBABILITIES] + REFUSED_DF + REFUSED_P
        xs = [(d, d * r) for d in DF for r in RELATIVE] + REFUSED_DF + REFUSED_X
        return [
            {"name": "the quantile",
             "inputs": {"settings": st, "df": v.enc([d for d, _ in qs]), "p": v.enc([p for _, p in qs])},
             "expected": {"quantile": values("chi_squared::quantile", qs),
                          "quantileComplement": values("chi_squared::quantile_complement", qs)}},
            {"name": "the pdf and cdf",
             "inputs": {"settings": st, "df": v.enc([d for d, _ in xs]), "x": v.enc([x for _, x in xs])},
             "expected": {"pdf": values("chi_squared::pdf", xs), "cdf": values("chi_squared::cdf", xs),
                          "cdfComplement": values("chi_squared::cdf_complement", xs)}},
        ]


if __name__ == "__main__":
    v.main(__file__, "boost/math/distributions/chi_squared.hpp", cases)
