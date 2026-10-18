# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DepositRateHelper and SwapRateHelper (ql/termstructures/yield/ratehelpers.hpp), through the
BootstrapHelper interface they inherit (ql/termstructures/bootstraphelper.hpp): 103 helpers, each built on three
evaluation dates (Monday 15 January 2024; Thursday 29 February 2024, the end of a month; Saturday 30 March 2024, in
TARGET's Easter holidays), 309 cases.

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

Each case's outputs are whether the helper builds, QuantLib's refusals recorded as its one throw, then its five dates
(earliestDate, maturityDate, latestRelevantDate, pillarDate, latestDate), and for a swap helper its swap's start and
maturity dates and fixed rate, and its spread.

The functions here build the helper specs and the wheel's helpers that the bootstrap generators share
(termstructures/yield/piecewiseyieldcurve, termstructures/iterativebootstrap and their harness counterpart
cxx/termstructures/yield/ratehelpers, which import this file). A helper spec is one of
- {"kind": "DepositRateHelper", "form": "tenor", "rate", "tenor", "fixingDays", "calendar", "convention",
  "endOfMonth", "dayCounter"}, {"kind": "DepositRateHelper", "form": "index", "rate", "index"} or {"kind":
  "DepositRateHelper", "form": "fixingDate", "rate", "fixingDate", "index"};
- {"kind": "SwapRateHelper", "form": "tenor", "rate", "tenor", "calendar", "fixedFrequency", "fixedConvention",
  "fixedDayCount", "index", "spread", "fwdStart", "discountingCurve", "settlementDays", "pillar", "customPillarDate",
  "endOfMonth", "useIndexedCoupons", "floatConvention"}, or the same with "form": "dates", "startDate" and "endDate"
  for "tenor", and without "fwdStart" and "settlementDays";
a period is [length, units]; a calendar and a day counter are specs of indexes/interestrateindex.py, null for
QuantLib's empty one; an index is a spec of instruments/fixedvsfloatingswap.py; the discounting curve is null or a flat
forward spec; null settlement days are Null<Natural>(), a null custom pillar date Date(), and null coupon and floating
convention choices std::nullopt; enums are QuantLib's integers. The wheel's helpers take the quote as a float, but for
the fixing-date and dated forms, which take a QuoteHandle; a zero spread is an empty handle, which QuantLib reads as 0.
QuantLib-Python can't build an empty calendar or day counter: the empty ones are ql.Schedule().calendar() and
ql.InterestRate().dayCounter(). SwapRateHelper.forDates is a static the wheel adds, calling QuantLib's dated
constructor. Read back by tests/NanoQL.Tests/Vectors/TermStructures/Yield/RateHelpersVectorTests.cs, with
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
    raise ValueError(f"unknown helper {kind} {form}")


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


def outputs(h, spec):
    out = {"built": True}
    out.update(helper_dates(h))
    if spec["kind"] == "SwapRateHelper":
        sw = h.swap()
        out.update({"swapStartDate": v.enc(sw.startDate()), "swapMaturityDate": v.enc(sw.maturityDate()),
                    "swapFixedRate": sw.fixedRate(), "spread": h.spread()})
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


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/ratehelpers.hpp", cases)
