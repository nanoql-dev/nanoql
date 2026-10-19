# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ASX date functions (ql/time/asx.hpp): every date of 2023 to 2026, the two-character codes,
the ASX dates' codes and the evaluation date's next dates.

- 4 cases "dates <year>": every day of the year, its isASXdate on the main cycle and on any month, its nextDate and
  nextCode on both; and a case of dates near the end of QuantLib's range, whose next date QuantLib refuses past 2199.
- 1 case "codes": the 96 codes of the twelve month letters, upper and lower case, and the digits 0, 3, 6 and 9, and 12
  strings that aren't codes (empty, one or three characters, a letter that isn't a month's, a digit first, blanks, a
  letter outside ASCII); each string's isASXcode on both cycles, its date with no reference date (the evaluation
  date's) and at four reference dates (in 1905, where a year digit 0 reads as 1910, 2024, 2029 and 1 December 2199,
  where QuantLib refuses dates past 2199), its next date on the main cycle with no reference date and on any month at
  the first reference date, and its next code on the main cycle at the last but one and on any month with none; a
  string that isn't a code is refused by each.
- 1 case "codes of dates": every ASX date of 2023 to 2026 on any month and 8 dates that aren't, each one's code,
  which QuantLib refuses for a date that isn't an ASX date.
- 6 cases "today": nextDate and nextCode with no date on both cycles, on evaluation dates on, before and after a second
  Friday, on a main-cycle month's and on the turn of a year.

The functions here are shared with time/imm.py, which builds IMM's cases with them. Each case's inputs hold its
settings, its "kind" ("dates", "codes", "code" or "today") and its "dates" or "codes" and "referenceDates". Read back
by tests/NanoQL.Tests/Vectors/Time/ASXVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

D = ql.Date

# The evaluation date of every case but the "today" ones: a Thursday, after the month's ASX and IMM dates.
EVALUATION_DATE = D(22, ql.October, 2026)

LETTERS = "FGHJKMNQUVXZ"
CODES = [letter + digit for digit in "0369" for letter in LETTERS + LETTERS.lower()]
NOT_CODES = ["", "H", "H10", "A5", "HX", "5H", " 5", "H ", "h/", "Z:", "é", "é5"]
REFERENCE_DATES = [D(1, ql.March, 1905), D(15, ql.January, 2024), D(30, ql.December, 2029), D(1, ql.December, 2199)]
LATE_DATES = [D(1, ql.November, 2199), D(10, ql.December, 2199), D(14, ql.December, 2199), D(31, ql.December, 2199)]


class Functions:
    """A date module's functions: QuantLib-Python's ASX or IMM."""

    def __init__(self, module, is_date, is_code):
        self.module = module
        self.is_date = is_date
        self.is_code = is_code


ASX = Functions(ql.ASX, ql.ASX.isASXdate, ql.ASX.isASXcode)


def year_dates(y):
    return [D(1, ql.January, y) + i for i in range(366 if ql.Date.isLeap(y) else 365)]


def dates_case(name, f, dates):
    with v.case_settings(EVALUATION_DATE) as st:
        expected = {"isDateMain": [f.is_date(d, True) for d in dates],
                    "isDateAny": [f.is_date(d, False) for d in dates],
                    "nextDateMain": [v.attempt(lambda d=d: f.module.nextDate(d, True)) for d in dates],
                    "nextDateAny": [v.attempt(lambda d=d: f.module.nextDate(d, False)) for d in dates],
                    "nextCodeMain": [v.attempt(lambda d=d: f.module.nextCode(d, True)) for d in dates],
                    "nextCodeAny": [v.attempt(lambda d=d: f.module.nextCode(d, False)) for d in dates]}
        return {"name": name, "inputs": {"settings": st, "kind": "dates", "dates": v.enc(dates)},
                "expected": expected}


def codes_case(name, f):
    codes = CODES + NOT_CODES
    refs = REFERENCE_DATES
    m = f.module
    with v.case_settings(EVALUATION_DATE) as st:
        expected = {"isCodeMain": [f.is_code(c, True) for c in codes],
                    "isCodeAny": [f.is_code(c, False) for c in codes],
                    "date": [v.attempt(lambda c=c: m.date(c)) for c in codes],
                    "dateAt": [[v.attempt(lambda c=c, r=r: m.date(c, r)) for r in refs] for c in codes],
                    "nextDateMain": [v.attempt(lambda c=c: m.nextDate(c, True)) for c in codes],
                    "nextDateAny": [v.attempt(lambda c=c: m.nextDate(c, False, refs[0])) for c in codes],
                    "nextCodeMain": [v.attempt(lambda c=c: m.nextCode(c, True, refs[2])) for c in codes],
                    "nextCodeAny": [v.attempt(lambda c=c: m.nextCode(c, False)) for c in codes]}
        return {"name": name, "inputs": {"settings": st, "kind": "codes", "codes": codes,
                                         "referenceDates": v.enc(refs)},
                "expected": expected}


def code_case(name, f, dates):
    with v.case_settings(EVALUATION_DATE) as st:
        return {"name": name, "inputs": {"settings": st, "kind": "code", "dates": v.enc(dates)},
                "expected": {"code": [v.attempt(lambda d=d: f.module.code(d)) for d in dates]}}


def today_case(name, f, today):
    m = f.module
    with v.case_settings(today) as st:
        return {"name": name, "inputs": {"settings": st, "kind": "today"},
                "expected": {"nextDateMain": v.enc(m.nextDate(ql.Date(), True)),
                             "nextDateAny": v.enc(m.nextDate(ql.Date(), False)),
                             "nextCodeMain": m.nextCode(ql.Date(), True),
                             "nextCodeAny": m.nextCode(ql.Date(), False)}}


def date_function_cases(f, label, todays, not_dates):
    """The cases of a date module's completed functions."""
    out = [dates_case(f"dates {y}", f, year_dates(y)) for y in range(2023, 2027)]
    out.append(dates_case("dates near 2199", f, LATE_DATES))
    out.append(codes_case("codes", f))
    dates = [d for y in range(2023, 2027) for d in year_dates(y) if f.is_date(d, False)] + not_dates
    out.append(code_case(f"codes of {label} dates", f, dates))
    out += [today_case(f"today {t.ISO()}", f, t) for t in todays]
    return out


def cases():
    # Friday 10 January 2025, the second of the month, and the days around it; Friday 14 March 2025, a main-cycle
    # month's; 31 December 2025, before the turn of a year
    todays = [D(9, ql.January, 2025), D(10, ql.January, 2025), D(11, ql.January, 2025), D(14, ql.March, 2025),
              D(15, ql.March, 2025), D(31, ql.December, 2025)]
    not_dates = [D(15, ql.March, 2024), D(7, ql.June, 2024), D(13, ql.June, 2024), D(19, ql.June, 2024),
                 D(1, ql.January, 2025), D(8, ql.August, 2026), D(9, ql.October, 2026), D(16, ql.October, 2026)]
    out = date_function_cases(ASX, "ASX", todays, not_dates)
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    # QuantLib's recursion: a second Friday's next ASX date is the next month's
    assert by_name["today 2025-01-10"]["nextDateAny"] == D(14, ql.February, 2025).serialNumber()
    # dates past 2199 are refused
    assert by_name["dates near 2199"]["nextDateAny"][-1] == v.THROWS
    assert v.THROWS in by_name["codes"]["date"]


if __name__ == "__main__":
    v.main(__file__, "ql/time/asx.hpp", cases)
