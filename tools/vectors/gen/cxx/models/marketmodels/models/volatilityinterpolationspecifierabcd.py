# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's VolatilityInterpolationSpecifierabcd
(ql/models/marketmodels/models/volatilityinterpolationspecifierabcd.hpp, .cpp), which the wheel doesn't expose, each
output computed by the C++ harness.

The specifier at offset 0, QuantLib's tested case (at other offsets QuantLib's in-between short rates reset at an
earlier rate's index, the S-mm plan's MR3 (iii);
tests/NanoQL.Tests/Divergences/VolatilityInterpolationSpecifierOffsetTests.cs): long rates on every 2nd and every 3rd of
twelve and thirteen semi-annual short rate times, each long rate's PiecewiseConstantAbcdVariance on its own reset with
abcd varying by rate; the last caplet's volatility left to the last long rate's (0) and given; and after
setScalingFactors, setLastCapletVol or both. The reads: the period, offset and counts, and each interpolated and
original variance's reads (piecewiseconstantabcdvariance.py's), an array of each read over the variances. Throws: long
and short rates of mismatched counts, long rate times off the short ones, scales of the wrong count.

The harness unit is tools/cxx/src/models/marketmodels/models/variances.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/VolatilityInterpolationSpecifierabcdCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
NAME = "VolatilityInterpolationSpecifierabcd"
READS = ["variances", "volatilities", "rateTimes", "variance", "volatility", "totalVariance", "totalVolatility"]


def flatten(r):
    """The counts, and each variance read over the interpolated and the original variances, an array each."""
    out = {k: r[k] for k in ("period", "offset", "noBigRates", "noSmallRates")}
    for prefix, key in (("interpolated", "interpolatedVariances"), ("original", "originalVariances")):
        for k in READS:
            out[prefix + k[0].upper() + k[1:]] = [x[k] for x in r[key]]
    return out


def long_variances(small, period, offset, count):
    times = [small[offset + j * period] for j in range(count + 1)]
    return [{"a": -0.05 + 0.002 * i, "b": 0.16 + 0.005 * i, "c": 0.5 + 0.01 * i, "d": 0.15 + 0.003 * i,
             "resetIndex": i, "rateTimes": times} for i in range(count)]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for slabel, small in (("twelve short rates", [0.5 * (i + 1) for i in range(13)]),
                              ("thirteen short rates", [0.5 * (i + 1) for i in range(14)])):
            n = len(small) - 1
            for period in (2, 3):
                count = n // period
                variances = long_variances(small, period, 0, count)
                for llabel, last in (("its own last caplet", 0.0), ("a last caplet of 21%", 0.21)):
                    for clabel, scales, new_last in (("as built", None, None),
                                                     ("scaled", [1.0 + 0.04 * i for i in range(count)], None),
                                                     ("a new last caplet", None, 0.23),
                                                     ("scaled, then a new last caplet",
                                                      [1.1 - 0.03 * i for i in range(count)], 0.19)):
                        args = [period, 0, variances, small, last, scales, new_last]
                        out.append({"name": f"{slabel}, period {period}, {llabel}, {clabel}",
                                    "inputs": {"settings": st, "call": "specifier", "period": period, "offset": 0,
                                               "variances": variances, "timesForSmallRates": small,
                                               "lastCapletVol": last, "scales": scales, "newLastCapletVol": new_last},
                                    "expected": v.enc(flatten(H.call(NAME, *args)))})
        small = [0.5 * (i + 1) for i in range(13)]
        good = long_variances(small, 2, 0, 6)
        refusals = {"countMismatch": [2, 0, good[:5], small, 0.0, None, None],
                    "timesOffShortTimes": [2, 0, long_variances([t * 1.01 for t in small], 2, 0, 6), small, 0.0, None,
                                           None],
                    "wrongScales": [2, 0, good, small, 0.0, [1.0, 1.0], None]}
        out.append({"name": "refused specifiers",
                    "inputs": {"settings": st, "call": "refused", "refusals": refusals},
                    "expected": {k: v.attempt(lambda a=a: H.call(NAME, *a)) for k, a in refusals.items()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/models/volatilityinterpolationspecifierabcd.hpp", cases)
