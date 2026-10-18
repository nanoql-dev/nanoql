# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MakeOIS (ql/instruments/makeois.hpp): 57 builds over its 38 keyword setters on the wheel,
on the evaluation date 15 January 2025 (a few on Saturday 18 January): the default build at the par rate on the
index's own forecasting curve, with the default settlement days of SONIA (0), CORRA (1), SOFR, ESTR and TONA (2), and
of an index cloned from SONIA, a plain OvernightIndex, which gets 2; a given fixed rate and forward starts, the type
both ways, nominals, settlement days, explicit effective and termination dates, the rules and payment frequencies
(Once and the zero rule giving zero-coupon legs), the payment adjustment, lag and calendar, the calendars and
conventions of each leg, the end-of-month flags of each leg and of the maturity (the default following the start
date's month end), from 31 January 2025 and from 30 April 2025, the end of a short month, where each flag moves a date
of its own, the fixed leg's day counter, the overnight spread, the par rate on a discounting term structure and on a
pricing engine, averaged rates, lookbacks, lockouts, the observation shift, the rounding precision, and telescopic
value dates; and QuantLib's refusals: settlement days with an effective date, no fixed rate with neither an engine nor
a forecasting curve, a telescopic lookback without the shift, and a rounding precision of 17. The last nine builds,
appended after the others, give the calendars QuantLib's empty calendar and the discounting term structure QuantLib's
empty handle, both of which QuantLib uses unchecked: the calendar builds where every use is an Unadjusted adjustment,
the overnight coupons then paid on the payment calendar or on WeekendsOnly, and refuses a settlement advance in days
and a following payment adjustment; the handle builds a swap with a fixed rate, whose pricing it refuses, and refuses
a par build.

Each build's outputs are whether it builds, QuantLib's refusals recorded as its one throw; the swap's start and
maturity dates, both schedules' dates, type, nominal, fixed rate (the par rate where none was given), spread, fixed
day counter, payment convention and lag, and its overnight conventions; and its results on the engine QuantLib's
builder attaches (the one given, a DiscountingSwapEngine on the discounting term structure, or one on the index's
forecasting curve): the NPV, the per-leg NPV and BPS, and the fair rate and spread. NPVs are compared at the nominal
and BPS at it times 1e-4.

Telescopic builds follow the plan's telescopic rule (S3R11, SD11): QuantLib builds with the flag first, so a refusal
is its own; the expected values are then the same build's without it, with
ArithmeticAveragedOvernightIndexedCouponPricer(byApprox=True) set on an averaged swap's overnight leg, the pricer the
flag selects. With no fixed rate, the flag would set that pricer on the swap the builder prices at 0% too, so the par
rate is computed as the builder computes it, on the same swap at 0% with the pricer set on its overnight leg, and the
swap is built at it. The wheel's ArithmeticAveragedOvernightIndexedCouponPricer is built with keyword arguments only.

The builds call QuantLib-Python's MakeOIS, a Python function that builds the C++ QuantLib::MakeOIS through its
deprecated four-argument constructor (the tenor, the index, the fixed rate or Null<Rate>(), the forward start), which
calls withFixedRate and withForwardStart, then calls each keyword's setter in the order given (QuantLib-SWIG 1.44,
SWIG/swap.i), and converts the builder to an OvernightIndexedSwap. Each case's inputs hold its settings and its
"build", as instruments/makevanillaswap.py writes it, a null calendar being QuantLib's empty calendar and a null curve
its empty handle. Read back by tests/NanoQL.Tests/Vectors/Instruments/MakeOISVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


fvf = _load("wheel_fixedvsfloatingswap", "instruments/fixedvsfloatingswap.py")
mvs = _load("wheel_makevanillaswap", "instruments/makevanillaswap.py")
irx = fvf.irx

TODAY = fvf.TODAY
SATURDAY = fvf.d(18, 1, 2025)
D = fvf.d
SIMPLE = int(ql.RateAveraging.Simple)
period, build, engine_spec = mvs.period, mvs.build, mvs.engine_spec

