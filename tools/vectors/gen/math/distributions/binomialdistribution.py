# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the binomial distributions the wheel exposes (ql/math/distributions/binomialdistribution.hpp):
BinomialDistribution's and CumulativeBinomialDistribution's operator(), for probabilities 0 to 1 and 0 to 500 trials,
at every count up to two past the trials (sampled for the largest), and their constructors' refusals of a negative
probability, one above 1 and NaN. The cumulative distribution reads the incomplete beta function of
ql/math/beta.hpp. The wheel's classes are SWIG wrappers of the C++ classes of the same names, with no logic of their
own; binomialCoefficient and PeizerPrattMethod2Inversion aren't exposed (test-suite/distributions.cpp's
testBinomialDistribution asserts them). Read back by
tests/NanoQL.Tests/Vectors/Numerics/Distributions/BinomialDistributionVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

PROBABILITIES = [0.0, 1e-6, 0.01, 0.25, 0.5, 0.75, 0.99, 1.0]
TRIALS = [0, 1, 4, 10, 50, 500]
REFUSED = [-0.1, 1.1, float("nan")]


def counts(n):
    if n <= 50:
        return list(range(n + 3))
    return [0, 1, 2, 10, 50, 100, 124, 125, 126, 250, 375, 499, 500, 501, 502]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for p in PROBABILITIES:
        for n in TRIALS:
            ks = counts(n)
            pmf = ql.BinomialDistribution(p, n)
            cdf = ql.CumulativeBinomialDistribution(p, n)
            out.append({"name": f"p {p!r}, n {n}", "inputs": {"settings": st, "p": p, "n": n, "k": ks},
                        "expected": {"probability": [v.attempt(lambda k=k: pmf(k)) for k in ks],
                                     "cumulative": [v.attempt(lambda k=k: cdf(k)) for k in ks]}})
    out.append({"name": "refused probabilities", "inputs": {"settings": st, "ps": v.enc(REFUSED), "n": 4},
                "expected": {"probability": [v.attempt(lambda p=p: ql.BinomialDistribution(p, 4)(1)) for p in REFUSED],
                             "cumulative": [v.attempt(lambda p=p: ql.CumulativeBinomialDistribution(p, 4)(1))
                                            for p in REFUSED]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/distributions/binomialdistribution.hpp", cases)
