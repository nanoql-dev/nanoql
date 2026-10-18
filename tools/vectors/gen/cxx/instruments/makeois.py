# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for MakeOIS's withSettlementCalendar (ql/instruments/makeois.hpp), which the wheel's MakeOIS doesn't reach,
each output computed by the C++ harness: the calendar the settlement days are counted on, from the evaluation date
itself, where QuantLib's default is the overnight leg's calendar.

28 builds on overnight indexes with ESTR's (TARGET) and SOFR's (US SOFR calendar) conventions and QuantLib's empty
currency, each on a flat curve, so that their default settlement days are 2: settlement days of 0, 1 and 2 and the
default counted on the US settlement, UK exchange and null calendars and on the overnight leg's, on evaluation dates
where the calendars diverge (2 July 2026, before the US Independence Day holiday; 24 December 2025; Saturday 20 June
2026); a settlement calendar with an explicit effective date, which wins; both legs' calendars set, which the default
counts on; a month-end start with the end-of-month flag; and the par rate where no fixed rate is given. Each build's
outputs are its start and maturity dates, both schedules' dates and its fixed rate, a refused build recording only
"built".

Each case's inputs hold its settings and its "build", as tools/vectors/gen/instruments/makevanillaswap.py writes it,
with the setters the harness takes (instruments/swapspec.hpp). tests/NanoQL.Tests/Vectors/Cxx/Instruments/
MakeOISCxxVectorTests.cs reads it back."""
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
DATES = [D(2, 7, 2026), D(24, 12, 2025), D(20, 6, 2026)]


def builds():
    """(name, build spec, evaluation date) for the 28 builds."""
    out = []
    k = 0
    for today in DATES:
        for index, label in ((fvf.ESTR, "ESTR"), (fvf.SOFR, "SOFR")):
            for days in (0, 1, 2, None):
                cal = CALENDARS[k % 3]
                setters = {"settlementCalendar": cal} if days is None else {"settlementDays": days,
                                                                           "settlementCalendar": cal}
                out.append((f"{today.ISO()} {label} settlement days {days} on {cal['type']}",
                            mvs.build((1, ql.Years), index, 0.03, **setters), today))
                k += 1
    first = DATES[0]
    out.append(("settlement calendar with an effective date",
                mvs.build((1, ql.Years), fvf.ESTR, 0.03, settlementCalendar=US_SETTLEMENT,
                          effectiveDate=v.enc(D(7, 7, 2026))), first))
    out.append(("leg calendars, default settlement",
                mvs.build((1, ql.Years), fvf.ESTR, 0.03, settlementDays=2, calendar=US_SETTLEMENT,
                          fixedLegCalendar=fvf.TARGET, overnightLegCalendar=US_SETTLEMENT), first))
    out.append(("month end with the end-of-month flag",
                mvs.build((1, ql.Years), fvf.ESTR, 0.03, settlementDays=0, settlementCalendar=US_SETTLEMENT,
                          endOfMonth=True), D(30, 9, 2026)))
    out.append(("par rate, settlement calendar",
                mvs.build((2, ql.Years), fvf.SOFR, None, settlementDays=2, settlementCalendar=fvf.UK_EXCHANGE),
                first))
    return out


def cases():
    with cxx.connect() as H:
        out = []
        for name, spec, today in builds():
            with H.case_settings(v.enc(today)) as st:
                try:
                    d = H.call("MakeOIS::withSettlementCalendar", spec)
                    expected = {"built": True}
                    expected.update(d)
                except RuntimeError:
                    expected = {"built": dict(v.THROWS)}
                out.append({"name": name, "inputs": {"settings": st, "build": spec}, "expected": expected})
        check_rows(out)
        return out


def check_rows(out):
    assert all(c["expected"]["built"] is True for c in out)
    # Somewhere the settlement calendar moves the start date off the overnight calendar's own count.
    moved = 0
    for c in out:
        setters = dict(c["inputs"]["build"]["setters"])
        if c["name"].startswith("2026-07-02 ESTR") and setters.get("settlementCalendar") == US_SETTLEMENT:
            days = setters.get("settlementDays", 2)
            own = ql.TARGET().advance(DATES[0], days, ql.Days).serialNumber()
            moved += c["expected"]["startDate"] != own
    assert moved > 0


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/makeois.hpp", cases)
