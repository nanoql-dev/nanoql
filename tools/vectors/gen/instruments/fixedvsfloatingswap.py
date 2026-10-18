# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FixedVsFloatingSwap (ql/instruments/fixedvsfloatingswap.hpp), read through its two concrete
classes on the wheel, VanillaSwap and OvernightIndexedSwap, since the wheel binds no constructor of the base: 40 swaps
on the evaluation date 15 January 2025, 20 of each, payer and receiver, with constant, amortizing and different
nominals on the two legs, forward-starting and seasoned schedules (the seasoned ones reading stored fixings), short
front stubs, explicit and default payment conventions, at-par and indexed Ibor coupons, compounded and averaged
overnight coupons, spreads and payment lags, a zero nominal (whose zero BPS leave the fair rate and spread null), and
a negative curve.

Each swap's outputs are the base's inspectors (type, nominal and nominals, which QuantLib refuses for a nominal that
isn't constant or for different nominals on the two legs, the nominal lists, both schedules' dates, the fixed rate and
day counter, the spread and floating day counter, null for QuantLib's empty one, the payment convention, lag and
calendar, null for none, and the index's name), each leg's payment dates, accrual dates, nominals, rates and amounts,
and the results of DiscountingSwapEngine on a flat discount curve (NPV, the per-leg NPV and BPS, the fair rate and
spread, and the four leg accessors); then the same results on an evaluation date after the swap's maturity, where it
has expired: zero NPV and leg BPS, and no fair rate or spread. NPVs are compared at the largest nominal and BPS at it
times 1e-4.

The functions here build the specs and the swaps every swap generator shares (instruments/vanillaswap,
overnightindexedswap, makevanillaswap, makeois and their harness counterparts, which import this file):
- an index is a spec of indexes/interestrateindex.py, an IborIndex or OvernightIndex with QuantLib's empty currency;
  the same with a "currency" code, built with that currency; or a preset, {"type": <class>, "curve"}, with a "tenor"
  for Euribor, Euribor365 and Tibor;
- a curve is null or a flat forward spec of indexes/interestrateindex.py, and a schedule a rule-based spec of
  cashflows/fixedratecoupon.py, or {"dates": [...], "calendar"} for a schedule of dates;
- a vanilla swap is {"kind": "VanillaSwap", "type", "nominal", "fixedSchedule", "fixedRate", "fixedDayCount",
  "floatSchedule", "index", "spread", "floatingDayCount", "paymentConvention", "useIndexedCoupons"} (a null payment
  convention or coupon choice is QuantLib's std::nullopt), its arguments in VanillaSwap's order;
- an overnight-indexed swap is {"kind": "OvernightIndexedSwap", "form", "type", "fixedNominals", "fixedSchedule",
  "fixedRate", "fixedDC", "overnightNominals", "overnightSchedule", "index", "spread", "paymentLag",
  "paymentAdjustment", "paymentCalendar", "telescopicValueDates", "averagingMethod", "lookbackDays", "lockoutDays",
  "applyObservationShift", "roundingPrecision"}, its form one of QuantLib's four constructors: "nominal" and "nominals"
  (one schedule for both legs), "nominal, two schedules" and "nominals, two schedules". The wheel binds three of them;
  QuantLib's third, taking one nominal and two schedules, delegates to the fourth with one-element nominal vectors
  (overnightindexedswap.cpp:106-142), so the wheel builds it so. The wheel takes the arguments by position and can't
  build QuantLib's empty calendar, so a swap with no payment calendar keeps every later argument at its default.

A telescopic overnight swap follows the plan's telescopic rule (S3R11, SD11): it is built with the flag first, so that
a refusal is QuantLib's own; its expected values are then those of the same swap built without the flag, with
ArithmeticAveragedOvernightIndexedCouponPricer(byApprox=True) set on its overnight leg (setCouponPricer) when it
averages, the pricer the flag selects. The wheel's ArithmeticAveragedOvernightIndexedCouponPricer is built with
keyword arguments only: a positional True binds to meanReversion.

Read back by tests/NanoQL.Tests/Vectors/Instruments/FixedVsFloatingSwapVectorTests.cs, with
tests/NanoQL.Tests/Vectors/SwapInputs.cs, which documents the specs."""
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


irx = _load("wheel_interestrateindex", "indexes/interestrateindex.py")
frc = _load("wheel_fixedratecoupon", "cashflows/fixedratecoupon.py")
ibc = _load("wheel_iborcoupon", "cashflows/iborcoupon.py")

TODAY = irx.TODAY
TARGET, NULL_CALENDAR, US_SOFR, UK_EXCHANGE = irx.TARGET, irx.NULL_CALENDAR, irx.US_SOFR, irx.UK_EXCHANGE
A360, A365, THIRTY360_BB = irx.A360, irx.A365, irx.THIRTY360_BB

# The currencies a "currency" code names.
CURRENCIES = {"EUR": ql.EURCurrency, "USD": ql.USDCurrency, "GBP": ql.GBPCurrency, "CHF": ql.CHFCurrency,
              "SEK": ql.SEKCurrency, "JPY": ql.JPYCurrency, "AUD": ql.AUDCurrency, "HKD": ql.HKDCurrency,
              "THB": ql.THBCurrency, "CAD": ql.CADCurrency, "NZD": ql.NZDCurrency}

# The presets, each from its handle (and tenor).
PRESETS = {"Euribor": lambda h, t: ql.Euribor(t, h), "Euribor1W": lambda h, t: ql.Euribor1W(h),
           "Euribor1M": lambda h, t: ql.Euribor1M(h), "Euribor3M": lambda h, t: ql.Euribor3M(h),
           "Euribor6M": lambda h, t: ql.Euribor6M(h), "Euribor1Y": lambda h, t: ql.Euribor1Y(h),
           "Euribor365": lambda h, t: ql.Euribor365(t, h), "Sofr": lambda h, t: ql.Sofr(h),
           "Estr": lambda h, t: ql.Estr(h), "Sonia": lambda h, t: ql.Sonia(h), "Tonar": lambda h, t: ql.Tonar(h),
           "Tibor": lambda h, t: ql.Tibor(t, h), "Corra": lambda h, t: ql.Corra(h)}


def d(day, month, year):
    return ql.Date(day, month, year)


def curve(forward, dc=A365, compounding=ql.Continuous, frequency=ql.Annual, reference=TODAY):
    return irx.curve_spec(reference, forward, dc, compounding, frequency)


def preset(name, curve_spec=None, tenor=None):
    spec = {"type": name, "curve": curve_spec}
    if tenor is not None:
        spec["tenor"] = [tenor.length(), int(tenor.units())]
    return spec


def with_currency(index_spec, code):
    return dict(index_spec, currency=code)


def make_index(spec):
    """The index a spec describes: a preset, an index with a currency, or one of indexes/interestrateindex.py's."""
    t = spec["type"]
    if t in PRESETS:
        tenor = ql.Period(spec["tenor"][0], spec["tenor"][1]) if "tenor" in spec else None
        return PRESETS[t](irx.handle(spec["curve"]), tenor)
    if "currency" not in spec:
        return irx.make_index(spec)
    ccy = CURRENCIES[spec["currency"]]()
    if t == "IborIndex":
        return ql.IborIndex(spec["familyName"], ql.Period(spec["tenor"][0], spec["tenor"][1]), spec["settlementDays"],
                            ccy, irx.calendar(spec["fixingCalendar"]), spec["convention"], spec["endOfMonth"],
                            irx.day_counter(spec["dayCounter"]), irx.handle(spec["curve"]))
    if t == "OvernightIndex":
        return ql.OvernightIndex(spec["familyName"], spec["settlementDays"], ccy, irx.calendar(spec["fixingCalendar"]),
                                 irx.day_counter(spec["dayCounter"]), irx.handle(spec["curve"]))
    raise ValueError(f"unknown index {t}")


def sched(eff, mat, tenor, cal=TARGET, convention=ql.ModifiedFollowing, rule=ql.DateGeneration.Backward, eom=False,
          first=None, next_to_last=None, termination_convention=None):
    s = frc.schedule_spec(eff, mat, tenor, cal, convention, rule, eom, first)
    s["nextToLastDate"] = v.enc(next_to_last)
    if termination_convention is not None:
        s["terminationDateConvention"] = int(termination_convention)
    return s


def dates_schedule(dates, cal=TARGET):
    return {"dates": v.enc(dates), "calendar": cal}


def schedule(spec):
    if "dates" in spec:
        return ql.Schedule([ql.Date(x) for x in spec["dates"]], irx.calendar(spec["calendar"]))
    return frc.schedule(spec)


def vanilla(swap_type, nominal, fixed_schedule, fixed_rate, fixed_dc, float_schedule, index, spread=0.0,
            floating_dc=A360, payment_convention=None, use_indexed=None):
    return {"kind": "VanillaSwap", "type": int(swap_type), "nominal": nominal, "fixedSchedule": fixed_schedule,
            "fixedRate": fixed_rate, "fixedDayCount": fixed_dc, "floatSchedule": float_schedule, "index": index,
            "spread": spread, "floatingDayCount": floating_dc,
            "paymentConvention": None if payment_convention is None else int(payment_convention),
            "useIndexedCoupons": use_indexed}


def ois(form, swap_type, fixed_nominals, fixed_schedule, fixed_rate, fixed_dc, index, overnight_nominals=None,
        overnight_schedule=None, spread=0.0, payment_lag=0, payment_adjustment=ql.Following, payment_calendar=None,
        telescopic=False, averaging=ql.RateAveraging.Compound, lookback=None, lockout=0, shift=False, rounding=None):
    return {"kind": "OvernightIndexedSwap", "form": form, "type": int(swap_type), "fixedNominals": list(fixed_nominals),
            "fixedSchedule": fixed_schedule, "fixedRate": fixed_rate, "fixedDC": fixed_dc,
            "overnightNominals": list(overnight_nominals if overnight_nominals is not None else fixed_nominals),
            "overnightSchedule": overnight_schedule if overnight_schedule is not None else fixed_schedule,
            "index": index, "spread": spread, "paymentLag": payment_lag,
            "paymentAdjustment": int(payment_adjustment), "paymentCalendar": payment_calendar,
            "telescopicValueDates": telescopic, "averagingMethod": int(averaging), "lookbackDays": lookback,
            "lockoutDays": lockout, "applyObservationShift": shift, "roundingPrecision": rounding}


def make_vanilla(spec, index=None):
    index = index if index is not None else make_index(spec["index"])
    return ql.VanillaSwap(spec["type"], spec["nominal"], schedule(spec["fixedSchedule"]), spec["fixedRate"],
                          irx.day_counter(spec["fixedDayCount"]), schedule(spec["floatSchedule"]), index,
                          spec["spread"], irx.day_counter(spec["floatingDayCount"]), spec["paymentConvention"],
                          spec["useIndexedCoupons"])


def _ois(spec, index, telescopic):
    form = spec["form"]
    fixed = schedule(spec["fixedSchedule"])
    head = [spec["type"]]
    if form == "nominal":
        head += [spec["fixedNominals"][0], fixed, spec["fixedRate"], irx.day_counter(spec["fixedDC"]), index]
    elif form == "nominals":
        head += [spec["fixedNominals"], fixed, spec["fixedRate"], irx.day_counter(spec["fixedDC"]), index]
    elif form in ("nominal, two schedules", "nominals, two schedules"):
        # QuantLib's third constructor delegates to the fourth with one-element vectors.
        head += [spec["fixedNominals"], fixed, spec["fixedRate"], irx.day_counter(spec["fixedDC"]),
                 spec["overnightNominals"], schedule(spec["overnightSchedule"]), index]
    else:
        raise ValueError(f"unknown form {form}")
    tail = [spec["spread"], spec["paymentLag"], spec["paymentAdjustment"]]
    later = [telescopic, spec["averagingMethod"],
             v.NULL_INTEGER if spec["lookbackDays"] is None else spec["lookbackDays"], spec["lockoutDays"],
             spec["applyObservationShift"], spec["roundingPrecision"]]
    if spec["paymentCalendar"] is None:
        # The wheel can't build QuantLib's empty calendar: the later arguments keep their defaults.
        assert later == [False, int(ql.RateAveraging.Compound), v.NULL_INTEGER, 0, False, None], spec
        return ql.OvernightIndexedSwap(*head, *tail)
    return ql.OvernightIndexedSwap(*head, *tail, irx.calendar(spec["paymentCalendar"]), *later)


def make_ois(spec, index=None):
    """The wheel's swap for a spec, by the telescopic rule: built with the flag first, so that QuantLib's refusal is
    its own; then, for a telescopic spec, built without it, an averaged swap's overnight leg given the approximating
    pricer the flag selects."""
    index = index if index is not None else make_index(spec["index"])
    sw = _ois(spec, index, spec["telescopicValueDates"])
    if spec["telescopicValueDates"]:
        sw = _ois(spec, index, False)
        if spec["averagingMethod"] == ql.RateAveraging.Simple:
            ql.setCouponPricer(sw.overnightLeg(), ql.ArithmeticAveragedOvernightIndexedCouponPricer(byApprox=True))
    return sw


def make_swap(spec, index=None):
    if spec["kind"] == "VanillaSwap":
        return make_vanilla(spec, index)
    if spec["kind"] == "OvernightIndexedSwap":
        return make_ois(spec, index)
    raise ValueError(f"unknown swap {spec['kind']}")


def scale_of(spec):
    """The scale an NPV of the swap is compared at: its largest nominal, or 1 when every nominal is zero."""
    if spec["kind"] == "VanillaSwap":
        nominals = [spec["nominal"]]
    else:
        nominals = spec["fixedNominals"] + spec["overnightNominals"]
    return max([abs(x) for x in nominals] + [1.0])


def schedule_dates(s):
    return v.enc(list(s.dates()))


def name_or_null(x):
    """A day counter's or a calendar's name, or None for QuantLib's empty one."""
    return None if x.empty() else x.name()


def leg_outputs(prefix, cashflows, scale, full=False, dates=True):
    """Per cash flow of a leg: its payment date (unless dates is false) and amount, and with full its accrual dates,
    nominal and rate."""
    coupons = [ql.as_coupon(cf) for cf in cashflows]
    out = {prefix + "Dates": [c.date().serialNumber() for c in coupons]} if dates else {}
    if full:
        out.update({
            prefix + "AccrualStartDates": [c.accrualStartDate().serialNumber() for c in coupons],
            prefix + "AccrualEndDates": [c.accrualEndDate().serialNumber() for c in coupons],
            prefix + "Nominals": [c.nominal() for c in coupons],
            prefix + "Rates": [v.attempt(c.rate) for c in coupons]})
    out[prefix + "Amounts"] = [ibc.scaled(c.amount, scale) for c in coupons]
    return out


def inspectors(sw):
    """The base's inspectors."""
    return {
        "type": int(sw.type()),
        "nominal": v.attempt(sw.nominal),
        "nominals": v.attempt(lambda: list(sw.nominals())),
        "fixedNominals": list(sw.fixedNominals()),
        "floatingNominals": list(sw.floatingNominals()),
        "fixedSchedule": schedule_dates(sw.fixedSchedule()),
        "floatingSchedule": schedule_dates(sw.floatingSchedule()),
        "fixedRate": sw.fixedRate(),
        "fixedDayCount": sw.fixedDayCount().name(),
        "spread": sw.spread(),
        "floatingDayCount": name_or_null(sw.floatingDayCount()),
        "paymentConvention": int(sw.paymentConvention()),
        "paymentLag": sw.paymentLag(),
        "paymentCalendar": name_or_null(sw.paymentCalendar()),
        "iborIndex": sw.iborIndex().name(),
    }


def results(sw, scale, prefix="", accessors=True):
    """The swap's results with the engine set on it: QuantLib's null results are null, and a refused pricing is the
    one output "calculate" (with a prefix, "<prefix>Calculate"). Without accessors, the four leg accessors, which
    read the per-leg arrays, are left out."""
    try:
        sw.NPV()
    except RuntimeError as e:
        if "not available" in str(e) or "not provided" in str(e):
            raise
        return {prefix + "Calculate" if prefix else "calculate": dict(v.THROWS)}
    r = v.result
    bps = scale * 1e-4
    out = {"NPV": v.amount(r(sw.NPV), scale),
           "legNPV": [v.amount(r(lambda j=j: sw.legNPV(j)), scale) for j in range(2)],
           "legBPS": [v.amount(r(lambda j=j: sw.legBPS(j)), bps) for j in range(2)],
           "fairRate": r(sw.fairRate),
           "fairSpread": r(sw.fairSpread)}
    if accessors:
        out.update({"fixedLegBPS": v.amount(r(sw.fixedLegBPS), bps),
                    "fixedLegNPV": v.amount(r(sw.fixedLegNPV), scale),
                    "floatingLegBPS": v.amount(r(sw.floatingLegBPS), bps),
                    "floatingLegNPV": v.amount(r(sw.floatingLegNPV), scale)})
    return {(prefix + k[0].upper() + k[1:]) if prefix else k: x for k, x in out.items()}


def engine(discount_spec, include=None):
    return ql.DiscountingSwapEngine(irx.handle(discount_spec), include)


# The indexes: Euribor 6M and 3M (TARGET), and overnight indexes with SOFR's, ESTR's and SONIA's conventions, on flat
# curves.
FORECAST = curve(0.031)
FORECAST_STEEP = curve(0.045, A360, ql.Simple)
FORECAST_NEGATIVE = curve(-0.004)
DISCOUNT = curve(0.027)
E6M = ibc.euribor((6, ql.Months), FORECAST)
E3M = ibc.euribor((3, ql.Months), FORECAST_STEEP)
E6M_NEG = ibc.euribor((6, ql.Months), FORECAST_NEGATIVE)
SOFR = irx.overnight_spec("SOFR", 0, US_SOFR, A360, curve(0.043, A360))
ESTR = irx.overnight_spec("ESTR", 0, TARGET, A360, FORECAST)
SONIA = irx.overnight_spec("SONIA", 0, UK_EXCHANGE, A365, curve(0.047))
ONE_Y, SIX_M, THREE_M = ql.Period(1, ql.Years), ql.Period(6, ql.Months), ql.Period(3, ql.Months)
PAYER, RECEIVER = ql.Swap.Payer, ql.Swap.Receiver


def fixings(index_spec, first, last, seed):
    """Fixings for an index on its business days from first to last, drawn with the seed."""
    return (make_index(index_spec), ibc.history(index_spec, first, last, seed))


def swaps():
    """(name, spec, at-par switch or None, fixings) for the 40 swaps."""
    spot = d(17, 1, 2025)
    out = []

    def add(name, spec, at_par=None, fx=None):
        out.append((name, spec, at_par, fx))

    fwd = sched(d(17, 1, 2026), d(17, 1, 2031), ONE_Y, convention=ql.Unadjusted)
    fwd6 = sched(d(17, 1, 2026), d(17, 1, 2031), SIX_M)
    live1 = sched(spot, d(17, 1, 2030), ONE_Y)
    live6 = sched(spot, d(17, 1, 2030), SIX_M)
    past1 = sched(d(15, 7, 2024), d(15, 7, 2027), ONE_Y)
    past6 = sched(d(15, 7, 2024), d(15, 7, 2027), SIX_M)
    stub1 = sched(spot, d(17, 4, 2029), ONE_Y, first=d(17, 4, 2025))
    stub6 = sched(spot, d(17, 4, 2029), SIX_M, first=d(17, 4, 2025))
    q3 = sched(spot, d(17, 1, 2028), THREE_M, convention=ql.Following)
    s6 = sched(spot, d(17, 1, 2028), SIX_M, convention=ql.Following)
    e6m_history = fixings(E6M, d(1, 6, 2024), TODAY - 1, v.SEED)

    # Ibor: vanilla swaps.
    add("vanilla payer 5Y", vanilla(PAYER, 1e6, live1, 0.03, THIRTY360_BB, live6, E6M))
    add("vanilla receiver 5Y", vanilla(RECEIVER, 1e6, live1, 0.03, THIRTY360_BB, live6, E6M))
    add("vanilla forward start, spread", vanilla(PAYER, 2.5e7, fwd, 0.034, THIRTY360_BB, fwd6, E6M, 0.0015))
    add("vanilla receiver forward start, A365", vanilla(RECEIVER, 1e7, fwd, 0.029, A365, fwd6, E6M, -0.001, A365))
    add("vanilla seasoned", vanilla(PAYER, 1e6, past1, 0.031, THIRTY360_BB, past6, E6M), fx=[e6m_history])
    add("vanilla seasoned receiver, spread", vanilla(RECEIVER, 3e6, past1, 0.028, A360, past6, E6M, 0.002),
        fx=[e6m_history])
    add("vanilla zero nominal", vanilla(PAYER, 0.0, live1, 0.03, THIRTY360_BB, live6, E6M))
    add("vanilla short front stubs", vanilla(PAYER, 1e6, stub1, 0.0305, THIRTY360_BB, stub6, E6M, 0.0005))
    add("vanilla Following payments", vanilla(PAYER, 1e6, live1, 0.03, THIRTY360_BB, live6, E6M, 0.0,
                                              payment_convention=ql.Following))
    add("vanilla Preceding payments", vanilla(RECEIVER, 1e6, live1, 0.03, THIRTY360_BB, live6, E6M, 0.0,
                                              payment_convention=ql.Preceding))
    add("vanilla indexed coupons", vanilla(PAYER, 1e6, live1, 0.03, THIRTY360_BB, live6, E6M, 0.0,
                                           use_indexed=True))
    add("vanilla at-par coupons", vanilla(PAYER, 1e6, live1, 0.03, THIRTY360_BB, live6, E6M, 0.0, use_indexed=False))
    add("vanilla, switch indexed", vanilla(PAYER, 1e6, live1, 0.03, THIRTY360_BB, live6, E6M), at_par=False)
    add("vanilla, switch at par", vanilla(RECEIVER, 1e6, live1, 0.03, THIRTY360_BB, live6, E6M), at_par=True)
    add("vanilla 3M floating, 6M fixed", vanilla(PAYER, 5e6, s6, 0.044, A360, q3, E3M, 0.0, A360))
    add("vanilla 3M floating, receiver, spread", vanilla(RECEIVER, 5e6, s6, 0.043, A360, q3, E3M, 0.003, A360))
    add("vanilla negative curve", vanilla(PAYER, 1e6, live1, -0.002, THIRTY360_BB, live6, E6M_NEG))
    add("vanilla negative curve, receiver", vanilla(RECEIVER, 1e6, live1, 0.001, THIRTY360_BB, live6, E6M_NEG,
                                                    -0.0005))
    add("vanilla 1Y", vanilla(PAYER, 1e8, sched(spot, d(17, 1, 2026), ONE_Y), 0.031, A360,
                              sched(spot, d(17, 1, 2026), SIX_M), E6M))
    add("vanilla 10Y", vanilla(RECEIVER, 1e6, sched(spot, d(17, 1, 2035), ONE_Y), 0.032, THIRTY360_BB,
                               sched(spot, d(17, 1, 2035), SIX_M), E6M, 0.0002))

    # Overnight: the four constructors.
    on1 = sched(spot, d(17, 1, 2027), ONE_Y, US_SOFR)
    on6 = sched(spot, d(17, 1, 2027), SIX_M, US_SOFR)
    onp = sched(d(15, 10, 2024), d(15, 10, 2026), ONE_Y, US_SOFR)
    ont = sched(spot, d(17, 1, 2028), ONE_Y, TARGET)
    onu = sched(spot, d(17, 1, 2028), SIX_M, UK_EXCHANGE)
    sofr_history = fixings(SOFR, d(1, 10, 2024), TODAY - 1, v.SEED + 1)
    add("OIS one nominal", ois("nominal", PAYER, [1e6], on1, 0.042, A360, SOFR))
    add("OIS one nominal, receiver", ois("nominal", RECEIVER, [1e6], on1, 0.042, A360, SOFR))
    add("OIS amortizing nominals", ois("nominals", PAYER, [1e6, 5e5], on1, 0.041, A360, SOFR))
    add("OIS amortizing nominals, receiver, spread", ois("nominals", RECEIVER, [2e6, 1e6], on1, 0.04, A365, SOFR,
                                                        spread=0.001))
    add("OIS one nominal, two schedules", ois("nominal, two schedules", PAYER, [1e6], on1, 0.043, A360, SOFR,
                                              overnight_schedule=on6))
    add("OIS different nominals", ois("nominals, two schedules", PAYER, [1e6, 5e5], on1, 0.042, A360, SOFR,
                                      [1e6, 1e6, 5e5, 5e5], on6))
    add("OIS same nominals, two schedules", ois("nominals, two schedules", RECEIVER, [1e6], on1, 0.042, A360, SOFR,
                                                [1e6], on6))
    add("OIS seasoned", ois("nominal", PAYER, [1e6], onp, 0.04, A360, SOFR), fx=[sofr_history])
    add("OIS seasoned, receiver, spread", ois("nominal", RECEIVER, [1e6], onp, 0.04, A360, SOFR, spread=-0.0005),
        fx=[sofr_history])
    add("OIS zero nominal", ois("nominal", PAYER, [0.0], on1, 0.042, A360, SOFR))
    add("OIS payment lag 2", ois("nominal", PAYER, [1e6], ont, 0.03, A360, ESTR, payment_lag=2))
    add("OIS payment lag, Preceding, calendar", ois("nominal", RECEIVER, [1e6], ont, 0.03, A360, ESTR, payment_lag=1,
                                                   payment_adjustment=ql.Preceding, payment_calendar=TARGET))
    add("OIS averaged", ois("nominal", PAYER, [1e6], ont, 0.03, A360, ESTR, payment_calendar=TARGET,
                            averaging=ql.RateAveraging.Simple))
    add("OIS lookback 2 with shift", ois("nominal", PAYER, [1e6], ont, 0.03, A360, ESTR, payment_calendar=TARGET,
                                         lookback=2, shift=True))
    add("OIS lockout 2", ois("nominal", RECEIVER, [1e6], ont, 0.03, A360, ESTR, payment_calendar=TARGET, lockout=2))
    add("OIS rounding 6", ois("nominal", PAYER, [1e6], ont, 0.0305, A360, ESTR, payment_calendar=TARGET, rounding=6))
    add("OIS telescopic", ois("nominal", PAYER, [1e6], ont, 0.03, A360, ESTR, payment_calendar=TARGET,
                              telescopic=True))
    add("OIS SONIA two schedules", ois("nominal, two schedules", PAYER, [1e7], sched(spot, d(17, 1, 2028), ONE_Y,
                                                                                       UK_EXCHANGE),
                                       0.047, A365, SONIA, overnight_schedule=onu))
    add("OIS SONIA receiver, spread", ois("nominals", RECEIVER, [1e7], onu, 0.046, A365, SONIA, spread=0.0008))
    add("OIS negative fixed rate", ois("nominal", PAYER, [1e6], ont, -0.001, THIRTY360_BB, ESTR))
    return out


# An evaluation date after every swap's maturity.
EXPIRED_ON = d(15, 1, 2036)


def cases():
    out = []
    for name, spec, at_par, fx in swaps():
        scale = scale_of(spec)
        with v.case_settings(TODAY, use_at_par_coupons=at_par, fixings=fx) as st:
            sw = make_swap(spec)
            expected = inspectors(sw)
            expected.update(leg_outputs("fixedLeg", sw.fixedLeg(), scale))
            expected.update(leg_outputs("floatingLeg", sw.floatingLeg(), scale))
            sw.setPricingEngine(engine(DISCOUNT))
            expected.update(results(sw, scale))
            ql.Settings.instance().evaluationDate = EXPIRED_ON
            expected["expiredIsExpired"] = sw.isExpired()
            expected.update(results(sw, scale, "expired", accessors=False))
            out.append({"name": name,
                        "inputs": {"settings": st, "swap": spec, "discountCurve": DISCOUNT,
                                   "expiredOn": v.enc(EXPIRED_ON)},
                        "expected": expected})
    check_rows(out)
    return out


def check_rows(out):
    """The rows the file must hold, whatever the swaps."""
    def any_case(pred):
        return any(pred(c["name"], c["expected"]) for c in out)
    assert any_case(lambda n, e: e["nominal"] == v.THROWS)
    assert any_case(lambda n, e: e["nominals"] == v.THROWS)
    assert any_case(lambda n, e: e["fairRate"] is None and e["fairSpread"] is None)
    assert any_case(lambda n, e: e["floatingDayCount"] is None)
    assert any_case(lambda n, e: e["paymentCalendar"] is not None)
    assert all(c["expected"]["expiredIsExpired"] and c["expected"]["expiredFairRate"] is None for c in out)


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/fixedvsfloatingswap.hpp", cases)
