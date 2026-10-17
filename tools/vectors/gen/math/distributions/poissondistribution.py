# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the Poisson distributions (ql/math/distributions/poissondistribution.hpp): for six means, the
probability and the cumulative probability (PoissonDistribution's and CumulativePoissonDistribution's operator()) of
k from 0 to 40 and of seven large k up to 4294967295, the largest BigNatural, where QuantLib's k + 1 wraps to 0 and
both refuse a nonzero mean; the same at a negative and a NaN mean, which the probability's constructor refuses and
the cumulative refuses on evaluation; and for six lambdas, the inverse (InverseCumulativePoisson's operator()) at 50
probabilities, among them test-suite/distributions.cpp's twelve, 0, 1, two outside [0, 1] and NaN, and the
constructor's refusal of a lambda of 0, -1 and NaN. The wheel's three classes are SWIG wrappers of the C++ classes of
the same names, with no logic of their own; it takes BigNatural as an unsigned 32-bit integer. Read back by
tests/NanoQL.Tests/Vectors/Numerics/Distributions/PoissonDistributionVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

NAN = float("nan")
MEANS = [0.0, 0.5, 1.0, 3.7, 10.0, 25.0]
REFUSED_MEANS = [-1.0, NAN]
KS = list(range(41)) + [100, 1000, 100000, 2147483647, 2147483648, 4294967294, 4294967295]
REFUSED_KS = [0, 1, 5]
LAMBDAS = [0.5, 1.0, 2.5, 4.0, 10.0, 30.0]
# test-suite/distributions.cpp's testInverseCumulativePoisson data, the edges, and a grid of 33.
TEST_SUITE = [0.2, 0.5, 0.9, 0.98, 0.99, 0.999, 0.9999, 0.99995, 0.99999, 0.999999, 0.9999999, 0.99999999]
PROBABILITIES = TEST_SUITE + [0.0, 1.0, -0.1, 1.1, NAN] + [(i + 0.5) / 33 for i in range(33)]
REFUSED_LAMBDAS = [0.0, -1.0, NAN]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    assert len(PROBABILITIES) == 50
    out = []
    for mu in MEANS + REFUSED_MEANS:
        ks = KS if mu in MEANS else REFUSED_KS
        cumulative = ql.CumulativePoissonDistribution(mu)
        out.append({"name": f"mu {mu!r}",
                    "inputs": {"settings": st, "mu": v.enc(mu), "k": ks},
                    "expected": {"probability": [v.attempt(lambda k=k: ql.PoissonDistribution(mu)(k)) for k in ks],
                                 "cumulative": [v.attempt(lambda k=k: cumulative(k)) for k in ks]}})
    for lam in LAMBDAS:
        inverse = ql.InverseCumulativePoisson(lam)
        out.append({"name": f"lambda {lam!r}",
                    "inputs": {"settings": st, "lambda": lam, "x": v.enc(PROBABILITIES)},
                    "expected": {"inverse": [v.attempt(lambda x=x: inverse(x)) for x in PROBABILITIES]}})
    # The constructor's refusal, evaluated at 0.5.
    out.append({"name": "refused lambdas",
                "inputs": {"settings": st, "lambdas": v.enc(REFUSED_LAMBDAS), "x": 0.5},
                "expected": {"inverse": [v.attempt(lambda lam=lam: ql.InverseCumulativePoisson(lam)(0.5))
                                         for lam in REFUSED_LAMBDAS]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/distributions/poissondistribution.hpp", cases)
