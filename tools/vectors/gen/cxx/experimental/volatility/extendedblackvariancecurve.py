# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ExtendedBlackVarianceCurve (ql/experimental/volatility/extendedblackvariancecurve.hpp, .cpp),
which the wheel doesn't expose, each output computed by the C++ harness: curves on the evaluation date 15 October 2026
on Actual/365 (Fixed), Actual/360 and Actual/Actual (ISDA), rising, humped and flat volatilities, on QuantLib's linear
variance and after setInterpolation with Cubic and BackwardFlat, and one whose variance falls with monotone variance not
forced; each curve's maximum date and strike range, and its variance and volatility at dates and times from the
reference date to past the last date (where QuantLib holds the last volatility flat), and the forward variances between
consecutive dates, extrapolating. Then QuantLib's refusals: a falling variance where monotone variance is forced, a first
date on the reference date, dates out of order, and fewer volatilities than dates.

No output is left out (the S-xeq plan's §2.8): the curve has no departure. Nothing is fitted, so no output is filtered.

The curve is tools/cxx/src/experimental/volatility/extendedblackvariancecurve.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Volatility/ExtendedBlackVarianceCurveCxxVectorTests.cs."""
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
AAISDA = {"type": "ActualActual", "convention": 2}
DATES = [R + n for n in (30, 91, 182, 365, 730, 1826)]
QUERY_DATES = [R + n for n in (0, 1, 15, 30, 60, 91, 200, 365, 500, 730, 1200, 1826, 2500)]
QUERY_TIMES = [0.0, 0.01, 0.05, 0.25, 0.5, 1.0, 1.7, 3.0, 5.0, 6.0, 9.0]
KEYS = ("referenceDate", "dayCounter", "dates", "vols", "forceMonotoneVariance", "interpolation", "queryDates",
        "queryTimes", "strike")


def spec(dc, vols, interpolation="Linear", force=True, dates=DATES, strike=100.0):
    return dict(zip(KEYS, (R, dc, dates, vols, force, interpolation, QUERY_DATES, QUERY_TIMES, strike)))


def cases():
    out = []
    rising = [0.18, 0.19, 0.20, 0.21, 0.22, 0.23]
    humped = [0.20, 0.24, 0.26, 0.25, 0.23, 0.22]
    flat = [0.2] * 6
    falling = [0.30, 0.25, 0.15, 0.11, 0.08, 0.07]
    with cxx.connect() as H:
        st = H.settings(R)
        setups = [
            ("rising, Actual/365 (Fixed), linear", spec(A365, rising)),
            ("humped, Actual/365 (Fixed), linear", spec(A365, humped)),
            ("flat, Actual/360, linear", spec(A360, flat)),
            ("humped, Actual/Actual (ISDA), linear", spec(AAISDA, humped)),
            ("humped, Actual/365 (Fixed), cubic", spec(A365, humped, "Cubic")),
            ("rising, Actual/360, cubic", spec(A360, rising, "Cubic")),
            ("humped, Actual/365 (Fixed), backward flat", spec(A365, humped, "BackwardFlat")),
            ("falling variance, monotone variance not forced", spec(A365, falling, force=False)),
            ("a single date, a strike of 0", spec(A365, [0.25], dates=[R + 365], strike=0.0)),
        ]
        for name, s in setups:
            expected = H.call("ExtendedBlackVarianceCurve::values", *[s[k] for k in KEYS])
            out.append({"name": name, "inputs": {"settings": st, "curve": s}, "expected": expected})
        for name, s in [("falling variance, monotone variance forced (refused)", spec(A365, falling)),
                        ("a first date on the reference date (refused)",
                         spec(A365, rising, dates=[R] + DATES[1:])),
                        ("dates out of order (refused)",
                         spec(A365, rising, dates=[DATES[1], DATES[0]] + DATES[2:])),
                        ("fewer volatilities than dates (refused)", spec(A365, rising[:5]))]:
            try:
                H.call("ExtendedBlackVarianceCurve::values", *[s[k] for k in KEYS])
                raise SystemExit(f"{name}: QuantLib priced it")
            except RuntimeError:
                out.append({"name": name, "inputs": {"settings": st, "curve": s},
                            "expected": {"maxDate": {"throws": True}}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/volatility/extendedblackvariancecurve.hpp", cases)
