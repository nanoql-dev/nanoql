# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SwapIndex and OvernightIndexedSwapIndex (ql/indexes/swapindex.hpp): 40 swap indexes on the
evaluation date 15 January 2025, a business day on every calendar.

- 30 swap indexes: tenors from 6M to 30Y (12M and 24M among them, which the name gives in years), 0 to 3 settlement
  days, the euro, the US dollar, the pound and QuantLib's empty currency, TARGET, the US SOFR, UK exchange and null
  calendars, fixed legs of 3M to 1Y over four conventions and three day counters, and Ibor indexes (the Euribor
  presets, and indexes of their own on each calendar, one of them on a calendar other than the swap index's), each on
  a flat forwarding curve, or none; 6 of them with exogenous discounting, on a curve or on QuantLib's empty handle.
- 10 overnight-indexed swap indexes on overnight indexes of their own (TARGET, US SOFR, UK exchange and null
  calendars), tenors from 1Y to 5Y, telescopic value dates or not, compounded or averaged.

Each index gives two cases, the first with at-par coupons, the second with indexed ones, each built on a fresh index
(QuantLib's caches its last swap, keyed by its fixing date only). On every third index fixings are stored on the past
dates that are valid fixing dates and on the evaluation date. The first case's outputs are the name, the family name,
the tenor ([length, units]), the fixing days, the fixed leg's tenor and convention, the day counter's, the fixing
calendar's and the Ibor index's names, and whether the forwarding and discounting curves are empty; fixingDate,
valueDate, maturityDate and fixing at 20 dates (3 past, the evaluation date, and later ones, weekend days and
holidays among them); the three clones (on another forwarding curve, on other forwarding and discounting curves, and
with a 3Y tenor): their names, their types (the wheel's clones are SwapIndex proxies) and their fixings at 5 later
dates, flattened clone by clone; and for an overnight-indexed swap index, the overnight index's name and its own
underlying swap at 3 dates, QuantLib's OvernightIndexedSwap: its start and maturity dates, and its fair rate on
MakeOIS's default engine, DiscountingSwapEngine on the overnight index's curve without the settlement date's flows.
The second case's outputs are the fixings at the 20 dates. A refused value is {"throws": true}: a value or maturity
date for a date that isn't a valid fixing date, a past fixing that isn't stored, and a forecast with no curve to
forecast or discount on. Each case's inputs hold the index ("index") and the 20 dates ("dates"); the first case adds
the clones' curves ("cloneForwarding", "cloneDiscounting") and dates ("cloneDates"), and for an overnight-indexed swap
index the underlying swaps' fixing dates ("swapDates").

The functions here build the specs and the indexes the swap-index generators share (cxx/indexes/swapindex, which
imports this file). A swap index is {"type": "SwapIndex", "familyName", "tenor", "settlementDays", "currency",
"fixingCalendar", "fixedLegTenor", "fixedLegConvention", "fixedLegDayCounter", "iborIndex", "exogenous",
"discountingTermStructure"}, built with QuantLib's constructor taking a discounting curve when "exogenous" is set; or
{"type": "OvernightIndexedSwapIndex", "familyName", "tenor", "settlementDays", "currency", "overnightIndex",
"telescopicValueDates", "averagingMethod"}. A period is [length, units]; a currency a code ("EUR", "USD", "GBP") or
null for QuantLib's empty one; calendars, day counters, curves and the overnight index are specs of
indexes/interestrateindex.py, and the Ibor index a spec of instruments/fixedvsfloatingswap.py. Read back by
tests/NanoQL.Tests/Vectors/Indexes/SwapIndexVectorTests.cs, with tests/NanoQL.Tests/Vectors/SwapIndexInputs.cs, which
documents the specs."""
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

TODAY = irx.TODAY
D = fvf.d
TARGET, NULL_CALENDAR, US_SOFR, UK_EXCHANGE = irx.TARGET, irx.NULL_CALENDAR, irx.US_SOFR, irx.UK_EXCHANGE
A360, A365, THIRTY360_BB = irx.A360, irx.A365, irx.THIRTY360_BB
Y, M = ql.Years, ql.Months
MF, F, P, U = ql.ModifiedFollowing, ql.Following, ql.Preceding, ql.Unadjusted

# 3 past dates (Thursday 2, Friday 10 and Monday 13 January 2025), the evaluation date, and later dates: Saturday 18
# January, Monday 20 January (a US holiday), month ends, Good Friday and Easter Monday (TARGET and UK holidays), 26 May
# (a US and UK holiday), Christmas Eve and New Year's Eve, and dates up to February 2028.
DATES = [D(2, 1, 2025), D(10, 1, 2025), D(13, 1, 2025), TODAY, D(16, 1, 2025), D(17, 1, 2025), D(18, 1, 2025),
         D(20, 1, 2025), D(31, 1, 2025), D(28, 2, 2025), D(18, 4, 2025), D(21, 4, 2025), D(26, 5, 2025),
         D(30, 6, 2025), D(15, 9, 2025), D(24, 12, 2025), D(31, 12, 2025), D(30, 1, 2026), D(15, 6, 2026),
         D(28, 2, 2028)]
# The clones' dates, valid fixing dates on every calendar.
CLONE_DATES = [D(16, 1, 2025), D(31, 1, 2025), D(30, 6, 2025), D(15, 9, 2025), D(30, 1, 2026)]
# The overnight-indexed swaps' fixing dates.
SWAP_DATES = [D(16, 1, 2025), D(31, 1, 2025), D(30, 6, 2025)]

C1 = fvf.curve(0.031)
C2 = fvf.curve(0.0475, A360, ql.Simple)
C3 = fvf.curve(-0.0035, A365, ql.Compounded, ql.Quarterly)
C4 = fvf.curve(0.02, A365, ql.Compounded, ql.Semiannual)
DISCOUNT_1 = fvf.curve(0.027)
DISCOUNT_2 = fvf.curve(0.015, A360, ql.Simple)
CLONE_FORWARDING = fvf.curve(0.05, A360, ql.Compounded, ql.Semiannual)
CLONE_DISCOUNTING = fvf.curve(0.022)


def usd_ibor(curve):
    return fvf.with_currency(irx.ibor_spec("UsdIbor", (3, M), 2, US_SOFR, MF, True, A360, curve), "USD")


def gbp_ibor(curve):
    return irx.ibor_spec("GbpIbor", (6, M), 0, UK_EXCHANGE, F, False, A365, curve)


def nul_ibor(curve):
    return irx.ibor_spec("NulIbor", (6, M), 1, NULL_CALENDAR, MF, False, A360, curve)


def period(p):
    return [p[0], int(p[1])]


def swap_index(family, tenor, days, currency, cal, fixed_tenor, convention, dc, ibor, exogenous=False, discount=None):
    return {"type": "SwapIndex", "familyName": family, "tenor": period(tenor), "settlementDays": days,
            "currency": currency, "fixingCalendar": cal, "fixedLegTenor": period(fixed_tenor),
            "fixedLegConvention": int(convention), "fixedLegDayCounter": dc, "iborIndex": ibor,
            "exogenous": exogenous, "discountingTermStructure": discount}


def ois_index(family, tenor, days, currency, overnight, telescopic=False, averaging=ql.RateAveraging.Compound):
    return {"type": "OvernightIndexedSwapIndex", "familyName": family, "tenor": period(tenor), "settlementDays": days,
            "currency": currency, "overnightIndex": overnight, "telescopicValueDates": telescopic,
            "averagingMethod": int(averaging)}


def empty(handle):
    """Whether a handle is empty: the wheel binds no empty(), and currentLink() refuses an empty handle."""
    try:
        handle.currentLink()
    except RuntimeError:
        return True
    return False


def currency(code):
    return ql.Currency() if code is None else fvf.CURRENCIES[code]()


def make_swap_index(spec):
    """The wheel's swap index for a spec, its positional constructors being QuantLib's."""
    tenor = ql.Period(*spec["tenor"])
    if spec["type"] == "SwapIndex":
        args = [spec["familyName"], tenor, spec["settlementDays"], currency(spec["currency"]),
                irx.calendar(spec["fixingCalendar"]), ql.Period(*spec["fixedLegTenor"]), spec["fixedLegConvention"],
                irx.day_counter(spec["fixedLegDayCounter"]), fvf.make_index(spec["iborIndex"])]
        if spec["exogenous"]:
            args.append(irx.handle(spec["discountingTermStructure"]))
        return ql.SwapIndex(*args)
    if spec["type"] == "OvernightIndexedSwapIndex":
        return ql.OvernightIndexedSwapIndex(spec["familyName"], tenor, spec["settlementDays"],
                                            currency(spec["currency"]), irx.make_index(spec["overnightIndex"]),
                                            spec["telescopicValueDates"], spec["averagingMethod"])
    raise ValueError(f"unknown swap index {spec['type']}")


def indexes():
    """(name, spec) for the 40 indexes."""
    E3M, E6M, E1Y = fvf.preset("Euribor3M", C1), fvf.preset("Euribor6M", C1), fvf.preset("Euribor1Y", C4)
    E1M = fvf.preset("Euribor", C1, ql.Period(1, M))
    bare = irx.ibor_spec("Bare", (6, M), 2, TARGET, MF, False, A360, None)
    out = [
        ("1Y on Euribor 3M", swap_index("EurSwap", (1, Y), 2, "EUR", TARGET, (1, Y), MF, THIRTY360_BB, E3M)),
        ("2Y on Euribor 6M", swap_index("EurSwap", (2, Y), 2, "EUR", TARGET, (1, Y), MF, THIRTY360_BB, E6M)),
        ("5Y on Euribor 6M", swap_index("EurSwap", (5, Y), 2, "EUR", TARGET, (1, Y), MF, THIRTY360_BB, E6M)),
        ("10Y, simple curve", swap_index("EurSwap", (10, Y), 2, "EUR", TARGET, (1, Y), MF, THIRTY360_BB,
                                         fvf.preset("Euribor6M", C2))),
        ("30Y, negative curve", swap_index("EurSwap", (30, Y), 2, "EUR", TARGET, (1, Y), MF, THIRTY360_BB,
                                           fvf.preset("Euribor6M", C3))),
        ("18M, semiannual fixed leg", swap_index("EurSwap", (18, M), 2, "EUR", TARGET, (6, M), MF, THIRTY360_BB,
                                                 E6M)),
        ("24M", swap_index("EurSwap", (24, M), 2, "EUR", TARGET, (1, Y), MF, THIRTY360_BB,
                           fvf.preset("Euribor6M", C4))),
        ("12M on Euribor 3M", swap_index("EurSwap", (12, M), 2, "EUR", TARGET, (1, Y), MF, THIRTY360_BB,
                                         fvf.preset("Euribor3M", C2))),
        ("5Y exogenous", swap_index("EurSwap", (5, Y), 2, "EUR", TARGET, (1, Y), MF, THIRTY360_BB, E6M, True,
                                    DISCOUNT_1)),
        ("10Y exogenous, simple", swap_index("EurSwap", (10, Y), 2, "EUR", TARGET, (1, Y), MF, THIRTY360_BB, E6M,
                                             True, DISCOUNT_2)),
        ("5Y exogenous on an empty handle", swap_index("EurSwap", (5, Y), 2, "EUR", TARGET, (1, Y), MF, THIRTY360_BB,
                                                       E6M, True, None)),
        ("7Y, no settlement days", swap_index("EurSwap", (7, Y), 0, "EUR", TARGET, (1, Y), MF, THIRTY360_BB, E6M)),
        ("3Y, one settlement day, Following", swap_index("EurSwap", (3, Y), 1, "EUR", TARGET, (1, Y), F,
                                                         THIRTY360_BB, E6M)),
        ("4Y, three settlement days, Unadjusted", swap_index("EurSwap", (4, Y), 3, "EUR", TARGET, (1, Y), U,
                                                             THIRTY360_BB, E6M)),
        ("6Y, Preceding, Actual/360", swap_index("EurSwap", (6, Y), 2, "EUR", TARGET, (1, Y), P, A360, E6M)),
        ("8Y, semiannual Actual/365", swap_index("EurSwap", (8, Y), 2, "EUR", TARGET, (6, M), MF, A365, E6M)),
        ("5Y US dollar", swap_index("UsdSwap", (5, Y), 2, "USD", US_SOFR, (6, M), MF, A360, usd_ibor(C2))),
        ("10Y US dollar exogenous", swap_index("UsdSwap", (10, Y), 2, "USD", US_SOFR, (1, Y), MF, A360, usd_ibor(C2),
                                               True, DISCOUNT_1)),
        ("2Y US dollar, quarterly", swap_index("UsdSwap", (2, Y), 2, "USD", US_SOFR, (3, M), F, THIRTY360_BB,
                                               usd_ibor(C4))),
        ("5Y pound", swap_index("GbpSwap", (5, Y), 0, "GBP", UK_EXCHANGE, (1, Y), MF, A365, gbp_ibor(C3))),
        ("1Y pound, quarterly", swap_index("GbpSwap", (1, Y), 0, "GBP", UK_EXCHANGE, (3, M), MF, A365, gbp_ibor(C1))),
        ("5Y null calendar, unadjusted", swap_index("NulSwap", (5, Y), 1, None, NULL_CALENDAR, (1, Y), U,
                                                    THIRTY360_BB, nul_ibor(C1))),
        ("3Y null calendar exogenous", swap_index("NulSwap", (3, Y), 2, None, NULL_CALENDAR, (6, M), MF, A360,
                                                  nul_ibor(C2), True, DISCOUNT_2)),
        ("5Y on Euribor 1Y", swap_index("EurSwap", (5, Y), 2, "EUR", TARGET, (1, Y), MF, THIRTY360_BB, E1Y)),
        ("5Y on Euribor 1M", swap_index("EurSwap", (5, Y), 2, "EUR", TARGET, (1, Y), MF, THIRTY360_BB, E1M)),
        ("5Y on an index with no curve", swap_index("EurSwap", (5, Y), 2, "EUR", TARGET, (1, Y), MF, THIRTY360_BB,
                                                    bare)),
        ("5Y exogenous, on an index with no curve", swap_index("EurSwap", (5, Y), 2, "EUR", TARGET, (1, Y), MF,
                                                               THIRTY360_BB, bare, True, DISCOUNT_1)),
        ("15Y on the US SOFR calendar, Euribor 6M", swap_index("MixSwap", (15, Y), 2, "EUR", US_SOFR, (1, Y), MF,
                                                               THIRTY360_BB, E6M)),
        ("20Y on the UK calendar", swap_index("MixSwap", (20, Y), 2, "EUR", UK_EXCHANGE, (1, Y), MF, THIRTY360_BB,
                                              fvf.preset("Euribor6M", C2))),
        ("6M, semiannual", swap_index("EurSwap", (6, M), 2, "EUR", TARGET, (6, M), MF, THIRTY360_BB, E3M)),
    ]
    estr = irx.overnight_spec("Estr", 0, TARGET, A360, C1)
    sofr = irx.overnight_spec("Sofr", 0, US_SOFR, A360, C2)
    sonia = irx.overnight_spec("Sonia", 0, UK_EXCHANGE, A365, C3)
    nul = irx.overnight_spec("Nul", 1, NULL_CALENDAR, THIRTY360_BB, C4)
    out += [
        ("overnight 1Y", ois_index("EstrSwap", (1, Y), 2, "EUR", estr)),
        ("overnight 2Y", ois_index("EstrSwap", (2, Y), 2, "EUR", estr)),
        ("overnight 5Y telescopic", ois_index("EstrSwap", (5, Y), 2, "EUR", estr, True)),
        ("overnight 3Y averaged", ois_index("EstrSwap", (3, Y), 1, "EUR", estr, False, ql.RateAveraging.Simple)),
        ("overnight US 1Y", ois_index("SofrSwap", (1, Y), 2, "USD", sofr)),
        ("overnight US 5Y telescopic, averaged", ois_index("SofrSwap", (5, Y), 2, "USD", sofr, True,
                                                           ql.RateAveraging.Simple)),
        ("overnight UK 1Y", ois_index("SoniaSwap", (1, Y), 0, "GBP", sonia)),
        ("overnight UK 2Y", ois_index("SoniaSwap", (2, Y), 0, "GBP", sonia)),
        ("overnight null calendar 1Y", ois_index("NulSwap", (1, Y), 1, None, nul)),
        ("overnight 18M", ois_index("EstrSwap", (18, M), 3, "EUR", estr)),
    ]
    assert len(out) == 40
    return out


def stored_fixings(index, k):
    """The fixings stored for the k-th index: on every third index, the past dates that are valid fixing dates and the
    evaluation date."""
    if k % 3 != 0:
        return None
    dates = [d for d in DATES if d <= TODAY and index.isValidFixingDate(d)]
    return [(index, [(d, round(0.021 + 0.0007 * i + 0.0003 * k, 7)) for i, d in enumerate(dates)])]


def first_case(spec, idx):
    clones = [idx.clone(irx.handle(CLONE_FORWARDING)),
              idx.clone(irx.handle(CLONE_FORWARDING), irx.handle(CLONE_DISCOUNTING)),
              idx.clone(ql.Period(3, Y))]
    out = {
        "name": idx.name(),
        "familyName": idx.familyName(),
        "tenor": [idx.tenor().length(), int(idx.tenor().units())],
        "fixingDays": idx.fixingDays(),
        "fixedLegTenor": [idx.fixedLegTenor().length(), int(idx.fixedLegTenor().units())],
        "fixedLegConvention": int(idx.fixedLegConvention()),
        "dayCounter": idx.dayCounter().name(),
        "fixingCalendar": idx.fixingCalendar().name(),
        "iborIndex": idx.iborIndex().name(),
        "forwardingTermStructureEmpty": empty(idx.forwardingTermStructure()),
        "discountingTermStructureEmpty": empty(idx.discountingTermStructure()),
        "fixingDate": [v.attempt(lambda d=d: idx.fixingDate(d)) for d in DATES],
        "valueDate": [v.attempt(lambda d=d: idx.valueDate(d)) for d in DATES],
        "maturityDate": [v.attempt(lambda d=d: idx.maturityDate(d)) for d in DATES],
        "fixing": [v.attempt(lambda d=d: idx.fixing(d)) for d in DATES],
        "cloneNames": [c.name() for c in clones],
        "cloneTypes": [type(c).__name__ for c in clones],
        "cloneFixings": [v.attempt(lambda c=c, d=d: c.fixing(d)) for c in clones for d in CLONE_DATES],
    }
    if spec["type"] == "OvernightIndexedSwapIndex":
        swaps = [idx.underlyingSwap(d) for d in SWAP_DATES]
        out.update({
            "overnightIndex": idx.overnightIndex().name(),
            "underlyingStartDate": [v.enc(s.startDate()) for s in swaps],
            "underlyingMaturityDate": [v.enc(s.maturityDate()) for s in swaps],
            "underlyingFairRate": [v.attempt(s.fairRate) for s in swaps],
        })
    return out


def cases():
    out = []
    for k, (name, spec) in enumerate(indexes()):
        for at_par in (True, False):
            # A fresh index for each setting: QuantLib's caches its last swap, whose coupons keep the switch's choice.
            idx = make_swap_index(spec)
            with v.case_settings(TODAY, use_at_par_coupons=at_par, fixings=stored_fixings(idx, k)) as st:
                inputs = {"settings": st, "index": spec, "dates": v.enc(DATES)}
                if at_par:
                    expected = first_case(spec, idx)
                    inputs.update({"cloneForwarding": CLONE_FORWARDING, "cloneDiscounting": CLONE_DISCOUNTING,
                                   "cloneDates": v.enc(CLONE_DATES)})
                    if spec["type"] == "OvernightIndexedSwapIndex":
                        inputs["swapDates"] = v.enc(SWAP_DATES)
                else:
                    expected = {"fixing": [v.attempt(lambda d=d: idx.fixing(d)) for d in DATES]}
                out.append({"name": f"{name} / {k} / {'at par' if at_par else 'indexed'}",
                            "inputs": inputs, "expected": expected})
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    # The overnight-indexed swap index forecasts from the vanilla swap, not from its own swap (Q2).
    first = by_name["overnight 1Y / 30 / at par"]
    assert first["fixing"][4] != first["underlyingFairRate"][0], first["fixing"][4]
    # The at-par switch reaches the forecasts.
    at_par, indexed = by_name["5Y on Euribor 6M / 2 / at par"], by_name["5Y on Euribor 6M / 2 / indexed"]
    assert at_par["fixing"][4] != indexed["fixing"][4]
    # An exogenous index on QuantLib's empty handle refuses a forecast.
    assert by_name["5Y exogenous on an empty handle / 10 / at par"]["fixing"][4] == v.THROWS


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/swapindex.hpp", cases)
