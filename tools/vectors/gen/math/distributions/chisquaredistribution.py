# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the chi-squared distributions the wheel exposes (ql/math/distributions/chisquaredistribution.hpp, .cpp):
CumulativeChiSquareDistribution for eight degrees of freedom at about 30 points each, and its refusal (through the
gamma distribution) of degrees of freedom of 0, -1 and NaN; NonCentralCumulativeChiSquareDistribution for seven degrees
of freedom and eight non-centralities at about 30 points each, at and below 0 among them; and
InverseNonCentralCumulativeChiSquareDistribution for three degrees of freedom and three non-centralities at five
probabilities, under QuantLib's default evaluations and accuracy and under three evaluations and 1e-12.

The non-central points leave out the region where QuantLib 1.44's series leaves its range and nanoql takes QuantLib's
master's, #2908 (the S-van plan's VR11; Divergences/ChiSquaredSeriesRefusalTests.cs): above the degrees of freedom where
the series' first term exp(f2·ln(x/2) − x/2 − ln Γ(f2 + 1)) leaves the normal range of doubles, tested here with a margin
of e^5; the non-centralities stay below 1,416.8, where the Poisson weight exp(−ncp/2) does, and the points below
df + 20,000, where 1.44's series' iterations end. The inverse's probabilities stay below 1, which
would double its bracket into that region. The wheel's classes are SWIG wrappers of the C++ classes of the same names,
with no logic of their own; NonCentralCumulativeChiSquareSankaranApprox isn't exposed (test-suite/distributions.cpp's
testSankaranApproximation asserts it). Read back by
tests/NanoQL.Tests/Vectors/Numerics/Distributions/ChiSquareDistributionVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

CENTRAL_DFS = [0.5, 1.0, 2.0, 3.0, 5.0, 10.0, 30.0, 100.0]
REFUSED_DFS = [0.0, -1.0, float("nan")]
NONCENTRAL_DFS = [0.5, 1.0, 2.0, 4.0, 10.0, 30.0, 100.0]
NCPS = [0.0, 0.5, 1.0, 5.0, 20.0, 100.0, 500.0, 1400.0]
INVERSE_DFS = [1.0, 4.0, 10.0]
INVERSE_NCPS = [0.5, 2.0, 10.0]
PROBABILITIES = [0.001, 0.05, 0.5, 0.95, 0.999]
MIN_NORMAL = 2.2250738585072014e-308


def central_points(df):
    top = 5.0 * df + 20.0
    return [-1.0, 0.0, 1e-8, 0.1] + [top * (i + 1) / 26 for i in range(26)]


def noncentral_points(df, ncp):
    top = 3.0 * (df + ncp) + 30.0
    xs = [-1.0, 0.0, 1e-8, 0.1, df, df + ncp] + [top * (i + 1) / 24 for i in range(24)]
    return [x for x in xs if not refused(df, x)]


def refused(df, x):
    # the series' first term below the normal range above df, with a margin
    if x <= df or x <= 0.0:
        return False
    f2, x2 = 0.5 * df, 0.5 * x
    return f2 * math.log(x2) - x2 - math.lgamma(f2 + 1.0) < math.log(MIN_NORMAL) + 5.0


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for df in CENTRAL_DFS:
        xs = central_points(df)
        d = ql.CumulativeChiSquareDistribution(df)
        out.append({"name": f"central, df {df!r}", "inputs": {"settings": st, "df": df, "x": xs},
                    "expected": {"cumulative": [v.attempt(lambda x=x: d(x)) for x in xs]}})
    out.append({"name": "central, refused degrees of freedom",
                "inputs": {"settings": st, "dfs": v.enc(REFUSED_DFS), "x": 1.0},
                "expected": {"cumulative": [v.attempt(lambda df=df: ql.CumulativeChiSquareDistribution(df)(1.0))
                                            for df in REFUSED_DFS]}})
    for df in NONCENTRAL_DFS:
        for ncp in NCPS:
            xs = noncentral_points(df, ncp)
            d = ql.NonCentralCumulativeChiSquareDistribution(df, ncp)
            out.append({"name": f"non-central, df {df!r}, ncp {ncp!r}",
                        "inputs": {"settings": st, "df": df, "ncp": ncp, "x": xs},
                        "expected": {"cumulative": [v.attempt(lambda x=x: d(x)) for x in xs]}})
    for df in INVERSE_DFS:
        for ncp in INVERSE_NCPS:
            for evaluations, accuracy in ((None, None), (3, 1e-12)):
                d = (ql.InverseNonCentralCumulativeChiSquareDistribution(df, ncp) if evaluations is None
                     else ql.InverseNonCentralCumulativeChiSquareDistribution(df, ncp, evaluations, accuracy))
                inputs = {"settings": st, "df": df, "ncp": ncp, "p": PROBABILITIES}
                if evaluations is not None:
                    inputs |= {"maxEvaluations": evaluations, "accuracy": accuracy}
                out.append({"name": f"inverse, df {df!r}, ncp {ncp!r}"
                                    + ("" if evaluations is None else f", {evaluations} evaluations"),
                            "inputs": inputs,
                            "expected": {"quantile": [v.attempt(lambda p=p: d(p)) for p in PROBABILITIES]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/distributions/chisquaredistribution.hpp", cases)
