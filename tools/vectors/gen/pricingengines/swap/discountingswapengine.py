# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DiscountingSwapEngine (ql/pricingengines/swap/discountingswapengine.hpp), through the Swap
it prices (ql/instruments/swap.hpp): the full results of six swaps of fixed-rate legs (two live legs, a forward
start, three legs with payer flags, an expired swap, a swap whose last flows fall on the first evaluation date, and
one with an empty leg) under 6 of 18 evaluation contexts (3 evaluation dates x includeReferenceDateEvents x
includeTodaysCashFlows null, false or true), on 3 flat curves whose reference date is the evaluation date
(continuous 3% Actual/365, annually compounded 4.5% Actual/360, simple -0.2% Actual/Actual ISDA), with 3 of 8 engine
settings: includeSettlementDateFlows (null, false or true), the settlement date (null, five days after the
evaluation date, or the day before it, which is before the curve's reference date) and the NPV date (null, 30 days
after the evaluation date, or the day before it). The contexts and the engine settings are drawn with the seed.

The results are the NPV, the error estimate, the valuation date, each leg's NPV and BPS, each leg's start and end
discounts, and the NPV-date discount. QuantLib throws "not available" or "not provided" on reading a result it left
null; that result is null here, as nanoql returns it. So the error estimate is null for every swap priced (the engine
never sets it), the start discount is null for a leg starting before the curve's reference date, and both discounts
are null for the empty leg. An expired swap gives zeros, an error estimate of 0 and a null valuation date, even under
a settlement date before the curve's reference date, because the expiry check comes before the engine runs; a live
swap under that setting throws, and its case's one output, "calculate", is that throw. NPVs are compared at the
swap's largest notional and BPS at that notional times 1e-4.

Each case's inputs describe the swap in full ("swap": its form, "pair" or "legs", its legs and its payer flags, as in
the swap vectors), the curve ("curve": reference date, forward rate, day counter, compounding and frequency) and the
engine settings. Calendars are {"type": <class>}; day counters {"type": <class>}, with a "convention" integer for
Thirty360 and ActualActual; enums and time units are QuantLib's integers.

The swaps are built with ql.Swap, the wheel's proxy of C++ QuantLib::Swap's two constructors, the empty leg with
ql.Leg(), and the schedules with ql.Schedule, the proxy of C++ QuantLib::Schedule's rule-based constructor. The legs
are built with ql.FixedRateLeg, which forwards to the wheel's C++ helper _FixedRateLeg (QuantLib-SWIG 1.44,
SWIG/cashflows.i). It builds a C++ QuantLib::FixedRateLeg and calls withNotionals, withPaymentAdjustment,
withPaymentCalendar (the schedule's calendar when none is given), withPaymentLag, withFirstPeriodDayCounter (an empty
day counter when none is given, which QuantLib ignores), withExCouponPeriod (an empty period when none is given,
which means no ex-coupon dates) and withCouponRates(rates, dayCount, compounding, frequency), and converts the
builder to a Leg. Each case builds its own swap. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/DiscountingSwapEngineVectorTests.cs."""
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
A360 = {"type": "Actual360"}
A365 = {"type": "Actual365Fixed"}
THIRTY360_BB = {"type": "Thirty360", "convention": int(ql.Thirty360.BondBasis)}
ACT_ISDA = {"type": "ActualActual", "convention": int(ql.ActualActual.ISDA)}

EVALUATION_DATES = [ql.Date(15, ql.October, 2026), ql.Date(31, ql.July, 2026), ql.Date(10, ql.March, 2030)]
# (compounding, forward rate, day counter), all annual
CURVES = [(ql.Continuous, 0.03, A365), (ql.Compounded, 0.045, A360), (ql.Simple, -0.002, ACT_ISDA)]
# (includeSettlementDateFlows, settlement date offset, NPV date offset), offsets from the evaluation date
ENGINE_SETTINGS = [(None, None, None), (True, None, None), (False, None, None), (None, 5, None), (None, None, 30),
                   (None, 5, 30), (None, -1, None), (None, None, -1)]


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

# (name, swap spec, scale)
SWAPS = [
    ("live 2 legs", {"form": "pair", "legs": [fixed(LIVE_FIXED, A360, 1e6, 0.03),
                                              fixed(LIVE_FLOATING, A360, 1e6, 0.025)], "payer": [True, False]}, 1e6),
    ("forward start", {"form": "pair", "legs": [fixed(FORWARD, THIRTY360_BB, 1e7, 0.035),
                                                fixed(FORWARD, A365, 1e7, 0.032)], "payer": [True, False]}, 1e7),
    ("three legs payer flags", {"form": "legs", "legs": [fixed(LIVE_FIXED, A360, 1e6, 0.03),
                                                         fixed(LIVE_FLOATING, A360, 1e6, 0.02),
                                                         fixed(FORWARD, A365, 5e5, 0.01)],
                                "payer": [False, True, True]}, 1e6),
    ("expired", {"form": "pair", "legs": [fixed(EXPIRED, A360, 1e6, 0.03), fixed(EXPIRED, THIRTY360_BB, 1e6, 0.025)],
                 "payer": [True, False]}, 1e6),
    ("last flow on evaluation date", {"form": "pair", "legs": [fixed(ON_EVALUATION_DATE, A360, 1e6, 0.03),
                                                               fixed(ON_EVALUATION_DATE, A360, 1e6, 0.02)],
                                      "payer": [True, False]}, 1e6),
    ("one empty leg", {"form": "legs", "legs": [fixed(LIVE_FIXED, A360, 1e6, 0.03), EMPTY], "payer": [True, False]},
     1e6),
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
    if t == "ActualActual":
        return ql.ActualActual(spec["convention"])
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


def results(sw, scale):
    """The swap's results, each null where QuantLib left it null; or None where pricing throws."""
    try:
        sw.NPV()
    except RuntimeError as e:
        if "not available" in str(e) or "not provided" in str(e):
            raise
        return None
    r = v.result
    n = sw.numberOfLegs()
    return {"NPV": v.amount(r(sw.NPV), scale),
            "errorEstimate": r(sw.errorEstimate),
            "valuationDate": r(sw.valuationDate),
            "legNPV": [v.amount(r(lambda j=j: sw.legNPV(j)), scale) for j in range(n)],
            "legBPS": [v.amount(r(lambda j=j: sw.legBPS(j)), scale * 1e-4) for j in range(n)],
            "startDiscounts": [r(lambda j=j: sw.startDiscounts(j)) for j in range(n)],
            "endDiscounts": [r(lambda j=j: sw.endDiscounts(j)) for j in range(n)],
            "npvDateDiscount": r(sw.npvDateDiscount)}


