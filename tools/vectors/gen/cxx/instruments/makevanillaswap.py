# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for MakeVanillaSwap's withSettlementCalendar (ql/instruments/makevanillaswap.hpp), which the wheel's
MakeVanillaSwap doesn't reach, each output computed by the C++ harness: the calendar the settlement days are counted
on, from the evaluation date itself, where QuantLib's default is the floating leg's calendar.

21 builds on a Euribor 6M-style index (TARGET, two fixing days, QuantLib's empty currency, on a flat 3.1% curve), with
the fixed leg's tenor and day counter given, since the empty currency has no defaults: settlement days of 0, 1 and 2
counted on the US settlement, UK exchange and null calendars and on the floating leg's, on evaluation dates where the
calendars diverge (2 July 2026, before the US Independence Day holiday; 24 December 2025; Good Friday 2025, a TARGET
holiday; Saturday 20 June 2026); a settlement calendar without settlement days, where the index's spot date stands,
and with an explicit effective date, which wins; a floating-leg calendar the default would count on; the par rate
where no fixed rate is given; and QuantLib's refusal of the empty currency's default fixed tenor. Each build's outputs
are its start and maturity dates, both schedules' dates and its fixed rate, a refused build recording only "built".

Each case's inputs hold its settings and its "build", as tools/vectors/gen/instruments/makevanillaswap.py writes it,
with the setters the harness takes (instruments/swapspec.hpp). tests/NanoQL.Tests/Vectors/Cxx/Instruments/
MakeVanillaSwapCxxVectorTests.cs reads it back."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_makevanillaswap",
                                               os.path.join(v.GEN_ROOT, "instruments", "makevanillaswap.py"))
mvs = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(mvs)
fvf = mvs.fvf

D = fvf.d
US_SETTLEMENT = {"type": "UnitedStates", "market": int(ql.UnitedStates.Settlement)}
CALENDARS = [US_SETTLEMENT, fvf.UK_EXCHANGE, fvf.NULL_CALENDAR]
DATES = [D(2, 7, 2026), D(24, 12, 2025), D(18, 4, 2025), D(20, 6, 2026)]
FIXED = {"fixedLegTenor": mvs.period(1, ql.Years), "fixedLegDayCount": fvf.THIRTY360_BB}
E6M = fvf.E6M


def builds():
    """(name, build spec, evaluation date) for the 21 builds."""
    out = []
    k = 0
    for today in DATES:
        for days, cal in ((2, CALENDARS[k % 3]), (1, CALENDARS[(k + 1) % 3]), (0, CALENDARS[(k + 2) % 3])):
            out.append((f"{today.ISO()} settlement days {days} on {cal['type']}",
                        mvs.build((5, ql.Years), E6M, 0.03, settlementDays=days, settlementCalendar=cal, **FIXED),
                        today))
            k += 1
        out.append((f"{today.ISO()} settlement days 2 on the floating calendar",
                    mvs.build((5, ql.Years), E6M, 0.03, settlementDays=2, **FIXED), today))
    first = DATES[0]
    out.append(("settlement calendar without settlement days",
                mvs.build((5, ql.Years), E6M, 0.03, settlementCalendar=US_SETTLEMENT, **FIXED), first))
    out.append(("settlement calendar with an effective date",
                mvs.build((5, ql.Years), E6M, 0.03, settlementCalendar=US_SETTLEMENT,
                          effectiveDate=v.enc(D(7, 7, 2026)), **FIXED), first))
    out.append(("floating-leg calendar, no settlement calendar",
                mvs.build((5, ql.Years), E6M, 0.03, settlementDays=2, floatingLegCalendar=US_SETTLEMENT, **FIXED),
                first))
    out.append(("par rate, settlement calendar",
                mvs.build((3, ql.Years), E6M, None, settlementDays=2, settlementCalendar=US_SETTLEMENT,
                          fixedLegCalendar=US_SETTLEMENT, **FIXED), first))
    out.append(("no default fixed tenor for the empty currency",
                mvs.build((5, ql.Years), E6M, 0.03, settlementDays=2, settlementCalendar=US_SETTLEMENT), first))
    return out


def cases():
    with cxx.connect() as H:
        out = []
        for name, spec, today in builds():
            with H.case_settings(v.enc(today)) as st:
                try:
                    d = H.call("MakeVanillaSwap::withSettlementCalendar", spec)
                    expected = {"built": True}
                    expected.update(d)
                except RuntimeError:
                    expected = {"built": dict(v.THROWS)}
                out.append({"name": name, "inputs": {"settings": st, "build": spec}, "expected": expected})
        check_rows(out)
        return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    assert by_name["no default fixed tenor for the empty currency"] == {"built": v.THROWS}
    us = ql.UnitedStates(ql.UnitedStates.Settlement)
    first = DATES[0]
    # The settlement calendar moves the start date where the calendars diverge.
    assert by_name[f"{first.ISO()} settlement days 2 on UnitedStates"]["startDate"] == \
        us.advance(first, 2, ql.Days).serialNumber()
    assert by_name[f"{first.ISO()} settlement days 2 on UnitedStates"]["startDate"] != \
        by_name[f"{first.ISO()} settlement days 2 on the floating calendar"]["startDate"]


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/makevanillaswap.hpp", cases)