# The setters whose values are calendars, day counters, curves and engines; the others are plain or dates.
DATES = {"effectiveDate", "terminationDate"}
CALENDARS = {"paymentCalendar", "calendar", "fixedLegCalendar", "overnightLegCalendar"}
DAY_COUNTERS = {"fixedLegDayCount"}


def wheel_value(key, x):
    if key in DATES:
        return ql.Date(x)
    if key in CALENDARS:
        return mvs.calendar_or_empty(x)
    if key in DAY_COUNTERS:
        return irx.day_counter(x)
    if key == "discountingTermStructure":
        return irx.handle(x)
    if key == "pricingEngine":
        return mvs.wheel_value(key, x)
    return x


def make(spec, telescopic=None, fixed_rate=None):
    """The wheel's build, with the telescopic flag replaced and the fixed rate given when they are."""
    kwargs = {}
    for k, x in spec["setters"]:
        if k == "telescopicValueDates" and telescopic is not None:
            x = telescopic
        kwargs[k] = wheel_value(k, x)
    rate = spec["fixedRate"] if fixed_rate is None else fixed_rate
    return ql.MakeOIS(ql.Period(*spec["swapTenor"]), fvf.make_index(spec["index"]), rate,
                      ql.Period(*spec["forwardStart"]), **kwargs)


def setter(spec, key, default=None):
    found = [x for k, x in spec["setters"] if k == key]
    return found[-1] if found else default


def approximated(sw):
    ql.setCouponPricer(sw.overnightLeg(), ql.ArithmeticAveragedOvernightIndexedCouponPricer(byApprox=True))
    return sw


def make_by_rule(spec):
    """The wheel's swap by the telescopic rule: the build with the flag first, so that QuantLib's refusal is its own;
    for a telescopic build, the same without the flag, an averaged swap's overnight leg given the approximating
    pricer, and the par rate computed so, on the swap at 0%, when no fixed rate is given."""
    sw = make(spec)
    if not setter(spec, "telescopicValueDates", False):
        return sw
    if setter(spec, "averagingMethod") != SIMPLE:
        return make(spec, telescopic=False)
    rate = spec["fixedRate"]
    if rate is None:
        temp = approximated(make(spec, telescopic=False, fixed_rate=0.0))
        temp.setPricingEngine(mvs.engine_of(spec, temp.overnightIndex()))
        rate = temp.fairRate()
    return approximated(make(spec, telescopic=False, fixed_rate=rate))


