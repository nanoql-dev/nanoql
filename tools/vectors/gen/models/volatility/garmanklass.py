# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Garman-Klass estimators (ql/models/volatility/garmanklass.hpp): GarmanKlassSigma1, 3, 4, 5
and 6 and ParkinsonSigma, the open-close ones (1, 3 and 6) from the previous close and today's open.

Eight interval-price series of 2 to 40 consecutive daily dates, a random walk from 100 with each interval's open, close,
high and low (the high the largest of the four, the low the smallest), each through every estimator at year fractions
1/252 and 1/360 and, for the open-close ones, market-open fractions 0.3 and 0.6: the output's dates and values. An empty
series, which the open-close estimators read past its end in QuantLib, is left out; the refusal is a divergence fact.
GarmanKlassSimpleSigma, which the wheel doesn't name, is the simple local estimator's harness file's.

The wheel's calls, by QuantLib-SWIG 1.44's volatilities.i: each estimator's constructor and calculate on an
IntervalPriceTimeSeries from IntervalPrice.makeSeries. Read back by
tests/NanoQL.Tests/Vectors/Models/Volatility/GarmanKlassVectorTests.cs."""
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
LENGTHS = [2, 3, 5, 8, 12, 20, 30, 40]


def walk(rng, n):
    """n intervals of a random walk: each open near the previous close, the close a step on, the high and low beyond."""
    o, c, h, lo = [], [], [], []
    price = 100.0
    for _ in range(n):
        op = round(price * (1 + rng.gauss(0, 0.004)), 4)
        cl = round(op * (1 + rng.gauss(0, 0.015)), 4)
        hi = round(max(op, cl) * (1 + abs(rng.gauss(0, 0.006))), 4)
        lw = round(min(op, cl) * (1 - abs(rng.gauss(0, 0.006))), 4)
        o.append(op)
        c.append(cl)
        h.append(hi)
        lo.append(lw)
        price = cl
    return o, c, h, lo


def cases():
    rng = random.Random(v.SEED)
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for n in LENGTHS:
        o, c, h, lo = walk(rng, n)
        dates = [START + k for k in range(n)]
        series = ql.IntervalPrice.makeSeries(dates, o, c, h, lo)
        inputs = {"settings": st, "dates": [d.serialNumber() for d in dates], "open": o, "close": c, "high": h, "low": lo}
        for y in (1 / 252.0, 1 / 360.0):
            estimators = [("ParkinsonSigma", None, ql.ParkinsonSigma(y)), ("GarmanKlassSigma4", None, ql.GarmanKlassSigma4(y)),
                          ("GarmanKlassSigma5", None, ql.GarmanKlassSigma5(y))]
            for f in (0.3, 0.6):
                estimators += [("GarmanKlassSigma1", f, ql.GarmanKlassSigma1(y, f)), ("GarmanKlassSigma3", f, ql.GarmanKlassSigma3(y, f)),
                               ("GarmanKlassSigma6", f, ql.GarmanKlassSigma6(y, f))]
            for name, f, estimator in estimators:
                result = estimator.calculate(series)
                case_inputs = dict(inputs, estimator=name, yearFraction=y)
                if f is not None:
                    case_inputs["marketOpenFraction"] = f
                out.append({"name": f"{name}, {n} intervals, y {y:.6g}" + (f", f {f}" if f is not None else ""),
                            "inputs": case_inputs,
                            "expected": {"dates": v.enc(list(result.dates())), "values": v.enc(list(result.values()))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/volatility/garmanklass.hpp", cases)
