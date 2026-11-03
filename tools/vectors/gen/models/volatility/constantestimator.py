# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ConstantEstimator (ql/models/volatility/constantestimator.hpp, .cpp): the dispersion of the
window of values before each date past the first, QuantLib's √(Σu²/n − (Σu)²/(n(n + 1))).

Twelve volatility series of 5 to 60 consecutive daily dates, their values drawn in [0.05, 0.6] and, in four, in [-0.6,
0.6], each through windows of 1, 2, 5 and 10 values no longer than the series: the output's dates and values. A window
longer than the series, where QuantLib advances past the series' end, is left out; the refusal is a divergence fact.

The wheel's calls, by QuantLib-SWIG 1.44's volatilities.i: ConstantEstimator(size).calculate(RealTimeSeries). Read
back by tests/NanoQL.Tests/Vectors/Models/Volatility/ConstantEstimatorVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

START = ql.Date(7, ql.July, 1962)
LENGTHS = [5, 8, 10, 12, 15, 20, 25, 30, 40, 45, 50, 60]


def cases():
    rng = random.Random(v.SEED)
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for i, n in enumerate(LENGTHS):
        signed = i % 3 == 2
        values = [round(rng.uniform(-0.6, 0.6) if signed else rng.uniform(0.05, 0.6), 6) for _ in range(n)]
        dates = [START + k for k in range(n)]
        series = ql.RealTimeSeries(dates, values)
        for size in (1, 2, 5, 10):
            if size > n:
                continue
            result = ql.ConstantEstimator(size).calculate(series)
            out.append({"name": f"{n} {'signed' if signed else 'positive'} values, a window of {size}",
                        "inputs": {"settings": st, "dates": [d.serialNumber() for d in dates], "values": values, "size": size},
                        "expected": {"dates": v.enc(list(result.dates())), "values": v.enc(list(result.values()))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/volatility/constantestimator.hpp", cases)
