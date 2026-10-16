# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Date (ql/time/date.hpp): the inspectors, start and end of month, nextWeekday, the long and ISO
text, period additions with throws past the range, nthWeekday, and differences with daysBetween."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

WEEKDAYS = [ql.Sunday, ql.Monday, ql.Tuesday, ql.Wednesday, ql.Thursday, ql.Friday, ql.Saturday]
UNITS = [ql.Days, ql.Weeks, ql.Months, ql.Years]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    edges = set()
    for y in (1901, 1904, 1999, 2000, 2024, 2026, 2100, 2198, 2199):
        for (d, m) in ((1, 1), (28, 2), (31, 1), (30, 4), (31, 12)):
            edges.add(ql.Date(d, m, y).serialNumber())
        if ql.Date.isLeap(y):
            edges.add(ql.Date(29, 2, y).serialNumber())
    serials = sorted(set(range(367, 109575, 97)) | edges | {367, 368, 109573, 109574})
    for s in serials:
        d = ql.Date(s)
        out.append({"name": f"accessors {s}",
                    "inputs": {"settings": st, "date": s},
                    "expected": {"weekday": int(d.weekday()), "dayOfMonth": d.dayOfMonth(),
                                 "dayOfYear": d.dayOfYear(), "month": int(d.month()), "year": d.year(),
                                 "isLeap": ql.Date.isLeap(d.year()),
                                 "startOfMonth": v.enc(ql.Date.startOfMonth(d)),
                                 "isStartOfMonth": ql.Date.isStartOfMonth(d),
                                 "endOfMonth": v.enc(ql.Date.endOfMonth(d)),
                                 "isEndOfMonth": ql.Date.isEndOfMonth(d),
                                 "nextWeekdayWednesday": v.attempt(lambda: ql.Date.nextWeekday(d, ql.Wednesday)),
                                 "toString": str(d), "iso": d.ISO()}})
    periods = [(1, 0), (-1, 0), (1, 1), (-2, 1), (1, 2), (-1, 2), (13, 2), (1, 3), (-1, 3), (10, 3)]
    for s in serials[::9]:
        d = ql.Date(s)
        out.append({"name": f"plus periods {s}",
                    "inputs": {"settings": st, "date": s, "periods": [list(p) for p in periods]},
                    "expected": {"result": [v.attempt(lambda p=p: d + ql.Period(p[0], UNITS[p[1]]))
                                            for p in periods]}})
    for y in (1901, 1904, 2000, 2026, 2100, 2199):
        for m in range(1, 13):
            out.append({"name": f"nthWeekday {y}-{m}",
                        "inputs": {"settings": st, "year": y, "month": m, "n": [1, 2, 5], "weekday": [1, 4, 7]},
                        "expected": {"result": [[v.attempt(lambda n=n, w=w: ql.Date.nthWeekday(n, WEEKDAYS[w - 1], m, y))
                                                 for w in (1, 4, 7)] for n in (1, 2, 5)]}})
    rnd = random.Random(v.SEED)
    for i in range(200):
        a, b = rnd.randrange(367, 109575), rnd.randrange(367, 109575)
        out.append({"name": f"difference {i}",
                    "inputs": {"settings": st, "d1": a, "d2": b},
                    "expected": {"difference": ql.Date(b) - ql.Date(a),
                                 "daysBetween": ql.daysBetween(ql.Date(a), ql.Date(b))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/date.hpp", cases)
