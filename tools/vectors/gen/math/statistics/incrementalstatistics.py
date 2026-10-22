# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's IncrementalStatistics (ql/math/statistics/incrementalstatistics.hpp, .cpp), the wrapper of
Boost.Accumulators' weighted accumulator set, through the wheel's IncrementalStatistics: generalstatistics.py's data
sets (0 to 4 samples; Gaussian samples with unit and random weights; around a large mean, where Boost's weighted
variance on its lazy mean and the immediate recurrence differ in the last bits; ties; a signed zero; infinities; a
NaN sample), each read through samples, weightSum, mean, variance, standardDeviation, errorEstimate, skewness,
kurtosis, min and max, then after reset; and add's refusal of a negative and of a NaN weight.

Left out: the variance, standard deviation and error estimate of a set whose leading samples weigh 0 ("Gaussian,
random weights"), where Boost's recurrence divides 0 by 0 and QuantLib's variance is NaN for good, and nanoql's skips
the update (the S-sr plan's C1 review, B1-2; tests/NanoQL.Tests/Divergences/
IncrementalStatisticsLeadingZeroWeightTests.cs).

The wheel binds no downside member (downsideSamples, downsideWeightSum, downsideVariance, downsideDeviation), which
QuantLib's stats case checks. Read back by tests/NanoQL.Tests/Vectors/Numerics/Statistics/
IncrementalStatisticsVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import QuantLib as ql  # noqa: E402

import generalstatistics  # noqa: E402
import vectors as v  # noqa: E402


# The members that read Boost's weighted variance, left out of a set whose leading samples weigh 0 (B1-2).
VARIANCE_MEMBERS = ("variance", "standardDeviation", "errorEstimate")


def leading_zero_weight(weights):
    k = next((i for i, w in enumerate(weights) if w != 0.0), None)
    return k is not None and k > 0


def cases():
    out = generalstatistics.cases_for(ql.IncrementalStatistics)
    for c in out:
        if leading_zero_weight(c["inputs"]["weights"]):
            for m in VARIANCE_MEMBERS:
                c["expected"].pop(m)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/statistics/incrementalstatistics.hpp", cases)
