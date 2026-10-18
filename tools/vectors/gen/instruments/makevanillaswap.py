# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MakeVanillaSwap (ql/instruments/makevanillaswap.hpp): 51 builds over its 32 keyword setters
on the wheel, on the evaluation date 15 January 2025 (one on Saturday 18 January): the default build at the par rate
on the index's own forecasting curve, a given fixed rate and forward starts, the type both ways, nominals, settlement
days and explicit effective and termination dates (a termination date clearing the tenor, so the fixed leg's default
tenor is inferred from the dates), every fixed-leg and floating-leg setter, the end-of-month flags of each leg and of
the maturity, from 31 January 2025 and from 30 April 2025, the end of a short month, where each flag moves a date of
its own (the maturity following the floating leg's flag by default), the par rate on a discounting term structure and
on a pricing engine (DiscountingSwapEngine with the settlement date's flows), indexed and at-par coupons and the
at-par switch, and the fixed leg's defaults for the index's currency (EUR, USD, CHF, SEK, GBP below and above a year,
JPY, AUD below and from four years, HKD, THB, and CAD, which QuantLib refuses); and QuantLib's refusals: settlement
days with an effective date, and no fixed rate with neither an engine nor a forecasting curve.

Each build's outputs are whether it builds, QuantLib's refusals recorded as its one throw; the swap's start and
maturity dates, both schedules' dates, type, nominal, fixed rate (the par rate where none was given), spread, day
counters and payment convention; and its results on the engine QuantLib's builder attaches (the one given, a
DiscountingSwapEngine on the discounting term structure, or one on the index's forecasting curve, which prices
nothing without a curve: "calculate" is then its throw): the NPV, the per-leg NPV and BPS, and the fair rate and
spread. NPVs are compared at the nominal and BPS at it times 1e-4.

The builds call QuantLib-Python's MakeVanillaSwap, a Python function that builds the C++ QuantLib::MakeVanillaSwap
through its deprecated four-argument constructor (the tenor, the index, the fixed rate or Null<Rate>(), the forward
start), which calls withFixedRate and withForwardStart, then calls each keyword's setter in the order given
(QuantLib-SWIG 1.44, SWIG/swap.i), and converts the builder to a VanillaSwap. Each case's inputs hold its settings
(with the at-par switch where the case sets it) and its "build": the tenor, the index (a spec of
instruments/fixedvsfloatingswap.py), the fixed rate (null for none), the forward start, and the setters as
[keyword, value] pairs in order, a value encoded as the vectors encode it (a period [length, units], a calendar or
day counter spec, a curve spec, or an engine {"type": "DiscountingSwapEngine", "curve",
"includeSettlementDateFlows"}). Read back by tests/NanoQL.Tests/Vectors/Instruments/MakeVanillaSwapVectorTests.cs."""
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

TODAY = fvf.TODAY
D = fvf.d
FORECAST = fvf.FORECAST
DISCOUNT = fvf.DISCOUNT
E6M = fvf.preset("Euribor6M", FORECAST)

# The setters whose values are periods, dates, calendars, day counters, curves and engines.
PERIODS = {"fixedLegTenor", "floatingLegTenor"}
DATES = {"effectiveDate", "terminationDate", "fixedLegFirstDate", "fixedLegNextToLastDate", "floatingLegFirstDate",
         "floatingLegNextToLastDate"}
CALENDARS = {"fixedLegCalendar", "floatingLegCalendar"}
DAY_COUNTERS = {"fixedLegDayCount", "floatingLegDayCount"}


def period(n, units):
    return [n, int(units)]


def engine_spec(curve, include=None):
    return {"type": "DiscountingSwapEngine", "curve": curve, "includeSettlementDateFlows": include}


def ibor(family, tenor, days, cal, dc, code, curve=FORECAST, convention=ql.ModifiedFollowing, eom=False):
    return fvf.with_currency(irx.ibor_spec(family, tenor, days, cal, convention, eom, dc, curve), code)


def build(tenor, index, fixed_rate=None, forward_start=(0, ql.Days), **setters):
    return {"swapTenor": period(*tenor), "index": index, "fixedRate": fixed_rate,
            "forwardStart": period(*forward_start), "setters": [[k, x] for k, x in setters.items()]}


def wheel_value(key, x):
    if key in PERIODS:
        return ql.Period(x[0], x[1])
    if key in DATES:
        return ql.Date(x)
    if key in CALENDARS:
        return irx.calendar(x)
    if key in DAY_COUNTERS:
        return irx.day_counter(x)
    if key == "discountingTermStructure":
        return irx.handle(x)
    if key == "pricingEngine":
        return ql.DiscountingSwapEngine(irx.handle(x["curve"]), x["includeSettlementDateFlows"])
    return x


def make(spec):
    kwargs = {k: wheel_value(k, x) for k, x in spec["setters"]}
    return ql.MakeVanillaSwap(ql.Period(*spec["swapTenor"]), fvf.make_index(spec["index"]), spec["fixedRate"],
                              ql.Period(*spec["forwardStart"]), **kwargs)


def builds():
    """(name, build spec, at-par switch or None, evaluation date) for the 51 builds."""
    usd = fvf.preset("Sofr", fvf.curve(0.043, fvf.A360))
    jpy = fvf.preset("Tibor", fvf.curve(0.006), ql.Period(6, ql.Months))
    gbp = ibor("GbpIbor", (6, ql.Months), 0, fvf.UK_EXCHANGE, fvf.A365, "GBP")
    chf = ibor("ChfIbor", (6, ql.Months), 2, fvf.TARGET, fvf.A360, "CHF", fvf.curve(0.005))
    sek = ibor("SekIbor", (3, ql.Months), 2, fvf.TARGET, fvf.A360, "SEK")
    aud = ibor("AudIbor", (6, ql.Months), 0, fvf.TARGET, fvf.A365, "AUD", fvf.curve(0.041))
    hkd = ibor("HkdIbor", (3, ql.Months), 0, fvf.TARGET, fvf.A365, "HKD")
    thb = ibor("ThbIbor", (6, ql.Months), 2, fvf.TARGET, fvf.A365, "THB")
    cad = ibor("CadIbor", (3, ql.Months), 0, fvf.TARGET, fvf.A365, "CAD")
    eom_start = D(31, 1, 2025)
    short_end = D(30, 4, 2025)
    out = []

    def add(name, spec, at_par=None, today=TODAY):
        out.append((name, spec, at_par, today))

    add("default, at the par rate", build((5, ql.Years), E6M))
    add("fixed rate", build((5, ql.Years), E6M, 0.03))
    add("forward start 1Y", build((5, ql.Years), E6M, None, (1, ql.Years)))
    add("negative forward start", build((2, ql.Years), E6M, 0.029, (-1, ql.Months)))
    add("receive fixed", build((5, ql.Years), E6M, 0.03, receiveFixed=True))
    add("receiver type, nominal", build((10, ql.Years), E6M, None, swapType=int(ql.Swap.Receiver), nominal=1e7))
    add("settlement days 0", build((3, ql.Years), E6M, 0.031, settlementDays=0))
    add("settlement days 3, forward start", build((3, ql.Years), E6M, None, (6, ql.Months), settlementDays=3))
    add("effective date", build((4, ql.Years), E6M, None, effectiveDate=v.enc(D(20, 2, 2025))))
    add("effective and termination dates", build((4, ql.Years), E6M, 0.03, effectiveDate=v.enc(D(20, 2, 2025)),
                                                 terminationDate=v.enc(D(20, 8, 2031))))
    add("termination date only", build((4, ql.Years), E6M, None, terminationDate=v.enc(D(17, 7, 2030))))
    add("forward rule, Preceding payments", build((5, ql.Years), E6M, None,
                                                  dateGenerationRule=int(ql.DateGeneration.Forward),
                                                  paymentConvention=int(ql.Preceding)))
    add("fixed-leg setters", build((7, ql.Years), E6M, None, fixedLegTenor=period(6, ql.Months),
                                   fixedLegCalendar=fvf.UK_EXCHANGE, fixedLegConvention=int(ql.Following),
                                   fixedLegTerminationDateConvention=int(ql.Unadjusted),
                                   fixedLegDateGenRule=int(ql.DateGeneration.Forward),
                                   fixedLegDayCount=fvf.A360))
    add("fixed-leg stubs", build((6, ql.Years), E6M, None, effectiveDate=v.enc(D(20, 2, 2025)),
                                 fixedLegFirstDate=v.enc(D(20, 8, 2025)), fixedLegNextToLastDate=v.enc(D(20, 8, 2030))))
    add("fixed leg end of month", build((5, ql.Years), E6M, None, effectiveDate=v.enc(eom_start),
                                        fixedLegEndOfMonth=True))
    add("floating-leg setters", build((7, ql.Years), E6M, None, floatingLegTenor=period(3, ql.Months),
                                      floatingLegCalendar=fvf.US_SOFR, floatingLegConvention=int(ql.Following),
                                      floatingLegTerminationDateConvention=int(ql.Preceding),
                                      floatingLegDateGenRule=int(ql.DateGeneration.Forward),
                                      floatingLegDayCount=fvf.A365, floatingLegSpread=0.0015))
    add("floating-leg stubs", build((6, ql.Years), E6M, None, effectiveDate=v.enc(D(20, 2, 2025)),
                                    floatingLegFirstDate=v.enc(D(20, 5, 2025)),
                                    floatingLegNextToLastDate=v.enc(D(20, 11, 2030))))
    add("floating leg end of month, maturity follows", build((5, ql.Years), E6M, None, effectiveDate=v.enc(eom_start),
                                                             floatingLegEndOfMonth=True))
    add("maturity end of month only", build((5, ql.Years), E6M, None, effectiveDate=v.enc(eom_start),
                                            maturityEndOfMonth=True))
    add("floating end of month, maturity not", build((5, ql.Years), E6M, None, effectiveDate=v.enc(eom_start),
                                                     floatingLegEndOfMonth=True, maturityEndOfMonth=False))
    # From the end of a short month, 30 April 2025, with periods ending in long ones, so that each flag moves a date:
    # 14 months to 30 June 2026, a month end the backward schedules roll from, with semiannual legs whose
    # 30 December 2025 becomes 31 December under a leg's flag; and 15 months to 30 July 2026, which the maturity's
    # flag, or the floating leg's it defaults to, moves to 31 July.
    six_months = period(6, ql.Months)
    for label, flags in (("no flag", {}), ("fixed leg end of month", {"fixedLegEndOfMonth": True}),
                         ("floating leg end of month", {"floatingLegEndOfMonth": True})):
        add(f"short month end, 14M, {label}", build((14, ql.Months), E6M, None, effectiveDate=v.enc(short_end),
                                                    fixedLegTenor=six_months, **flags))
    for label, flags in (("no flag", {}), ("maturity end of month", {"maturityEndOfMonth": True}),
                         ("floating leg end of month, maturity follows", {"floatingLegEndOfMonth": True}),
                         ("floating leg end of month, maturity not", {"floatingLegEndOfMonth": True,
                                                                      "maturityEndOfMonth": False})):
        add(f"short month end, 15M, {label}", build((15, ql.Months), E6M, None, effectiveDate=v.enc(short_end),
                                                    fixedLegTenor=six_months, **flags))
    add("discounting term structure", build((5, ql.Years), E6M, None, discountingTermStructure=DISCOUNT))
    add("pricing engine with settlement flows", build((5, ql.Years), E6M, None,
                                                      pricingEngine=engine_spec(DISCOUNT, True)))
    add("discounting term structure, then an engine", build((5, ql.Years), E6M, None, discountingTermStructure=DISCOUNT,
                                                            pricingEngine=engine_spec(FORECAST, False)))
    add("indexed coupons", build((5, ql.Years), E6M, None, withIndexedCoupons=True))
    add("at-par coupons off", build((5, ql.Years), E6M, None, atParCoupons=False))
    add("switch indexed", build((5, ql.Years), E6M, None), at_par=False)
    add("indexed, then at par", build((5, ql.Years), E6M, None, withIndexedCoupons=True, atParCoupons=True),
        at_par=False)
    add("USD", build((5, ql.Years), usd, None))
    add("JPY", build((5, ql.Years), jpy, None))
    add("GBP 1Y", build((1, ql.Years), gbp, None))
    add("GBP 5Y", build((5, ql.Years), gbp, None))
    add("GBP termination date over a year", build((6, ql.Months), gbp, 0.04, effectiveDate=v.enc(D(20, 1, 2025)),
                                                  terminationDate=v.enc(D(20, 1, 2030))))
    add("CHF and SEK", build((3, ql.Years), chf, None))
    add("SEK", build((2, ql.Years), sek, 0.025))
    add("AUD 3Y", build((3, ql.Years), aud, None))
    add("AUD 4Y", build((4, ql.Years), aud, None))
    add("HKD", build((2, ql.Years), hkd, 0.02))
    add("THB, fixed tenor given", build((2, ql.Years), thb, 0.02, fixedLegTenor=period(6, ql.Months)))
    add("CAD refused", build((5, ql.Years), cad, 0.03))
    add("settlement days with an effective date", build((5, ql.Years), E6M, 0.03, settlementDays=2,
                                                        effectiveDate=v.enc(D(20, 1, 2025))))
    add("no curve, no rate", build((5, ql.Years), fvf.preset("Euribor6M"), None))
    add("no curve, a rate", build((5, ql.Years), fvf.preset("Euribor6M"), 0.03))
    add("weekend evaluation date, settlement days", build((5, ql.Years), E6M, 0.03, settlementDays=2),
        today=D(18, 1, 2025))
    add("weekend evaluation date, the index's spot", build((5, ql.Years), E6M, 0.03), today=D(18, 1, 2025))
    return out


def engine_of(spec, index):
    """The engine QuantLib's builder attaches: the last one given, or one on the index's forecasting curve."""
    chosen = None
    for k, x in spec["setters"]:
        if k == "discountingTermStructure":
            chosen = ql.DiscountingSwapEngine(irx.handle(x), False)
        elif k == "pricingEngine":
            chosen = wheel_value(k, x)
    return chosen if chosen is not None else ql.DiscountingSwapEngine(index.forwardingTermStructure(), False)


def outputs(sw, scale):
    return {"startDate": sw.startDate().serialNumber(), "maturityDate": sw.maturityDate().serialNumber(),
            "fixedSchedule": fvf.schedule_dates(sw.fixedSchedule()),
            "floatingSchedule": fvf.schedule_dates(sw.floatingSchedule()),
            "type": int(sw.type()), "nominal": sw.nominal(), "fixedRate": sw.fixedRate(), "spread": sw.spread(),
            "fixedDayCount": sw.fixedDayCount().name(), "floatingDayCount": sw.floatingDayCount().name(),
            "paymentConvention": int(sw.paymentConvention())}


def cases():
    out = []
    for name, spec, at_par, today in builds():
        with v.case_settings(today, use_at_par_coupons=at_par) as st:
            inputs = {"settings": st, "build": spec}
            try:
                sw = make(spec)
            except RuntimeError:
                out.append({"name": name, "inputs": inputs, "expected": {"built": dict(v.THROWS)}})
                continue
            nominal = sw.nominal()
            scale = max(abs(nominal), 1.0)
            expected = {"built": True}
            expected.update(outputs(sw, scale))
            # The wheel's swap holds the engine its builder attached; it is set again here, as nanoql's test
            # prices the swap with it.
            sw.setPricingEngine(engine_of(spec, sw.iborIndex()))
            expected.update(fvf.results(sw, scale, accessors=False))
            out.append({"name": name, "inputs": inputs, "expected": expected})
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    for refused in ("CAD refused", "settlement days with an effective date", "no curve, no rate"):
        assert by_name[refused] == {"built": v.THROWS}, refused
    assert by_name["no curve, a rate"].get("calculate") == v.THROWS
    assert abs(by_name["default, at the par rate"]["NPV"]["value"]) < 1e-10
    assert by_name["GBP 1Y"]["fixedSchedule"] != by_name["GBP 5Y"]["fixedSchedule"]
    check_short_month_ends(by_name)


def check_short_month_ends(by_name):
    """Each flag moves the dates it sets from the end of a short month, and no other."""
    def dates(label):
        e = by_name[f"short month end, {label}"]
        return e["maturityDate"], e["fixedSchedule"], e["floatingSchedule"]

    dec30, dec31 = v.enc(D(30, 12, 2025)), v.enc(D(31, 12, 2025))
    maturity, fixed, floating = dates("14M, no flag")
    assert dec30 in fixed and dec30 in floating
    m, f, fl = dates("14M, fixed leg end of month")
    assert (m, fl) == (maturity, floating) and dec31 in f and dec30 not in f
    m, f, fl = dates("14M, floating leg end of month")
    assert (m, f) == (maturity, fixed) and dec31 in fl and dec30 not in fl
    maturity = dates("15M, no flag")[0]
    assert maturity == v.enc(D(30, 7, 2026))
    for label in ("maturity end of month", "floating leg end of month, maturity follows"):
        assert dates(f"15M, {label}")[0] == v.enc(D(31, 7, 2026)), label
    assert dates("15M, floating leg end of month, maturity not")[0] == maturity


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/makevanillaswap.hpp", cases)
