# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ContinuousArithmeticAsianVecerEngine
(ql/experimental/exoticoptions/continuousarithmeticasianvecerengine.hpp, .cpp), which the wheel doesn't expose, each
output computed by the C++ harness: continuous arithmetic average-price calls and puts by Vecer's PDE.

QuantLib's case's markets (spots of 1.9 to 2.1, a strike of 2, rates of 1.25% to 18%, volatilities of 10% to 50%) over a
year and two on 200 steps a side; puts, by Vecer's parity, at r = q and r != q; an average starting a month after the
evaluation date; other grids and step counts; the NPV. Then the refusals: an average starting before the evaluation date
("Seasoned Asian not yet implemented"), a grid that excludes 0 and a grid that excludes the initial point.

The outputs nanoql fixes are left out: the process's structures share the evaluation date and Act/365, so the engine's
clock is QuantLib's, and no initial point lies on a grid's upper end. The process and option are
tools/cxx/src/experimental/exoticoptions/continuousarithmeticasianvecerengine.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/ExoticOptions/ContinuousArithmeticAsianVecerEngineCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
CALL, PUT = 1, -1
KEYS = ("spot", "riskFreeRate", "dividendYield", "vol", "optionType", "strike", "expiryDays", "startDays",
        "timeSteps", "assetSteps", "zMin", "zMax")


def spec(spot, r, vol, days, option_type=CALL, strike=2.0, q=0.0, start=0, steps=200, asset_steps=200, z_min=-1.0,
         z_max=1.0):
    return dict(zip(KEYS, (spot, r, q, vol, option_type, strike, days, start, steps, asset_steps, z_min, z_max)))


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        setups = [
            ("spot 1.9, r 5%, vol 50%, a year", spec(1.9, 0.05, 0.5, 365)),
            ("spot 2.0, r 5%, vol 50%, a year", spec(2.0, 0.05, 0.5, 365)),
            ("spot 2.1, r 5%, vol 50%, a year", spec(2.1, 0.05, 0.5, 365)),
            ("spot 2.0, r 2%, vol 10%, a year", spec(2.0, 0.02, 0.1, 365)),
            ("spot 2.0, r 18%, vol 30%, a year", spec(2.0, 0.18, 0.3, 365)),
            ("spot 2.0, r 1.25%, vol 25%, two years", spec(2.0, 0.0125, 0.25, 730)),
            ("a put, r != q", spec(2.0, 0.05, 0.3, 365, PUT, q=0.02)),
            ("a put, r = q", spec(2.0, 0.03, 0.3, 365, PUT, q=0.03)),
            ("an average from a month ahead", spec(2.0, 0.05, 0.3, 365, start=30)),
            ("a put on an average from a month ahead", spec(1.9, 0.05, 0.3, 365, PUT, q=0.01, start=30)),
            ("100 steps on a narrower grid", spec(2.0, 0.05, 0.3, 365, steps=100, asset_steps=100, z_min=-0.5,
                                                  z_max=0.5)),
            ("a spot of 100, strike 95", spec(100.0, 0.04, 0.25, 182, strike=95.0, q=0.01)),
            ("an average starting yesterday (refused)", spec(2.0, 0.05, 0.3, 365, start=-1)),
            ("a grid above 0 (refused)", spec(2.0, 0.05, 0.3, 365, z_min=0.1, z_max=1.0)),
            ("a grid that misses the spot (refused)", spec(1.0, 0.05, 0.3, 365, strike=2.0, z_min=-0.2, z_max=0.2)),
        ]
        for name, s in setups:
            try:
                expected = {"NPV": H.call("ContinuousArithmeticAsianVecerEngine", *[s[k] for k in KEYS])}
            except RuntimeError:
                expected = {"NPV": {"throws": True}}
            out.append({"name": name, "inputs": {"settings": st, "option": s}, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/exoticoptions/continuousarithmeticasianvecerengine.hpp", cases)
