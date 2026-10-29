# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's interpolated year-on-year optionlet volatility curve
(ql/experimental/inflation/yoyinflationoptionletvolatilitystructure2.hpp), which the wheel doesn't bind, each output
computed by the C++ harness (tools/cxx/src/experimental/inflation/yoyoptionletvolatility.cpp,
"InterpolatedYoYOptionletVolatilityCurve::values").

- "values" cases: linear curves from the base date to ten years, observed flat and interpolated, with a three-month lag
  and none, on TARGET and Actual/365 (Fixed): the reference, base and last dates, the base level, the times, dates,
  data and nodes, and the volatility and total variance at dates inside and past the curve, with extrapolation (past
  the last time QuantLib's interpolation, which doesn't extrapolate, refuses).

Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Inflation/InterpolatedYoYOptionletVolatilityCurveCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TODAY = ql.Date(23, ql.November, 2007)
M = int(ql.Months)
TARGET = {"type": "TARGET"}
A365 = {"type": "Actual365Fixed"}


def curve_spec(lag, interpolated, vols):
    base = ql.Date(1, ql.August, 2007) if lag else ql.Date(1, ql.November, 2007)
    dates = [base] + [TODAY + ql.Period(n, ql.Years) for n in (1, 2, 3, 5, 10)][:len(vols) - 1]
    return {"settlementDays": 0, "calendar": TARGET, "convention": int(ql.ModifiedFollowing), "dayCounter": A365,
            "lag": [lag, M], "frequency": int(ql.Monthly), "indexIsInterpolated": interpolated,
            "dates": [v.enc(d) for d in dates], "volatilities": vols, "minStrike": -1.0, "maxStrike": 3.0}


CURVES = [("three-month lag, flat", curve_spec(3, False, [0.01, 0.009, 0.008, 0.0075, 0.007, 0.0065])),
          ("three-month lag, interpolated", curve_spec(3, True, [0.01, 0.009, 0.008, 0.0075, 0.007, 0.0065])),
          ("no lag, two dates", curve_spec(0, False, [0.012, 0.006]))]
DATES = [TODAY + n for n in (30, 200, 365, 800, 1500, 3000, 3800, 4500)]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(v.enc(TODAY))
        for label, c in CURVES:
            ds = [v.enc(d) for d in DATES]
            out.append({"name": label, "inputs": {"settings": st, "curve": c, "dates": ds, "strike": 0.02},
                        "expected": H.call("InterpolatedYoYOptionletVolatilityCurve::values", c, ds, 0.02)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/inflation/yoyinflationoptionletvolatilitystructure2.hpp", cases)
