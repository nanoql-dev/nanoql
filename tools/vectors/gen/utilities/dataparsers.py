# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The data parsers (ql/utilities/dataparsers.hpp) on well-formed input: DateParser::parseISO on every 97th date from 1
January 1901 to 31 December 2199, with the range's ends, leap days and month ends, and on the strings QuantLib refuses
itself (a bad length or separator, a month or day out of range); PeriodParser::parse on periods of each unit, in either
case, signed, and combined, and on the strings QuantLib refuses itself (too short, no unit, no number, a combination of
months and days). A malformed number QuantLib's std::stoi reads leniently is a documented divergence and left out. A
period is written as its length and its unit, QuantLib's TimeUnit integer. parseFormatted isn't ported. Read back by
tests/NanoQL.Tests/Vectors/Utilities/DataParsersVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

PERIODS = ["1D", "3d", "2W", "1w", "1M", "6m", "1Y", "10y", "0D", "12M", "18M", "1Y6M", "2Y3M", "1W2D", "2w3d", "-3M",
           "+3M", "-1Y6M", "1Y-6M", "+1y+1m", "5Y12M", "100Y", "365D", "52W", "007D", "-0M", "1M1Y", "1D1W",
           "5", "5X", "xD", "M", "3MD", "1M1D", "1Y2D", "1W1M", "1Y6", "DD", "12", "--"]
EXTRA_DATES = ["1901-01-01", "2199-12-31", "2000-02-29", "2024-02-29", "1904-02-29", "2100-02-28", "2006-01-15",
               "2006-12-31", "2006-13-15", "2006-00-15", "2006-02-30", "2006-04-31", "2100-02-29", "1900-12-31",
               "2200-01-01", "2006/01/15", "2006-01-155", "06-01-15", "20060115", "2006-1-15"]


def iso(d):
    return f"{d.year():04d}-{int(d.month()):02d}-{d.dayOfMonth():02d}"


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    dates = []
    d = ql.Date(1, ql.January, 1901)
    while True:
        dates.append(iso(d))
        if d.serialNumber() + 97 > ql.Date.maxDate().serialNumber():
            break
        d = d + 97
    strings = dates + EXTRA_DATES
    out = [{"name": "parseISO", "inputs": {"settings": st, "strings": strings},
            "expected": {"date": [v.attempt(lambda s=s: ql.DateParser.parseISO(s)) for s in strings]}}]
    out.append({"name": "PeriodParser.parse", "inputs": {"settings": st, "strings": PERIODS},
                "expected": {"length": [v.attempt(lambda s=s: ql.PeriodParser.parse(s).length()) for s in PERIODS],
                             "units": [v.attempt(lambda s=s: int(ql.PeriodParser.parse(s).units())) for s in PERIODS]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/utilities/dataparsers.hpp", cases)
