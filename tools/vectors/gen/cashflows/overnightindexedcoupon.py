# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's OvernightIndexedCoupon and OvernightLeg (ql/cashflows/overnightindexedcoupon.hpp), on the
wheel: about 150 coupons over the conventions of the coupon, 20 legs, and the three paths of a spread compounded daily.

The coupons sample, for five overnight indexes (fixing 0, 1 or 2 days back, on the SOFR, TARGET and UK calendars),
the averaging (compounded or simple) × the lookback (none, 0 or 2) × the lockout (0 or 2) × the observation shift ×
telescopic value dates (where QuantLib accepts them) × the spread (added or compounded daily) × the rounding (none, 4
or 8) × past, spanning and future periods, with gearings, explicit day counters, ex-coupon dates, payment lags and
in-advance rate-computation dates among them; a few combinations QuantLib refuses (simple averaging with a lookback, a
lockout or the shift; telescopic dates with a lookback and no shift; a lockout as long as the period) record its
throw. Each coupon's outputs are whether it builds, its rate, amount and accrued amounts at four dates, its fixing,
value and interest dates, dt, index fixings, effective spread and index fixing, the rate-computation dates and its
conventions' inspectors. Two coupons start on a Saturday with the shift, one with a null lookback and one with a
lookback of 0, so dt[0] accrues on the value date or on the interest date (D10, SD26); one misses a past fixing.

Telescopic cases take QuantLib's expected values by the plan's telescopic rule (S3R11, SD11): QuantLib builds the case
with the flag first, so a refusal is its own; the values are then the same case's built without the flag, an averaged
coupon given ArithmeticAveragedOvernightIndexedCouponPricer(byApprox=True), the pricer the flag selects. So no output
is overridden. The wheel's ArithmeticAveragedOvernightIndexedCouponPricer is built with keyword arguments only: a
positional True binds to meanReversion.

