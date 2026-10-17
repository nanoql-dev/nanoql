# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for days(const Period&), io::long_period and operator*(TimeUnit, T) (ql/time/period.hpp), which no wheel
binding or ported QuantLib test checks (docs/porting-playbook.md §9.4), each output computed by the C++ harness: the three over thirteen lengths in
every TimeUnit, Days to Microseconds, and days and io::long_period in the two units just outside the enum, 9 and -1.
tests/NanoQL.Tests/Vectors/Cxx/Time/PeriodCxxVectorTests.cs reads it back."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

# Thirteen lengths: ten around zero, and -2, 5 and 30 from the own tests of PeriodFunctions and TimeUnitExtensions
# these vectors replaced.
LENGTHS = [-3, -2, -1, 0, 1, 2, 3, 5, 7, 10, 30, 52, 366]
# TimeUnit's members in its order (ql/time/timeunit.hpp), each named as its operator<< writes it.
UNITS = ["Days", "Weeks", "Months", "Years", "Hours", "Minutes", "Seconds", "Milliseconds", "Microseconds"]
# Values just outside TimeUnit. nanoql refuses them (docs/porting-playbook.md §2), so one appears only where QuantLib
# throws too: days() returns 0.0 for a zero length before it reads the unit, so its case leaves 0 out.
OUTSIDE = [9, -1]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def days(u, lengths):
            return [v.attempt(lambda n=n: H.call("days", {"length": n, "units": u})) for n in lengths]

        def long_period(u, lengths):
            return [v.attempt(lambda n=n: H.call("io::long_period", {"length": n, "units": u})) for n in lengths]

        def times(u, n):
            p = H.call("operator*(TimeUnit,Integer)", u, n)
            return [p["length"], p["units"]]

        out = []
        for u, name in enumerate(UNITS):
            out.append({"name": name, "inputs": {"settings": st, "units": u, "lengths": LENGTHS},
                        "expected": {"days": days(u, LENGTHS), "longPeriod": long_period(u, LENGTHS),
                                     "unitTimesLength": [v.attempt(lambda n=n: times(u, n)) for n in LENGTHS]}})
        nonzero = [n for n in LENGTHS if n != 0]
        for u in OUTSIDE:
            for output, f, lengths in (("days", days, nonzero), ("longPeriod", long_period, LENGTHS)):
                expected = f(u, lengths)
                assert all(x == v.THROWS for x in expected), f"QuantLib's {output} returns for unit {u}: {expected}"
                out.append({"name": f"unit {u}, {output}", "inputs": {"settings": st, "units": u, "lengths": lengths},
                            "expected": {output: expected}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/period.hpp", cases)
