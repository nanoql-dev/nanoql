# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Swap (ql/instruments/swap.hpp): six swaps of fixed-rate legs (two live legs, a forward
start, three legs with payer flags, an expired swap, a swap whose last flows fall on the first evaluation date, and
one with an empty leg) under 18 evaluation contexts (3 evaluation dates x includeReferenceDateEvents x
includeTodaysCashFlows null, false or true): whether the swap has expired, its number of legs, each leg's payer flag,
and its start and maturity dates (which throw for the swap with an empty leg).

Each case's inputs describe the swap in full: "swap" holds its form ("pair" for the constructor taking a paid and a
received leg, "legs" for the one taking legs and payer flags), its legs and its payer flags. A leg is "fixedRateLeg"
(its schedule and every FixedRateLeg setting, as in the fixed-rate coupon vectors; null for no coupons) followed by
"extraFlows" (none here), so the empty leg is {"fixedRateLeg": null, "extraFlows": []}. Calendars are
{"type": <class>}; day counters {"type": <class>}, with a "convention" integer for Thirty360; enums and time units are
QuantLib's integers.

The swaps are built with ql.Swap, the wheel's proxy of C++ QuantLib::Swap's two constructors, and the empty leg with
ql.Leg(). The schedules are built with ql.Schedule, the proxy of C++ QuantLib::Schedule's rule-based constructor.
The legs are built with ql.FixedRateLeg, which forwards to the wheel's C++ helper _FixedRateLeg (QuantLib-SWIG 1.44,
SWIG/cashflows.i). It builds a C++ QuantLib::FixedRateLeg and calls withNotionals, withPaymentAdjustment,
withPaymentCalendar (the schedule's calendar when none is given), withPaymentLag, withFirstPeriodDayCounter (an empty
day counter when none is given, which QuantLib ignores), withExCouponPeriod (an empty period when none is given,
which means no ex-coupon dates) and withCouponRates(rates, dayCount, compounding, frequency), and converts the
builder to a Leg. Read back by tests/NanoQL.Tests/Vectors/Instruments/SwapVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

TARGET = {"type": "TARGET"}
A360 = {"type": "Actual360"}
A365 = {"type": "Actual365Fixed"}
THIRTY360_BB = {"type": "Thirty360", "convention": int(ql.Thirty360.BondBasis)}

EVALUATION_DATES = [ql.Date(15, ql.October, 2026), ql.Date(31, ql.July, 2026), ql.Date(10, ql.March, 2030)]


def schedule_spec(eff, mat, months):
    return {"effectiveDate": v.enc(eff), "terminationDate": v.enc(mat), "tenor": [months, int(ql.Months)],
            "calendar": TARGET, "convention": int(ql.ModifiedFollowing),
            "terminationDateConvention": int(ql.ModifiedFollowing), "rule": int(ql.DateGeneration.Backward),
            "endOfMonth": False, "firstDate": None, "nextToLastDate": None}


def fixed(schedule, dc, notional, rate):
    return {"fixedRateLeg": {"notionals": [notional], "couponRates": [rate], "dayCounter": dc,
                             "compounding": int(ql.Simple), "frequency": int(ql.Annual), "firstPeriodDayCounter": None,
                             "paymentCalendar": None, "paymentLag": 0, "paymentAdjustment": int(ql.Following),
                             "exCouponPeriod": None, "schedule": schedule},
            "extraFlows": []}


EMPTY = {"fixedRateLeg": None, "extraFlows": []}
LIVE_FIXED = schedule_spec(ql.Date(15, 1, 2026), ql.Date(15, 1, 2031), 6)
LIVE_FLOATING = schedule_spec(ql.Date(15, 1, 2026), ql.Date(15, 1, 2031), 3)
FORWARD = schedule_spec(ql.Date(15, 6, 2027), ql.Date(15, 6, 2037), 12)
EXPIRED = schedule_spec(ql.Date(15, 1, 2020), ql.Date(15, 1, 2024), 6)
ON_EVALUATION_DATE = schedule_spec(ql.Date(15, 10, 2025), ql.Date(15, 10, 2026), 6)

