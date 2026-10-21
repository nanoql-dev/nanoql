# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ConstNotionalCrossCurrencySwap (ql/instruments/constnotionalcrosscurrencyswap.hpp), with its
base CrossCurrencySwap (ql/instruments/crosscurrencyswap.hpp), which the wheel doesn't bind: swaps on the evaluation
date 15 January 2025 built from a paid and a received leg and from legs, payer flags and currencies, of EUR and USD
fixed-rate and Ibor legs with notional exchanges, three legs, payment lags, a swap that has expired, and the
constructors' refusals: payer flags not matching the legs, or the currencies ("Size mismatch between payer (2) and
currencies (3)"); and pricings that refuse a leg in a currency the engine doesn't name, or QuantLib's empty currency.

Each case's outputs are whether the swap builds, QuantLib's refusal recorded as its throw; then its number of legs,
payer flags, leg currencies (codes), start and maturity dates and whether it has expired; each leg's payment dates and
amounts; and DiscountingConstNotionalCrossCurrencySwapEngine's results, USD domestic and EUR foreign at a spot of 1.1:
the NPV, the per-leg NPV and BPS, each in the NPV's currency and in the leg's own, the start and end discounts, or the
refused pricing, the one output "calculate". NPVs and amounts are compared at the largest notional, BPS at it times
1e-4. Left out (the S-rx plan's SRXR3 (xviii)): legs whose BPS is zero.

The functions here build the specs and the swaps every cross-currency generator shares (instruments/
constnotionalcrosscurrencybasisswap, constnotionalcrosscurrencyfixedvsfloatingswap, pricingengines/swap/
discountingconstnotionalcrosscurrencyswapengine, and experimental/fx's and experimental/termstructures' cross-currency
generators, which import this file):
- an index is instruments/fixedvsfloatingswap.py's preset spec, or {"type": "USDLibor" or "GBPLibor", "tenor",
  "curve"};