def builds():
    """(name, build spec, evaluation date) for the 66 builds."""
    def curve(forward, dc=fvf.A365):
        return fvf.curve(forward, dc)
    sonia = fvf.preset("Sonia", curve(0.047))
    corra = fvf.preset("Corra", curve(0.032))
    sofr = fvf.preset("Sofr", curve(0.043, fvf.A360))
    estr = fvf.preset("Estr", curve(0.029, fvf.A360))
    tonar = fvf.preset("Tonar", curve(0.004))
    cloned_sonia = fvf.with_currency(irx.overnight_spec("Sonia", 0, fvf.UK_EXCHANGE, fvf.A365, curve(0.047)), "GBP")
    one_y, two_y, five_y = (1, ql.Years), (2, ql.Years), (5, ql.Years)
    eom = D(31, 1, 2025)
    short_end = D(30, 4, 2025)
    out = []

    def add(name, spec, today=TODAY):
        out.append((name, spec, today))

    for label, index in (("SONIA", sonia), ("CORRA", corra), ("SOFR", sofr), ("ESTR", estr), ("TONA", tonar),
                         ("cloned SONIA", cloned_sonia)):
        add(f"{label} default", build(one_y, index))
    for label, index in (("SONIA", sonia), ("CORRA", corra), ("ESTR", estr), ("TONA", tonar)):
        add(f"{label} default on a Saturday", build(one_y, index, 0.03), SATURDAY)
    add("fixed rate", build(two_y, estr, 0.028))
    add("forward start", build(two_y, estr, None, (6, ql.Months)))
    add("negative forward start", build(two_y, sofr, 0.04, (-1, ql.Weeks)))
    add("receive fixed, nominal", build(five_y, sofr, None, receiveFixed=True, nominal=2.5e7))
    add("receiver type", build(two_y, sonia, 0.046, swapType=int(ql.Swap.Receiver)))
    add("settlement days 3", build(two_y, estr, None, settlementDays=3))
    add("settlement days 0 on a Saturday", build(two_y, estr, 0.03, settlementDays=0), SATURDAY)
    add("effective date", build(two_y, estr, None, effectiveDate=v.enc(D(20, 2, 2025))))
    add("termination date", build(two_y, estr, None, effectiveDate=v.enc(D(20, 2, 2025)),
                                  terminationDate=v.enc(D(20, 11, 2026))))
    add("forward rule, quarterly", build(two_y, sofr, None, dateGenerationRule=int(ql.DateGeneration.Forward),
                                         paymentFrequency=int(ql.Quarterly)))
    add("zero-coupon fixed leg", build(five_y, sofr, None, fixedLegPaymentFrequency=int(ql.Once)))
    add("zero rule on the overnight leg", build(two_y, estr, None, overnightLegRule=int(ql.DateGeneration.Zero)))
    add("fixed-leg rule, semiannual overnight leg", build(two_y, estr, None,
                                                          fixedLegRule=int(ql.DateGeneration.Forward),
                                                          overnightLegPaymentFrequency=int(ql.Semiannual)))
    add("payment adjustment, lag, calendar", build(two_y, sofr, None,
                                                   paymentAdjustmentConvention=int(ql.Preceding), paymentLag=2,
                                                   paymentCalendar=fvf.TARGET))
    add("calendar", build(two_y, sofr, None, calendar=fvf.TARGET))
    add("leg calendars", build(two_y, sofr, None, fixedLegCalendar=fvf.UK_EXCHANGE, overnightLegCalendar=fvf.TARGET))
    add("convention", build(two_y, estr, None, convention=int(ql.Following)))
    add("leg conventions", build(two_y, estr, None, fixedLegConvention=int(ql.Preceding),
                                 overnightLegConvention=int(ql.Unadjusted)))
    add("termination date conventions", build(two_y, estr, None, terminationDateConvention=int(ql.Preceding)))
    add("leg termination date conventions", build(two_y, estr, None,
                                                  fixedLegTerminationDateConvention=int(ql.Unadjusted),
                                                  overnightLegTerminationDateConvention=int(ql.Following)))
    add("default end of month", build(two_y, estr, None, effectiveDate=v.enc(eom)))
    add("end of month off", build(two_y, estr, None, effectiveDate=v.enc(eom), endOfMonth=False))
    add("maturity end of month", build(two_y, estr, None, effectiveDate=v.enc(eom), maturityEndOfMonth=True))
    add("leg end of month", build(two_y, estr, None, effectiveDate=v.enc(eom), fixedLegEndOfMonth=True,
                                  overnightLegEndOfMonth=False))
    # From the end of a short month, 30 April 2025, with semiannual legs and periods ending in long months, so that
    # each flag moves a date: 14 months to 30 June 2026, a month end the backward schedules roll from, whose
    # 30 December 2025 becomes 31 December under a leg's flag; and 15 months to 30 July 2026, which the maturity's flag
    # moves to 31 July. The default follows the start date's month end for all three.
    semiannual = int(ql.Semiannual)
    for label, flags in (("default", {}), ("end of month off", {"endOfMonth": False}),
                         ("fixed leg end of month alone", {"fixedLegEndOfMonth": True}),
                         ("overnight leg end of month alone", {"overnightLegEndOfMonth": True})):
        add(f"short month end, 14M, {label}", build((14, ql.Months), estr, None, effectiveDate=v.enc(short_end),
                                                    paymentFrequency=semiannual, **flags))
    for label, flags in (("default", {}), ("maturity end of month alone", {"maturityEndOfMonth": True}),
                         ("maturity end of month off", {"maturityEndOfMonth": False})):
        add(f"short month end, 15M, {label}", build((15, ql.Months), estr, None, effectiveDate=v.enc(short_end),
                                                    paymentFrequency=semiannual, **flags))
    add("fixed-leg day counter, spread", build(two_y, estr, None, fixedLegDayCount=fvf.THIRTY360_BB,
                                               overnightLegSpread=0.001))
    add("discounting term structure", build(two_y, estr, None, discountingTermStructure=fvf.DISCOUNT))
    add("pricing engine with settlement flows", build(two_y, estr, None, pricingEngine=engine_spec(fvf.DISCOUNT, True)))
    add("averaged", build(two_y, sofr, None, averagingMethod=SIMPLE))
    add("lookback 2 with shift", build(two_y, sofr, None, lookbackDays=2, applyObservationShift=True))
    add("lookback 2, lockout 2", build(two_y, sofr, None, lookbackDays=2, lockoutDays=2))
    add("rounding 6", build(two_y, estr, None, roundingPrecision=6))
    add("telescopic", build(two_y, estr, None, telescopicValueDates=True))
    add("telescopic, averaged", build(two_y, sofr, None, telescopicValueDates=True, averagingMethod=SIMPLE))
    add("telescopic, averaged, fixed rate", build(two_y, sofr, 0.042, telescopicValueDates=True,
                                                  averagingMethod=SIMPLE))
    add("telescopic, lookback with shift", build(two_y, sofr, None, telescopicValueDates=True, lookbackDays=2,
                                                 applyObservationShift=True))
    add("telescopic lookback without shift refused", build(two_y, sofr, None, telescopicValueDates=True,
                                                           lookbackDays=2))
    add("rounding 17 refused", build(two_y, estr, 0.03, roundingPrecision=17))
    add("settlement days with an effective date", build(two_y, estr, 0.03, effectiveDate=v.enc(D(20, 1, 2025)),
                                                        settlementDays=2))
    add("no curve, no rate", build(two_y, fvf.preset("Estr"), None))
    add("no curve, a rate", build(two_y, fvf.preset("Estr"), 0.03))
    # QuantLib's empty calendar and handle, appended so that the cases above keep their bytes. From 1 May 2025, two
    # annual periods end on Friday 1 May 2026, a TARGET holiday, and Saturday 1 May 2027.
    unadjusted = int(ql.Unadjusted)
    may_day = v.enc(D(1, 5, 2025))
    overnight_unadjusted = {"endOfMonth": False, "overnightLegConvention": unadjusted,
                            "overnightLegTerminationDateConvention": unadjusted}
    fixed_unadjusted = {"fixedLegConvention": unadjusted, "fixedLegTerminationDateConvention": unadjusted}
    add("empty calendar, Unadjusted", build(two_y, estr, None, effectiveDate=may_day, calendar=None, endOfMonth=False,
                                            convention=unadjusted, terminationDateConvention=unadjusted,
                                            paymentAdjustmentConvention=unadjusted))
    add("empty calendar, defaults refused", build(two_y, estr, 0.03, calendar=None))
    add("empty fixed-leg calendar, Unadjusted, a payment calendar", build(two_y, estr, None, fixedLegCalendar=None,
                                                                          paymentCalendar=fvf.TARGET,
                                                                          **fixed_unadjusted))
    add("empty fixed-leg calendar, Following payments refused", build(two_y, estr, 0.03, fixedLegCalendar=None,
                                                                      **fixed_unadjusted))
    add("empty overnight-leg calendar, Unadjusted", build(two_y, estr, None, effectiveDate=may_day,
                                                          overnightLegCalendar=None, **overnight_unadjusted))
    add("empty overnight-leg calendar, Unadjusted, a payment lag", build(two_y, estr, None, effectiveDate=may_day,
                                                                         overnightLegCalendar=None, paymentLag=2,
                                                                         **overnight_unadjusted))
    add("empty overnight-leg calendar, defaults refused", build(two_y, estr, 0.03, overnightLegCalendar=None))
    add("empty discounting term structure, a fixed rate", build(two_y, estr, 0.03, discountingTermStructure=None))
    add("empty discounting term structure, at the par rate refused", build(two_y, estr, None,
                                                                          discountingTermStructure=None))
    return out


