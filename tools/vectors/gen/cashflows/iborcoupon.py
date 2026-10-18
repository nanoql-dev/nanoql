# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's IborCoupon and IborLeg (ql/cashflows/iborcoupon.hpp), on the evaluation date 15 January
2025 and Euribor-style indexes (the wheel's IborIndex with Euribor's conventions, on flat curves): 10 hasFixed
scenarios, and 35 IborLegs (33 built and priced under the case's settings, and 2 built under one at-par switch and
priced under the other).

The hasFixed scenarios are a 6-month coupon fixing the day before, on, or the day after the evaluation date, or
months either side, with the enforcesTodaysHistoricFixings flag either way and a fixing stored on the fixing date or
not; each gives hasFixed and rate, which QuantLib refuses for a missing past fixing. The legs cover short and long
front and back stubs, notional, gearing, spread and fixing-days vectors (null fixing days taking the index's), caps,
floors and collars, in-arrears fixing, ex-coupon periods, payment lags, calendars and conventions, a payment day
counter, indexed and at-par coupons (the leg's explicit choice, or the switch under both settings), legs whose past
coupons read stored fixings, a zero gearing (a fixed-rate coupon paying the spread, floored and capped: a NaN spread
keeps the floor or the cap, as QuantLib's std::max and std::min do), legs with caps, floors or in-arrears fixing and
no pricer set (their coupons hold none, and their rates throw), a leg built under each at-par switch and priced under
the other (it keeps the choice it was built with), and the conversions QuantLib refuses (too many notionals, a
one-date schedule, a stub selection on a capped, in-arrears or at-par leg or on a schedule without regularity
information). A leg with caps, floors or in-arrears fixing has no pricer of its own; its spec's "pricer" is set on it
with setCouponPricer after it is built, and its capped coupons are read before any underlying. Each leg's outputs
are, per cash flow, its payment date, nominal, accrual and reference-period dates, ex-coupon date, rate and amount,
and for a floating-rate coupon its fixing date, fixing days, gearing, spread and whether it holds a pricer (null for
a fixed-rate coupon); a refused conversion records only "leg": {"throws": true}. Amounts are compared at the leg's
largest notional.

The legs are built with ql.IborLeg, which forwards to the wheel's C++ function _IborLeg (QuantLib-SWIG 1.44,
SWIG/cashflows.i): it builds a C++ QuantLib::IborLeg and calls withNotionals, withPaymentDayCounter,
withPaymentAdjustment, withPaymentCalendar (the schedule's calendar when none is given, QuantLib's own default),
withPaymentLag, withFixingDays, withFixingConvention (Preceding, its default), withGearings, withSpreads, withCaps,
withFloors, inArrears, withExCouponPeriod, withIndexedCoupons and withStubIndexSelection, and converts the builder to
a Leg. It doesn't pass withZeroPayments, whose legs the harness's cxx/cashflows/iborcoupon covers.

The functions here build the specs every Ibor coupon generator shares (cashflows/floatingratecoupon,
capflooredcoupon, couponpricer, stubiborcoupon and their harness counterparts, which import this file):
- an index is a spec of indexes/interestrateindex.py (an IborIndex with QuantLib's empty currency);
- a volatility is null or {"referenceDate", "calendar", "convention", "volatility", "dayCounter", "volatilityType",
  "displacement"}, a ConstantOptionletVolatility with a fixed reference date;
- a pricer is {"type": "BlackIborCouponPricer", "volatility", "timingAdjustment", "correlation", "useIndexedCoupon"},
  where a null useIndexedCoupon builds it without an explicit choice, reading the at-par switch;
- a coupon is {"type": "IborCoupon", "paymentDate", "nominal", "startDate", "endDate", "fixingDays", "index",
  "gearing", "spread", "refPeriodStart", "refPeriodEnd", "dayCounter", "isInArrears", "exCouponDate",
  "fixingConvention", "pricer"}, the same with "cap" and "floor" for "CappedFlooredIborCoupon", or {"type":
  "CappedFlooredCoupon", "underlying", "cap", "floor", "pricer"}; null fixing days are QuantLib's Null<Natural>(), a
  null day counter its empty one, a null cap or floor Null<Rate>(), and a pricer is set with setPricer once built;
- a leg is {"schedule", "index", "notionals", "paymentDayCounter", "paymentConvention", "fixingDays", "gearings",
  "spreads", "caps", "floors", "isInArrears", "exCouponPeriod", "paymentCalendar", "paymentLag",
  "withIndexedCoupons", "stubIndexSelection", "pricer"}: a schedule spec of fixedratecoupon.py, or {"dates": [...]}
  for a schedule of dates with no regularity information; an ex-coupon period {"period", "calendar", "convention",
  "endOfMonth"}; a stub selection {"convention", "indices"}; the harness's legs add "zeroPayments" and
  "fixingConvention".
Read back by tests/NanoQL.Tests/Vectors/Coupons/IborCouponVectorTests.cs, with
tests/NanoQL.Tests/Vectors/CouponInputs.cs."""
import importlib.util
import math
import os
import random
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


irx = _load("wheel_interestrateindex", "indexes/interestrateindex.py")
frc = _load("wheel_fixedratecoupon", "cashflows/fixedratecoupon.py")

TODAY = irx.TODAY
TARGET, NULL_CALENDAR, US_SOFR, UK_EXCHANGE = irx.TARGET, irx.NULL_CALENDAR, irx.US_SOFR, irx.UK_EXCHANGE
A360, A365, THIRTY360_BB = irx.A360, irx.A365, irx.THIRTY360_BB
CURVE = irx.curve_spec(TODAY, 0.031)
CURVE_STEEP = irx.curve_spec(TODAY, 0.045, A360, ql.Simple)
CURVE_NEGATIVE = irx.curve_spec(TODAY, -0.004)
DISCOUNT = irx.curve_spec(TODAY, 0.025)


def d(day, month, year):
    return ql.Date(day, month, year)


def euribor(tenor, curve=CURVE, family="Euribor"):
    """An IborIndex with Euribor's conventions: TARGET, 2 fixing days, Actual/360, and for a tenor of weeks
    Following without end of month, else ModifiedFollowing with it."""
    weeks = tenor[1] in (ql.Days, ql.Weeks)
    return irx.ibor_spec(family, tenor, 2, TARGET, ql.Following if weeks else ql.ModifiedFollowing, not weeks, A360,
                         curve)


E1W, E1M, E3M = euribor((1, ql.Weeks)), euribor((1, ql.Months)), euribor((3, ql.Months))
E6M, E1Y = euribor((6, ql.Months)), euribor((1, ql.Years))


def vol_spec(volatility, vtype=ql.ShiftedLognormal, displacement=0.0, reference=TODAY, dc=A365, cal=TARGET,
             convention=ql.Following):
    return {"referenceDate": v.enc(reference), "calendar": cal, "convention": int(convention),
            "volatility": volatility, "dayCounter": dc, "volatilityType": int(vtype), "displacement": displacement}


VOL_LN = vol_spec(0.20)
VOL_SLN = vol_spec(0.15, ql.ShiftedLognormal, 0.02)
VOL_N = vol_spec(0.0085, ql.Normal)


def volatility(spec):
    if spec is None:
        return ql.OptionletVolatilityStructureHandle()
    return ql.OptionletVolatilityStructureHandle(ql.ConstantOptionletVolatility(
        ql.Date(spec["referenceDate"]), irx.calendar(spec["calendar"]), spec["convention"], spec["volatility"],
        irx.day_counter(spec["dayCounter"]), spec["volatilityType"], spec["displacement"]))


def pricer_spec(vol=None, timing=ql.BlackIborCouponPricer.Black76, correlation=1.0, use_indexed=None):
    return {"type": "BlackIborCouponPricer", "volatility": vol, "timingAdjustment": int(timing),
            "correlation": correlation, "useIndexedCoupon": use_indexed}


def pricer(spec):
    """The pricer a spec describes, built when this is called: one without an explicit choice reads the at-par
    switch then, as QuantLib's constructor does."""
    if spec is None:
        return None
    assert spec["type"] == "BlackIborCouponPricer", spec
    return ql.BlackIborCouponPricer(volatility(spec["volatility"]), spec["timingAdjustment"],
                                    ql.QuoteHandle(ql.SimpleQuote(spec["correlation"])), spec["useIndexedCoupon"])


def ibor_coupon_spec(payment, nominal, start, end, fixing_days, index, gearing=1.0, spread=0.0, ref_start=None,
                     ref_end=None, dc=None, in_arrears=False, ex_coupon=None, fixing_convention=ql.Preceding,
                     pricer=None, cap=None, floor=None, capped=False):
    spec = {"type": "CappedFlooredIborCoupon" if capped else "IborCoupon", "paymentDate": v.enc(payment),
            "nominal": nominal, "startDate": v.enc(start), "endDate": v.enc(end), "fixingDays": fixing_days,
            "index": index, "gearing": gearing, "spread": spread, "refPeriodStart": v.enc(ref_start),
            "refPeriodEnd": v.enc(ref_end), "dayCounter": dc, "isInArrears": in_arrears,
            "exCouponDate": v.enc(ex_coupon), "fixingConvention": int(fixing_convention), "pricer": pricer}
    if capped:
        spec["cap"], spec["floor"] = cap, floor
    return spec


def capped_coupon_spec(underlying, cap=None, floor=None, pricer=None):
    return {"type": "CappedFlooredCoupon", "underlying": underlying, "cap": cap, "floor": floor, "pricer": pricer}


def _date(serial):
    return ql.Date(serial) if serial is not None else ql.Date()


def _null_real(x):
    return ql.nullDouble() if x is None else x


def coupon(spec):
    """The coupon a spec describes, with its pricer set once it is built. The arguments go by name, so a null day
    counter leaves the constructor's default, QuantLib's empty day counter, which the wheel can't build."""
    t = spec["type"]
    if t == "CappedFlooredCoupon":
        c = ql.CappedFlooredCoupon(coupon(spec["underlying"]), _null_real(spec["cap"]), _null_real(spec["floor"]))
    else:
        kw = {"gearing": spec["gearing"], "spread": spec["spread"], "refPeriodStart": _date(spec["refPeriodStart"]),
              "refPeriodEnd": _date(spec["refPeriodEnd"]), "isInArrears": spec["isInArrears"],
              "exCouponDate": _date(spec["exCouponDate"]), "fixingConvention": spec["fixingConvention"]}
        if spec["dayCounter"] is not None:
            kw["dayCounter"] = irx.day_counter(spec["dayCounter"])
        args = [_date(spec["paymentDate"]), spec["nominal"], _date(spec["startDate"]), _date(spec["endDate"]),
                ql.nullInt() if spec["fixingDays"] is None else spec["fixingDays"], irx.make_index(spec["index"])]
        if t == "IborCoupon":
            c = ql.IborCoupon(*args, **kw)
        elif t == "CappedFlooredIborCoupon":
            c = ql.CappedFlooredIborCoupon(*args, cap=_null_real(spec["cap"]), floor=_null_real(spec["floor"]), **kw)
        else:
            raise ValueError(f"unknown coupon {t}")
    p = pricer(spec["pricer"])
    if p is not None:
        c.setPricer(p)
    return c


def schedule(spec):
    if "dates" in spec:
        return ql.Schedule([ql.Date(s) for s in spec["dates"]])
    return frc.schedule(spec)


def stub_selection(spec):
    if spec is None:
        return ql.StubIndexSelection()
    return ql.StubIndexSelection(spec["convention"], [irx.make_index(i) for i in spec["indices"]])


def leg_spec(schedule_spec, index, notionals, payment_dc=None, payment_convention=ql.Following, fixing_days=(),
             gearings=(), spreads=(), caps=(), floors=(), in_arrears=False, ex_coupon=None, payment_calendar=None,
             payment_lag=0, indexed=None, stub=None, pricer=None):
    return {"schedule": schedule_spec, "index": index, "notionals": list(notionals), "paymentDayCounter": payment_dc,
            "paymentConvention": int(payment_convention), "fixingDays": list(fixing_days), "gearings": list(gearings),
            "spreads": list(spreads), "caps": list(caps), "floors": list(floors), "isInArrears": in_arrears,
            "exCouponPeriod": ex_coupon, "paymentCalendar": payment_calendar, "paymentLag": payment_lag,
            "withIndexedCoupons": indexed, "stubIndexSelection": stub, "pricer": pricer}


def leg(spec):
    """The leg a spec describes, with its spec's pricer set on it once it is built."""
    kw = {"paymentConvention": spec["paymentConvention"],
          "fixingDays": [ql.nullInt() if x is None else x for x in spec["fixingDays"]],
          "gearings": spec["gearings"], "spreads": spec["spreads"],
          "caps": [_null_real(x) for x in spec["caps"]], "floors": [_null_real(x) for x in spec["floors"]],
          "isInArrears": spec["isInArrears"], "paymentLag": spec["paymentLag"],
          "withIndexedCoupons": spec["withIndexedCoupons"],
          "stubIndexSelection": stub_selection(spec["stubIndexSelection"])}
    if spec["paymentDayCounter"] is not None:
        kw["paymentDayCounter"] = irx.day_counter(spec["paymentDayCounter"])
    if spec["paymentCalendar"] is not None:
        kw["paymentCalendar"] = irx.calendar(spec["paymentCalendar"])
    ex = spec["exCouponPeriod"]
    if ex is not None:
        kw.update(exCouponPeriod=ql.Period(ex["period"][0], ex["period"][1]), exCouponCalendar=irx.calendar(ex["calendar"]),
                  exCouponConvention=ex["convention"], exCouponEndOfMonth=ex["endOfMonth"])
    result = ql.IborLeg(spec["notionals"], schedule(spec["schedule"]), irx.make_index(spec["index"]), **kw)
    p = pricer(spec["pricer"])
    if p is not None:
        ql.setCouponPricer(result, p)
    return result


def ex_coupon(n, units=ql.Days, cal=TARGET, convention=ql.Preceding, eom=False):
    return {"period": [n, int(units)], "calendar": cal, "convention": int(convention), "endOfMonth": eom}


def history(index_spec, first, last, seed, skip=()):
    """Fixings for an index on its business days from first to last, drawn with the seed, as (date, value) pairs."""
    rnd = random.Random(seed)
    cal = irx.calendar(index_spec["fixingCalendar"])
    out = []
    day = first
    while day <= last:
        if cal.isBusinessDay(day) and day not in skip:
            out.append((day, round(rnd.uniform(0.025, 0.036), 6)))
        day += 1
    return out


def scaled(f, scale):
    """f()'s value as an amount at the scale, or {"throws": true} where QuantLib throws."""
    try:
        x = f()
    except RuntimeError:
        return dict(v.THROWS)
    return v.amount(x, scale)


def leg_outputs(cashflows, scale):
    """Per cash flow: its dates, nominal, rate and amount, and a floating coupon's fixing date, fixing days, gearing,
    spread and whether it holds a pricer (null for a fixed-rate coupon)."""
    coupons = [ql.as_coupon(cf) for cf in cashflows]
    floating = [ql.as_floating_rate_coupon(cf) for cf in cashflows]

    def each(f, xs=None):
        return [v.attempt(lambda c=c: f(c)) for c in (xs if xs is not None else coupons)]

    def each_floating(f):
        return [None if fc is None else v.attempt(lambda fc=fc: f(fc)) for fc in floating]

    return {
        "date": each(lambda c: c.date()),
        "nominal": each(lambda c: c.nominal()),
        "accrualStartDate": each(lambda c: c.accrualStartDate()),
        "accrualEndDate": each(lambda c: c.accrualEndDate()),
        "referencePeriodStart": each(lambda c: c.referencePeriodStart()),
        "referencePeriodEnd": each(lambda c: c.referencePeriodEnd()),
        "exCouponDate": each(lambda c: c.exCouponDate()),
        "fixingDate": each_floating(lambda c: c.fixingDate()),
        "fixingDays": each_floating(lambda c: c.fixingDays()),
        "gearing": each_floating(lambda c: c.gearing()),
        "spread": each_floating(lambda c: c.spread()),
        # a capped, floored or in-arrears leg is built with none (iborcoupon.cpp:322-327)
        "hasPricer": each_floating(lambda c: c.pricer() is not None),
        "rate": each(lambda c: c.rate()),
        "amount": [scaled(lambda c=c: c.amount(), scale) for c in coupons],
    }


def sched(eff, mat, tenor, convention=ql.ModifiedFollowing, rule=ql.DateGeneration.Backward, eom=False, first=None,
          cal=TARGET, next_to_last=None):
    s = frc.schedule_spec(eff, mat, tenor, cal, convention, rule, eom, first)
    s["nextToLastDate"] = v.enc(next_to_last)
    return s


SIX_M = ql.Period(6, ql.Months)
THREE_M = ql.Period(3, ql.Months)
ONE_Y = ql.Period(1, ql.Years)

# Past fixings for the indexes the legs fix on: every TARGET business day of the year to the evaluation date.
FIXINGS_FROM = d(15, 1, 2024)


def fixings_for(*specs):
    return [(irx.make_index(s), history(s, FIXINGS_FROM, TODAY - 1, v.SEED + k)) for k, s in enumerate(specs)]


def has_fixed_cases():
    """(name, fixing-date offset in business days, enforce flag, store a fixing on the fixing date)."""
    return [("fixing a day before", -1, False, False), ("fixing a day before, stored", -1, False, True),
            ("fixing today, none stored", 0, False, False), ("fixing today, stored", 0, False, True),
            ("fixing today, enforced, none stored", 0, True, False), ("fixing today, enforced, stored", 0, True, True),
            ("fixing a day after", 1, False, False), ("fixing a day after, enforced", 1, True, False),
            ("fixing months before, stored", -60, False, True), ("fixing months after", 80, True, False)]


def ibor_legs():
    eur6 = sched(d(17, 1, 2024), d(17, 1, 2028), SIX_M)
    short_front = sched(d(5, 3, 2024), d(17, 7, 2027), SIX_M)
    long_front = sched(d(17, 9, 2024), d(17, 7, 2027), SIX_M, rule=ql.DateGeneration.Forward, first=d(17, 7, 2025))
    short_back = sched(d(20, 2, 2025), d(5, 1, 2028), SIX_M, rule=ql.DateGeneration.Forward)
    both_stubs = sched(d(3, 2, 2025), d(28, 9, 2027), SIX_M, rule=ql.DateGeneration.Backward, first=d(17, 4, 2025),
                       next_to_last=d(17, 4, 2027))
    quarterly = sched(d(20, 11, 2024), d(20, 11, 2026), THREE_M, convention=ql.Following)
    eom = sched(d(31, 1, 2025), d(31, 1, 2029), SIX_M, eom=True)
    annual = sched(d(15, 1, 2025), d(15, 1, 2031), ONE_Y, rule=ql.DateGeneration.Forward)
    us = sched(d(21, 1, 2025), d(21, 1, 2027), THREE_M, cal=US_SOFR)
    one_date = sched(d(17, 1, 2025), d(17, 1, 2025), SIX_M)
    dates_only = {"dates": [v.enc(x) for x in (d(20, 2, 2025), d(17, 7, 2025), d(19, 1, 2026), d(17, 7, 2026))]}
    vol = pricer_spec(VOL_LN)
    nan = math.nan
    usd3m = irx.ibor_spec("UsdIbor", (3, ql.Months), 2, US_SOFR, ql.ModifiedFollowing, False, A360, CURVE_STEEP)
    stub = {"convention": int(ql.StubIndexSelection.Interpolated), "indices": [E1M, E3M, E6M]}
    # (name, leg spec, at-par setting, fixings)
    return [
        ("plain semiannual, past and future coupons", leg_spec(eur6, E6M, [1e6]), True, fixings_for(E6M)),
        ("plain semiannual, indexed by the switch", leg_spec(eur6, E6M, [1e6]), False, fixings_for(E6M)),
        ("plain semiannual, at par by the leg", leg_spec(eur6, E6M, [1e6], indexed=False), False, fixings_for(E6M)),
        ("plain semiannual, indexed by the leg", leg_spec(eur6, E6M, [1e6], indexed=True), True, fixings_for(E6M)),
        ("short front stub on 3M", leg_spec(short_front, E3M, [2e6]), True, fixings_for(E3M)),
        ("long front stub", leg_spec(long_front, E6M, [1e6], indexed=True), True, fixings_for(E6M)),
        ("short back stub", leg_spec(short_back, E6M, [5e5]), False, None),
        ("front and back stubs", leg_spec(both_stubs, E6M, [1e6]), True, None),
        ("amortizing notionals, gearings and spreads", leg_spec(
            annual, E1Y, [1e6, 8e5, 6e5, 4e5], gearings=[1.0, 1.5, 0.75], spreads=[0.001, -0.0005, 0.002, 0.0]),
         True, fixings_for(E1Y)),
        ("fixing days vector with nulls", leg_spec(quarterly, E3M, [1e6], fixing_days=[0, None, 3, 1]), True,
         fixings_for(E3M)),
        ("payment day counter and lag", leg_spec(eom, E6M, [3e6], payment_dc=THIRTY360_BB, payment_lag=2,
                                                 payment_convention=ql.ModifiedFollowing), True, None),
        ("payment calendar", leg_spec(eur6, E6M, [1e6], payment_calendar=UK_EXCHANGE, payment_lag=1,
                                      payment_convention=ql.Preceding), False, fixings_for(E6M)),
        ("ex-coupon business days", leg_spec(eur6, E6M, [1e6], ex_coupon=ex_coupon(3)), True, fixings_for(E6M)),
        ("ex-coupon calendar days, end of month", leg_spec(eom, E6M, [1e6], ex_coupon=ex_coupon(
            1, ql.Months, NULL_CALENDAR, ql.Unadjusted, True)), True, None),
        ("caps", leg_spec(eur6, E6M, [1e6], caps=[0.032], pricer=vol), True, fixings_for(E6M)),
        ("floors", leg_spec(eur6, E6M, [1e6], floors=[0.03], pricer=vol), False, fixings_for(E6M)),
        ("collars with uncapped periods", leg_spec(eur6, E6M, [1e6], caps=[0.035, None, 0.033],
                                                   floors=[None, 0.028, 0.029], pricer=vol), True, fixings_for(E6M)),
        ("caps at negative gearing", leg_spec(eur6, E6M, [1e6], gearings=[-1.0], spreads=[0.06], caps=[0.03],
                                              pricer=pricer_spec(VOL_N)), True, fixings_for(E6M)),
        ("in arrears", leg_spec(quarterly, E3M, [1e6], in_arrears=True, pricer=vol), True, fixings_for(E3M)),
        # No default pricer on a capped, floored or in-arrears leg (iborcoupon.cpp:322-327): priced without one,
        # its floating coupons refuse "pricer not set".
        ("caps, no pricer set", leg_spec(eur6, E6M, [1e6], caps=[0.032]), True, fixings_for(E6M)),
        ("floors, no pricer set", leg_spec(eur6, E6M, [1e6], floors=[0.03]), False, fixings_for(E6M)),
        ("in arrears, no pricer set", leg_spec(quarterly, E3M, [1e6], in_arrears=True), True, fixings_for(E3M)),
        ("in arrears without a volatility", leg_spec(quarterly, E3M, [1e6], in_arrears=True, pricer=pricer_spec()),
         True, fixings_for(E3M)),
        ("capped in arrears, bivariate lognormal", leg_spec(annual, E1Y, [1e6], in_arrears=True, caps=[0.034],
                                                           pricer=pricer_spec(VOL_SLN,
                                                                              ql.BlackIborCouponPricer.BivariateLognormal,
                                                                              0.7)), False, None),
        ("zero gearing: floored, capped and collared spreads", leg_spec(
            annual, E1Y, [1e6], gearings=[0.0], spreads=[0.02, nan, nan, nan, -0.01, nan],
            caps=[None, None, 0.05, 0.05, None, None], floors=[None, 0.01, None, 0.01, 0.0, None],
            payment_dc=A360), True, None),
        ("zero and nonzero gearings", leg_spec(eur6, E6M, [1e6], gearings=[1.0, 0.0, 2.0], spreads=[0.0, 0.025],
                                               payment_dc=A365), True, fixings_for(E6M)),
        ("a US Ibor on its calendar", leg_spec(us, usd3m, [1e6], spreads=[0.0015]), True, None),
        ("a stub selection on a capped leg", leg_spec(short_front, E6M, [1e6], caps=[0.05], indexed=True, stub=stub),
         True, None),
        ("a stub selection in arrears", leg_spec(short_front, E6M, [1e6], in_arrears=True, indexed=True, stub=stub),
         True, None),
        ("a stub selection on an at-par leg", leg_spec(short_front, E6M, [1e6], stub=stub), True, None),
        ("a stub selection without regularity information", leg_spec(dates_only, E6M, [1e6], indexed=True, stub=stub),
         True, None),
        ("too many notionals", leg_spec(quarterly, E3M, [1e6] * 9), True, None),
        ("a one-date schedule", leg_spec(one_date, E6M, [1e6]), True, None),
    ]


def has_fixed_scenarios():
    cal = irx.calendar(TARGET)
    out = []
    for name, offset, enforce, store in has_fixed_cases():
        fixing = cal.advance(TODAY, offset, ql.Days)
        index = irx.make_index(E6M)
        start = index.valueDate(fixing)
        spec = ibor_coupon_spec(cal.advance(start, SIX_M), 1e6, start, cal.advance(start, SIX_M), 2, E6M,
                                pricer=pricer_spec())
        out.append((name, spec, enforce, [(index, [(fixing, 0.0291)])] if store else None))
    return out


def cases():
    out = []
    for name, spec, enforce, fixings in has_fixed_scenarios():
        with v.case_settings(TODAY, enforces_todays_historic_fixings=enforce, fixings=fixings) as st:
            c = coupon(spec)
            out.append({"name": f"hasFixed: {name}", "inputs": {"settings": st, "coupon": v.enc(spec)},
                        "expected": {"fixingDate": v.enc(c.fixingDate()), "hasFixed": c.hasFixed(),
                                     "rate": v.attempt(c.rate)}})
    for name, spec, at_par, fixings in ibor_legs():
        with v.case_settings(TODAY, use_at_par_coupons=at_par, fixings=fixings) as st:
            inputs = {"settings": st, "leg": v.enc(spec)}
            try:
                cashflows = leg(spec)
            except RuntimeError:
                out.append({"name": f"leg: {name}", "inputs": inputs, "expected": {"leg": dict(v.THROWS)}})
                continue
            out.append({"name": f"leg: {name}", "inputs": inputs,
                        "expected": leg_outputs(cashflows, max(spec["notionals"]))})
    # A leg keeps the at-par choice it was built with (couponpricer.cpp:46-52): built under one switch, it is priced
    # under the other, which the case's settings record; "builtWithAtParCoupons" names the switch it was built under.
    for built_at_par in (True, False):
        spec = leg_spec(sched(d(17, 1, 2024), d(17, 1, 2028), SIX_M), E6M, [1e6])
        fixings = fixings_for(E6M)
        with v.case_settings(TODAY, use_at_par_coupons=built_at_par, fixings=fixings):
            cashflows = leg(spec)
            # the pricing settings: the other switch, and the same fixings (equal duplicates, which QuantLib accepts)
            st = v.settings(TODAY, use_at_par_coupons=not built_at_par, fixings=fixings)
            label = ("at par, priced under the indexed switch" if built_at_par
                     else "indexed, priced under the at-par switch")
            out.append({"name": f"leg: built {label}",
                        "inputs": {"settings": st, "builtWithAtParCoupons": built_at_par, "leg": v.enc(spec)},
                        "expected": leg_outputs(cashflows, 1e6)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/iborcoupon.hpp", cases)
