# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's loss distributions (ql/experimental/credit/lossdistribution.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness (tools/cxx/src/experimental/credit/lossdistribution.cpp).

The probabilities of n events, binomial (on the first name's probability) and exact (Ma's recursion), static and as
functors, for every n from 0 to the pool's size plus one where defined (the binomial at least 0 wraps to 0 in
QuantLib's, held in Divergences/DistributionMembersTests.cs); the homogeneous pool's distribution with its
by-products, on a volume and on volume vectors; Hull-White bucketing on unequal volumes and probabilities; and the
Monte Carlo distribution at QuantLib's seed, 42, and at 7, each with its buckets and maximum. The binomial
distribution reads a member QuantLib never sets, so of it only the bucket count and maximum are read. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/LossDistCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 38960  # 31 August 2006, the evaluation date, which no output reads
POOLS = [[0.1, 0.2, 0.05, 0.3], [0.02] * 6, [0.5, 0.0, 1.0], [0.25]]
HOMOGENEOUS = [(20, 10.0, 2.0, [0.1, 0.2, 0.05, 0.3]), (50, 6.0, 1.0, [0.02] * 6), (8, 3.0, 1.5, [0.5, 0.0, 1.0])]
BUCKETING = [(20, 10.0, 1e-6, [1.0, 2.5, 0.7, 3.2], [0.1, 0.2, 0.05, 0.3]),
             (40, 8.0, 1e-6, [0.6, 0.6, 1.2, 1.8, 0.3, 2.4], [0.02, 0.05, 0.01, 0.03, 0.04, 0.02]),
             (10, 12.0, 1e-6, [5.0, 6.0], [0.5, 0.25])]
BINOMIAL = [(10, 5.0), (20, 1.5)]
MONTE_CARLO = [(20, 10.0, 2000, 42, 1e-6, [1.0, 2.5, 0.7, 3.2], [0.1, 0.2, 0.05, 0.3]),
               (10, 6.0, 500, 7, 1e-6, [0.6, 1.2, 1.8, 2.4], [0.3, 0.2, 0.1, 0.05])]


def cases():
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        st = H.settings(R)
        expected = {
            "probabilityOfNEvents": [call("LossDist::probabilityOfNEvents", p) for p in POOLS],
            "binomialProbabilityOfNEvents": [call("LossDist::binomialProbabilityOfNEvents", n, p)
                                             for p in POOLS for n in range(len(p) + 1)],
            "binomialProbabilityOfAtLeastNEvents": [call("LossDist::binomialProbabilityOfAtLeastNEvents", n, p)
                                                    for p in POOLS for n in range(1, len(p) + 2)],
            "probabilityOfNEventsAt": [call("LossDist::probabilityOfNEventsAt", n, p)
                                       for p in POOLS for n in range(len(p) + 1)],
            "probabilityOfAtLeastNEvents": [call("LossDist::probabilityOfAtLeastNEvents", n, p)
                                            for p in POOLS for n in range(len(p) + 2)],
            "functors": [[call(f, n, p) for f in ("ProbabilityOfNEvents", "ProbabilityOfAtLeastNEvents",
                                                  "BinomialProbabilityOfAtLeastNEvents")]
                         for p in POOLS for n in range(1, len(p) + 1)],
            "homogeneous": [call("LossDistHomogeneous::evaluate", nb, mx, vol, p) for nb, mx, vol, p in HOMOGENEOUS],
            "homogeneousVectors": [call("LossDistHomogeneous::onVectors", nb, mx, [vol] * len(p), p)
                                   for nb, mx, vol, p in HOMOGENEOUS],
            "bucketing": [call("LossDistBucketing::evaluate", *b) for b in BUCKETING],
            "monteCarlo": [call("LossDistMonteCarlo::evaluate", *m) for m in MONTE_CARLO],
            "binomialLimits": [call("LossDistBinomial::limits", nb, mx) for nb, mx in BINOMIAL]}
        return [{"name": "loss distributions",
                 "inputs": {"settings": st, "pools": POOLS, "homogeneous": [list(h) for h in HOMOGENEOUS],
                            "bucketing": [list(b) for b in BUCKETING], "monteCarlo": [list(m) for m in MONTE_CARLO],
                            "binomial": [list(b) for b in BINOMIAL]},
                 "expected": expected}]


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/lossdistribution.hpp", cases)
