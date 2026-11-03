# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Distribution (ql/experimental/credit/distribution.hpp, .cpp), which the wheel doesn't expose,
each output computed by the C++ harness (tools/cxx/src/experimental/credit/distribution.cpp).

Distributions of added values (a fixed pseudo-random sample, with values below and above the range), of added densities
(a uniform and a two-point distribution) and of added averages; their buckets, densities, cumulative and excess
probabilities and averages; the expected value, and of the square; the confidence levels; the cumulative density at
coordinates past the first bucket (in it, QuantLib's reads index -1, held in Divergences/DistributionMembersTests.cs),
refused at 0 and beyond the range; the tranche expected values and the integrated excess probabilities; the buckets
located and their widths, refused outside the range; and the tranches' coordinates, cumulative probabilities and their
densities and excess probabilities but the last, which QuantLib computes after setting the last cumulative to 1. And the
convolutions QuantLib refuses (unequal or varying buckets, an offset), its others assigning where they add. The
shortfall is left out, QuantLib's form wrong at every level. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/DistributionCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 38960  # 31 August 2006, the evaluation date, which no output reads


def sample(n, lo, hi, seed=12345):
    """A fixed sample from a linear congruential generator, in [lo, hi)."""
    x, out = seed, []
    for _ in range(n):
        x = (1103515245 * x + 12345) % 2147483648
        out.append(lo + (hi - lo) * x / 2147483648)
    return out


def spec(n, xmin, xmax, values=(), densities=(), averages=()):
    return {"nBuckets": n, "xmin": xmin, "xmax": xmax, "values": list(values),
            "densities": [list(p) for p in densities], "averages": [list(p) for p in averages]}


DISTRIBUTIONS = [
    ("added values", spec(10, 0.0, 10.0, sample(500, -0.5, 10.5))),
    ("added values, unequal last bucket", spec(7, 0.0, 10.0, sample(300, 0.0, 10.0, 99))),
    ("uniform densities", spec(20, 0.0, 10.0, densities=[(i, 0.1) for i in range(20)])),
    ("two-point densities and averages", spec(10, 0.0, 10.0, densities=[(2, 0.4), (7, 0.6)],
                                             averages=[(2, 2.3), (7, 7.8)])),
]
LEVELS = [0.0, 0.05, 0.3, 0.5, 0.9, 0.999, 1.0]
CUMULATIVE = [0.0, -1.0, 1.5, 2.0, 3.7, 5.0, 9.99, 10.0, 10.5]
TRANCHE_VALUES = [(0.0, 3.0), (2.0, 6.0), (5.0, 10.0), (3.0, 3.5)]
INTERVALS = [(0.0, 10.0), (2.0, 6.5), (3.3, 3.3), (-1.0, 5.0), (0.0, 11.0)]
LOCATES = [0.0, 0.5, 1.0, 4.999, 9.99, 10.0, -0.5, 10.5]
TRANCHES = [(2.0, 6.0), (0.0, 3.0), (3.0, 10.0), (6.0, 4.0)]
REFUSED = [(spec(2, 0.0, 2.0, densities=[(0, 0.5), (1, 0.5)]), spec(2, 0.0, 4.0, densities=[(0, 0.25), (1, 0.25)])),
           (spec(3, 0.0, 2.5, densities=[(0, 0.4)]), spec(2, 0.0, 2.0, densities=[(0, 0.5)])),
           (spec(2, 1.0, 3.0, densities=[(0, 0.5)]), spec(2, 1.0, 3.0, densities=[(0, 0.5)]))]


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for name, d in DISTRIBUTIONS:
            st = H.settings(R)
            out.append({"name": name,
                        "inputs": {"settings": st, "distribution": d, "levels": LEVELS, "cumulative": CUMULATIVE,
                                   "trancheValues": [list(p) for p in TRANCHE_VALUES],
                                   "intervals": [list(p) for p in INTERVALS], "locates": LOCATES,
                                   "tranches": [list(p) for p in TRANCHES]},
                        "expected": {
                            "summary": call("Distribution::summary", d),
                            "expectedValue": call("Distribution::expectedValue", d),
                            "expectedSquare": call("Distribution::expectedSquare", d),
                            "confidenceLevel": [call("Distribution::confidenceLevel", d, q) for q in LEVELS],
                            "cumulativeDensity": [call("Distribution::cumulativeDensity", d, x) for x in CUMULATIVE],
                            "trancheExpectedValue": [call("Distribution::trancheExpectedValue", d, a, b)
                                                     for a, b in TRANCHE_VALUES],
                            "cumulativeExcessProbability": [call("Distribution::cumulativeExcessProbability", d, a, b)
                                                            for a, b in INTERVALS],
                            "locate": [call("Distribution::locate", d, x) for x in LOCATES],
                            "dx": [call("Distribution::dx", d, x) for x in LOCATES],
                            "tranche": [call("Distribution::tranche", d, a, b) for a, b in TRANCHES]}})
        st = H.settings(R)
        out.append({"name": "refused convolutions", "inputs": {"settings": st, "pairs": [list(p) for p in REFUSED]},
                    "expected": {"convolve": [call("ManipulateDistribution::convolve", a, b) for a, b in REFUSED]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/distribution.hpp", cases)
