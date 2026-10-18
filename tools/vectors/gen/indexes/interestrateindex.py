# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's InterestRateIndex (ql/indexes/interestrateindex.hpp), read through its two concrete
classes, IborIndex and OvernightIndex: 40 indexes (8 overnight indexes with 0 to 3 fixing days on TARGET and the
SOFR calendar; 4 Ibor indexes of tenor 1D with 0 to 3 fixing days; and the tenors 1W, 1M, 3M, 6M, 12M, 18M and 24M
on each of four calendars), over three day counters, with a flat forwarding curve or none (every fifth index).

Each index gives two cases on the evaluation date 15 January 2025, a business day on every calendar, with the same
stored fixings: six of the eight business days before it, and the evaluation date itself on every other index. In
the first case enforcesTodaysHistoricFixings is false, and its outputs are the name, the tenor ([length, units]: a
tenor of whole years in months becomes years), the family name and the fixing days, and fixingDate, valueDate,
maturityDate, isValidFixingDate and fixing at 20 dates (the eight business days; the latest weekend day and the latest
weekday holiday within 60 days before it, or two earlier business days on a calendar with neither; the evaluation
date; and nine later dates, weekend days and a US holiday among them); and fixing on the evaluation date with
forecastTodaysFixing. The second case sets the flag and gives only the two fixing outputs, so a missing fixing on the
evaluation date is refused there. A refused value is {"throws": true}. On every seventh index the fixings are stored
through an index whose family name differs by ASCII case, so the index reads them by QuantLib's case-insensitive
name.