def outputs(sw):
    return {"startDate": sw.startDate().serialNumber(), "maturityDate": sw.maturityDate().serialNumber(),
            "fixedSchedule": fvf.schedule_dates(sw.fixedSchedule()),
            "overnightSchedule": fvf.schedule_dates(sw.floatingSchedule()),
            "type": int(sw.type()), "nominal": sw.nominal(), "fixedRate": sw.fixedRate(), "spread": sw.spread(),
            "fixedDayCount": sw.fixedDayCount().name(), "paymentConvention": int(sw.paymentConvention()),
            "paymentLag": sw.paymentLag(), "averagingMethod": int(sw.averagingMethod()),
            "lookbackDays": v.enc(sw.lookbackDays()), "lockoutDays": sw.lockoutDays(),
            "applyObservationShift": sw.applyObservationShift()}


def cases():
    out = []
    for name, spec, today in builds():
        with v.case_settings(today) as st:
            inputs = {"settings": st, "build": spec}
            try:
                sw = make_by_rule(spec)
            except RuntimeError:
                out.append({"name": name, "inputs": inputs, "expected": {"built": dict(v.THROWS)}})
                continue
            scale = max(abs(sw.nominal()), 1.0)
            expected = {"built": True}
            expected.update(outputs(sw))
            sw.setPricingEngine(mvs.engine_of(spec, sw.overnightIndex()))
            expected.update(fvf.results(sw, scale, accessors=False))
            out.append({"name": name, "inputs": inputs, "expected": expected})
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    starts = {k: by_name[f"{k} default"]["startDate"] for k in ("SONIA", "CORRA", "SOFR", "cloned SONIA")}
    today = TODAY.serialNumber()
    assert starts["SONIA"] == today and starts["CORRA"] == today + 1, starts
    assert starts["cloned SONIA"] == ql.UnitedKingdom(ql.UnitedKingdom.Exchange).advance(TODAY, 2,
                                                                                        ql.Days).serialNumber()
    for refused in ("telescopic lookback without shift refused", "rounding 17 refused",
                    "settlement days with an effective date", "no curve, no rate",
                    "empty calendar, defaults refused", "empty fixed-leg calendar, Following payments refused",
                    "empty overnight-leg calendar, defaults refused",
                    "empty discounting term structure, at the par rate refused"):
        assert by_name[refused] == {"built": v.THROWS}, refused
    for priced_nothing in ("no curve, a rate", "empty discounting term structure, a fixed rate"):
        assert by_name[priced_nothing].get("calculate") == v.THROWS, priced_nothing
    for built in ("empty calendar, Unadjusted", "empty fixed-leg calendar, Unadjusted, a payment calendar",
                  "empty overnight-leg calendar, Unadjusted", "empty overnight-leg calendar, Unadjusted, a payment lag"):
        assert by_name[built]["built"] is True, built
    for name in ("SONIA default", "telescopic, averaged", "rounding 6"):
        assert abs(by_name[name]["NPV"]["value"]) < 1e-12 * by_name[name]["NPV"]["scale"] + 1e-12, name
    check_short_month_ends(by_name)


def check_short_month_ends(by_name):
    """Each flag moves the dates it sets from the end of a short month, and no other; the default sets all three."""
    def dates(label):
        e = by_name[f"short month end, {label}"]
        return e["maturityDate"], e["fixedSchedule"], e["overnightSchedule"]

    dec30, dec31 = v.enc(D(30, 12, 2025)), v.enc(D(31, 12, 2025))
    maturity, fixed, overnight = dates("14M, end of month off")
    assert dec30 in fixed and dec30 in overnight
    m, f, o = dates("14M, default")
    assert m == maturity and dec31 in f and dec31 in o
    m, f, o = dates("14M, fixed leg end of month alone")
    assert (m, o) == (maturity, overnight) and dec31 in f
    m, f, o = dates("14M, overnight leg end of month alone")
    assert (m, f) == (maturity, fixed) and dec31 in o
    jul30, jul31 = v.enc(D(30, 7, 2026)), v.enc(D(31, 7, 2026))
    assert dates("15M, maturity end of month off")[0] == jul30
    for label in ("default", "maturity end of month alone"):
        assert dates(f"15M, {label}")[0] == jul31, label


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/makeois.hpp", cases)
