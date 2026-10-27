# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SimpleLocalEstimator (ql/models/volatility/simplelocalestimator.hpp) and
GarmanKlassSimpleSigma (garmanklass.hpp), which the wheel doesn't expose, each output computed by the C++ harness: the
volatility series of quote series of 1 to 40 dates, consecutive and with gaps, at year fractions 1/252 and 1/360, and
of interval-price series likewise. An empty quote series, whose begin QuantLib increments past its end, is left out (the
S-bar plan's §2.10: BR5 (xxxv)); the refusal is a divergence fact.

The harness unit is tools/cxx/src/models/volatility/simplelocalestimator.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/Volatility/SimpleLocalEstimatorCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
FIRST = 22833


def cases():
    rng = random.Random(v.SEED)
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for n in (1, 2, 3, 7, 15, 40):
            serials = [FIRST + k + (k // 5) * 2 for k in range(n)]
            quotes = [round(100 * (1 + rng.gauss(0, 0.05)), 4) for _ in range(n)]
            o = [round(100 * (1 + rng.gauss(0, 0.05)), 4) for _ in range(n)]
            c = [round(x * (1 + rng.gauss(0, 0.02)), 4) for x in o]
            h = [round(max(x, y) * 1.01, 4) for x, y in zip(o, c)]
            lo = [round(min(x, y) * 0.99, 4) for x, y in zip(o, c)]
            for y in (1 / 252.0, 1 / 360.0):
                r = H.call("SimpleLocalEstimator.calculate", y, serials, quotes)
                out.append({"name": f"{n} quotes, y {y:.6g}",
                            "inputs": {"settings": st, "call": "simple", "yearFraction": y, "dates": serials, "values": quotes},
                            "expected": {"dates": r["dates"], "values": r["values"]}})
                r = H.call("GarmanKlassSimpleSigma.calculate", y, serials, o, c, h, lo)
                out.append({"name": f"{n} interval prices, y {y:.6g}",
                            "inputs": {"settings": st, "call": "garmanKlass", "yearFraction": y, "dates": serials,
                                       "open": o, "close": c, "high": h, "low": lo},
                            "expected": {"dates": r["dates"], "values": r["values"]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/volatility/simplelocalestimator.hpp", cases)
