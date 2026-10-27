# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's IntervalPrice (ql/prices.hpp, .cpp): its inspectors, its modifiers and its series helpers.

Twenty prices of random open, close, high and low: each value by inspector and by value(type), and each after
setValue on each type and after setValues (the wheel's modify the object, so each case reads a fresh one; nanoql's
return a copy). Then eight series of 1 to 30 dates, unsorted: makeSeries' dates in date order, and extractValues and
extractComponent for each type. midEquivalent and midSafe, which the wheel doesn't name, are
QuantLib's own cases' (prices.cpp).

The wheel's calls, by QuantLib-SWIG 1.44's prices.i: IntervalPrice(open, close, high, low), open(), close(), high(),
low(), value(type), setValue, setValues, makeSeries, extractValues and extractComponent. Read back by
tests/NanoQL.Tests/Vectors/PricesVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

TYPES = [("Open", ql.IntervalPrice.Open), ("Close", ql.IntervalPrice.Close), ("High", ql.IntervalPrice.High),
         ("Low", ql.IntervalPrice.Low)]
FIRST = ql.Date(1, ql.January, 1990).serialNumber()


def four(rng):
    return [round(rng.uniform(1.0, 200.0), rng.choice([2, 6, 15])) for _ in range(4)]


def cases():
    rng = random.Random(v.SEED)
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for i in range(20):
        o, c, h, lo = four(rng)
        replacement = round(rng.uniform(1.0, 200.0), 6)
        news = four(rng)
        p = ql.IntervalPrice(o, c, h, lo)
        expected = {"open": p.open(), "close": p.close(), "high": p.high(), "low": p.low()}
        for name, t in TYPES:
            expected["value" + name] = p.value(t)
        for name, t in TYPES:
            q = ql.IntervalPrice(o, c, h, lo)
            q.setValue(replacement, t)
            expected["set" + name] = [q.open(), q.close(), q.high(), q.low()]
        q = ql.IntervalPrice(o, c, h, lo)
        q.setValues(*news)
        expected["setValues"] = [q.open(), q.close(), q.high(), q.low()]
        out.append({"name": f"price {i}", "inputs": {"settings": st, "price": [o, c, h, lo], "replacement": replacement, "values": news},
                    "expected": expected})
    for n in (1, 2, 3, 5, 8, 12, 20, 30):
        serials = rng.sample(range(FIRST, FIRST + 20000), n)
        quads = [four(rng) for _ in range(n)]
        cols = [[q[k] for q in quads] for k in range(4)]
        series = ql.IntervalPrice.makeSeries([ql.Date(s) for s in serials], *cols)
        expected = {"dates": v.enc(list(series.dates()))}
        for name, t in TYPES:
            expected["values" + name] = list(ql.IntervalPrice.extractValues(series, t))
            component = ql.IntervalPrice.extractComponent(series, t)
            expected["component" + name + "Dates"] = v.enc(list(component.dates()))
            expected["component" + name + "Values"] = list(component.values())
        out.append({"name": f"a series of {n} dates",
                    "inputs": {"settings": st, "dates": serials, "open": cols[0], "close": cols[1], "high": cols[2], "low": cols[3]},
                    "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/prices.hpp", cases)
