# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for FilonIntegral (ql/math/integrals/filonintegral.hpp, .cpp), which the wheel doesn't bind, each output
computed by the C++ harness: the sine and cosine integrals of four integrands over three intervals, at frequencies of
both signs and 2 to 1,000 intervals; and an odd number of intervals, which QuantLib refuses. Only inputs whose theta = t
(b - a) / intervals is at least 1.7 in size are kept: below it nanoql takes the coefficients' Taylor series, where
QuantLib's closed forms cancel (the S-core plan's CR7 (x); tests/NanoQL.Tests/Divergences/FilonIntegralTests.cs). Read
back by tests/NanoQL.Tests/Vectors/Cxx/Numerics/Integrals/FilonIntegralCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

INTEGRANDS = ["1", "exp(-x/2)", "x x", "1/(1+x x)"]
RANGES = [(0.0, 1.0), (0.0, 10.0), (-2.0, 3.0)]
FREQUENCIES = [2.0, 20.0, 100.0, -50.0, 1000.0]
INTERVALS = [2, 10, 100, 1000]
THRESHOLD = 1.7


def cases():
    out = []
    with cxx.connect() as H:
        for kind in ("Sine", "Cosine"):
            for f in INTEGRANDS:
                for a, b in RANGES:
                    for t in FREQUENCIES:
                        for n in INTERVALS:
                            if abs(t * (b - a) / n) < THRESHOLD:
                                continue
                            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
                            out.append({"name": f"{kind}, {f} on [{a}, {b}], t {t}, {n} intervals",
                                        "inputs": {"settings": st, "type": kind, "t": t, "intervals": n,
                                                   "integrand": f, "a": a, "b": b},
                                        "expected": {"integral": v.enc(H.call("FilonIntegral", kind, t, n, f, a, b))}})
        for n in (3, 11):
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            out.append({"name": f"{n} intervals (refused)",
                        "inputs": {"settings": st, "type": "Cosine", "t": 100.0, "intervals": n, "integrand": "1",
                                   "a": 0.0, "b": 1.0},
                        "expected": {"integral": v.attempt(lambda: H.call("FilonIntegral", "Cosine", 100.0, n, "1",
                                                                          0.0, 1.0))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/integrals/filonintegral.hpp", cases)
