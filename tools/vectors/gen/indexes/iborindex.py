# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's IborIndex and OvernightIndex (ql/indexes/iborindex.hpp): 21 indexes on flat forwarding
curves, 16 Ibor indexes over the seven business-day conventions, end of month or not, tenors from 0D to 2Y, the four
calendars and three day counters of indexes/interestrateindex.py, and 5 overnight indexes.

One case per index, on the evaluation date 15 January 2025 with no stored fixings. Its outputs are the convention, the
end-of-month flag, maturityDate at 20 value dates (month ends, a leap day, holidays and weekend days among them), and
fixing at the same 20 dates, which forecasts from the curve for every later date (the 0D index's forecast is refused,
its period having no length); clone onto another flat curve: the clone's name, its type (the wheel's clone of an
overnight index returns an OvernightIndex, of any other index an IborIndex) and its forecasts at the 20 dates; and
the refusal of a forecast by a clone with no curve, QuantLib's empty handle, at the first later date that is a valid
fixing date. A refused value is {"throws": true}.

Each case's inputs hold the index spec ("index", as in indexes/interestrateindex.py, whose functions build it), the 20
dates ("dates"), the clone's curve ("cloneCurve") and the date of the refused forecast ("nullCurveDate"). Read back by
tests/NanoQL.Tests/Vectors/Indexes/IborIndexVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_interestrateindex",
                                               os.path.join(v.GEN_ROOT, "indexes", "interestrateindex.py"))
irx = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(irx)

TODAY = irx.TODAY

CONVENTIONS = [ql.Following, ql.ModifiedFollowing, ql.Preceding, ql.ModifiedPreceding, ql.Unadjusted,
               ql.HalfMonthModifiedFollowing, ql.Nearest]
TENORS = [(1, ql.Months), (3, ql.Months), (6, ql.Months), (12, ql.Months), (2, ql.Weeks), (1, ql.Days),
          (18, ql.Months), (2, ql.Years)]

# The value and fixing dates: month ends (31 January, the last business day of May and August 2025, 28 February
# 2025 and 2026), 29 February 2028, Good Friday and Easter Monday 2025, 26 May 2025 (a US and UK holiday), the
# weekend of 18 January 2025, Christmas Eve, the evaluation date, and dates mid-month.
DATES = [ql.Date(15, 1, 2025), ql.Date(17, 1, 2025), ql.Date(18, 1, 2025), ql.Date(31, 1, 2025),
         ql.Date(28, 2, 2025), ql.Date(31, 3, 2025), ql.Date(18, 4, 2025), ql.Date(21, 4, 2025),
         ql.Date(30, 4, 2025), ql.Date(26, 5, 2025), ql.Date(30, 5, 2025), ql.Date(30, 6, 2025),
         ql.Date(29, 8, 2025), ql.Date(28, 11, 2025), ql.Date(24, 12, 2025), ql.Date(31, 12, 2025),
         ql.Date(30, 1, 2026), ql.Date(27, 2, 2026), ql.Date(28, 2, 2028), ql.Date(29, 2, 2028)]


def indexes():
    out = []
    for k in range(16):
        cal = irx.CALENDARS[k % 4]
        tenor = TENORS[k % len(TENORS)]
        curve = irx.curve_spec(TODAY, [0.031, 0.0475, -0.0035, 0.012][k % 4],
                               [irx.A365, irx.A360][k % 2], [ql.Continuous, ql.Simple, ql.Compounded][k % 3],
                               [ql.Annual, ql.Quarterly][k % 2])
        out.append(irx.ibor_spec(["Ibor", "IBOR", "Libor", "Tibor"][k % 4], tenor, [2, 0, 1, 2, 3][k % 5], cal,
                                 CONVENTIONS[k % 7], k % 3 != 1, irx.DAY_COUNTERS[k % 3], curve))
    out.append(irx.ibor_spec("Zero", (0, ql.Days), 2, irx.TARGET, ql.ModifiedFollowing, False, irx.A360,
                             irx.curve_spec(TODAY, 0.02)))
    for k, (family, days, cal, dc) in enumerate([("Sofr", 0, irx.US_SOFR, irx.A360), ("Estr", 1, irx.TARGET, irx.A360),
                                                 ("Sonia", 0, irx.UK_EXCHANGE, irx.A365),
                                                 ("Nul", 2, irx.NULL_CALENDAR, irx.THIRTY360_BB)]):
        out.append(irx.overnight_spec(family, days, cal, dc, irx.curve_spec(TODAY, [0.043, 0.029, 0.047, -0.001][k])))
    return out


def clone_curve(k):
    return irx.curve_spec(TODAY, [0.05, 0.015, 0.0][k % 3], irx.A360, ql.Compounded, ql.Semiannual)


def cases():
    out = []
    for k, spec in enumerate(indexes()):
        with v.case_settings(TODAY) as st:
            idx = irx.make_index(spec)
            cc = clone_curve(k)
            clone = idx.clone(irx.handle(cc))
            bare = idx.clone(ql.YieldTermStructureHandle())
            first = next(d for d in DATES if d > TODAY and idx.isValidFixingDate(d))
            out.append({"name": f"{idx.name()} / {k}",
                        "inputs": {"settings": st, "index": spec, "dates": v.enc(DATES), "cloneCurve": cc,
                                   "nullCurveDate": v.enc(first)},
                        "expected": {
                            "businessDayConvention": int(idx.businessDayConvention()),
                            "endOfMonth": idx.endOfMonth(),
                            "maturityDate": [v.attempt(lambda d=d: idx.maturityDate(d)) for d in DATES],
                            "fixing": [v.attempt(lambda d=d: idx.fixing(d)) for d in DATES],
                            "cloneName": clone.name(),
                            "cloneType": type(clone).__name__,
                            "cloneFixing": [v.attempt(lambda d=d: clone.fixing(d)) for d in DATES],
                            "nullCurveFixing": v.attempt(lambda: bare.fixing(first))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/iborindex.hpp", cases)
