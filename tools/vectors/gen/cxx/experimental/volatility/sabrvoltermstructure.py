# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SABRVolTermStructure (ql/experimental/volatility/sabrvoltermstructure.hpp), which the wheel
doesn't expose, each output computed by the C++ harness: structures on the evaluation date 15 October 2026, on
Actual/365 (Fixed) and Actual/360, with beta 0, 0.5 and 1, rho negative, 0 and positive, a growing, a flat and a falling
forward; each structure's maximum date and strike range, and its volatility and variance at times from 0 to 30 years
and at dates, at strikes from a third of the spot to three times it, extrapolating. Then QuantLib's refusals: a
strike of 0 (Hagan's formula reads its logarithm) and a negative spot.

No output is left out (the S-xeq plan's §2.8): the structure has no departure. Nothing is fitted, so no output is
filtered.

The structure is tools/cxx/src/experimental/volatility/sabrvoltermstructure.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Volatility/SABRVolTermStructureCxxVectorTests.cs."""
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
QUERY_TIMES = [0.0, 0.01, 0.25, 1.0, 2.5, 5.0, 10.0, 30.0]
QUERY_DAYS = [0, 1, 91, 365, 1826, 3653]
KEYS = ("alpha", "beta", "gamma", "rho", "s0", "r", "referenceDate", "dayCounter", "queryTimes", "queryDates",
        "queryStrikes")


def spec(alpha, beta, gamma, rho, s0, r, dc=A365, strikes=None):
    ks = strikes if strikes is not None else [s0 * m for m in (1 / 3, 0.6, 0.9, 1.0, 1.1, 1.5, 3.0)]
    return dict(zip(KEYS, (alpha, beta, gamma, rho, s0, r, R, dc, QUERY_TIMES, [R + n for n in QUERY_DAYS], ks)))


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        setups = [
            ("rates: beta 0.5, rho -0.3, a growing forward", spec(0.03, 0.5, 0.4, -0.3, 0.03, 0.02)),
            ("rates: beta 0, rho 0.2, a flat forward, Actual/360", spec(0.006, 0.0, 0.3, 0.2, 0.025, 0.0, A360)),
            ("equity: beta 1, rho -0.6, a growing forward", spec(0.25, 1.0, 0.8, -0.6, 100.0, 0.03)),
            ("equity: beta 0.7, rho 0, a falling forward", spec(0.6, 0.7, 0.5, 0.0, 100.0, -0.01)),
            ("rates: beta 0.3, rho 0.5, a high volatility of volatility", spec(0.015, 0.3, 1.2, 0.5, 0.04, 0.01)),
        ]
        for name, s in setups:
            expected = H.call("SABRVolTermStructure::values", *[s[k] for k in KEYS])
            out.append({"name": name, "inputs": {"settings": st, "structure": s}, "expected": expected})
        for name, s in [("a strike of 0 (refused)", spec(0.03, 0.5, 0.4, -0.3, 0.03, 0.02, strikes=[0.0])),
                        ("a negative spot (refused)", spec(0.03, 0.5, 0.4, -0.3, -0.03, 0.02, strikes=[0.03]))]:
            try:
                H.call("SABRVolTermStructure::values", *[s[k] for k in KEYS])
                raise SystemExit(f"{name}: QuantLib priced it")
            except RuntimeError:
                out.append({"name": name, "inputs": {"settings": st, "structure": s},
                            "expected": {"volAtTime": {"throws": True}}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/volatility/sabrvoltermstructure.hpp", cases)
