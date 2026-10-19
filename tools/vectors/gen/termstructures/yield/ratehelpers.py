# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's rate helpers (ql/termstructures/yield/ratehelpers.hpp), through the BootstrapHelper
interface they inherit (ql/termstructures/bootstraphelper.hpp): 206 helpers, each built on three evaluation dates
(Monday 15 January 2024; Thursday 29 February 2024, the end of a month; Saturday 30 March 2024, in TARGET's Easter
holidays), 618 cases: first the 103 deposit and swap helpers on each date, then the 103 FRA, futures and FX swap
helpers on each date, appended after them.

- 40 deposit helpers: 16 from a tenor and conventions (tenors from 0 days to 2 years, 0 to 5 fixing days, TARGET, the
  US SOFR, UK exchange and null calendars, five business-day conventions, end of month either way, three day
  counters; and an intraday tenor, which QuantLib refuses), 14 on an index (the Euribor presets, Euribor and
  Euribor365 of other tenors, Tibor, and indexes of QuantLib's empty currency on four calendars), and 10 from a fixing
  date on an index, the dates absolute (in the past, on the first evaluation date, around month ends and Easter; a
  Saturday and Good Friday, which QuantLib refuses as fixing dates).
- 63 swap helpers on Euribor-style indexes, the base 5 years on Euribor 6M against an annual, unadjusted 30/360 fixed
  leg on TARGET: tenors from 1 to 30 years; fixed frequencies (Once among them) and conventions; fixed day counters;
  spreads; forward starts, a negative one among them; a discounting curve; settlement days from 0 to 5; the three
  pillar choices, a custom pillar too late and none at all refused; end of month; indexed and at-par coupons, by the
  helper's choice and by the context's switch; floating-leg conventions; Euribor 3M and 1Y, and an index of the US
  dollar on the US SOFR calendar; S5R4's three: QuantLib's empty calendar with unadjusted conventions, which builds,
  and with an adjusting one, which QuantLib refuses ("no calendar implementation provided"), and no fixed day counter,
  which takes the currency's; and 12 dated helpers (built with forDates), month ends with end of month either way, a
  short front stub, a seasoned swap, and the Once frequency and reversed dates, which QuantLib refuses.
- 42 FRA helpers in QuantLib's five wrapped forms: from months to start and end with conventions (the months out of
  order and equal, which QuantLib refuses after building the dates; the calendars, conventions, end of month, day
  counters, pillar choices, a custom pillar too late and none at all refused, and the indexed coupon); from months to start on an index; from a period to start on an
  index (weeks and days added to a tenor in months, which QuantLib refuses); from IMM offsets on an index (the spot
  date itself for an offset of 0, offsets out of order, which QuantLib takes as they are, DS14); and between dates
  (built with forDates), reversed dates among them, which QuantLib takes as they are.
- 41 futures helpers in QuantLib's three forms, IMM, ASX and custom: a start date that is an IMM or ASX date on either
  cycle, and one that isn't, which QuantLib refuses; lengths, calendars, conventions, end of month, day counters and
  convexity adjustments; from dates, with no end date (three IMM or ASX dates on, and for a custom helper a null
  maturity, DS13), an end date not after the start (refused for IMM and ASX, taken for custom); and on an index.
