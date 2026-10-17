# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the gamma distribution and function the wheel exposes (ql/math/distributions/gammadistribution.hpp):
for nine shapes, the cumulative distribution (CumulativeGammaDistribution's operator()) at 40 points, on both sides
of a + 1, where it switches from its series to its continued fraction, and at and below 0, the last shape, 1e4, so
large that neither converges in QuantLib's 100 iterations near a + 1, which it refuses; the constructor's refusal
of a shape of 0, -1 and NaN; and GammaFunction::logValue at 200 points from 1e-300 to 1e300, with its refusal of 0,
a negative and NaN. The wheel's two classes are SWIG wrappers of the C++ classes of the same names, with no logic of
their own; it doesn't expose GammaFunction::value, which tools/vectors/gen/cxx/math/distributions/gammadistribution.py
vectors through the C++ harness. Read back by
tests/NanoQL.Tests/Vectors/Numerics/Distributions/GammaDistributionVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

NAN = float("nan")
SHAPES = [0.1, 0.5, 1.0, 1.5, 2.0, 3.7, 10.0, 50.0, 1e4]
REFUSED_SHAPES = [0.0, -1.0, NAN]


def cdf_points(a):
    # 0 and below; 32 points over (0, 3(a + 1)]; the switch at a + 1 and its neighbours; and far above.
    return ([-1.0, 0.0] + [3.0 * (a + 1.0) * (i + 1) / 32 for i in range(32)]
            + [a + 1.0 - 1e-9, a + 1.0, a + 1.0 + 1e-9, 1e-12, 10.0 * (a + 1.0), 200.0])


# 200 points for logValue: 190 logarithmically spaced from 1e-300 to 1e300, the integers 1 to 6, and the refusals.
LOG_POINTS = [10.0 ** (-300 + 600 * i / 189) for i in range(190)] + [1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 0.0, -1.0, -0.0,
                                                                      NAN]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    assert len(LOG_POINTS) == 200
    out = []
    for a in SHAPES:
        xs = cdf_points(a)
        assert len(xs) == 40
        cdf = ql.CumulativeGammaDistribution(a)
        out.append({"name": f"a {a!r}", "inputs": {"settings": st, "a": a, "x": xs},
                    "expected": {"cumulative": [v.attempt(lambda x=x: cdf(x)) for x in xs]}})
    out.append({"name": "refused shapes", "inputs": {"settings": st, "as": v.enc(REFUSED_SHAPES), "x": 1.0},
                "expected": {"cumulative": [v.attempt(lambda a=a: ql.CumulativeGammaDistribution(a)(1.0))
                                            for a in REFUSED_SHAPES]}})
    gamma = ql.GammaFunction()
    out.append({"name": "logValue", "inputs": {"settings": st, "x": v.enc(LOG_POINTS)},
                "expected": {"logValue": [v.attempt(lambda x=x: gamma.logValue(x)) for x in LOG_POINTS]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/distributions/gammadistribution.hpp", cases)
