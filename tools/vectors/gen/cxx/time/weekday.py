# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the weekday text of ql/time/weekday.hpp, io::long_weekday, io::short_weekday, io::shortest_weekday
and operator<<, each output computed by the C++ harness: every Weekday, Sunday (1) to Saturday (7), and the values 0
and 8 just outside it. tests/NanoQL.Tests/Vectors/Cxx/Time/WeekdayCxxVectorTests.cs reads it back."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

WEEKDAYS = list(range(0, 9))
# Values just outside Weekday. nanoql refuses them (docs/porting-playbook.md §2), so one appears only where QuantLib
# throws too.
OUTSIDE = [0, 8]
METHODS = {"longWeekday": "io::long_weekday", "shortWeekday": "io::short_weekday",
           "shortestWeekday": "io::shortest_weekday", "toString": "operator<<(Weekday)"}


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        expected = {k: [v.attempt(lambda w=w: H.call(m, w)) for w in WEEKDAYS] for k, m in METHODS.items()}
        for k, xs in expected.items():
            assert all(xs[WEEKDAYS.index(w)] == v.THROWS for w in OUTSIDE), f"QuantLib's {k} returns outside Weekday"
        return [{"name": "weekdays 0 to 8", "inputs": {"settings": st, "weekdays": WEEKDAYS}, "expected": expected}]


if __name__ == "__main__":
    v.main(__file__, "ql/time/weekday.hpp", cases)
