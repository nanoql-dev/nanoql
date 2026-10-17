# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for io::short_date (ql/time/date.hpp), which no wheel binding or ported QuantLib test checks
(docs/porting-playbook.md §9.4), each output computed by the C++ harness: every 97th serial from the first date
QuantLib allows to the last; the two edges, the four dates nanoql's own tests used before these vectors replaced them,
and three more; and the null date.
tests/NanoQL.Tests/Vectors/Cxx/Time/DateCxxVectorTests.cs reads it back."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

# (day, month, year): the replaced own tests' four dates, then a leap day, the first day of 2000 and the last of February 2100,
# a century year that isn't leap.
NAMED = [(5, 3, 2026), (31, 12, 2199), (1, 1, 1901), (15, 10, 2026), (29, 2, 2024), (1, 1, 2000), (28, 2, 2100)]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def case(name, dates):
            return {"name": name, "inputs": {"settings": st, "dates": v.enc(dates)},
                    "expected": {"shortDate": [v.attempt(lambda d=d: H.call("io::short_date", d)) for d in dates]}}

        named = [H.call("Date(Day,Month,Year)", d, m, y) for d, m, y in NAMED]
        return [case("every 97th serial", list(range(367, 109575, 97))),
                case("edges and named dates", [367, 109574] + named),
                case("null date", [None])]


if __name__ == "__main__":
    v.main(__file__, "ql/time/date.hpp", cases)
