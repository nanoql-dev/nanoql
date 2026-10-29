# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's EquityFXVolSurface (ql/experimental/volatility/equityfxvolsurface.hpp, .cpp), an abstract
surface QuantLib derives nothing from, each output computed by the C++ harness on its surface: a flat section at each
time t of volatility v0 + slope·t, on the evaluation date 15 October 2026, Actual/365 (Fixed); its at-the-money forward
volatility and variance between consecutive dates and times, rising, flat and falling volatilities, each pair QuantLib
refuses (out of order, or a variance that doesn't increase) a throw.

No output is left out (the S-xeq plan's §2.8): the surface has no departure. Nothing is fitted, so no output is
filtered.

The surface is tools/cxx/src/experimental/volatility/equityfxvolsurface.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Volatility/EquityFXVolSurfaceCxxVectorTests.cs, on its twin."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
DATES = [R + n for n in (30, 91, 182, 365, 730, 1826, 1826, 1000)]
TIMES = [0.05, 0.25, 0.5, 1.0, 2.0, 5.0, 5.0, 3.0]
KEYS = ("referenceDate", "v0", "slope", "dates", "times")


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for name, v0, slope in [("rising volatility", 0.2, 0.01), ("flat volatility", 0.25, 0.0),
                                ("slowly falling volatility", 0.3, -0.01),
                                ("falling volatility, the variance falling at last", 0.3, -0.05)]:
            s = dict(zip(KEYS, (R, v0, slope, DATES, TIMES)))
            expected = H.call("EquityFXVolSurface::values", *[s[k] for k in KEYS])
            out.append({"name": name, "inputs": {"settings": st, "surface": s}, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/volatility/equityfxvolsurface.hpp", cases)
