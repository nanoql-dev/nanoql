# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's TimeSeries<Real> (ql/timeseries.hpp): 20 series built from a list of dates and a list of
values, of 0 to 50 dates, unsorted, with duplicate dates in some (a date given twice keeps its last value) and surplus
values in others (QuantLib reads one value per date and leaves the rest). Each series gives its size, whether it is
empty, its dates and values in date order, its first and last dates (which throw for an empty series), and its value
at each stored date, in date order.

Each case's inputs are the two lists: "dates" as serials, in the order given, and "values". Read back by
tests/NanoQL.Tests/Vectors/TimeSeriesVectorTests.cs.

The wheel's RealTimeSeries is QuantLib-SWIG's %template(RealTimeSeries) TimeSeries<Real> (QuantLib-SWIG 1.44,
SWIG/timeseries.i). Its constructor from a DateVector and a DoubleVector, an %extend, calls TimeSeries<Real>(d.begin(),
d.end(), v.begin()) and checks no sizes, so every case gives at least as many values as dates (fewer would read past
the vector). dates, values, firstDate and lastDate are the C++ members; __len__ is size(); __bool__ is !empty();
__getitem__ calls the non-const operator[], which inserts a missing date, so it is called only on stored dates, after
every other output."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

# (number of dates, how the dates are drawn, surplus values)
SERIES = [
    (0, "distinct", 0), (0, "distinct", 2), (1, "distinct", 0), (2, "distinct", 0), (2, "repeated", 0),
    (3, "distinct", 1), (5, "pool", 0), (8, "distinct", 0), (10, "pool", 3), (12, "distinct", 0),
    (15, "consecutive", 0), (20, "pool", 0), (25, "distinct", 2), (30, "pool", 0), (33, "consecutive", 1),
    (37, "distinct", 0), (40, "pool", 0), (45, "distinct", 0), (48, "pool", 5), (50, "distinct", 0),
]
FIRST = ql.Date(1, ql.January, 1990).serialNumber()
LAST = ql.Date(31, ql.December, 2060).serialNumber()


def draw_dates(rng, n, how):
    """n serials in random order: distinct ones over the whole range; one date given n times; n draws from a pool of
    about two thirds as many dates, so that some repeat; or a run of consecutive dates, shuffled."""
    if how == "distinct":
        return rng.sample(range(FIRST, LAST + 1), n)
    if how == "repeated":
        return [rng.randrange(FIRST, LAST + 1)] * n
    if how == "pool":
        pool = rng.sample(range(FIRST, LAST + 1), max(1, (2 * n) // 3))
        return [rng.choice(pool) for _ in range(n)]
    start = rng.randrange(FIRST, LAST - n)
    out = list(range(start, start + n))
    rng.shuffle(out)
    return out


def cases():
    rng = random.Random(v.SEED)
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for n, how, surplus in SERIES:
        serials = draw_dates(rng, n, how)
        values = [round(rng.uniform(-100.0, 100.0), rng.choice([2, 6, 15])) for _ in range(n + surplus)]
        ts = ql.RealTimeSeries([ql.Date(s) for s in serials], values)
        stored = list(ts.dates())
        expected = {
            "size": len(ts),
            "empty": not bool(ts),
            "dates": v.enc(stored),
            "values": v.enc(list(ts.values())),
            "firstDate": v.attempt(ts.firstDate),
            "lastDate": v.attempt(ts.lastDate),
        }
        # Last: __getitem__ is the inserting operator[], here only on stored dates.
        expected["valueAt"] = [v.enc(ts[d]) for d in stored]
        out.append({"name": f"{n} {how} dates, {surplus} surplus values",
                    "inputs": {"settings": st, "dates": serials, "values": values}, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/timeseries.hpp", cases)
