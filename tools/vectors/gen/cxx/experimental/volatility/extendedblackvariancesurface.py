# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ExtendedBlackVarianceSurface (ql/experimental/volatility/extendedblackvariancesurface.hpp,
.cpp), which the wheel doesn't expose, each output computed by the C++ harness: surfaces on the evaluation date 15
October 2026 on Actual/365 (Fixed) and Actual/360, a smile rising with time and a skew, on QuantLib's bilinear variance
and after setInterpolation with Bicubic, each extrapolation constant or the interpolator's below and above the strikes;
each surface's maximum date and strike range, and its variance and volatility at dates and times from the reference
date to past the last date (where QuantLib holds the last date's volatility flat) and at strikes inside and outside its
own, and the forward variances between consecutive dates, extrapolating. Then QuantLib's refusals: a strike's falling
variance, a first date on the reference date, dates out of order, and fewer volatilities than dates times strikes.

No output is left out (the S-xeq plan's §2.8): the surface has no departure. Nothing is fitted, so no output is
filtered.

The surface is tools/cxx/src/experimental/volatility/extendedblackvariancesurface.cpp's, its volatilities in QuantLib's
order, strike by strike, each strike's at every date. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Volatility/ExtendedBlackVarianceSurfaceCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
A365 = {"type": "Actual365Fixed"}
A360 = {"type": "Actual360"}
CONSTANT, DEFAULT = 0, 1
DATES = [R + n for n in (91, 182, 365, 730, 1826)]
STRIKES = [70.0, 85.0, 100.0, 115.0, 130.0]
QUERY_DATES = [R + n for n in (0, 1, 45, 91, 180, 366, 731, 1100, 1826, 2600)]
QUERY_TIMES = [0.0, 0.01, 0.25, 1.0, 3.0, 6.0]
QUERY_STRIKES = [60.0, 70.0, 85.0, 92.5, 100.0, 122.5, 130.0, 140.0]
KEYS = ("referenceDate", "dayCounter", "dates", "strikes", "vols", "lowerExtrapolation", "upperExtrapolation",
        "interpolation", "queryDates", "queryTimes", "queryStrikes")


def smile(k, j):
    return 0.18 + 0.01 * j + 0.15 * ((k - 100.0) / 100.0) ** 2


def skew(k, j):
    return 0.22 + 0.004 * j - 0.1 * (k - 100.0) / 100.0 + 0.05 * ((k - 100.0) / 100.0) ** 2


def vols(f, dates=DATES, strikes=STRIKES):
    return [f(k, j) for k in strikes for j in range(len(dates))]


def spec(dc, quotes, lower=DEFAULT, upper=DEFAULT, interpolation="Bilinear", dates=DATES, strikes=STRIKES):
    return dict(zip(KEYS, (R, dc, dates, strikes, quotes, lower, upper, interpolation, QUERY_DATES, QUERY_TIMES,
                           QUERY_STRIKES)))


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        setups = [
            ("a smile, Actual/365 (Fixed), bilinear", spec(A365, vols(smile))),
            ("a skew, Actual/360, bilinear", spec(A360, vols(skew))),
            ("a smile, constant extrapolation both sides", spec(A365, vols(smile), CONSTANT, CONSTANT)),
            ("a skew, constant below, the interpolator's above", spec(A365, vols(skew), CONSTANT, DEFAULT)),
            ("a skew, the interpolator's below, constant above", spec(A365, vols(skew), DEFAULT, CONSTANT)),
            ("a smile, bicubic", spec(A365, vols(smile), interpolation="Bicubic")),
            ("a skew, bicubic, constant extrapolation both sides",
             spec(A360, vols(skew), CONSTANT, CONSTANT, "Bicubic")),
        ]
        for name, s in setups:
            expected = H.call("ExtendedBlackVarianceSurface::values", *[s[k] for k in KEYS])
            out.append({"name": name, "inputs": {"settings": st, "surface": s}, "expected": expected})
        falling = vols(smile)
        falling[STRIKES.index(100.0) * len(DATES) + 2] = 0.05
        for name, s in [("a strike's falling variance (refused)", spec(A365, falling)),
                        ("a first date on the reference date (refused)",
                         spec(A365, vols(smile), dates=[R] + DATES[1:])),
                        ("dates out of order (refused)",
                         spec(A365, vols(smile), dates=[DATES[1], DATES[0]] + DATES[2:])),
                        ("fewer volatilities than dates times strikes (refused)",
                         spec(A365, vols(smile)[:-1]))]:
            try:
                H.call("ExtendedBlackVarianceSurface::values", *[s[k] for k in KEYS])
                raise SystemExit(f"{name}: QuantLib priced it")
            except RuntimeError:
                out.append({"name": name, "inputs": {"settings": st, "surface": s},
                            "expected": {"maxDate": {"throws": True}}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/volatility/extendedblackvariancesurface.hpp", cases)