def cases():
    rnd = random.Random(v.SEED)
    out = []
    for name, spec, scale in SWAPS:
        for ev, ref_events, todays in rnd.sample(contexts(), 6):
            st = v.settings(ev, ref_events, todays)
            for comp, rate, dcs in CURVES:
                curve = {"referenceDate": v.enc(ev), "forward": rate, "dayCounter": dcs, "compounding": int(comp),
                         "frequency": int(ql.Annual)}
                crv = ql.FlatForward(ev, rate, day_counter(dcs), comp, ql.Annual)
                for include, sd, nd in rnd.sample(ENGINE_SETTINGS, 3):
                    settlement = ev + sd if sd is not None else ql.Date()
                    npv_date = ev + nd if nd is not None else ql.Date()
                    sw = swap(spec)
                    sw.setPricingEngine(ql.DiscountingSwapEngine(ql.YieldTermStructureHandle(crv), include,
                                                                 settlement, npv_date))
                    expected = results(sw, scale)
                    out.append({
                        "name": f"{name} / {ev.ISO()} {ref_events} {todays} / {dcs['type']} {rate} / "
                                f"{include} {sd} {nd}",
                        "inputs": {"settings": st, "swap": spec, "curve": curve, "includeSettlementDateFlows": include,
                                   "settlementDate": v.enc(settlement), "npvDate": v.enc(npv_date)},
                        "expected": expected if expected is not None else {"calculate": dict(v.THROWS)}})
    check_rows(out)
    return out


def check_rows(out):
    """The rows the file must hold, whatever the draws."""
    def any_case(pred):
        return any(pred(c["name"], c["inputs"], c["expected"]) for c in out)

    def before(i):
        return i["settlementDate"] is not None and i["settlementDate"] < i["curve"]["referenceDate"]
    assert any_case(lambda n, i, e: n.startswith("expired") and before(i) and e.get("NPV") == {"value": 0.0,
                                                                                               "scale": 1e6})
    assert any_case(lambda n, i, e: before(i) and "calculate" in e)
    assert any_case(lambda n, i, e: i["npvDate"] is not None and i["npvDate"] < i["curve"]["referenceDate"]
                    and "calculate" in e)
    assert any_case(lambda n, i, e: n.startswith("expired") and e.get("errorEstimate") == 0.0
                    and "valuationDate" in e and e["valuationDate"] is None)
    assert any_case(lambda n, i, e: "errorEstimate" in e and e["errorEstimate"] is None)
    assert any_case(lambda n, i, e: n.startswith("live") and None in e.get("startDiscounts", [0.0]))
    assert any_case(lambda n, i, e: n.startswith("one empty leg") and e.get("endDiscounts", [0, 0])[1] is None)
    for flag in (False, True):
        assert any_case(lambda n, i, e: n.startswith("last flow") and "calculate" not in e
                        and (e["valuationDate"] is None) == flag)


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/swap/discountingswapengine.hpp", cases)
