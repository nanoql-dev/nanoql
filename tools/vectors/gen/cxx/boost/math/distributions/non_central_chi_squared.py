# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Boost.Math's non-central chi-squared distribution (boost/math/distributions/non_central_chi_squared.hpp,
Boost 1.90) for double under the default policy, as QuantLib's CEV engine and density calculator call it, each output
computed by the C++ harness: the pdf, cdf and complement cdf over degrees of freedom from 0.1 to 200 and
non-centralities from 0 to 1e4 at points relative to their sum, which reach the central distribution (a non-centrality
of 0), Benton and Krishnamoorthy's complement series (a point above the sum), Ding's series (a non-centrality below 200)
and Benton and Krishnamoorthy's series above it, the density's Bessel form (a non-centrality up to 50) and its series;
the quantile and its complement at probabilities from 1e-10 to 1 - 1e-10, which take Pearson's guess or, where it falls
below 0.005, Luu's; and the refused arguments. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Boost/NonCentralChiSquaredCxxVectorTests.cs through nanoql's
NonCentralChiSquaredFunctions."""
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

DF = [0.1, 0.5, 1.0, 2.0, 3.0, 3.4285714285714284, 4.0, 4.5, 10.0, 50.0, 200.0]
NC = [0.0, 1e-5, 0.01, 1.0, 10.0, 49.0, 51.0, 150.0, 199.0, 201.0, 500.0, 1e3, 1e4]
RELATIVE = [0.0, 1e-6, 0.01, 0.1, 0.5, 0.8, 0.95, 1.0, 1.05, 1.2, 1.5, 2.0, 3.0]
PROBABILITIES = [0.0, 1e-10, 1e-4, 0.01, 0.1, 0.5, 0.9, 0.99, 0.9999, 1 - 1e-10, 1.0]
QUANTILE_NC = [0.0, 0.01, 1.0, 10.0, 51.0, 201.0, 1e3]
REFUSED = [(0.0, 1.0, 1.0), (-1.0, 1.0, 1.0), (NAN, 1.0, 1.0), (INF, 1.0, 1.0), (2.0, -1.0, 1.0), (2.0, NAN, 1.0),
           (2.0, INF, 1.0), (2.0, 1e19, 1.0)]
REFUSED_X = [(2.0, 1.0, -1.0), (2.0, 1.0, NAN), (2.0, 1.0, INF)]
REFUSED_P = [(2.0, 1.0, -0.1), (2.0, 1.0, 1.1), (2.0, 1.0, NAN)]


def cases():
    with cxx.connect() as H:
        st = H.settings(R)

        def values(method, triples):
            return [v.attempt(lambda k=k, l=l, x=x: H.call(method, k, l, x)) for k, l, x in triples]

        xs = [(k, l, (k + l) * r) for k in DF for l in NC for r in RELATIVE] + REFUSED + REFUSED_X
        qs = [(k, l, p) for k in DF for l in QUANTILE_NC for p in PROBABILITIES] + REFUSED + REFUSED_P

        def inputs(triples, third):
            return {"settings": st, "k": v.enc([k for k, _, _ in triples]), "l": v.enc([l for _, l, _ in triples]),
                    third: v.enc([x for _, _, x in triples])}

        return [
            {"name": "the pdf and cdf", "inputs": inputs(xs, "x"),
             "expected": {"pdf": values("non_central_chi_squared::pdf", xs),
                          "cdf": values("non_central_chi_squared::cdf", xs),
                          "cdfComplement": values("non_central_chi_squared::cdf_complement", xs)}},
            {"name": "the quantile", "inputs": inputs(qs, "p"),
             "expected": {"quantile": values("non_central_chi_squared::quantile", qs),
                          "quantileComplement": values("non_central_chi_squared::quantile_complement", qs)}},
        ]


if __name__ == "__main__":
    v.main(__file__, "boost/math/distributions/non_central_chi_squared.hpp", cases)