# (name, swap spec)
SWAPS = [
    ("live 2 legs", {"form": "pair", "legs": [fixed(LIVE_FIXED, A360, 1e6, 0.03),
                                              fixed(LIVE_FLOATING, A360, 1e6, 0.025)], "payer": [True, False]}),
    ("forward start", {"form": "pair", "legs": [fixed(FORWARD, THIRTY360_BB, 1e7, 0.035),
                                                fixed(FORWARD, A365, 1e7, 0.032)], "payer": [True, False]}),
    ("three legs payer flags", {"form": "legs", "legs": [fixed(LIVE_FIXED, A360, 1e6, 0.03),
                                                         fixed(LIVE_FLOATING, A360, 1e6, 0.02),
                                                         fixed(FORWARD, A365, 5e5, 0.01)],
                                "payer": [False, True, True]}),
    ("expired", {"form": "pair", "legs": [fixed(EXPIRED, A360, 1e6, 0.03), fixed(EXPIRED, THIRTY360_BB, 1e6, 0.025)],
                 "payer": [True, False]}),
    ("last flow on evaluation date", {"form": "pair", "legs": [fixed(ON_EVALUATION_DATE, A360, 1e6, 0.03),
                                                               fixed(ON_EVALUATION_DATE, A360, 1e6, 0.02)],
                                      "payer": [True, False]}),
    ("one empty leg", {"form": "legs", "legs": [fixed(LIVE_FIXED, A360, 1e6, 0.03), EMPTY], "payer": [True, False]}),
]


def calendar(spec):
    if spec["type"] == "TARGET":
        return ql.TARGET()
    raise ValueError(f"unknown calendar {spec['type']}")


def day_counter(spec):
    t = spec["type"]
    if t == "Actual365Fixed":
        return ql.Actual365Fixed()
    if t == "Actual360":
        return ql.Actual360()
    if t == "Thirty360":
        return ql.Thirty360(spec["convention"])
    raise ValueError(f"unknown day counter {t}")


def date_or_null(serial):
    return ql.Date(serial) if serial is not None else ql.Date()


def schedule(spec):
    return ql.Schedule(ql.Date(spec["effectiveDate"]), ql.Date(spec["terminationDate"]),
                       ql.Period(spec["tenor"][0], spec["tenor"][1]), calendar(spec["calendar"]), spec["convention"],
                       spec["terminationDateConvention"], spec["rule"], spec["endOfMonth"],
                       date_or_null(spec["firstDate"]), date_or_null(spec["nextToLastDate"]))


def fixed_rate_leg(spec):
    """The leg a "fixedRateLeg" spec describes. These legs set no first-period day counter, payment calendar or
    ex-coupon period, so the helper's defaults stand for them."""
    assert spec["firstPeriodDayCounter"] is None and spec["paymentCalendar"] is None
    assert spec["exCouponPeriod"] is None
    return ql.FixedRateLeg(schedule(spec["schedule"]), dayCount=day_counter(spec["dayCounter"]),
                           nominals=spec["notionals"], couponRates=spec["couponRates"],
                           compounding=spec["compounding"], compoundingFrequency=spec["frequency"],
                           paymentLag=spec["paymentLag"], paymentAdjustment=spec["paymentAdjustment"])


def leg(spec):
    """The leg a "leg" spec describes: its fixed-rate coupons, then its extra flows (none here)."""
    assert not spec["extraFlows"]
    if spec["fixedRateLeg"] is None:
        return ql.Leg()
    return fixed_rate_leg(spec["fixedRateLeg"])


def swap(spec):
    legs = [leg(x) for x in spec["legs"]]
    if spec["form"] == "pair":
        assert spec["payer"] == [True, False]
        return ql.Swap(legs[0], legs[1])
    return ql.Swap(legs, spec["payer"])


def contexts():
    return [(ev, ref_events, todays) for ev in EVALUATION_DATES for ref_events in (False, True)
            for todays in (None, False, True)]


def cases():
    out = []
    for name, spec in SWAPS:
        for ev, ref_events, todays in contexts():
            st = v.settings(ev, ref_events, todays)
            sw = swap(spec)
            out.append({"name": f"{name} / {ev.ISO()} {ref_events} {todays}",
                        "inputs": {"settings": st, "swap": spec},
                        "expected": {"isExpired": sw.isExpired(), "numberOfLegs": sw.numberOfLegs(),
                                     "payer": [sw.payer(j) for j in range(sw.numberOfLegs())],
                                     "startDate": v.attempt(sw.startDate),
                                     "maturityDate": v.attempt(sw.maturityDate)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/swap.hpp", cases)
