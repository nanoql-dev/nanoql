# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""ECB's reserve maintenance dates on QuantLib's known dates (ql/time/ecb.hpp), which no case edits: - "isECBcode": 40
strings, codes in either case and malformed ones; - "knownDates": for each known date d, isECBdate(d) and isECBdate(d -
1), code(d), nextDate(d - 1) and nextDate(d) (which refuses after the last), and nextCode(d - 1); - "months": date(m, y)
for every month of 2004 to 2025 (refused after the last known date); - "codes": date(code, referenceDate),
nextDate(code, referenceDate) and nextDates(code, referenceDate) for every month's code from 2004 to 2025, against
references in 2000 and 2150 (the latter refused), and nextCode(code); - "nextDates": nextDates(d) for 24 dates from 2004
to 2025. Dates at the start of Date's range, where QuantLib steps out of it, are a documented divergence and left out.
The evaluation date is never read: every date is given. Read back by
tests/NanoQL.Tests/Vectors/Time/ECBVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

MONTHS = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"]
STRINGS = ["JAN00", "FEB78", "mar58", "aPr99", "may05", "Jun10", "jUL15", "AUG24", "sep20", "OCT01", "nov99",
           "DEC09", "", "JUNE99", "JUN1999", "JUNE", "JUNE1999", "1999", "JAN5 ", "ja 05", "JAN0", "JANN5", "JAN-5",
           "XYZ05", "J AN5", "0JAN5", "dec 9", "DE C9", "Dec9x", "MAY5A", "AAA00", "JAN 5", "jan10", "JAN10", "MAR13",
           "FEB15", "OCT17", "dEC17", "dec99", "NOV17"]


def known_dates():
    """The known dates: the wheel binds no knownDates(), so nextDates(minDate), every one, as QuantLib's ecbDates case
    reads them (test-suite/dates.cpp:70)."""
    return list(ql.ECB.nextDates(ql.Date.minDate()))


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = [{"name": "isECBcode", "inputs": {"settings": st, "strings": STRINGS},
            "expected": {"isECBcode": [ql.ECB.isECBcode(s) for s in STRINGS]}}]

    ds = known_dates()
    out.append({"name": "knownDates", "inputs": {"settings": st, "dates": v.enc(ds)},
                "expected": {"isECBdate": [v.attempt(lambda d=d: ql.ECB.isECBdate(d)) for d in ds],
                             "isECBdateBefore": [v.attempt(lambda d=d: ql.ECB.isECBdate(d - 1)) for d in ds],
                             "code": [v.attempt(lambda d=d: ql.ECB.code(d)) for d in ds],
                             "nextDateBefore": [v.attempt(lambda d=d: ql.ECB.nextDate(d - 1)) for d in ds],
                             "nextDate": [v.attempt(lambda d=d: ql.ECB.nextDate(d)) for d in ds],
                             "nextCodeBefore": [v.attempt(lambda d=d: ql.ECB.nextCode(d - 1)) for d in ds]}})

    for y in range(2004, 2026):
        out.append({"name": f"months {y}", "inputs": {"settings": st, "year": y},
                    "expected": {"date": [v.attempt(lambda m=m: ql.ECB.date(m, y)) for m in range(1, 13)]}})

    codes = [f"{m}{y % 100:02d}" for y in range(2004, 2026) for m in MONTHS]
    for ref in (ql.Date(1, ql.January, 2000), ql.Date(1, ql.January, 2150)):
        out.append({"name": f"codes {ref.serialNumber()}",
                    "inputs": {"settings": st, "codes": codes, "referenceDate": v.enc(ref)},
                    "expected": {
                        "date": [v.attempt(lambda c=c: ql.ECB.date(c, ref)) for c in codes],
                        "nextDate": [v.attempt(lambda c=c: ql.ECB.nextDate(c, ref)) for c in codes],
                        "nextDatesCount": [v.attempt(lambda c=c: len(ql.ECB.nextDates(c, ref))) for c in codes],
                        "nextCode": [v.attempt(lambda c=c: ql.ECB.nextCode(c)) for c in codes]}})

    dates = [ql.Date(15, m, y) for y in range(2004, 2026, 2) for m in (ql.March, ql.October)] + \
        [ql.Date(18, ql.December, 2024), ql.Date(17, ql.December, 2024)]
    out.append({"name": "nextDates", "inputs": {"settings": st, "dates": v.enc(dates)},
                "expected": {f"nextDates{i}": v.attempt(lambda d=d: list(ql.ECB.nextDates(d)))
                             for i, d in enumerate(dates)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/time/ecb.hpp", cases)