- 20 FX swap helpers: from a tenor (spot and tomorrow-next swaps, tenors to 2 years, fixing days, the TARGET, US and UK
  calendars, a trading calendar, conventions and end of month, either currency the collateral's, and no collateral
  curve, with which QuantLib builds the helper but can't bind it); and between dates (built with forDates).

Each case's outputs are whether the helper builds, QuantLib's refusals recorded as its one throw, then its five dates
(earliestDate, maturityDate, latestRelevantDate, pillarDate, latestDate); for a swap helper its swap's start and
maturity dates and fixed rate, and its spread; for a futures helper its convexity adjustment; for an FX swap helper its
spot, tenor ([length, units]), base-currency flag and the names of its calendar, trading calendar and adjustment
calendar (null for QuantLib's empty calendar), and, built from a tenor, its fixing days, convention and end-of-month
flag, which QuantLib leaves uninitialized for one built from dates.

The functions here build the helper specs and the wheel's helpers that the bootstrap generators share
(termstructures/yield/piecewiseyieldcurve, termstructures/iterativebootstrap, termstructures/yield/oisratehelper and
the harness counterpart cxx/termstructures/yield/ratehelpers, which import this file). A helper spec is one of
- {"kind": "DepositRateHelper", "form": "tenor", "rate", "tenor", "fixingDays", "calendar", "convention",
  "endOfMonth", "dayCounter"}, {"kind": "DepositRateHelper", "form": "index", "rate", "index"} or {"kind":
  "DepositRateHelper", "form": "fixingDate", "rate", "fixingDate", "index"};
- {"kind": "SwapRateHelper", "form": "tenor", "rate", "tenor", "calendar", "fixedFrequency", "fixedConvention",
  "fixedDayCount", "index", "spread", "fwdStart", "discountingCurve", "settlementDays", "pillar", "customPillarDate",
  "endOfMonth", "useIndexedCoupons", "floatConvention"}, or the same with "form": "dates", "startDate" and "endDate"
  for "tenor", and without "fwdStart" and "settlementDays";
- {"kind": "FraRateHelper", "form": "months", "rate", "monthsToStart", "monthsToEnd", "fixingDays", "calendar",
  "convention", "endOfMonth", "dayCounter", "pillar", "customPillarDate", "useIndexedCoupon"}; the same with "form":
  "periodLength", "periodToStart" and "lengthInMonths" for the months (the harness's only, which the wheel doesn't
  wrap); and with "form" "monthsIndex" ("monthsToStart", "index"), "periodIndex" ("periodToStart", "index"), "imm"
  ("immOffsetStart", "immOffsetEnd", "index") or "dates" ("startDate", "endDate", "index") for the conventions;
- {"kind": "FuturesRateHelper", "form": "length", "rate", "iborStartDate", "lengthInMonths", "calendar",
  "convention", "endOfMonth", "dayCounter", "convexityAdjustment", "type"}, the same with "form" "dates" and
  "iborEndDate" (null for Date()) for the length and its conventions, or "index" and "index" for them; the rate is the
  futures price; the harness's may add "quotes", [price as a quote, adjustment as a quote], for QuantLib's mixed forms;
- {"kind": "FxSwapRateHelper", "form": "tenor", "rate", "spotFx", "tenor", "fixingDays", "calendar", "convention",
  "endOfMonth", "isFxBaseCurrencyCollateralCurrency", "collateralCurve", "tradingCalendar"} or {"kind":
  "FxSwapRateHelper", "form": "dates", "rate", "spotFx", "startDate", "endDate", "isFxBaseCurrencyCollateralCurrency",
  "collateralCurve"}; the rate is the forward points, the collateral curve null (QuantLib's empty handle) or a flat
  forward spec, the trading calendar null for none;
- {"kind": "OISRateHelper", "form": "tenor", "rate", "settlementDays", "tenor", "index", "discountingCurve",
  "telescopicValueDates", "paymentLag", "paymentConvention", "paymentFrequency", "paymentCalendar", "forwardStart",
  "overnightSpread", "pillar", "customPillarDate", "averagingMethod", "endOfMonth", "fixedPaymentFrequency",
  "fixedCalendar", "lookbackDays", "lockoutDays", "applyObservationShift", "pricer", "rule", "overnightCalendar",
  "convention", "fixedDayCount"}, or the same with "form": "dates", "startDate" and "endDate" for "settlementDays"
  and "tenor", and without "forwardStart"; the index an overnight preset or spec, the pricer null or a pricer spec of
  cashflows/overnightindexedcoupon.py, null calendars, day counter, lookback, end-of-month flag and fixed frequency
  QuantLib's empty values and nulls;
a period is [length, units]; a calendar and a day counter are specs of indexes/interestrateindex.py, null for
QuantLib's empty one; an index is a spec of instruments/fixedvsfloatingswap.py; the discounting curve is null or a flat
forward spec; null settlement days are Null<Natural>(), a null custom pillar date Date(), and null coupon and floating
convention choices std::nullopt; enums are QuantLib's integers. The wheel's helpers take the quote as a float, but for
the fixing-date and dated forms, which take a QuoteHandle; a zero spread is an empty handle, which QuantLib reads as 0.
QuantLib-Python can't build an empty calendar or day counter: the empty ones are ql.Schedule().calendar() and
ql.InterestRate().dayCounter(). SwapRateHelper.forDates, FraRateHelper.forDates, FxSwapRateHelper.forDates and
OISRateHelper.forDates are statics the wheel adds, calling QuantLib's dated constructors.

An OIS helper with telescopic value dates takes QuantLib's values by S3R11's rule, as the overnight coupons' vectors do:
QuantLib builds it with the flag first, so a refusal is its own; its values are then those of the same helper built
without the flag, its coupons given ArithmeticAveragedOvernightIndexedCouponPricer(byApprox=True) where they average,
the pricer the flag selects (value_spec). Read back by
tests/NanoQL.Tests/Vectors/TermStructures/Yield/RateHelpersVectorTests.cs, with
tests/NanoQL.Tests/Vectors/RateHelperInputs.cs, which documents the specs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_fixedvsfloatingswap",
                                               os.path.join(v.GEN_ROOT, "instruments", "fixedvsfloatingswap.py"))
fvf = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(fvf)
irx = fvf.irx
ocp = fvf._load("wheel_overnightindexedcoupon", "cashflows/overnightindexedcoupon.py")

D = fvf.d
TARGET, NULL_CALENDAR, US_SOFR, UK_EXCHANGE = irx.TARGET, irx.NULL_CALENDAR, irx.US_SOFR, irx.UK_EXCHANGE
A360, A365, THIRTY360_BB = irx.A360, irx.A365, irx.THIRTY360_BB
ACT_ISDA = {"type": "ActualActual", "convention": int(ql.ActualActual.ISDA)}

# Monday 15 January 2024; Thursday 29 February 2024; Saturday 30 March 2024, between Good Friday and Easter Monday.
EVALUATION_DATES = [D(15, 1, 2024), D(29, 2, 2024), D(30, 3, 2024)]

E1W, E1M, E3M = fvf.preset("Euribor1W"), fvf.preset("Euribor1M"), fvf.preset("Euribor3M")
E6M, E1Y = fvf.preset("Euribor6M"), fvf.preset("Euribor1Y")


def euribor(n, units):
    """The Euribor preset of a tenor: QuantLib's Euribor(tenor)."""
    return fvf.preset("Euribor", None, ql.Period(n, units))


def period(p):
    return [p[0], int(p[1])]


def deposit_tenor(rate, tenor, fixing_days, cal, convention, eom, dc):
    return {"kind": "DepositRateHelper", "form": "tenor", "rate": rate, "tenor": period(tenor),
            "fixingDays": fixing_days, "calendar": cal, "convention": int(convention), "endOfMonth": eom,
            "dayCounter": dc}


def deposit_index(rate, index):
    return {"kind": "DepositRateHelper", "form": "index", "rate": rate, "index": index}


def deposit_date(rate, fixing_date, index):
    return {"kind": "DepositRateHelper", "form": "fixingDate", "rate": rate, "fixingDate": v.enc(fixing_date),
            "index": index}


def _swap(rate, cal, freq, convention, dc, index, spread, discount, pillar, custom, eom, indexed, float_convention):
    return {"calendar": cal, "fixedFrequency": int(freq), "fixedConvention": int(convention), "fixedDayCount": dc,
            "index": index, "spread": spread, "discountingCurve": discount, "pillar": int(pillar),
            "customPillarDate": v.enc(custom), "endOfMonth": eom, "useIndexedCoupons": indexed,
            "floatConvention": None if float_convention is None else int(float_convention), "rate": rate}


def swap_tenor(rate, tenor, cal=TARGET, freq=ql.Annual, convention=ql.Unadjusted, dc=THIRTY360_BB, index=E6M,
               spread=0.0, fwd_start=(0, ql.Days), discount=None, settlement_days=None,
               pillar=ql.Pillar.LastRelevantDate, custom=None, eom=False, indexed=None, float_convention=None):
    spec = {"kind": "SwapRateHelper", "form": "tenor", "tenor": period(tenor)}
    spec.update(_swap(rate, cal, freq, convention, dc, index, spread, discount, pillar, custom, eom, indexed,
                      float_convention))
    spec.update({"fwdStart": period(fwd_start), "settlementDays": settlement_days})
    return spec


def swap_dates(rate, start, end, cal=TARGET, freq=ql.Annual, convention=ql.Unadjusted, dc=THIRTY360_BB, index=E6M,
               spread=0.0, discount=None, pillar=ql.Pillar.LastRelevantDate, custom=None, eom=False, indexed=None,
               float_convention=None):
    spec = {"kind": "SwapRateHelper", "form": "dates", "startDate": v.enc(start), "endDate": v.enc(end)}
    spec.update(_swap(rate, cal, freq, convention, dc, index, spread, discount, pillar, custom, eom, indexed,
                      float_convention))
    return spec


def _quote_tail(pillar, custom, indexed):
    return {"pillar": int(pillar), "customPillarDate": v.enc(custom), "useIndexedCoupon": indexed}


def fra_months(rate, start, end, fixing_days=2, cal=TARGET, convention=ql.ModifiedFollowing, eom=False, dc=A360,
               pillar=ql.Pillar.LastRelevantDate, custom=None, indexed=True):
    spec = {"kind": "FraRateHelper", "form": "months", "rate": rate, "monthsToStart": start, "monthsToEnd": end,
            "fixingDays": fixing_days, "calendar": cal, "convention": int(convention), "endOfMonth": eom,
            "dayCounter": dc}
    spec.update(_quote_tail(pillar, custom, indexed))
    return spec


def fra_period_length(rate, period_to_start, length, fixing_days=2, cal=TARGET, convention=ql.ModifiedFollowing,
                      eom=False, dc=A360, pillar=ql.Pillar.LastRelevantDate, custom=None, indexed=True):
    spec = {"kind": "FraRateHelper", "form": "periodLength", "rate": rate, "periodToStart": period(period_to_start),
            "lengthInMonths": length, "fixingDays": fixing_days, "calendar": cal, "convention": int(convention),
            "endOfMonth": eom, "dayCounter": dc}
    spec.update(_quote_tail(pillar, custom, indexed))
    return spec


def fra_months_index(rate, start, index, pillar=ql.Pillar.LastRelevantDate, custom=None, indexed=True):
    spec = {"kind": "FraRateHelper", "form": "monthsIndex", "rate": rate, "monthsToStart": start, "index": index}
    spec.update(_quote_tail(pillar, custom, indexed))
    return spec


def fra_period_index(rate, period_to_start, index, pillar=ql.Pillar.LastRelevantDate, custom=None, indexed=True):
    spec = {"kind": "FraRateHelper", "form": "periodIndex", "rate": rate, "periodToStart": period(period_to_start),
            "index": index}
    spec.update(_quote_tail(pillar, custom, indexed))
    return spec


def fra_imm(rate, start, end, index, pillar=ql.Pillar.LastRelevantDate, custom=None, indexed=True):
    spec = {"kind": "FraRateHelper", "form": "imm", "rate": rate, "immOffsetStart": start, "immOffsetEnd": end,
            "index": index}
    spec.update(_quote_tail(pillar, custom, indexed))
    return spec


def fra_dates(rate, start, end, index, pillar=ql.Pillar.LastRelevantDate, custom=None, indexed=True):
    spec = {"kind": "FraRateHelper", "form": "dates", "rate": rate, "startDate": v.enc(start),
            "endDate": v.enc(end), "index": index}
    spec.update(_quote_tail(pillar, custom, indexed))
    return spec


def futures_length(price, start, length, cal=TARGET, convention=ql.ModifiedFollowing, eom=False, dc=A360,
                   convexity=0.0, futures_type=ql.Futures.IMM):
    return {"kind": "FuturesRateHelper", "form": "length", "rate": price, "iborStartDate": v.enc(start),
            "lengthInMonths": length, "calendar": cal, "convention": int(convention), "endOfMonth": eom,
            "dayCounter": dc, "convexityAdjustment": convexity, "type": int(futures_type)}


def futures_dates(price, start, end, dc=A360, convexity=0.0, futures_type=ql.Futures.IMM):
    return {"kind": "FuturesRateHelper", "form": "dates", "rate": price, "iborStartDate": v.enc(start),
            "iborEndDate": v.enc(end), "dayCounter": dc, "convexityAdjustment": convexity,
            "type": int(futures_type)}


def futures_index(price, start, index, convexity=0.0, futures_type=ql.Futures.IMM):
    return {"kind": "FuturesRateHelper", "form": "index", "rate": price, "iborStartDate": v.enc(start),
            "index": index, "convexityAdjustment": convexity, "type": int(futures_type)}


# The collateral curve of the FX swap helpers: a flat forward from the first evaluation date.
COLLATERAL = fvf.curve(0.052, A360, ql.Simple, reference=ql.Date(15, ql.January, 2024))


def fx_tenor(points, spot, tenor, fixing_days=2, cal=TARGET, convention=ql.Following, eom=False, is_base=True,
             collateral=COLLATERAL, trading=None):
    return {"kind": "FxSwapRateHelper", "form": "tenor", "rate": points, "spotFx": spot, "tenor": period(tenor),
            "fixingDays": fixing_days, "calendar": cal, "convention": int(convention), "endOfMonth": eom,
            "isFxBaseCurrencyCollateralCurrency": is_base, "collateralCurve": collateral, "tradingCalendar": trading}


def fx_dates(points, spot, start, end, is_base=True, collateral=COLLATERAL):
    return {"kind": "FxSwapRateHelper", "form": "dates", "rate": points, "spotFx": spot, "startDate": v.enc(start),
            "endDate": v.enc(end), "isFxBaseCurrencyCollateralCurrency": is_base, "collateralCurve": collateral}


ESTR = fvf.preset("Estr")


def _ois(rate, index, discount, telescopic, payment_lag, payment_convention, payment_frequency, payment_calendar,
         spread, pillar, custom, averaging, eom, fixed_payment_frequency, fixed_calendar, lookback, lockout, shift,
         pricer, rule, overnight_calendar, convention, fixed_dc):
    return {"rate": rate, "index": index, "discountingCurve": discount, "telescopicValueDates": telescopic,
            "paymentLag": payment_lag, "paymentConvention": int(payment_convention),
            "paymentFrequency": int(payment_frequency), "paymentCalendar": payment_calendar,
            "overnightSpread": spread, "pillar": int(pillar), "customPillarDate": v.enc(custom),
            "averagingMethod": int(averaging), "endOfMonth": eom,
            "fixedPaymentFrequency": None if fixed_payment_frequency is None else int(fixed_payment_frequency),
            "fixedCalendar": fixed_calendar, "lookbackDays": lookback, "lockoutDays": lockout,
            "applyObservationShift": shift, "pricer": pricer, "rule": int(rule),
            "overnightCalendar": overnight_calendar, "convention": int(convention), "fixedDayCount": fixed_dc}


def ois_tenor(rate, tenor, settlement_days=2, index=ESTR, discount=None, telescopic=False, payment_lag=0,
              payment_convention=ql.Following, payment_frequency=ql.Annual, payment_calendar=None,
              fwd_start=(0, ql.Days), spread=0.0, pillar=ql.Pillar.LastRelevantDate, custom=None,
              averaging=ql.RateAveraging.Compound, eom=None, fixed_payment_frequency=None, fixed_calendar=None,
              lookback=None, lockout=0, shift=False, pricer=None, rule=ql.DateGeneration.Backward,
              overnight_calendar=None, convention=ql.ModifiedFollowing, fixed_dc=None):
    spec = {"kind": "OISRateHelper", "form": "tenor", "settlementDays": settlement_days, "tenor": period(tenor)}
    spec.update(_ois(rate, index, discount, telescopic, payment_lag, payment_convention, payment_frequency,
                     payment_calendar, spread, pillar, custom, averaging, eom, fixed_payment_frequency,
                     fixed_calendar, lookback, lockout, shift, pricer, rule, overnight_calendar, convention, fixed_dc))
    spec["forwardStart"] = period(fwd_start)
    return spec


def ois_dates(rate, start, end, index=ESTR, discount=None, telescopic=False, payment_lag=0,
              payment_convention=ql.Following, payment_frequency=ql.Annual, payment_calendar=None, spread=0.0,
              pillar=ql.Pillar.LastRelevantDate, custom=None, averaging=ql.RateAveraging.Compound, eom=None,
              fixed_payment_frequency=None, fixed_calendar=None, lookback=None, lockout=0, shift=False, pricer=None,
              rule=ql.DateGeneration.Backward, overnight_calendar=None, convention=ql.ModifiedFollowing,
              fixed_dc=None):
    spec = {"kind": "OISRateHelper", "form": "dates", "startDate": v.enc(start), "endDate": v.enc(end)}
    spec.update(_ois(rate, index, discount, telescopic, payment_lag, payment_convention, payment_frequency,
                     payment_calendar, spread, pillar, custom, averaging, eom, fixed_payment_frequency,
                     fixed_calendar, lookback, lockout, shift, pricer, rule, overnight_calendar, convention, fixed_dc))
    return spec


def value_spec(spec):
    """The spec whose wheel helper gives QuantLib's values by S3R11's rule: an OIS helper with telescopic value dates
    without the flag, its coupons given the averaging approximation's pricer where they average and have none."""
    if spec["kind"] != "OISRateHelper" or not spec["telescopicValueDates"]:
        return spec
    out = dict(spec, telescopicValueDates=False)
    if spec["averagingMethod"] == int(ql.RateAveraging.Simple) and spec["pricer"] is None:
        out["pricer"] = ocp.pricer_spec("ArithmeticAveraged", by_approx=True)
    return out


def _calendar(spec):
    return ql.Schedule().calendar() if spec is None else irx.calendar(spec)


def _day_counter(spec):
    return ql.InterestRate().dayCounter() if spec is None else irx.day_counter(spec)


def _date(serial):
    return ql.Date() if serial is None else ql.Date(serial)


def make_helper(spec):
    """The wheel's helper for a spec, under the evaluation date and at-par switch already set."""
    kind, form = spec["kind"], spec["form"]
    if kind == "DepositRateHelper":
        if form == "tenor":
            return ql.DepositRateHelper(spec["rate"], ql.Period(*spec["tenor"]), spec["fixingDays"],
                                        irx.calendar(spec["calendar"]), spec["convention"], spec["endOfMonth"],
                                        irx.day_counter(spec["dayCounter"]))
        if form == "index":
            return ql.DepositRateHelper(spec["rate"], fvf.make_index(spec["index"]))
        if form == "fixingDate":
            return ql.DepositRateHelper(ql.QuoteHandle(ql.SimpleQuote(spec["rate"])), ql.Date(spec["fixingDate"]),
                                        fvf.make_index(spec["index"]))
    elif kind == "SwapRateHelper":
        spread = ql.QuoteHandle() if spec["spread"] == 0.0 else ql.QuoteHandle(ql.SimpleQuote(spec["spread"]))
        common = (_calendar(spec["calendar"]), spec["fixedFrequency"], spec["fixedConvention"],
                  _day_counter(spec["fixedDayCount"]), fvf.make_index(spec["index"]), spread)
        tail = (spec["pillar"], _date(spec["customPillarDate"]), spec["endOfMonth"], spec["useIndexedCoupons"],
                spec["floatConvention"])
        discount = irx.handle(spec["discountingCurve"])
        if form == "tenor":
            days = v.NULL_INTEGER if spec["settlementDays"] is None else spec["settlementDays"]
            return ql.SwapRateHelper(spec["rate"], ql.Period(*spec["tenor"]), *common,
                                     ql.Period(*spec["fwdStart"]), discount, days, *tail)
        if form == "dates":
            return ql.SwapRateHelper.forDates(ql.QuoteHandle(ql.SimpleQuote(spec["rate"])),
                                              ql.Date(spec["startDate"]), ql.Date(spec["endDate"]), *common,
                                              discount, *tail)
    elif kind == "FraRateHelper":
        tail = (spec["pillar"], _date(spec["customPillarDate"]), spec["useIndexedCoupon"])
        if form == "months":
            return ql.FraRateHelper(spec["rate"], spec["monthsToStart"], spec["monthsToEnd"], spec["fixingDays"],
                                    irx.calendar(spec["calendar"]), spec["convention"], spec["endOfMonth"],
                                    irx.day_counter(spec["dayCounter"]), *tail)
        if form == "monthsIndex":
            return ql.FraRateHelper(spec["rate"], spec["monthsToStart"], fvf.make_index(spec["index"]), *tail)
        if form == "periodIndex":
            return ql.FraRateHelper(spec["rate"], ql.Period(*spec["periodToStart"]), fvf.make_index(spec["index"]),
                                    *tail)
        if form == "imm":
            return ql.FraRateHelper(spec["rate"], spec["immOffsetStart"], spec["immOffsetEnd"],
                                    fvf.make_index(spec["index"]), *tail)
        if form == "dates":
            return ql.FraRateHelper.forDates(ql.QuoteHandle(ql.SimpleQuote(spec["rate"])),
                                             ql.Date(spec["startDate"]), ql.Date(spec["endDate"]),
                                             fvf.make_index(spec["index"]), *tail)
    elif kind == "FuturesRateHelper":
        start, convexity, futures_type = ql.Date(spec["iborStartDate"]), spec["convexityAdjustment"], spec["type"]
        if form == "length":
            return ql.FuturesRateHelper(spec["rate"], start, spec["lengthInMonths"], irx.calendar(spec["calendar"]),
                                        spec["convention"], spec["endOfMonth"], irx.day_counter(spec["dayCounter"]),
                                        convexity, futures_type)
        if form == "dates":
            return ql.FuturesRateHelper(spec["rate"], start, _date(spec["iborEndDate"]),
                                        irx.day_counter(spec["dayCounter"]), convexity, futures_type)
        if form == "index":
            return ql.FuturesRateHelper(spec["rate"], start, fvf.make_index(spec["index"]), convexity, futures_type)
    elif kind == "FxSwapRateHelper":
        points = ql.QuoteHandle(ql.SimpleQuote(spec["rate"]))
        spot = ql.QuoteHandle(ql.SimpleQuote(spec["spotFx"]))
        collateral = irx.handle(spec["collateralCurve"])
        if form == "tenor":
            args = [points, spot, ql.Period(*spec["tenor"]), spec["fixingDays"], irx.calendar(spec["calendar"]),
                    spec["convention"], spec["endOfMonth"], spec["isFxBaseCurrencyCollateralCurrency"], collateral]
            if spec["tradingCalendar"] is not None:
                args.append(irx.calendar(spec["tradingCalendar"]))
            return ql.FxSwapRateHelper(*args)
        if form == "dates":
            return ql.FxSwapRateHelper.forDates(points, spot, ql.Date(spec["startDate"]), ql.Date(spec["endDate"]),
                                                spec["isFxBaseCurrencyCollateralCurrency"], collateral)
    elif kind == "OISRateHelper":
        fixed_frequency = spec["fixedPaymentFrequency"]
        lookback = v.NULL_INTEGER if spec["lookbackDays"] is None else spec["lookbackDays"]
        pricer = None if spec["pricer"] is None else ocp.make_pricer(spec["pricer"])
        middle = (spec["telescopicValueDates"], spec["paymentLag"], spec["paymentConvention"],
                  spec["paymentFrequency"], _calendar(spec["paymentCalendar"]))
        tail = (spec["overnightSpread"], spec["pillar"], _date(spec["customPillarDate"]), spec["averagingMethod"],
                spec["endOfMonth"], fixed_frequency, _calendar(spec["fixedCalendar"]), lookback,
                spec["lockoutDays"], spec["applyObservationShift"], pricer, spec["rule"],
                _calendar(spec["overnightCalendar"]), spec["convention"], _day_counter(spec["fixedDayCount"]))
        index = fvf.make_index(spec["index"])
        discount = irx.handle(spec["discountingCurve"])
        if form == "tenor":
            return ql.OISRateHelper(spec["settlementDays"], ql.Period(*spec["tenor"]), spec["rate"], index,
                                    discount, *middle, ql.Period(*spec["forwardStart"]), *tail)
        if form == "dates":
            return ql.OISRateHelper.forDates(ql.Date(spec["startDate"]), ql.Date(spec["endDate"]), spec["rate"],
                                             index, discount, *middle, *tail)
    raise ValueError(f"unknown helper {kind} {form}")


def make_value_helper(spec):
    """The wheel's helper whose values are QuantLib's by S3R11's rule (value_spec), after the helper as specified,
    whose refusal is QuantLib's own, has been built."""
    h = make_helper(spec)
    return h if value_spec(spec) is spec else make_helper(value_spec(spec))


def helper_dates(h):
    return {"earliestDate": v.enc(h.earliestDate()), "maturityDate": v.enc(h.maturityDate()),
            "latestRelevantDate": v.enc(h.latestRelevantDate()), "pillarDate": v.enc(h.pillarDate()),
            "latestDate": v.enc(h.latestDate())}


def deposits():
    """(name, spec) for the 40 deposit helpers."""
    W, M, Y, Dd = ql.Weeks, ql.Months, ql.Years, ql.Days
    MF, F, P, MP, U = ql.ModifiedFollowing, ql.Following, ql.Preceding, ql.ModifiedPreceding, ql.Unadjusted
    out = [
        ("1W", deposit_tenor(0.0391, (1, W), 2, TARGET, F, False, A360)),
        ("2W", deposit_tenor(0.0392, (2, W), 2, TARGET, F, False, A360)),
        ("1M end of month", deposit_tenor(0.0393, (1, M), 2, TARGET, MF, True, A360)),
        ("2M no fixing days, end of month", deposit_tenor(0.0394, (2, M), 0, TARGET, MF, True, A360)),
        ("3M one fixing day, Actual/365", deposit_tenor(0.0395, (3, M), 1, TARGET, MF, False, A365)),
        ("6M end of month", deposit_tenor(0.0396, (6, M), 2, TARGET, MF, True, A360)),
        ("9M three fixing days, Preceding", deposit_tenor(0.0397, (9, M), 3, TARGET, P, False, A360)),
        ("12M 30/360", deposit_tenor(0.0398, (12, M), 2, TARGET, MF, True, THIRTY360_BB)),
        ("1Y on the US SOFR calendar", deposit_tenor(0.0399, (1, Y), 2, US_SOFR, MF, False, A360)),
        ("3M UK exchange, no fixing days", deposit_tenor(0.0400, (3, M), 0, UK_EXCHANGE, F, True, A365)),
        ("6M null calendar, unadjusted", deposit_tenor(0.0401, (6, M), 2, NULL_CALENDAR, U, False, A360)),
        ("1M US SOFR, ModifiedPreceding", deposit_tenor(0.0402, (1, M), 0, US_SOFR, MP, True, A360)),
        ("18M", deposit_tenor(0.0403, (18, M), 2, TARGET, MF, False, A360)),
        ("2Y five fixing days", deposit_tenor(0.0404, (2, Y), 5, TARGET, F, False, A365)),
        ("0D", deposit_tenor(0.0405, (0, Dd), 2, TARGET, MF, False, A360)),
        ("1 hour", deposit_tenor(0.0406, (1, ql.Hours), 2, TARGET, MF, False, A360)),
        ("on Euribor 1W", deposit_index(0.0391, E1W)),
        ("on Euribor 1M", deposit_index(0.0392, E1M)),
        ("on Euribor 3M", deposit_index(0.0393, E3M)),
        ("on Euribor 6M", deposit_index(0.0394, E6M)),
        ("on Euribor 1Y", deposit_index(0.0395, E1Y)),
        ("on Euribor 2M", deposit_index(0.0396, euribor(2, M))),
        ("on Euribor 9M", deposit_index(0.0397, euribor(9, M))),
        ("on Euribor 2W", deposit_index(0.0398, euribor(2, W))),
        ("on Euribor365 3M", deposit_index(0.0399, fvf.preset("Euribor365", None, ql.Period(3, M)))),
        ("on Tibor 3M", deposit_index(0.0040, fvf.preset("Tibor", None, ql.Period(3, M)))),
        ("on an index of the empty currency, TARGET",
         deposit_index(0.0401, irx.ibor_spec("Euribor", (6, M), 2, TARGET, MF, True, A360))),
        ("on an index of the US SOFR calendar",
         deposit_index(0.0402, irx.ibor_spec("UsdIbor", (3, M), 2, US_SOFR, MF, True, A360))),
        ("on an index of the UK exchange calendar",
         deposit_index(0.0403, irx.ibor_spec("GbpIbor", (1, M), 0, UK_EXCHANGE, MF, True, A365))),
        ("on an index of the null calendar",
         deposit_index(0.0404, irx.ibor_spec("NulIbor", (6, M), 1, NULL_CALENDAR, F, False, A360))),
        ("fixing on 15 January 2024", deposit_date(0.0391, D(15, 1, 2024), E6M)),
        ("fixing on Friday 12 January 2024", deposit_date(0.0392, D(12, 1, 2024), E3M)),
        ("fixing on 1 March 2024", deposit_date(0.0393, D(1, 3, 2024), E1M)),
        ("fixing on Thursday 28 March 2024", deposit_date(0.0394, D(28, 3, 2024), E6M)),
        ("fixing on Easter Tuesday 2 April 2024", deposit_date(0.0395, D(2, 4, 2024), E1Y)),
        ("fixing on 31 January 2024", deposit_date(0.0396, D(31, 1, 2024), euribor(2, M))),
        ("fixing on 16 February 2024, US SOFR",
         deposit_date(0.0397, D(16, 2, 2024), irx.ibor_spec("UsdIbor", (3, M), 2, US_SOFR, MF, True, A360))),
        ("fixing on 29 February 2024, UK exchange",
         deposit_date(0.0398, D(29, 2, 2024), irx.ibor_spec("GbpIbor", (1, M), 0, UK_EXCHANGE, MF, True, A365))),
        ("fixing on Saturday 13 January 2024", deposit_date(0.0399, D(13, 1, 2024), E6M)),
        ("fixing on Good Friday 29 March 2024", deposit_date(0.0400, D(29, 3, 2024), E6M)),
    ]
    assert len(out) == 40
    return out


FLAT_DISCOUNT = fvf.curve(0.027, reference=EVALUATION_DATES[0])
FLAT_DISCOUNT_SIMPLE = fvf.curve(0.031, A360, ql.Simple, reference=EVALUATION_DATES[0])
USD_INDEX = fvf.with_currency(irx.ibor_spec("UsdIbor", (3, ql.Months), 2, US_SOFR, ql.ModifiedFollowing, True, A360),
                              "USD")


def swaps():
    """(name, spec, at-par switch or None) for the 63 swap helpers."""
    Y, M, W, Dd = ql.Years, ql.Months, ql.Weeks, ql.Days
    MF, F, P, MP, U = ql.ModifiedFollowing, ql.Following, ql.Preceding, ql.ModifiedPreceding, ql.Unadjusted
    MAT, LAST, CUSTOM = ql.Pillar.MaturityDate, ql.Pillar.LastRelevantDate, ql.Pillar.CustomDate
    out = [
        ("1Y", swap_tenor(0.0301, (1, Y))),
        ("2Y", swap_tenor(0.0302, (2, Y))),
        ("5Y", swap_tenor(0.0303, (5, Y))),
        ("10Y", swap_tenor(0.0304, (10, Y))),
        ("30Y", swap_tenor(0.0305, (30, Y))),
        ("18M", swap_tenor(0.0306, (18, M))),
        ("semiannual fixed leg", swap_tenor(0.0307, (5, Y), freq=ql.Semiannual)),
        ("quarterly fixed leg", swap_tenor(0.0308, (5, Y), freq=ql.Quarterly)),
        ("fixed leg paying once", swap_tenor(0.0309, (5, Y), freq=ql.Once)),
        ("monthly fixed leg", swap_tenor(0.0310, (3, Y), freq=ql.Monthly)),
        ("fixed convention ModifiedFollowing", swap_tenor(0.0311, (5, Y), convention=MF)),
        ("fixed convention Following", swap_tenor(0.0312, (5, Y), convention=F)),
        ("fixed convention Preceding", swap_tenor(0.0313, (5, Y), convention=P)),
        ("fixed Actual/360", swap_tenor(0.0314, (5, Y), dc=A360)),
        ("fixed Actual/365", swap_tenor(0.0315, (5, Y), dc=A365)),
        ("fixed Actual/Actual (ISDA)", swap_tenor(0.0316, (5, Y), dc=ACT_ISDA)),
        ("spread 10bp", swap_tenor(0.0317, (5, Y), spread=0.001)),
        ("spread -5bp", swap_tenor(0.0318, (5, Y), spread=-0.0005)),
        ("forward start 1M", swap_tenor(0.0319, (5, Y), fwd_start=(1, M))),
        ("forward start 1Y", swap_tenor(0.0320, (5, Y), fwd_start=(1, Y))),
        ("forward start -1W", swap_tenor(0.0321, (5, Y), fwd_start=(-1, W))),
        ("forward start 3D", swap_tenor(0.0322, (5, Y), fwd_start=(3, Dd))),
        ("discounting curve", swap_tenor(0.0323, (5, Y), discount=FLAT_DISCOUNT)),
        ("10Y discounting curve, simple", swap_tenor(0.0324, (10, Y), discount=FLAT_DISCOUNT_SIMPLE)),
        ("settlement days 0", swap_tenor(0.0325, (5, Y), settlement_days=0)),
        ("settlement days 1", swap_tenor(0.0326, (5, Y), settlement_days=1)),
        ("settlement days 3", swap_tenor(0.0327, (5, Y), settlement_days=3)),
        ("settlement days 5", swap_tenor(0.0328, (5, Y), settlement_days=5)),
        ("pillar at maturity", swap_tenor(0.0329, (5, Y), pillar=MAT)),
        ("pillar at the last relevant date", swap_tenor(0.0330, (5, Y), pillar=LAST)),
        ("custom pillar", swap_tenor(0.0331, (5, Y), pillar=CUSTOM, custom=D(3, 7, 2028))),
        ("custom pillar after the last relevant date", swap_tenor(0.0332, (5, Y), pillar=CUSTOM, custom=D(1, 1, 2040))),
        ("custom pillar not given", swap_tenor(0.0333, (5, Y), pillar=CUSTOM)),
        ("end of month, no settlement days", swap_tenor(0.0334, (5, Y), settlement_days=0, eom=True)),
        ("end of month, forward start 1M", swap_tenor(0.0335, (1, Y), fwd_start=(1, M), eom=True)),
        ("indexed coupons", swap_tenor(0.0336, (5, Y), indexed=True)),
        ("at-par coupons", swap_tenor(0.0337, (5, Y), indexed=False)),
        ("the switch's indexed coupons", swap_tenor(0.0338, (5, Y)), False),
        ("floating convention Unadjusted", swap_tenor(0.0339, (5, Y), float_convention=U)),
        ("floating convention Following", swap_tenor(0.0340, (5, Y), float_convention=F)),
        ("floating convention ModifiedPreceding", swap_tenor(0.0341, (5, Y), float_convention=MP)),
        ("on Euribor 3M, quarterly", swap_tenor(0.0342, (5, Y), freq=ql.Quarterly, index=E3M)),
        ("7Y on Euribor 1Y", swap_tenor(0.0343, (7, Y), index=E1Y)),
        ("on a US dollar index", swap_tenor(0.0344, (5, Y), cal=US_SOFR, freq=ql.Semiannual, convention=MF,
                                            dc=A360, index=USD_INDEX)),
        ("pillar at maturity, indexed coupons", swap_tenor(0.0345, (5, Y), pillar=MAT, indexed=True)),
        ("2Y forward start 6M, settlement days 2", swap_tenor(0.0346, (2, Y), fwd_start=(6, M), settlement_days=2)),
        ("15Y spread, discounting curve, settlement days 1",
         swap_tenor(0.0347, (15, Y), spread=0.0002, discount=FLAT_DISCOUNT, settlement_days=1)),
        ("4Y monthly Euribor 1M", swap_tenor(0.0348, (4, Y), index=E1M)),
        ("empty calendar, unadjusted", swap_tenor(0.0349, (5, Y), cal=None, float_convention=U)),
        ("empty calendar, ModifiedFollowing", swap_tenor(0.0350, (5, Y), cal=None, convention=MF)),
        ("no fixed day counter", swap_tenor(0.0351, (5, Y), dc=None)),
        ("from 17 January 2024", swap_dates(0.0352, D(17, 1, 2024), D(17, 1, 2029))),
        ("from 31 January 2024, end of month", swap_dates(0.0353, D(31, 1, 2024), D(31, 1, 2027), eom=True)),
        ("from 31 January 2024", swap_dates(0.0354, D(31, 1, 2024), D(31, 1, 2027))),
        ("from 29 February 2024, end of month", swap_dates(0.0355, D(29, 2, 2024), D(28, 2, 2030), eom=True)),
        ("short front stub", swap_dates(0.0356, D(15, 3, 2024), D(15, 9, 2026))),
        ("semiannual from 2 April 2024, spread",
         swap_dates(0.0357, D(2, 4, 2024), D(2, 4, 2034), freq=ql.Semiannual, spread=0.0015)),
        ("dated, discounting curve", swap_dates(0.0358, D(17, 1, 2024), D(17, 1, 2029), discount=FLAT_DISCOUNT)),
        ("dated, pillar at maturity, indexed coupons",
         swap_dates(0.0359, D(17, 1, 2024), D(17, 1, 2029), pillar=MAT, indexed=True)),
        ("dated, custom pillar", swap_dates(0.0360, D(17, 1, 2024), D(17, 1, 2029), pillar=CUSTOM,
                                            custom=D(15, 6, 2027))),
        ("seasoned", swap_dates(0.0361, D(10, 1, 2023), D(12, 1, 2026), float_convention=F)),
        ("dated, paying once", swap_dates(0.0362, D(17, 1, 2024), D(17, 1, 2029), freq=ql.Once)),
        ("dated, reversed", swap_dates(0.0363, D(17, 1, 2029), D(17, 1, 2024))),
    ]
    out = [(x[0], x[1], x[2] if len(x) > 2 else None) for x in out]
    assert len(out) == 63
    return out


def all_helpers():
    """(name, spec, at-par switch or None) for the 103 helpers."""
    return [(f"deposit {n}", s, None) for n, s in deposits()] + [(f"swap {n}", s, a) for n, s, a in swaps()]


US_SETTLEMENT = {"type": "UnitedStates", "market": int(ql.UnitedStates.Settlement)}
US_GOVERNMENT = {"type": "UnitedStates", "market": int(ql.UnitedStates.GovernmentBond)}


def fras():
    """(name, spec) for the 42 FRA helpers."""
    M, W, Dd, Y = ql.Months, ql.Weeks, ql.Days, ql.Years
    MF, F, P = ql.ModifiedFollowing, ql.Following, ql.Preceding
    MAT, CUSTOM = ql.Pillar.MaturityDate, ql.Pillar.CustomDate
    out = [
        ("1x4", fra_months(0.0381, 1, 4)),
        ("2x5", fra_months(0.0382, 2, 5)),
        ("3x6", fra_months(0.0383, 3, 6)),
        ("6x9", fra_months(0.0384, 6, 9)),
        ("9x12", fra_months(0.0385, 9, 12)),
        ("0x3", fra_months(0.0386, 0, 3)),
        ("12x18 end of month, Actual/365", fra_months(0.0387, 12, 18, eom=True, dc=A365)),
        ("1x7 pillar at maturity", fra_months(0.0388, 1, 7, pillar=MAT)),
        ("3x6 not indexed", fra_months(0.0389, 3, 6, indexed=False)),
        ("3x6 one fixing day, Following, US SOFR", fra_months(0.0390, 3, 6, 1, US_SOFR, F)),
        ("3x6 no fixing days, 30/360, Preceding", fra_months(0.0391, 3, 6, 0, TARGET, P, dc=THIRTY360_BB)),
        ("6x3, out of order", fra_months(0.0392, 6, 3)),
        ("3x3, equal", fra_months(0.0393, 3, 3)),
        ("2x8 custom pillar", fra_months(0.0394, 2, 8, pillar=CUSTOM, custom=D(1, 9, 2024))),
        ("3x6 custom pillar after the last relevant date, refused",
         fra_months(0.0395, 3, 6, pillar=CUSTOM, custom=D(1, 1, 2026))),
        ("3x6 custom pillar not given, refused", fra_months(0.0396, 3, 6, pillar=CUSTOM)),
        ("1 month on Euribor 3M", fra_months_index(0.0381, 1, E3M)),
        ("3 months on Euribor 3M", fra_months_index(0.0382, 3, E3M)),
        ("6 months on Euribor 6M", fra_months_index(0.0383, 6, E6M)),
        ("0 months on Euribor 6M", fra_months_index(0.0384, 0, E6M)),
        ("9 months on Euribor 3M, not indexed", fra_months_index(0.0385, 9, E3M, indexed=False)),
        ("2 months on Euribor 1M, pillar at maturity", fra_months_index(0.0386, 2, E1M, pillar=MAT)),
        ("1M on Euribor 3M", fra_period_index(0.0381, (1, M), E3M)),
        ("1Y on Euribor 6M", fra_period_index(0.0382, (1, Y), E6M)),
        ("6M on Euribor 6M, not indexed", fra_period_index(0.0383, (6, M), E6M, indexed=False)),
        ("2W on Euribor 3M, refused", fra_period_index(0.0384, (2, W), E3M)),
        ("15D on Euribor 6M, refused", fra_period_index(0.0385, (15, Dd), E6M)),
        ("2W on Euribor 1W", fra_period_index(0.0386, (2, W), E1W)),
        ("IMM 0 to 1 on Euribor 3M", fra_imm(0.0381, 0, 1, E3M)),
        ("IMM 1 to 2 on Euribor 3M", fra_imm(0.0382, 1, 2, E3M)),
        ("IMM 2 to 3 on Euribor 3M", fra_imm(0.0383, 2, 3, E3M)),
        ("IMM 1 to 3 on Euribor 6M", fra_imm(0.0384, 1, 3, E6M)),
        ("IMM 3 to 1, out of order", fra_imm(0.0385, 3, 1, E3M)),
        ("IMM 0 to 1 pillar at maturity", fra_imm(0.0386, 0, 1, E3M, pillar=MAT)),
        ("IMM 2 to 4 not indexed", fra_imm(0.0387, 2, 4, E3M, indexed=False)),
        ("IMM 4 to 5 on Euribor 1Y", fra_imm(0.0388, 4, 5, E1Y)),
        ("from 17 April 2024 on Euribor 3M", fra_dates(0.0381, D(17, 4, 2024), D(17, 7, 2024), E3M)),
        ("from 2 May 2024 on Euribor 6M, pillar at maturity",
         fra_dates(0.0382, D(2, 5, 2024), D(4, 11, 2024), E6M, pillar=MAT)),
        ("from 17 April 2024, not indexed", fra_dates(0.0383, D(17, 4, 2024), D(17, 10, 2024), E6M, indexed=False)),
        ("from 10 January 2024, before the evaluation dates", fra_dates(0.0384, D(10, 1, 2024), D(10, 4, 2024), E3M)),
        ("reversed dates", fra_dates(0.0385, D(17, 7, 2024), D(17, 4, 2024), E3M, indexed=False)),
        ("from 17 April 2024, custom pillar",
         fra_dates(0.0386, D(17, 4, 2024), D(17, 7, 2024), E3M, pillar=CUSTOM, custom=D(1, 7, 2024))),
    ]
    assert len(out) == 42
    return out


def futures():
    """(name, spec) for the 41 futures helpers."""
    IMM, ASX, CUSTOM = ql.Futures.IMM, ql.Futures.ASX, ql.Futures.Custom
    MF, F = ql.ModifiedFollowing, ql.Following
    out = [
        ("IMM March 2024, 3 months", futures_length(96.21, D(20, 3, 2024), 3)),
        ("IMM June 2024, 3 months, convexity", futures_length(96.31, D(19, 6, 2024), 3, convexity=0.0005)),
        ("IMM September 2024, 3 months, Following", futures_length(96.41, D(18, 9, 2024), 3, convention=F)),
        ("IMM December 2024, 6 months, Actual/365", futures_length(96.51, D(18, 12, 2024), 6, dc=A365)),
        ("IMM April 2024, off the main cycle", futures_length(96.22, D(17, 4, 2024), 1)),
        ("IMM, a date that isn't", futures_length(96.23, D(21, 3, 2024), 3)),
        ("IMM June 2024, end of month, US SOFR", futures_length(96.24, D(19, 6, 2024), 3, US_SOFR, MF, True)),
        ("IMM March 2024, 30/360, negative convexity",
         futures_length(96.25, D(20, 3, 2024), 3, dc=THIRTY360_BB, convexity=-0.0002)),
        ("ASX March 2024, 3 months", futures_length(96.26, D(8, 3, 2024), 3, futures_type=ASX)),
        ("ASX June 2024, 3 months", futures_length(96.27, D(14, 6, 2024), 3, futures_type=ASX)),
        ("ASX April 2024, off the main cycle", futures_length(96.28, D(12, 4, 2024), 1, futures_type=ASX)),
        ("ASX, a date that isn't", futures_length(96.29, D(15, 3, 2024), 3, futures_type=ASX)),
        ("ASX September 2024, UK exchange, Following",
         futures_length(96.30, D(13, 9, 2024), 3, UK_EXCHANGE, F, futures_type=ASX)),
        ("custom from 20 February 2024, 2 months", futures_length(97.0, D(20, 2, 2024), 2, futures_type=CUSTOM)),
        ("custom from 15 May 2024, 4 months, end of month",
         futures_length(96.5, D(15, 5, 2024), 4, eom=True, futures_type=CUSTOM)),
        ("custom from 31 January 2024, 1 month, end of month",
         futures_length(96.6, D(31, 1, 2024), 1, eom=True, futures_type=CUSTOM)),
        ("custom, no months", futures_length(96.7, D(15, 5, 2024), 0, futures_type=CUSTOM)),
        ("IMM March 2024 with no end date", futures_dates(96.21, D(20, 3, 2024), None)),
        ("IMM June 2024 to September", futures_dates(96.31, D(19, 6, 2024), D(18, 9, 2024))),
        ("IMM April 2024 with no end date, convexity", futures_dates(96.32, D(17, 4, 2024), None, convexity=0.0003)),
        ("IMM June 2024 ending on its start, refused", futures_dates(96.33, D(19, 6, 2024), D(19, 6, 2024))),
        ("IMM June 2024 ending before it, refused", futures_dates(96.34, D(19, 6, 2024), D(18, 6, 2024))),
        ("IMM, a date that isn't, with no end date", futures_dates(96.35, D(21, 6, 2024), None)),
        ("ASX March 2024 with no end date", futures_dates(96.36, D(8, 3, 2024), None, futures_type=ASX)),
        ("ASX June 2024 to September, Actual/365",
         futures_dates(96.37, D(14, 6, 2024), D(13, 9, 2024), A365, futures_type=ASX)),
        ("ASX June 2024 ending before it, refused",
         futures_dates(96.38, D(14, 6, 2024), D(1, 6, 2024), futures_type=ASX)),
        ("custom from 20 February 2024 to 6 April", futures_dates(96.5, D(20, 2, 2024), D(6, 4, 2024),
                                                                  futures_type=CUSTOM)),
        ("custom with no end date", futures_dates(96.4, D(20, 2, 2024), None, futures_type=CUSTOM)),
        ("custom ending before it", futures_dates(96.3, D(20, 2, 2024), D(10, 2, 2024), futures_type=CUSTOM)),
        ("custom ending on its start", futures_dates(96.2, D(20, 2, 2024), D(20, 2, 2024), futures_type=CUSTOM)),
        ("IMM March 2024 on Euribor 3M", futures_index(96.21, D(20, 3, 2024), E3M)),
        ("IMM June 2024 on Euribor 3M, convexity", futures_index(96.31, D(19, 6, 2024), E3M, 0.0004)),
        ("IMM April 2024 on Euribor 1M", futures_index(96.22, D(17, 4, 2024), E1M)),
        ("IMM, a date that isn't, on Euribor 3M", futures_index(96.23, D(22, 3, 2024), E3M)),
        ("ASX March 2024 on Euribor 3M", futures_index(96.26, D(8, 3, 2024), E3M, futures_type=ASX)),
        ("ASX December 2024 on Euribor 6M", futures_index(96.27, D(13, 12, 2024), E6M, futures_type=ASX)),
        ("custom from 15 July 2024 on Euribor 3M", futures_index(96.0, D(15, 7, 2024), E3M, futures_type=CUSTOM)),
        ("custom from 31 May 2024 on Euribor 1M, Saturday's",
         futures_index(96.1, D(1, 6, 2024), E1M, futures_type=CUSTOM)),
        ("IMM September 2024 on a US dollar index", futures_index(95.0, D(18, 9, 2024), USD_INDEX)),
        ("custom from 15 July 2024 on Euribor 1Y", futures_index(96.2, D(15, 7, 2024), E1Y, futures_type=CUSTOM)),
        ("IMM December 2025 on Euribor 6M", futures_index(96.3, D(17, 12, 2025), E6M)),
    ]
    assert len(out) == 41
    return out


def fx_swaps():
    """(name, spec) for the 20 FX swap helpers."""
    M, W, Dd, Y = ql.Months, ql.Weeks, ql.Days, ql.Years
    MF, F = ql.ModifiedFollowing, ql.Following
    out = [
        ("1W", fx_tenor(0.0012, 1.0950, (1, W))),
        ("1M", fx_tenor(0.0045, 1.0950, (1, M))),
        ("3M", fx_tenor(0.0131, 1.0950, (3, M))),
        ("6M, Modified Following", fx_tenor(0.0262, 1.0950, (6, M), convention=MF)),
        ("1Y, end of month", fx_tenor(0.0521, 1.0950, (1, Y), eom=True)),
        ("2Y", fx_tenor(0.1032, 1.0950, (2, Y))),
        ("overnight", fx_tenor(0.0001, 1.0950, (1, Dd), 0)),
        ("tomorrow-next", fx_tenor(0.0001, 1.0950, (1, Dd), 1)),
        ("3M on a US trading calendar", fx_tenor(0.0131, 1.0950, (3, M), trading=US_SETTLEMENT)),
        ("1M on a US trading calendar, end of month",
         fx_tenor(0.0045, 1.0950, (1, M), convention=MF, eom=True, trading=US_SETTLEMENT)),
        ("6M on the US government bond calendar", fx_tenor(30.368, 56.881, (6, M), 2, US_GOVERNMENT)),
        ("3M on the UK exchange calendar, collateral in the quote currency",
         fx_tenor(-0.0052, 0.8610, (3, M), 2, UK_EXCHANGE, is_base=False)),
        ("9M, 3 fixing days", fx_tenor(0.0393, 1.0950, (9, M), 3)),
        ("1M with no collateral curve", fx_tenor(0.0045, 1.0950, (1, M), collateral=None)),
        ("2W, collateral in the quote currency", fx_tenor(0.0024, 1.0950, (2, W), is_base=False)),
        ("18M on a US trading calendar, Modified Following",
         fx_tenor(0.0781, 1.0950, (18, M), convention=MF, trading=US_SETTLEMENT)),
        ("from 17 January 2024 to 17 April", fx_dates(0.0131, 1.0950, D(17, 1, 2024), D(17, 4, 2024))),
        ("from 4 March 2024 to 4 March 2025, collateral in the quote currency",
         fx_dates(0.0521, 1.0950, D(4, 3, 2024), D(4, 3, 2025), is_base=False)),
        ("from 3 April 2024 to 3 July", fx_dates(0.0130, 1.0950, D(3, 4, 2024), D(3, 7, 2024))),
        ("from 17 January 2024 with no collateral curve",
         fx_dates(0.0131, 1.0950, D(17, 1, 2024), D(17, 4, 2024), collateral=None)),
    ]
    assert len(out) == 20
    return out


def more_helpers():
    """(name, spec) for the 103 FRA, futures and FX swap helpers, appended after the deposit and swap helpers."""
    return ([(f"fra {n}", s) for n, s in fras()] + [(f"futures {n}", s) for n, s in futures()]
            + [(f"fx {n}", s) for n, s in fx_swaps()])


def calendar_name(cal):
    return None if cal.empty() else cal.name()


def outputs(h, spec):
    out = {"built": True}
    out.update(helper_dates(h))
    if spec["kind"] == "SwapRateHelper":
        sw = h.swap()
        out.update({"swapStartDate": v.enc(sw.startDate()), "swapMaturityDate": v.enc(sw.maturityDate()),
                    "swapFixedRate": sw.fixedRate(), "spread": h.spread()})
    elif spec["kind"] == "FuturesRateHelper":
        out["convexityAdjustment"] = h.convexityAdjustment()
    elif spec["kind"] == "FxSwapRateHelper":
        tenor = h.tenor()
        out.update({"spot": h.spot(), "tenor": [tenor.length(), int(tenor.units())],
                    "isFxBaseCurrencyCollateralCurrency": h.isFxBaseCurrencyCollateralCurrency(),
                    "calendar": calendar_name(h.calendar()), "tradingCalendar": calendar_name(h.tradingCalendar()),
                    "adjustmentCalendar": calendar_name(h.adjustmentCalendar())})
        if spec["form"] == "tenor":
            # QuantLib leaves these uninitialized for a helper built from dates
            out.update({"fixingDays": h.fixingDays(), "businessDayConvention": h.businessDayConvention(),
                        "endOfMonth": h.endOfMonth()})
    return out


def cases():
    out = []
    for today in EVALUATION_DATES:
        for name, spec, at_par in all_helpers():
            with v.case_settings(today, use_at_par_coupons=at_par) as st:
                try:
                    h = make_helper(spec)
                except RuntimeError:
                    expected = {"built": dict(v.THROWS)}
                else:
                    expected = outputs(h, spec)
                out.append({"name": f"{today.ISO()} {name}", "inputs": {"settings": st, "helper": spec},
                            "expected": expected})
    for today in EVALUATION_DATES:
        for name, spec in more_helpers():
            with v.case_settings(today) as st:
                try:
                    h = make_helper(spec)
                except RuntimeError:
                    expected = {"built": dict(v.THROWS)}
                else:
                    expected = outputs(h, spec)
                out.append({"name": f"{today.ISO()} {name}", "inputs": {"settings": st, "helper": spec},
                            "expected": expected})
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    first = EVALUATION_DATES[0].ISO()
    for refused in ("deposit 1 hour", "deposit fixing on Saturday 13 January 2024",
                    "deposit fixing on Good Friday 29 March 2024", "swap custom pillar after the last relevant date",
                    "swap custom pillar not given", "swap empty calendar, ModifiedFollowing",
                    "swap dated, paying once", "swap dated, reversed"):
        assert by_name[f"{first} {refused}"] == {"built": v.THROWS}, refused
    # S5R4: QuantLib's empty calendar builds with unadjusted conventions.
    assert by_name[f"{first} swap empty calendar, unadjusted"]["built"] is True
    # The at-par switch moves the default pillar through the last coupon's fixing end date.
    assert (by_name[f"{first} swap the switch's indexed coupons"]["pillarDate"]
            == by_name[f"{first} swap indexed coupons"]["pillarDate"])
    for refused in ("fra 6x3, out of order", "fra 3x3, equal", "fra 2W on Euribor 3M, refused",
                    "fra 3x6 custom pillar after the last relevant date, refused",
                    "fra 3x6 custom pillar not given, refused",
                    "fra 15D on Euribor 6M, refused", "futures IMM, a date that isn't",
                    "futures ASX, a date that isn't", "futures IMM June 2024 ending on its start, refused",
                    "futures IMM June 2024 ending before it, refused", "futures IMM, a date that isn't, with no end date",
                    "futures ASX June 2024 ending before it, refused", "futures IMM, a date that isn't, on Euribor 3M"):
        assert by_name[f"{first} {refused}"] == {"built": v.THROWS}, refused
    # DS13: a custom futures helper with no end date has a null maturity, and so null pillar and latest dates.
    null_end = by_name[f"{first} futures custom with no end date"]
    assert null_end["maturityDate"] is None and null_end["pillarDate"] is None and null_end["latestDate"] is None
    # DS14: IMM offsets out of order are taken as they are.
    reversed_imm = by_name[f"{first} fra IMM 3 to 1, out of order"]
    assert reversed_imm["built"] is True and reversed_imm["maturityDate"] < reversed_imm["earliestDate"]
    # The FX swap helper with no collateral curve builds; it can't be bound.
    assert by_name[f"{first} fx 1M with no collateral curve"]["built"] is True


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/ratehelpers.hpp", cases)