- a currency is a code, or null for QuantLib's empty currency;
- a stub index selection is null (QuantLib's empty one) or {"convention", "indices"}, the indices index specs;
- a leg is {"kind": "fixed", "schedule", "notional", "rate", "dayCounter", "paymentLag", "exchanges"} (QuantLib's
  FixedRateLeg with one notional and rate) or {"kind": "ibor", "schedule", "notional", "index", "spread",
  "dayCounter", "paymentLag", "exchanges"} (QuantLib's IborLeg with one notional and spread, the day counter its
  payment day counter); with "exchanges" its flows are a payment of the notional on the schedule's first date, the
  coupons, and its repayment on the last coupon's payment date, each a SimpleCashFlow;
- an engine is {"domesticCcy", "domesticCurve", "foreignCcy", "foreignCurve", "spotFX", "includeSettlementDateFlows",
  "settlementDate", "npvDate", "spotFXSettleDate"}, a null flag or date QuantLib's default (std::nullopt, Date()).

Each case's inputs hold its settings, "swap" ({"form": "pair" or "legs", "legs", "payer", "currencies"}) and "engine".
Read back by tests/NanoQL.Tests/Vectors/Instruments/ConstNotionalCrossCurrencySwapVectorTests.cs, with
tests/NanoQL.Tests/Vectors/CrossCurrencyInputs.cs, which documents the specs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_fixedvsfloatingswap", os.path.join(v.GEN_ROOT, "instruments", "fixedvsfloatingswap.py"))
fvf = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(fvf)
irx = fvf.irx

TODAY = fvf.TODAY
D = fvf.d
M, Y = int(ql.Months), int(ql.Years)
TARGET, A360, A365 = irx.TARGET, irx.A360, irx.A365
MF, F = int(ql.ModifiedFollowing), int(ql.Following)
SPOT = D(17, 1, 2025)

# The discount and forecast curves, flat.
EUR_DISCOUNT, USD_DISCOUNT, GBP_DISCOUNT = fvf.curve(0.025), fvf.curve(0.04), fvf.curve(0.035)
EUR_FORECAST, USD_FORECAST, GBP_FORECAST = fvf.curve(0.027), fvf.curve(0.043), fvf.curve(0.037)

E3M, E6M = fvf.preset("Euribor3M", EUR_FORECAST), fvf.preset("Euribor6M", EUR_FORECAST)
ESTR = fvf.preset("Estr", EUR_FORECAST)
SOFR, SONIA = fvf.preset("Sofr", USD_FORECAST), fvf.preset("Sonia", GBP_FORECAST)


def libor(kind, months, curve):
    return {"type": kind, "tenor": [months, M], "curve": curve}


USD3M, USD6M = libor("USDLibor", 3, USD_FORECAST), libor("USDLibor", 6, USD_FORECAST)
GBP3M = libor("GBPLibor", 3, GBP_FORECAST)


def make_index(spec):
    if spec["type"] == "USDLibor":
        return ql.USDLibor(ql.Period(*spec["tenor"]), irx.handle(spec["curve"]))
    if spec["type"] == "GBPLibor":
        return ql.GBPLibor(ql.Period(*spec["tenor"]), irx.handle(spec["curve"]))
    return fvf.make_index(spec)


def currency(code):
    return ql.Currency() if code is None else fvf.CURRENCIES[code]()


def stub(convention, indices):
    return {"convention": int(convention), "indices": indices}


def make_stub(spec):
    if spec is None:
        return ql.StubIndexSelection()
    return ql.StubIndexSelection(spec["convention"], [make_index(i) for i in spec["indices"]])


def sched(start, end, tenor, cal=TARGET, convention=ql.ModifiedFollowing):
    return fvf.sched(start, end, ql.Period(*tenor), cal, convention)


def fixed_leg(schedule, notional, rate, dc=A360, lag=0, exchanges=True):
    return {"kind": "fixed", "schedule": schedule, "notional": notional, "rate": rate, "dayCounter": dc,
            "paymentLag": lag, "exchanges": exchanges}


def ibor_leg(schedule, notional, index, spread=0.0, dc=A360, lag=0, exchanges=True):
    return {"kind": "ibor", "schedule": schedule, "notional": notional, "index": index, "spread": spread,
            "dayCounter": dc, "paymentLag": lag, "exchanges": exchanges}


def make_leg(spec):
    s = fvf.schedule(spec["schedule"])
    n = spec["notional"]
    if spec["kind"] == "fixed":
        coupons = list(ql.FixedRateLeg(s, irx.day_counter(spec["dayCounter"]), [n], [spec["rate"]],
                                       paymentLag=spec["paymentLag"]))
    else:
        coupons = list(ql.IborLeg([n], s, make_index(spec["index"]),
                                  paymentDayCounter=irx.day_counter(spec["dayCounter"]), spreads=[spec["spread"]],
                                  paymentLag=spec["paymentLag"]))
    if not spec["exchanges"]:
        return ql.Leg(coupons)
    return ql.Leg([ql.SimpleCashFlow(-n, s.startDate())] + coupons + [ql.SimpleCashFlow(n, coupons[-1].date())])


def engine(domestic="USD", domestic_curve=USD_DISCOUNT, foreign="EUR", foreign_curve=EUR_DISCOUNT, spot=1.1,
           include=None, settlement=None, npv=None, fx_settle=None):
    return {"domesticCcy": domestic, "domesticCurve": domestic_curve, "foreignCcy": foreign,
            "foreignCurve": foreign_curve, "spotFX": spot, "includeSettlementDateFlows": include,
            "settlementDate": v.enc(settlement), "npvDate": v.enc(npv), "spotFXSettleDate": v.enc(fx_settle)}


def date_or_null(serial):
    return ql.Date() if serial is None else ql.Date(serial)


def make_engine(spec, kind=ql.DiscountingConstNotionalCrossCurrencySwapEngine):
    return kind(currency(spec["domesticCcy"]), irx.handle(spec["domesticCurve"]), currency(spec["foreignCcy"]),
                irx.handle(spec["foreignCurve"]), ql.QuoteHandle(ql.SimpleQuote(spec["spotFX"])),
                spec["includeSettlementDateFlows"], date_or_null(spec["settlementDate"]),
                date_or_null(spec["npvDate"]), date_or_null(spec["spotFXSettleDate"]))


def code_or_null(c):
    return None if c.empty() else c.code()


def leg_outputs(prefix, leg, scale):
    return {prefix + "Dates": [cf.date().serialNumber() for cf in leg],
            prefix + "Amounts": [fvf.ibc.scaled(cf.amount, scale) for cf in leg]}


def results(sw, scale, legs, in_ccy_bps=True):
    """The cross-currency swap's results with the engine set on it, or the refused pricing, the one output
    "calculate"; without in_ccy_bps, the in-currency BPS, which the wheel's MtM swap doesn't bind, left out."""
    try:
        sw.NPV()
    except RuntimeError:
        return {"calculate": dict(v.THROWS)}
    r = v.result
    bps = scale * 1e-4
    out = {"NPV": v.amount(r(sw.NPV), scale),
           "legNPV": [v.amount(r(lambda j=j: sw.legNPV(j)), scale) for j in range(legs)],
           "legBPS": [v.amount(r(lambda j=j: sw.legBPS(j)), bps) for j in range(legs)],
           "inCcyLegNPV": [v.amount(r(lambda j=j: sw.inCcyLegNPV(j)), scale) for j in range(legs)]}
    if in_ccy_bps:
        out["inCcyLegBPS"] = [v.amount(r(lambda j=j: sw.inCcyLegBPS(j)), bps) for j in range(legs)]
    out.update({"startDiscounts": [r(lambda j=j: sw.startDiscounts(j)) for j in range(legs)],
                "endDiscounts": [r(lambda j=j: sw.endDiscounts(j)) for j in range(legs)]})
    return out


def swap_spec(form, legs, payer, currencies):
    return {"form": form, "legs": legs, "payer": payer, "currencies": currencies}


def make_swap(spec):
    legs = [make_leg(x) for x in spec["legs"]]
    if spec["form"] == "pair":
        c = spec["currencies"]
        return ql.ConstNotionalCrossCurrencySwap(legs[0], currency(c[0]), legs[1], currency(c[1]))
    return ql.ConstNotionalCrossCurrencySwap(legs, spec["payer"], [currency(c) for c in spec["currencies"]])


def swaps():
    """(name, swap, engine) for the cases."""
    two1, two3 = sched(SPOT, D(17, 1, 2027), (1, Y)), sched(SPOT, D(17, 1, 2027), (3, M))
    fwd1, fwd3 = sched(D(17, 7, 2025), D(17, 7, 2028), (1, Y)), sched(D(17, 7, 2025), D(17, 7, 2028), (3, M))
    five6, five3 = sched(SPOT, D(17, 1, 2030), (6, M)), sched(SPOT, D(17, 1, 2030), (3, M))
    old1, old3 = sched(D(15, 1, 2020), D(15, 1, 2022), (1, Y)), sched(D(15, 1, 2020), D(15, 1, 2022), (3, M))
    eur_fixed = fixed_leg(two1, 1e6, 0.03)
    usd_float = ibor_leg(two3, 1.1e6, USD3M)
    return [
        ("pair, EUR fixed paid against USD LIBOR 3M", swap_spec("pair", [eur_fixed, usd_float], [True, False],
                                                                ["EUR", "USD"]), engine()),
        ("pair, Euribor 3M plus a spread paid against USD fixed, forward start",
         swap_spec("pair", [ibor_leg(fwd3, 1e6, E3M, 0.001), fixed_leg(fwd1, 1.05e6, 0.041, A365)], [True, False],
                   ["EUR", "USD"]), engine()),
        ("legs, received EUR fixed and paid USD LIBOR 6M", swap_spec("legs", [
            fixed_leg(five6, 1e6, 0.028), ibor_leg(five6, 1.08e6, USD6M, -0.0005)], [False, True], ["EUR", "USD"]),
         engine()),
        ("legs, three: a EUR fee leg without exchanges", swap_spec("legs", [
            eur_fixed, usd_float, fixed_leg(two1, 1e6, 0.002, exchanges=False)], [True, False, True],
            ["EUR", "USD", "EUR"]), engine()),
        ("legs, payment lags of 2", swap_spec("legs", [
            fixed_leg(two1, 1e6, 0.03, lag=2), ibor_leg(two3, 1.1e6, USD3M, lag=2)], [True, False], ["EUR", "USD"]),
         engine()),
        ("legs, both in USD", swap_spec("legs", [fixed_leg(five3, 1e6, 0.04), ibor_leg(five3, 1e6, USD3M)],
                                        [True, False], ["USD", "USD"]), engine()),
        ("pair, GBP LIBOR against EUR fixed under a GBP engine", swap_spec("pair", [
            ibor_leg(two3, 0.86e6, GBP3M), fixed_leg(two1, 1e6, 0.03)], [True, False], ["GBP", "EUR"]),
         engine("GBP", GBP_DISCOUNT, "EUR", EUR_DISCOUNT, 0.86)),
        ("expired", swap_spec("pair", [fixed_leg(old1, 1e6, 0.01), fixed_leg(old1, 1.1e6, 0.02)], [True, False],
                              ["EUR", "USD"]), engine()),
        ("expired, an Ibor leg", swap_spec("pair", [fixed_leg(old1, 1e6, 0.01), ibor_leg(old3, 1.1e6, USD3M)],
                                           [True, False], ["EUR", "USD"]), engine()),
        ("a leg in a currency the engine doesn't name", swap_spec("pair", [eur_fixed, usd_float], [True, False],
                                                                   ["EUR", "GBP"]), engine()),
        ("a leg in the empty currency", swap_spec("legs", [eur_fixed, usd_float], [True, False], ["EUR", None]),
         engine()),
        ("payer flags not matching the currencies", swap_spec("legs", [eur_fixed, usd_float], [True, False],
                                                               ["EUR", "USD", "EUR"]), engine()),
        ("payer flags not matching the legs", swap_spec("legs", [eur_fixed, usd_float], [True, False, True],
                                                         ["EUR", "USD", "EUR"]), engine()),
    ]


def scale_of(legs):
    return max([abs(x["notional"]) for x in legs] + [1.0])


def cases():
    out = []
    for name, spec, engine_spec in swaps():
        scale = scale_of(spec["legs"])
        with v.case_settings(TODAY) as st:
            try:
                sw = make_swap(spec)
            except RuntimeError:
                expected = {"build": dict(v.THROWS)}
            else:
                n = sw.numberOfLegs()
                expected = {"numberOfLegs": n, "payer": [sw.payer(j) for j in range(n)],
                            "legCurrencies": [code_or_null(sw.legCurrency(j)) for j in range(n)],
                            "startDate": v.attempt(sw.startDate), "maturityDate": v.attempt(sw.maturityDate),
                            "isExpired": sw.isExpired()}
                for j in range(n):
                    expected.update(leg_outputs(f"leg{j + 1}", sw.leg(j), scale))
                sw.setPricingEngine(make_engine(engine_spec))
                expected.update(results(sw, scale, n))
            out.append({"name": name, "inputs": {"settings": st, "swap": spec, "engine": engine_spec},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/constnotionalcrosscurrencyswap.hpp", cases)
