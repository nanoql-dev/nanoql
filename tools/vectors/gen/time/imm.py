# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's IMM date functions (ql/time/imm.hpp).

- 3 cases "isIMMdate <year>": every day of 2026 to 2028, isIMMdate on the main cycle and on any month.
- Appended after them, the cases of IMM's other functions, built by time/asx.py's functions as ASX's are: every day of
  2023 to 2026 and 4 dates near the end of QuantLib's range with isIMMdate, nextDate and nextCode on both cycles; the
  96 codes and 12 strings that aren't codes with isIMMcode, date, nextDate and nextCode, with and without reference
  dates; every IMM date of 2023 to 2026 on any month and 8 dates that aren't, with their codes; and 6 evaluation dates
  on, before and after a third Wednesday and the turn of a year, with nextDate and nextCode with no date.

Read back by tests/NanoQL.Tests/Vectors/Time/IMMVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_asx", os.path.join(v.GEN_ROOT, "time", "asx.py"))
asx = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(asx)

D = ql.Date
IMM = asx.Functions(ql.IMM, ql.IMM.isIMMdate, ql.IMM.isIMMcode)


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for y in (2026, 2027, 2028):
        days = [ql.Date(1, 1, y) + i for i in range(366 if ql.Date.isLeap(y) else 365)]
        out.append({"name": f"isIMMdate {y}",
                    "inputs": {"settings": st, "dates": [d.serialNumber() for d in days]},
                    "expected": {"mainCycle": [ql.IMM.isIMMdate(d, True) for d in days],
                                 "anyMonth": [ql.IMM.isIMMdate(d, False) for d in days]}})
    # Wednesday 15 January 2025, the third of the month, and the days around it; Wednesday 19 March 2025, a
    # main-cycle month's; 31 December 2025, before the turn of a year
    todays = [D(14, ql.January, 2025), D(15, ql.January, 2025), D(16, ql.January, 2025), D(19, ql.March, 2025),
              D(20, ql.March, 2025), D(31, ql.December, 2025)]
    not_dates = [D(21, ql.March, 2024), D(12, ql.June, 2024), D(18, ql.June, 2024), D(20, ql.June, 2024),
                 D(1, ql.January, 2025), D(12, ql.August, 2026), D(14, ql.October, 2026), D(22, ql.October, 2026)]
    out += asx.date_function_cases(IMM, "IMM", todays, not_dates)
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    # QuantLib's recursion: a third Wednesday's next IMM date is the next month's
    assert by_name["today 2025-01-15"]["nextDateAny"] == D(19, ql.February, 2025).serialNumber()
    assert by_name["dates near 2199"]["nextDateAny"][-1] == v.THROWS


if __name__ == "__main__":
    v.main(__file__, "ql/time/imm.hpp", cases)
