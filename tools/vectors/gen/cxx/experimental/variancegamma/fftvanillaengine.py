# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FFTVanillaEngine and FFTEngine (ql/experimental/variancegamma/fftvanillaengine.hpp, .cpp,
fftengine.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness: European calls and puts
priced by the Carr-Madan transform under Black-Scholes.

Two markets, each with calls and puts at strikes of 70 to 130 over three months, six, a year and two: each option on its
own, and every option as one list (precalculate); the NPV.

The outputs nanoql fixes are left out (the S-xeq plan's §2.8): every strike is above 1 and every expiry's largest strike
gives QuantLib at least 2^14 points at the default log-strike spacing, where nanoql sizes its grid as QuantLib does
(XER5 (xix)); no list holds an expired option, which QuantLib's precalculate refuses ("negative time") and nanoql prices
as expired (XER1, tests/NanoQL.Tests/Conventions/ExperimentalExoticConventionTests.cs). The process and options are
tools/cxx/src/experimental/variancegamma/fftvanillaengine.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/VarianceGamma/FFTVanillaEngineCxxVectorTests.cs."""
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
MARKETS = [("a low-volatility market", {"spot": 100.0, "riskFreeRate": 0.05, "dividendYield": 0.02, "vol": 0.15}),
           ("a high-volatility market", {"spot": 100.0, "riskFreeRate": 0.03, "dividendYield": 0.0, "vol": 0.45})]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for name, m in MARKETS:
            options = [[t, k, d] for d in (91, 182, 365, 730) for t in (CALL, PUT) for k in (70.0, 100.0, 130.0)]
            for listed in (False, True):
                values = H.call("FFTVanillaEngine", m["spot"], m["riskFreeRate"], m["dividendYield"], m["vol"],
                                options, listed, 0.001)
                out.append({"name": f"{name}, {'one list' if listed else 'each alone'}",
                            "inputs": {"settings": st, "market": m, "options": options, "list": listed},
                            "expected": {"NPV": values}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/variancegamma/fftvanillaengine.hpp", cases)