The fixings are stored with vectors.settings(fixings=...), so each case's inputs.settings.fixings holds them by the
name QuantLib forms, and vectors.case_settings clears them after the case. Each case's inputs hold the index spec
("index") and the 20 dates ("dates"). An index spec is {"type": "IborIndex", "familyName", "tenor" ([length, units]),
"settlementDays", "fixingCalendar", "convention", "endOfMonth", "dayCounter", "curve"} or {"type": "OvernightIndex",
"familyName", "settlementDays", "fixingCalendar", "dayCounter", "curve"}, every index with QuantLib's empty currency;
a curve is null (QuantLib's empty handle) or {"referenceDate", "forward", "dayCounter", "compounding", "frequency"}, a
FlatForward with a fixed reference date. Calendars are {"type": <class>}, with a "market" integer for UnitedStates and
UnitedKingdom; day counters {"type": <class>}, with a "convention" integer for Thirty360. The index and calendar
functions here are shared by the other index generators (index.py, indexes/iborindex.py and their harness
counterparts), which import this file. Read back by tests/NanoQL.Tests/Vectors/Indexes/InterestRateIndexVectorTests.cs.
"""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

TARGET = {"type": "TARGET"}
US_SOFR = {"type": "UnitedStates", "market": int(ql.UnitedStates.SOFR)}
UK_EXCHANGE = {"type": "UnitedKingdom", "market": int(ql.UnitedKingdom.Exchange)}
NULL_CALENDAR = {"type": "NullCalendar"}
CALENDARS = [TARGET, US_SOFR, UK_EXCHANGE, NULL_CALENDAR]

A360 = {"type": "Actual360"}
A365 = {"type": "Actual365Fixed"}
THIRTY360_BB = {"type": "Thirty360", "convention": int(ql.Thirty360.BondBasis)}
DAY_COUNTERS = [A360, A365, THIRTY360_BB]

# 15 January 2025, a Wednesday and a business day on all four calendars.
TODAY = ql.Date(15, ql.January, 2025)


def calendar(spec):
    t = spec["type"]
    if t == "TARGET":
        return ql.TARGET()
    if t == "NullCalendar":
        return ql.NullCalendar()
    if t == "UnitedStates":
        return ql.UnitedStates(spec["market"])
    if t == "UnitedKingdom":
        return ql.UnitedKingdom(spec["market"])
    raise ValueError(f"unknown calendar {t}")


def day_counter(spec):
    t = spec["type"]
    if t == "Actual360":
        return ql.Actual360()
    if t == "Actual365Fixed":
        return ql.Actual365Fixed()
    if t == "Thirty360":
        return ql.Thirty360(spec["convention"])
    if t == "ActualActual":
        return ql.ActualActual(spec["convention"])
    raise ValueError(f"unknown day counter {t}")


def curve_spec(reference, forward, dc=A365, compounding=ql.Continuous, frequency=ql.Annual):
    return {"referenceDate": v.enc(reference), "forward": forward, "dayCounter": dc,
            "compounding": int(compounding), "frequency": int(frequency)}


def handle(spec):
    """The forwarding curve's handle: empty for a null spec, else a FlatForward with a fixed reference date."""
    if spec is None:
        return ql.YieldTermStructureHandle()
    return ql.YieldTermStructureHandle(ql.FlatForward(ql.Date(spec["referenceDate"]), spec["forward"],
                                                      day_counter(spec["dayCounter"]), spec["compounding"],
                                                      spec["frequency"]))


def ibor_spec(family, tenor, days, cal, convention, eom, dc, curve=None):
    return {"type": "IborIndex", "familyName": family, "tenor": [tenor[0], int(tenor[1])], "settlementDays": days,
            "fixingCalendar": cal, "convention": int(convention), "endOfMonth": eom, "dayCounter": dc,
            "curve": curve}


def overnight_spec(family, days, cal, dc, curve=None):
    return {"type": "OvernightIndex", "familyName": family, "settlementDays": days, "fixingCalendar": cal,
            "dayCounter": dc, "curve": curve}


def make_index(spec):
    """The index a spec describes, with QuantLib's empty currency, built with the wheel's IborIndex or
    OvernightIndex, the proxies of the C++ classes' constructors."""
    if spec["type"] == "IborIndex":
        return ql.IborIndex(spec["familyName"], ql.Period(spec["tenor"][0], spec["tenor"][1]),
                            spec["settlementDays"], ql.Currency(), calendar(spec["fixingCalendar"]),
                            spec["convention"], spec["endOfMonth"], day_counter(spec["dayCounter"]),
                            handle(spec["curve"]))
    if spec["type"] == "OvernightIndex":
        return ql.OvernightIndex(spec["familyName"], spec["settlementDays"], ql.Currency(),
                                 calendar(spec["fixingCalendar"]), day_counter(spec["dayCounter"]),
                                 handle(spec["curve"]))
    raise ValueError(f"unknown index {spec['type']}")


TENORS = [(1, ql.Weeks), (1, ql.Months), (3, ql.Months), (6, ql.Months), (12, ql.Months), (18, ql.Months),
          (24, ql.Months)]
FAMILIES = {"TARGET": "Euribor", "UnitedStates": "UsdIbor", "UnitedKingdom": "GbpIbor", "NullCalendar": "NulIbor"}
FIXING_DAYS = [2, 0, 1, 2, 3]
CONVENTIONS = [ql.ModifiedFollowing, ql.Following, ql.Preceding, ql.ModifiedPreceding, ql.Unadjusted]
CURVES = [curve_spec(TODAY, 0.03), curve_spec(TODAY, 0.045, A360, ql.Simple), curve_spec(TODAY, -0.004),
          curve_spec(TODAY, 0.02, A365, ql.Compounded, ql.Semiannual)]
# Offsets from the evaluation date of the nine later dates: 18 and 19 January are a weekend, 20 January a US holiday.
LATER = [1, 3, 4, 5, 7, 30, 93, 181, 366]


def curve_of(k):
    return None if k % 5 == 4 else CURVES[k % 4]


def indexes():
    out = []
    for family, cal in [("Estr", TARGET), ("Sofr", US_SOFR)]:
        for days in range(4):
            out.append(overnight_spec(family, days, cal, A360, curve_of(len(out))))
    for days in range(4):
        cal = UK_EXCHANGE if days % 2 == 0 else NULL_CALENDAR
        out.append(ibor_spec("Sonia" if days % 2 == 0 else "Nul", (1, ql.Days), days, cal, ql.Following, False, A365,
                             curve_of(len(out))))
    for tenor in TENORS:
        for cal in CALENDARS:
            k = len(out)
            out.append(ibor_spec(FAMILIES[cal["type"]], tenor, FIXING_DAYS[k % 5], cal, CONVENTIONS[k % 5],
                                 k % 2 == 1, DAY_COUNTERS[k % 3], curve_of(k)))
    return out


def case_dates(cal):
    """The 20 dates: the eight business days before the evaluation date; two earlier days that are not business days,
    the latest weekend day and the latest holiday on a weekday within 60 days (two earlier business days on a calendar
    with neither); the evaluation date; and the nine later dates."""
    business = []
    d = TODAY - 1
    while len(business) < 8:
        if cal.isBusinessDay(d):
            business.append(d)
        d -= 1
    window = [TODAY - n for n in range(1, 61)]
    weekend = [d for d in window if not cal.isBusinessDay(d) and d.weekday() in (ql.Saturday, ql.Sunday)]
    holiday = [d for d in window if not cal.isBusinessDay(d) and d.weekday() not in (ql.Saturday, ql.Sunday)]
    other = weekend[:1] + holiday[:1]
    if not other:
        other = [business[-1] - 10, business[-1] - 20]
    return sorted(business), other, [TODAY + n for n in LATER]


def cases():
    rnd = random.Random(v.SEED)
    out = []
    for k, spec in enumerate(indexes()):
        idx = make_index(spec)
        business, other, later = case_dates(idx.fixingCalendar())
        dates = business + other + [TODAY] + later
        stored = [d for i, d in enumerate(business) if i not in (2, 5)] + ([TODAY] if k % 2 == 0 else [])
        pairs = [(d, round(rnd.uniform(-0.005, 0.06), 7)) for d in stored]
        store = make_index(dict(spec, familyName=spec["familyName"].upper())) if k % 7 == 3 else idx
        inputs = {"index": spec, "dates": v.enc(dates)}
        for enforce in (False, True):
            with v.case_settings(TODAY, enforces_todays_historic_fixings=enforce, fixings=[(store, pairs)]) as st:
                expected = {}
                if not enforce:
                    expected.update({
                        "name": idx.name(),
                        "tenor": [idx.tenor().length(), int(idx.tenor().units())],
                        "familyName": idx.familyName(),
                        "fixingDays": idx.fixingDays(),
                        "fixingDate": [v.attempt(lambda d=d: idx.fixingDate(d)) for d in dates],
                        "valueDate": [v.attempt(lambda d=d: idx.valueDate(d)) for d in dates],
                        "maturityDate": [v.attempt(lambda d=d: idx.maturityDate(d)) for d in dates],
                        "isValidFixingDate": [idx.isValidFixingDate(d) for d in dates]})
                expected["fixing"] = [v.attempt(lambda d=d: idx.fixing(d)) for d in dates]
                expected["forecastTodaysFixing"] = v.attempt(lambda: idx.fixing(TODAY, True))
                out.append({"name": f"{idx.name()} / {k} / enforce {enforce}",
                            "inputs": {"settings": st, **inputs}, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/interestrateindex.hpp", cases)
