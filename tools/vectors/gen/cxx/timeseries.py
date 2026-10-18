# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's TimeSeries<Real> (ql/timeseries.hpp) that the wheel doesn't expose
(docs/porting-playbook.md §9.4), each output computed by the C++ harness: the constructor from a first date and a
list of values, which assigns them to consecutive dates; iteration from begin() to end(), in date order; and the const
operator[], which reads a date without inserting it and returns Null<Real>() for a date the series doesn't hold.

- First-date series: empty, one value, a month across 29 February 2024, a run of 30 values, and runs ending near
  31 December 2199. QuantLib moves its date past every value, the last included, so a run whose last value falls on
  31 December 2199 throws.
- Date-list series: unsorted, with duplicate dates (the last value is kept) and surplus values (left unread), as in
  tools/vectors/gen/timeseries.py.

Each case's input "series" is the harness's spec, {"firstDate", "values"} or {"dates", "values"}, dates as serials;
"lookups" lists the dates read, stored and missing ones (before, between and after them). Its outputs are "iteration",
the [date, value] pairs, and "lookup", the value at each date, or null where the series has none. A series QuantLib
refuses gives the throw as "iteration" and no lookups. Read back by
tests/NanoQL.Tests/Vectors/Cxx/TimeSeriesCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

MAX_SERIAL = 109574  # 31 December 2199


def first_date_series(H, rng):
    """(name, firstDate, number of values)."""
    leap = H.call("Date(Day,Month,Year)", 15, 2, 2024)
    return [
        ("first date, no values", leap, 0),
        ("first date, one value", H.call("Date(Day,Month,Year)", 15, 3, 2005), 1),
        ("first date, across 29 February 2024", leap, 20),
        ("first date, 30 values", rng.randrange(367, 100000), 30),
        ("first date, ending the day before the last valid date", MAX_SERIAL - 3, 3),
        ("first date, ending on the last valid date", MAX_SERIAL - 2, 3),
        ("first date, one value on the last valid date", MAX_SERIAL, 1),
    ]


def date_list_series(rng):
    """(name, dates, values)."""
    out = []
    for n, pool, surplus in [(6, 4, 0), (12, 12, 2), (20, 9, 1), (25, 25, 0)]:
        dates = rng.sample(range(38000, 39000), pool)
        serials = [rng.choice(dates) for _ in range(n)] if pool < n else dates
        values = [round(rng.uniform(-50.0, 50.0), 4) for _ in range(n + surplus)]
        out.append((f"{n} dates from {pool}, {surplus} surplus values", serials, values))
    return out


def lookups(rng, stored):
    """Every stored date, and missing dates: before the first, after the last, and between stored ones."""
    if not stored:
        return [38426, 367, MAX_SERIAL]
    lo, hi, held = min(stored), max(stored), set(stored)
    gaps = [d for d in range(lo, hi + 1) if d not in held]
    missing = [lo - 1, hi + 1] + rng.sample(gaps, min(3, len(gaps)))
    return sorted(held) + [d for d in missing if 367 <= d <= MAX_SERIAL]


def cases():
    rng = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        specs = []
        for name, first, n in first_date_series(H, rng):
            specs.append((name, {"firstDate": first, "values": [round(rng.uniform(0.0, 10.0), 3) for _ in range(n)]}))
        for name, serials, values in date_list_series(rng):
            specs.append((name, {"dates": serials, "values": values}))
        out = []
        for name, spec in specs:
            try:
                iteration = H.call("TimeSeries<Real>::begin", spec)
            except RuntimeError:
                out.append({"name": name, "inputs": {"settings": st, "series": spec, "lookups": []},
                            "expected": {"iteration": dict(v.THROWS), "lookup": []}})
                continue
            dates = lookups(rng, [d for d, _ in iteration])
            out.append({"name": name, "inputs": {"settings": st, "series": spec, "lookups": dates}, "expected": {
                "iteration": iteration,
                "lookup": [v.attempt(lambda d=d: H.call("TimeSeries<Real>::operator[](Date) const", spec, d))
                           for d in dates]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/timeseries.hpp", cases)