The legs (QuantLib-Python's OvernightLeg, which forwards each keyword to the C++ builder's setter of the same meaning:
nominals to withNotionals, paymentConvention to withPaymentAdjustment, paymentCalendar to withPaymentCalendar, or the
schedule's when none is given, and roundingPrecision to withRoundingPrecision only when given) cover rule-based and
date-list schedules, a one-date schedule (an empty leg, D12), vectors of nominals, gearings, spreads, caps and floors
(a gearing of 0 making a fixed-rate coupon), payment lags, calendars and explicit payment dates, in-advance fixing,
and the conventions; each leg's outputs are its length and each cash flow's type, dates, nominal, rate and amount, and
for overnight and capped coupons the rate-computation dates, gearing and spread, cap and floor. A capped coupon of a
wheel leg has no pricer (the wheel's OvernightLeg takes none), so its rate is QuantLib's throw.

The three paths of a spread compounded daily (D23; plan premise SP26): a 3-month SOFR coupon on a flat 3.12345% curve
with a 1.23456% spread, every fixing ahead, where the telescopic formula applies and QuantLib 1.44 drops the spread;
the same with a lookback of 2 days and no shift, where it doesn't apply; and the same coupon once fixed.

Each case's inputs hold its settings (with the fixings stored, by vectors.case_settings) and its "coupon" or "leg"
spec. The spec functions here are shared by the other overnight generators, which import this file. Read back by
tests/NanoQL.Tests/Vectors/Coupons/OvernightIndexedCouponVectorTests.cs, whose inputs class
(tests/NanoQL.Tests/Vectors/OvernightInputs.cs) documents the specs."""
import importlib.util
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

A360, A365 = irx.A360, irx.A365

# The overnight indexes: (family, fixing days, calendar, day counter). SOFR, ESTR and SONIA are their presets'
# conventions; Ond and Ont fix two and one business days back.
INDEXES = [("SOFR", 0, irx.US_SOFR, A360), ("ESTR", 0, irx.TARGET, A360), ("SONIA", 0, irx.UK_EXCHANGE, A365),
           ("Ond", 2, irx.TARGET, A360), ("Ont", 1, irx.US_SOFR, A365)]


def index_spec(k, today, forward=None, dc=None, compounding=ql.Continuous, frequency=ql.Annual):
    family, days, cal, idc = INDEXES[k]
    curve = None if forward is None else irx.curve_spec(today, forward, dc or idc, compounding, frequency)
    return irx.overnight_spec(family, days, cal, idc, curve)


def make_index(spec):
    return irx.make_index(spec)


def coupon_spec(index, payment, start, end, nominal=1e6, gearing=1.0, spread=0.0, ref_start=None, ref_end=None,
                dc=None, telescopic=False, averaging=ql.RateAveraging.Compound, lookback=None, lockout=0,
                shift=False, compound_spread=False, rc_start=None, rc_end=None, ex_coupon=None, rounding=None):
    return {"paymentDate": v.enc(payment), "nominal": nominal, "startDate": v.enc(start), "endDate": v.enc(end),
            "index": index, "gearing": gearing, "spread": spread, "refPeriodStart": v.enc(ref_start),
            "refPeriodEnd": v.enc(ref_end), "dayCounter": dc, "telescopicValueDates": telescopic,
            "averagingMethod": int(averaging), "lookbackDays": lookback, "lockoutDays": lockout,
            "applyObservationShift": shift, "compoundSpreadDaily": compound_spread,
            "rateComputationStartDate": v.enc(rc_start), "rateComputationEndDate": v.enc(rc_end),
            "exCouponDate": v.enc(ex_coupon), "roundingPrecision": rounding}


def _date(serial):
    return ql.Date() if serial is None else ql.Date(serial)


def coupon_kwargs(spec, telescopic):
    kw = {"gearing": spec["gearing"], "spread": spec["spread"], "refPeriodStart": _date(spec["refPeriodStart"]),
          "refPeriodEnd": _date(spec["refPeriodEnd"]), "telescopicValueDates": telescopic,
          "averagingMethod": spec["averagingMethod"], "lockoutDays": spec["lockoutDays"],
          "applyObservationShift": spec["applyObservationShift"], "compoundSpread": spec["compoundSpreadDaily"],
          "rateComputationStartDate": _date(spec["rateComputationStartDate"]),
          "rateComputationEndDate": _date(spec["rateComputationEndDate"]),
          "exCouponDate": _date(spec["exCouponDate"])}
    if spec["dayCounter"] is not None:
        kw["dayCounter"] = irx.day_counter(spec["dayCounter"])
    if spec["lookbackDays"] is not None:
        kw["lookbackDays"] = spec["lookbackDays"]
    if spec["roundingPrecision"] is not None:
        kw["roundingPrecision"] = spec["roundingPrecision"]
    return kw


def make_coupon(spec, index=None):
    """The wheel's coupon for a spec, by the telescopic rule: built with the flag first, so that QuantLib's refusal is
    its own; then, for a telescopic spec, built without it, an averaged coupon given the approximating pricer the flag
    selects. Returns the coupon to read."""
    index = index or make_index(spec["index"])
    args = (ql.Date(spec["paymentDate"]), spec["nominal"], ql.Date(spec["startDate"]), ql.Date(spec["endDate"]),
            index)
    c = ql.OvernightIndexedCoupon(*args, **coupon_kwargs(spec, spec["telescopicValueDates"]))
    if spec["telescopicValueDates"]:
        c = ql.OvernightIndexedCoupon(*args, **coupon_kwargs(spec, False))
        if spec["averagingMethod"] == ql.RateAveraging.Simple:
            c.setPricer(ql.ArithmeticAveragedOvernightIndexedCouponPricer(byApprox=True))
    return c


def capped_spec(underlying, cap=None, floor=None, naked=False, daily=False):
    return {"underlying": underlying, "cap": cap, "floor": floor, "nakedOption": naked, "dailyCapFloor": daily}


def make_capped(spec, index=None):
    u = make_coupon(spec["underlying"], index)
    cap = v.NULL_REAL if spec["cap"] is None else spec["cap"]
    floor = v.NULL_REAL if spec["floor"] is None else spec["floor"]
    return ql.CappedFlooredOvernightIndexedCoupon(u, cap, floor, spec["nakedOption"], spec["dailyCapFloor"])


def vol_spec(reference, volatility, cal=irx.TARGET, convention=ql.Following, dc=A365,
             vol_type=ql.ShiftedLognormal, displacement=0.0):
    return {"referenceDate": v.enc(reference), "calendar": cal, "convention": int(convention),
            "volatility": volatility, "dayCounter": dc, "type": int(vol_type), "displacement": displacement}


def make_vol(spec):
    return ql.ConstantOptionletVolatility(ql.Date(spec["referenceDate"]), irx.calendar(spec["calendar"]),
                                          spec["convention"], spec["volatility"], irx.day_counter(spec["dayCounter"]),
                                          spec["type"], spec["displacement"])


def vol_handle(spec):
    return ql.OptionletVolatilityStructureHandle() if spec is None \
        else ql.OptionletVolatilityStructureHandle(make_vol(spec))


def pricer_spec(kind, vol=None, effective=False, mean_reversion=0.03, volatility=0.0, by_approx=False):
    """A pricer: Compounding, ArithmeticAveraged (with its mean reversion, volatility and approximation),
    BlackCompounding or BlackAveraging, each with its optionlet volatility (or none) and effective-volatility flag."""
    spec = {"type": kind, "vol": vol, "effectiveVolatilityInput": effective}
    if kind == "ArithmeticAveraged":
        spec.update(meanReversion=mean_reversion, volatility=volatility, byApprox=by_approx)
    return spec


def make_pricer(spec):
    h, e = vol_handle(spec["vol"]), spec["effectiveVolatilityInput"]
    kind = spec["type"]
    if kind == "Compounding":
        return ql.CompoundingOvernightIndexedCouponPricer(h, e)
    if kind == "ArithmeticAveraged":
        return ql.ArithmeticAveragedOvernightIndexedCouponPricer(
            meanReversion=spec["meanReversion"], volatility=spec["volatility"], byApprox=spec["byApprox"], v=h,
            effectiveVolatilityInput=e)
    if kind == "BlackCompounding":
        return ql.BlackCompoundingOvernightIndexedCouponPricer(h, e)
    if kind == "BlackAveraging":
        return ql.BlackAveragingOvernightIndexedCouponPricer(h, e)
    raise ValueError(f"unknown pricer {kind}")


def rule_schedule(eff, mat, tenor, cal, convention, rule=ql.DateGeneration.Forward, eom=False, first=None):
    return {"effectiveDate": v.enc(eff), "terminationDate": v.enc(mat), "tenor": [tenor.length(), int(tenor.units())],
            "calendar": cal, "convention": int(convention), "terminationDateConvention": int(convention),
            "rule": int(rule), "endOfMonth": eom, "firstDate": v.enc(first), "nextToLastDate": None}


def dates_schedule(dates, cal=None, tenor=None, is_regular=None):
    """A schedule from a list of dates, with the calendar (QuantLib's NullCalendar when none), the tenor and the
    regularity flags it may carry."""
    return {"dates": v.enc(dates), "calendar": cal, "tenor": None if tenor is None else [tenor.length(),
                                                                                         int(tenor.units())],
            "isRegular": is_regular or []}


def make_schedule(spec):
    if "dates" in spec:
        cal = ql.NullCalendar() if spec["calendar"] is None else irx.calendar(spec["calendar"])
        dates = [ql.Date(d) for d in spec["dates"]]
        tenor = ql.Period(spec["tenor"][0], spec["tenor"][1]) if spec["tenor"] is not None else None
        return ql.Schedule(dates, cal, ql.Unadjusted, None, tenor, None, None, spec["isRegular"])
    return ql.Schedule(ql.Date(spec["effectiveDate"]), ql.Date(spec["terminationDate"]),
                       ql.Period(spec["tenor"][0], spec["tenor"][1]), irx.calendar(spec["calendar"]),
                       spec["convention"], spec["terminationDateConvention"], spec["rule"], spec["endOfMonth"],
                       _date(spec["firstDate"]), ql.Date())


def leg_spec(schedule, index, notionals, payment_dc=None, payment_convention=ql.Following, payment_calendar=None,
             payment_lag=0, gearings=(), spreads=(), telescopic=False, averaging=ql.RateAveraging.Compound,
             lookback=None, lockout=0, shift=False, compound_spread=False, caps=(), floors=(), daily=False,
             in_arrears=True, naked=False, payment_dates=(), rounding=None):
    return {"schedule": schedule, "index": index, "notionals": list(notionals), "paymentDayCounter": payment_dc,
            "paymentConvention": int(payment_convention), "paymentCalendar": payment_calendar,
            "paymentLag": payment_lag, "gearings": list(gearings), "spreads": list(spreads),
            "telescopicValueDates": telescopic, "averagingMethod": int(averaging), "lookbackDays": lookback,
            "lockoutDays": lockout, "applyObservationShift": shift, "compoundSpreadDaily": compound_spread,
            "caps": list(caps), "floors": list(floors), "dailyCapFloor": daily, "inArrears": in_arrears,
            "nakedOption": naked, "paymentDates": v.enc(list(payment_dates)), "roundingPrecision": rounding}


def leg_kwargs(spec, telescopic):
    kw = {"paymentConvention": spec["paymentConvention"], "paymentLag": spec["paymentLag"],
          "gearings": spec["gearings"], "spreads": spec["spreads"], "telescopicValueDates": telescopic,
          "averagingMethod": spec["averagingMethod"], "lockoutDays": spec["lockoutDays"],
          "applyObservationShift": spec["applyObservationShift"], "compoundSpreadDaily": spec["compoundSpreadDaily"],
          "caps": [v.NULL_REAL if x is None else x for x in spec["caps"]],
          "floors": [v.NULL_REAL if x is None else x for x in spec["floors"]],
          "dailyCapFloor": spec["dailyCapFloor"], "inArrears": spec["inArrears"], "nakedOption": spec["nakedOption"],
          "paymentDates": [ql.Date(d) for d in spec["paymentDates"]]}
    if spec["paymentDayCounter"] is not None:
        kw["paymentDayCounter"] = irx.day_counter(spec["paymentDayCounter"])
    if spec["paymentCalendar"] is not None:
        kw["paymentCalendar"] = irx.calendar(spec["paymentCalendar"])
    if spec["lookbackDays"] is not None:
        kw["lookbackDays"] = spec["lookbackDays"]
    if spec["roundingPrecision"] is not None:
        kw["roundingPrecision"] = spec["roundingPrecision"]
    return kw


def make_leg(spec, index=None):
    """The wheel's leg for a spec, by the telescopic rule as make_coupon applies it."""
    index = index or make_index(spec["index"])
    schedule = make_schedule(spec["schedule"])
    leg = ql.OvernightLeg(spec["notionals"], schedule, index, **leg_kwargs(spec, spec["telescopicValueDates"]))
    if spec["telescopicValueDates"]:
        leg = ql.OvernightLeg(spec["notionals"], schedule, index, **leg_kwargs(spec, False))
        if spec["averagingMethod"] == ql.RateAveraging.Simple:
            if any(ql.as_capped_floored_overnight_indexed_coupon(cf) is not None for cf in leg):
                raise ValueError("the telescopic rule for a capped averaged leg isn't implemented here")
            approx = ql.ArithmeticAveragedOvernightIndexedCouponPricer(byApprox=True)
            for cf in leg:
                c = ql.as_overnight_indexed_coupon(cf)
                if c is not None:
                    c.setPricer(approx)
    return leg


def amounts(fs, scale):
    """Each f()'s value as an amount of the given scale, or {"throws": true}."""
    out = []
    for f in fs:
        value = v.attempt(f)
        out.append(value if isinstance(value, dict) or value is None else {"value": value, "scale": float(scale)})
    return out


def business_days(cal, start, end):
    """The business days of a calendar from start to end, inclusive."""
    out, d = [], start
    while d <= end:
        if cal.isBusinessDay(d):
            out.append(d)
        d += 1
    return out


def fixings_for(index, first, last, rng, skip=None):
    """A fixing for each valid fixing date of the index from first to last, random around 3.5%, skipping one date."""
    dates = [d for d in business_days(index.fixingCalendar(), first, last) if d != skip]
    return [(d, round(0.02 + 0.03 * rng.random(), 6)) for d in dates]


def coupon_outputs(c, spec, accrual_dates):
    nominal = spec["nominal"]
    return {
        "built": True,
        "rate": v.attempt(c.rate),
        "amount": amounts([c.amount], nominal)[0],
        "accruedAmount": amounts([(lambda d=d: c.accruedAmount(d)) for d in accrual_dates], nominal),
        "fixingDate": v.enc(c.fixingDate()),
        "fixingDates": v.enc(list(c.fixingDates())),
        "valueDates": v.enc(list(c.valueDates())),
        "interestDates": v.enc(list(c.interestDates())),
        "dt": v.enc(list(c.dt())),
        "indexFixings": v.attempt(lambda: list(c.indexFixings())),
        "effectiveSpread": v.attempt(c.effectiveSpread),
        "effectiveIndexFixing": v.attempt(c.effectiveIndexFixing),
        "rateComputationStartDate": v.enc(c.rateComputationStartDate()),
        "rateComputationEndDate": v.enc(c.rateComputationEndDate()),
        "averagingMethod": int(c.averagingMethod()),
        "lockoutDays": c.lockoutDays(),
        "applyObservationShift": c.applyObservationShift(),
        "compoundSpreadDaily": c.compoundSpreadDaily(),
        "canApplyTelescopicFormula": c.canApplyTelescopicFormula(),
        "fixingDays": c.fixingDays(),
    }


# The evaluation dates: a Wednesday, a Saturday, a Monday, Good Friday (a SOFR, TARGET and UK holiday) and a
# Thursday before a US holiday.
TODAYS = [ql.Date(15, 1, 2025), ql.Date(18, 1, 2025), ql.Date(3, 3, 2025), ql.Date(18, 4, 2025),
          ql.Date(26, 6, 2025)]
CURVES = [(0.043, A360, ql.Continuous, ql.Annual), (0.031, A365, ql.Simple, ql.Annual),
          (0.0525, A360, ql.Compounded, ql.Semiannual), (-0.004, A365, ql.Continuous, ql.Annual)]


def grid_coupon(k, rng):
    """The k-th sampled coupon: its spec, its evaluation date and the dates its accrued amounts are read at."""
    today = TODAYS[k % len(TODAYS)]
    ix = k % len(INDEXES)
    fwd, cdc, comp, freq = CURVES[(k // 5) % len(CURVES)]
    index = index_spec(ix, today, fwd, cdc, comp, freq)
    cal = irx.calendar(index["fixingCalendar"])
    timing = ["past", "spanning", "future"][(k // 3) % 3]
    weeks = 1 + rng.randrange(4)
    if timing == "past":
        start = today - (7 * weeks + 10 + rng.randrange(20))
    elif timing == "spanning":
        start = today - (2 + rng.randrange(7 * weeks - 2))
    else:
        start = today + (1 + rng.randrange(40))
    end = start + 7 * weeks + rng.randrange(3)
    simple = rng.random() < 0.25
    averaging = ql.RateAveraging.Simple if simple else ql.RateAveraging.Compound
    if simple:
        lookback = rng.choice([None, INDEXES[ix][1]])
        lockout, shift = 0, False
    else:
        lookback = rng.choice([None, 0, 2])
        lockout = rng.choice([0, 0, 2])
        shift = rng.random() < 0.4
    days = INDEXES[ix][1]
    fixing_days = days if lookback is None else lookback
    can_telescope = fixing_days == days or (shift and days == 0)
    telescopic = can_telescope and rng.random() < 0.35
    compound_spread = rng.random() < 0.4
    spread = rng.choice([0.0, 0.0015, -0.00071234, 0.0123456])
    gearing = 1.0 if compound_spread else rng.choice([1.0, 1.0, 1.5, 0.75])
    rounding = rng.choice([None, None, 4, 8])
    dc = rng.choice([None, None, None, A365 if INDEXES[ix][3] == A360 else A360])
    lag = rng.choice([0, 0, 2])
    payment = cal.advance(end, lag, ql.Days) if lag else end
    ex_coupon = cal.advance(end, -3, ql.Days) if rng.random() < 0.15 else None
    rc_start = rc_end = None
    if rng.random() < 0.1:
        rc_end = start
        rc_start = start - 7 * weeks
    spec = coupon_spec(index, payment, start, end, 1e6, gearing, spread, None, None, dc, telescopic, averaging,
                       lookback, lockout, shift, compound_spread, rc_start, rc_end, ex_coupon, rounding)
    accrual_dates = [start + 1, start + (end - start) // 2, end, payment + 1]
    if ex_coupon is not None:
        accrual_dates[1] = ex_coupon
    return spec, today, accrual_dates


def invalid_coupons(today):
    """Combinations QuantLib refuses, each recording its throw."""
    index = index_spec(0, today, 0.04)
    start, end = today + 10, today + 40
    return [
        ("simple averaging with a lookback", coupon_spec(index, end, start, end, averaging=ql.RateAveraging.Simple,
                                                         lookback=2)),
        ("simple averaging with a lockout", coupon_spec(index, end, start, end, averaging=ql.RateAveraging.Simple,
                                                        lockout=2)),
        ("simple averaging with the shift", coupon_spec(index, end, start, end, averaging=ql.RateAveraging.Simple,
                                                        shift=True)),
        ("telescopic dates with a lookback and no shift", coupon_spec(index, end, start, end, telescopic=True,
                                                                      lookback=2)),
        ("a lockout as long as the period", coupon_spec(index, end, start, end, lockout=21)),
        ("a payment before the accrual end", coupon_spec(index, end - 1, start, end)),
        ("dates in the wrong order", coupon_spec(index, end, end, start)),
        ("a rate computation over one weekend", coupon_spec(index, end, start, end, rc_start=ql.Date(18, 1, 2025),
                                                            rc_end=ql.Date(19, 1, 2025))),
        ("a reversed rate computation", coupon_spec(index, end, start, end, rc_start=ql.Date(20, 2, 2025),
                                                    rc_end=ql.Date(10, 2, 2025))),
        ("a zero gearing", coupon_spec(index, end, start, end, gearing=0.0)),
    ]


def named_coupons():
    """D10's two Saturday starts and the three D23 paths, each (name, spec, today, accrual dates, fixings window)."""
    out = []
    # D10, SD26: a SOFR coupon starting on Saturday 14 June 2025 with the shift, the lookback null or 0.
    today = ql.Date(2, 6, 2025)
    index = index_spec(0, today, 0.042)
    for lookback in (None, 0):
        spec = coupon_spec(index, ql.Date(14, 7, 2025), ql.Date(14, 6, 2025), ql.Date(14, 7, 2025), shift=True,
                           lookback=lookback)
        out.append((f"D10 Saturday start, shift, lookback {lookback}", spec, today,
                    [ql.Date(16, 6, 2025), ql.Date(1, 7, 2025), ql.Date(14, 7, 2025), ql.Date(15, 7, 2025)], None))
    # D23 (SP26): the spread compounded daily, on SP26's coupon.
    today = ql.Date(15, 1, 2024)
    index = index_spec(0, today, 0.0312345, A360)
    start, end = ql.Date(17, 4, 2024), ql.Date(17, 7, 2024)
    dates = [ql.Date(18, 4, 2024), ql.Date(1, 6, 2024), end, ql.Date(18, 7, 2024)]
    out.append(("D23 spread added", coupon_spec(index, end, start, end, 1.0, spread=0.0123456), today, dates, None))
    out.append(("D23 spread compounded daily, telescopic formula applicable",
                coupon_spec(index, end, start, end, 1.0, spread=0.0123456, compound_spread=True), today, dates, None))
    out.append(("D23 spread compounded daily, lookback 2 without shift",
                coupon_spec(index, end, start, end, 1.0, spread=0.0123456, compound_spread=True, lookback=2), today,
                dates, None))
    past = ql.Date(1, 8, 2024)
    out.append(("D23 spread compounded daily, past coupon",
                coupon_spec(index_spec(0, past, 0.0312345, A360), end, start, end, 1.0, spread=0.0123456,
                            compound_spread=True), past, dates, (start - 10, past - 1)))
    return out


def coupon_case(name, spec, today, accrual_dates, window, rng, store_today=False, skip=None):
    index = make_index(spec["index"])
    start = ql.Date(spec["startDate"])
    first = (ql.Date(spec["rateComputationStartDate"]) if spec["rateComputationStartDate"] is not None else start) - 20
    lo, hi = window if window is not None else (first, today if store_today else today - 1)
    pairs = fixings_for(index, lo, hi, rng, skip) if hi >= lo else []
    kw = {"fixings": [(index, pairs)]} if pairs else {}
    with v.case_settings(today, **kw) as st:
        try:
            c = make_coupon(spec, index)
        except RuntimeError:
            expected = {"built": dict(v.THROWS)}
        else:
            expected = coupon_outputs(c, spec, accrual_dates)
        return {"name": name, "inputs": {"settings": st, "coupon": spec, "accrualDates": v.enc(accrual_dates)},
                "expected": expected}


def leg_outputs(leg, nominal_scale):
    types, payment, start, end, ref_start, ref_end, nominals, rates, amts = [], [], [], [], [], [], [], [], []
    rc_start, rc_end, gearings, spreads, caps, floors = [], [], [], [], [], []
    for cf in leg:
        cfon = ql.as_capped_floored_overnight_indexed_coupon(cf)
        on = ql.as_overnight_indexed_coupon(cf)
        fixed = ql.as_fixed_rate_coupon(cf)
        c = ql.as_coupon(cf)
        types.append("CappedFlooredOvernightIndexedCoupon" if cfon is not None else
                     "OvernightIndexedCoupon" if on is not None else "FixedRateCoupon" if fixed is not None else "?")
        payment.append(v.enc(cf.date()))
        start.append(v.enc(c.accrualStartDate()))
        end.append(v.enc(c.accrualEndDate()))
        ref_start.append(v.enc(c.referencePeriodStart()))
        ref_end.append(v.enc(c.referencePeriodEnd()))
        nominals.append(c.nominal())
        rates.append(v.attempt(c.rate))
        amts.extend(amounts([cf.amount], nominal_scale))
        u = cfon.underlying() if cfon is not None else on
        rc_start.append(v.enc(u.rateComputationStartDate()) if u is not None else None)
        rc_end.append(v.enc(u.rateComputationEndDate()) if u is not None else None)
        f = ql.as_floating_rate_coupon(cf)
        gearings.append(f.gearing() if f is not None else None)
        spreads.append(f.spread() if f is not None else None)
        caps.append(v.enc(cfon.cap()) if cfon is not None else None)
        floors.append(v.enc(cfon.floor()) if cfon is not None else None)
    return {"size": len(leg), "types": types, "paymentDates": payment, "accrualStartDates": start,
            "accrualEndDates": end, "referencePeriodStarts": ref_start, "referencePeriodEnds": ref_end,
            "nominals": nominals, "rates": rates, "amounts": amts, "rateComputationStartDates": rc_start,
            "rateComputationEndDates": rc_end, "gearings": gearings, "spreads": spreads, "caps": caps,
            "floors": floors}


def legs(today):
    us_gov = {"type": "UnitedStates", "market": int(ql.UnitedStates.GovernmentBond)}
    sofr = index_spec(0, today, 0.0405)
    estr = index_spec(1, today, 0.0291, A360, ql.Simple)
    ond = index_spec(3, today, 0.035)
    q = rule_schedule(ql.Date(20, 2, 2025), ql.Date(20, 2, 2026), ql.Period(3, ql.Months), us_gov,
                      ql.ModifiedFollowing)
    m = rule_schedule(ql.Date(31, 1, 2025), ql.Date(31, 7, 2025), ql.Period(1, ql.Months), irx.TARGET,
                      ql.ModifiedFollowing, ql.DateGeneration.Backward, True)
    stub = rule_schedule(ql.Date(10, 1, 2025), ql.Date(25, 6, 2025), ql.Period(2, ql.Months), us_gov,
                         ql.Following, ql.DateGeneration.Backward)
    back = rule_schedule(ql.Date(16, 1, 2025), ql.Date(1, 7, 2025), ql.Period(2, ql.Months), irx.TARGET,
                         ql.Following, ql.DateGeneration.Forward)
    weekend = dates_schedule([ql.Date(27, 3, 2026), ql.Date(28, 3, 2026), ql.Date(30, 3, 2026)])
    one = dates_schedule([ql.Date(20, 2, 2025)])
    flagged = dates_schedule([ql.Date(6, 1, 2025), ql.Date(20, 2, 2025), ql.Date(20, 5, 2025)], irx.TARGET,
                             None, [False, True])
    tenored = dates_schedule([ql.Date(6, 1, 2025), ql.Date(20, 2, 2025), ql.Date(20, 5, 2025)], irx.TARGET,
                             ql.Period(3, ql.Months), [False, True])
    # in advance on a schedule without a tenor, or with a tenor of 0D: the first period's rate-computation start is
    # the start less the period's length (overnightindexedcoupon.cpp:641-644)
    untenored = dates_schedule([ql.Date(6, 1, 2025), ql.Date(20, 2, 2025), ql.Date(20, 5, 2025)], irx.TARGET)
    zero_tenor = dates_schedule([ql.Date(6, 1, 2025), ql.Date(20, 2, 2025), ql.Date(20, 5, 2025)], irx.TARGET,
                                ql.Period(0, ql.Days))
    a360 = A360
    return [
        ("quarterly SOFR", leg_spec(q, sofr, [1e6])),
        ("quarterly SOFR, day counter and lag", leg_spec(q, sofr, [1e6], a360, payment_lag=2)),
        ("quarterly SOFR, amortizing", leg_spec(q, sofr, [1e6, 8e5, 6e5], a360)),
        ("quarterly SOFR, gearings and spreads", leg_spec(q, sofr, [1e6], a360, gearings=[1.0, 1.25, 2.0, 0.5],
                                                          spreads=[0.0001, 0.0001, 0.0002])),
        ("quarterly SOFR, lookback and shift", leg_spec(q, sofr, [1e6], a360, lookback=2, shift=True)),
        ("quarterly SOFR, lockout", leg_spec(q, sofr, [1e6], a360, lockout=3)),
        ("quarterly SOFR, telescopic", leg_spec(q, sofr, [1e6], a360, telescopic=True)),
        ("quarterly SOFR, averaged and telescopic", leg_spec(q, sofr, [1e6], a360, telescopic=True,
                                                             averaging=ql.RateAveraging.Simple)),
        ("quarterly SOFR, spread compounded daily, rounding", leg_spec(q, sofr, [1e6], a360, spreads=[0.00254321],
                                                                      compound_spread=True, rounding=6)),
        ("quarterly SOFR, caps and floors", leg_spec(q, sofr, [1e6], a360, caps=[0.045, None, 0.04],
                                                     floors=[0.02, 0.025], daily=True, naked=True)),
        ("quarterly SOFR, zero gearing", leg_spec(q, sofr, [1e6], a360, gearings=[1.0, 0.0],
                                                  spreads=[0.001, 0.03], caps=[None, 0.025], floors=[None, 0.01])),
        ("quarterly SOFR, in advance", leg_spec(q, sofr, [1e6], a360, in_arrears=False)),
        ("quarterly SOFR, payment dates", leg_spec(q, sofr, [1e6], a360, payment_dates=[
            ql.Date(25, 5, 2025), ql.Date(25, 8, 2025), ql.Date(25, 11, 2025), ql.Date(25, 2, 2026)])),
        ("quarterly SOFR, payment dates of the wrong count", leg_spec(q, sofr, [1e6], a360,
                                                                      payment_dates=[ql.Date(25, 5, 2025)])),
        ("monthly ESTR, payment calendar and convention", leg_spec(m, estr, [5e6], payment_lag=1,
                                                                   payment_calendar=irx.US_SOFR,
                                                                   payment_convention=ql.Preceding)),
        ("bimonthly SOFR, short front stub, in advance", leg_spec(stub, sofr, [1e6], a360, in_arrears=False)),
        ("bimonthly Ond, short back stub, averaged", leg_spec(back, ond, [2e6], averaging=ql.RateAveraging.Simple)),
        ("weekend stub", leg_spec(weekend, sofr, [1e7])),
        ("one-date schedule", leg_spec(one, sofr, [1e6])),
        ("dates schedule with regularity flags and no tenor", leg_spec(flagged, estr, [1e6])),
        ("dates schedule with regularity flags and a tenor", leg_spec(tenored, estr, [1e6])),
        ("dates schedule without a tenor, in advance", leg_spec(untenored, estr, [1e6], in_arrears=False)),
        ("dates schedule with a 0D tenor, in advance", leg_spec(zero_tenor, estr, [1e6], in_arrears=False)),
        ("no nominal", leg_spec(q, sofr, [])),
    ]


def leg_case(name, spec, today, rng):
    index = make_index(spec["index"])
    pairs = fixings_for(index, today - 120, today - 1, rng)
    with v.case_settings(today, fixings=[(index, pairs)]) as st:
        try:
            leg = make_leg(spec, index)
        except RuntimeError:
            expected = {"size": dict(v.THROWS)}
        else:
            expected = leg_outputs(leg, max(spec["notionals"]))
        return {"name": name, "inputs": {"settings": st, "leg": spec}, "expected": expected}


def cases():
    rng = random.Random(v.SEED)
    out = []
    for k in range(136):
        spec, today, accrual_dates = grid_coupon(k, rng)
        store_today = k % 4 == 1
        skip = None
        if k in (17, 101):
            # a missing past fixing: the coupon's second fixing date before the evaluation date
            c = ql.OvernightIndexedCoupon(ql.Date(spec["paymentDate"]), spec["nominal"], ql.Date(spec["startDate"]),
                                          ql.Date(spec["endDate"]), make_index(spec["index"]),
                                          **coupon_kwargs(spec, False))
            past = [d for d in c.fixingDates() if d < today]
            skip = past[1] if len(past) > 1 else None
        out.append(coupon_case(f"coupon {k}", spec, today, accrual_dates, None, rng, store_today, skip))
    for name, spec in invalid_coupons(ql.Date(15, 1, 2025)):
        out.append(coupon_case(name, spec, ql.Date(15, 1, 2025), [ql.Date(15, 1, 2025)] * 4, None, rng))
    for name, spec, today, dates, window in named_coupons():
        out.append(coupon_case(name, spec, today, dates, window, rng))
    for name, spec in legs(ql.Date(3, 3, 2025)):
        out.append(leg_case(name, spec, ql.Date(3, 3, 2025), rng))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/overnightindexedcoupon.hpp", cases)
