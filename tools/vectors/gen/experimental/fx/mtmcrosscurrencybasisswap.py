# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MtMCrossCurrencyBasisSwap (ql/experimental/fx/mtmcrosscurrencybasisswap.hpp): mark-to-market
cross-currency basis swaps on the evaluation date 15 January 2025, EUR (the FX base currency) against USD: paying and
receiving the base currency, either leg resettable, spreads and gearings, FX resets two days before each period on
TARGET and on the schedule's calendar (an empty calendar resolved), payment lags and conventions, SOFR compounded with
a lookback and a lockout against ESTR averaged, indexed coupons with an interpolated stub index, a seasoned swap whose
first reset and fixings are past, a swap that has expired, and the refusals of a zero constant-leg notional and of a
leg in a currency the engine doesn't name.

Each case's outputs are whether the swap builds, QuantLib's refusal recorded as its throw; then its inspectors (type,
whether it pays the base currency, each leg's nominal, currency code, spread, gearing and index name, the paid and
received legs' nominals, currencies, spreads, gearings, index names and schedules' dates, the resettable and constant
legs' indexes, the constant leg's notional, the reset fixing days and calendar, and the payment conventions); each leg's
payment dates, which of its flows are FX-reset coupons and notional exchanges, the coupons' reset dates, and its
amounts, the resettable leg's refused until a pricer is set; and DiscountingMtMCrossCurrencyBasisSwapEngine's results,
USD domestic and EUR foreign at a spot of 1.1: those of instruments/constnotionalcrosscurrencyswap.py but the
in-currency BPS, which the wheel doesn't bind, the fair base and quote spreads, the fair pay and receive spreads, and
the resettable coupons' FX rates and notionals. NPVs and amounts are compared at the largest notional, BPS at it times
1e-4.

Each case's inputs hold its settings (with the past FX fixings the case reads), "swap" ({"type", "fxBaseNominal",
"fxBaseCurrency", "fxBaseSchedule", "fxBaseIndex", "fxBaseSpread", "fxBaseGearing", the same six for "fxQuote",
"isFxBaseCurrencyLegResettable", "fxResetFixingDays", "fxResetFixingCalendar", "fxBasePaymentLag", "fxQuotePaymentLag",
"fxBasePaymentConvention", "fxQuotePaymentConvention", "fxBaseCompoundSpread", "fxBaseLookbackDays",
"fxBaseObservationShift", "fxBaseLockoutDays", "fxBaseAveragingMethod", the same five for "fxQuote",
"useIndexedCoupons", "fxBaseStubIndexSelection", "fxQuoteStubIndexSelection"}, a null calendar QuantLib's empty one, a
null lookback or coupon choice QuantLib's default) and "engine", in instruments/constnotionalcrosscurrencyswap.py's
specs. The wheel's constructor takes the arguments after the resettable flag by name. Left out: telescopic value dates,
which nanoql builds in full (tests/NanoQL.Tests/Divergences/TelescopicValueDatesTests.cs); and the fair spreads of a leg
compounding its spread (its fair base or quote spread and its fair pay or receive spread), which nanoql solves on the
swap's pricing where QuantLib's BPS route is inexact
(tests/NanoQL.Tests/Divergences/CrossCurrencySwapCompoundedSpreadFairSpreadTests.cs). Read back by
tests/NanoQL.Tests/Vectors/Experimental/FX/MtMCrossCurrencyBasisSwapVectorTests.cs."""
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
    "wheel_constnotionalcrosscurrencyswap",
    os.path.join(v.GEN_ROOT, "instruments", "constnotionalcrosscurrencyswap.py"))
xcs = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(xcs)
fvf, irx = xcs.fvf, xcs.irx

TODAY, D, M, Y = xcs.TODAY, xcs.D, xcs.M, xcs.Y
COMPOUND, SIMPLE = int(ql.RateAveraging.Compound), int(ql.RateAveraging.Simple)
PAY = int(ql.MtMCrossCurrencyBasisSwap.PayFxBaseCurrency)
RECEIVE = int(ql.MtMCrossCurrencyBasisSwap.ReceiveFxBaseCurrency)
F, MF, P = int(ql.Following), int(ql.ModifiedFollowing), int(ql.Preceding)
US = {"type": "UnitedStates", "market": int(ql.UnitedStates.Settlement)}
E1M = fvf.preset("Euribor1M", xcs.EUR_FORECAST)
EUR, USD = ql.EURCurrency(), ql.USDCurrency()


def swap(swap_type, base, quote, base_resettable, fixing_days=0, calendar=None, lags=(0, 0), conventions=(F, F),
         base_on=None, quote_on=None, indexed=None, base_stub=None, quote_stub=None):
    """base and quote are (nominal, currency, schedule, index, spread, gearing); an overnight leg's options are
    (compoundSpread, lookbackDays, observationShift, lockoutDays, averagingMethod)."""
    on = (False, None, False, 0, COMPOUND)
    out = {"type": swap_type}
    for prefix, (nominal, ccy, schedule, index, spread, gearing) in (("fxBase", base), ("fxQuote", quote)):
        out.update({prefix + "Nominal": nominal, prefix + "Currency": ccy, prefix + "Schedule": schedule,
                    prefix + "Index": index, prefix + "Spread": spread, prefix + "Gearing": gearing})
    out.update({"isFxBaseCurrencyLegResettable": base_resettable, "fxResetFixingDays": fixing_days,
                "fxResetFixingCalendar": calendar, "fxBasePaymentLag": lags[0], "fxQuotePaymentLag": lags[1],
                "fxBasePaymentConvention": conventions[0], "fxQuotePaymentConvention": conventions[1]})
    for prefix, options in (("fxBase", base_on or on), ("fxQuote", quote_on or on)):
        out.update({prefix + "CompoundSpread": options[0], prefix + "LookbackDays": options[1],
                    prefix + "ObservationShift": options[2], prefix + "LockoutDays": options[3],
                    prefix + "AveragingMethod": options[4]})
    out.update({"useIndexedCoupons": indexed, "fxBaseStubIndexSelection": base_stub,
                "fxQuoteStubIndexSelection": quote_stub})
    return out


def make_swap(spec):
    args = [spec["type"]]
    for prefix in ("fxBase", "fxQuote"):
        args += [spec[prefix + "Nominal"], xcs.currency(spec[prefix + "Currency"]),
                 fvf.schedule(spec[prefix + "Schedule"]), xcs.make_index(spec[prefix + "Index"]),
                 spec[prefix + "Spread"], spec[prefix + "Gearing"]]
    args.append(spec["isFxBaseCurrencyLegResettable"])
    kw = {"fxResetFixingDays": spec["fxResetFixingDays"], "fxBasePaymentLag": spec["fxBasePaymentLag"],
          "fxQuotePaymentLag": spec["fxQuotePaymentLag"], "fxBasePaymentConvention": spec["fxBasePaymentConvention"],
          "fxQuotePaymentConvention": spec["fxQuotePaymentConvention"], "useIndexedCoupons": spec["useIndexedCoupons"],
          "fxBaseStubIndexSelection": xcs.make_stub(spec["fxBaseStubIndexSelection"]),
          "fxQuoteStubIndexSelection": xcs.make_stub(spec["fxQuoteStubIndexSelection"])}
    if spec["fxResetFixingCalendar"] is not None:
        kw["fxResetFixingCalendar"] = irx.calendar(spec["fxResetFixingCalendar"])
    for prefix in ("fxBase", "fxQuote"):
        kw.update({prefix + "CompoundSpread": spec[prefix + "CompoundSpread"],
                   prefix + "ObservationShift": spec[prefix + "ObservationShift"],
                   prefix + "LockoutDays": spec[prefix + "LockoutDays"],
                   prefix + "AveragingMethod": spec[prefix + "AveragingMethod"]})
        if spec[prefix + "LookbackDays"] is not None:
            kw[prefix + "LookbackDays"] = spec[prefix + "LookbackDays"]
    return ql.MtMCrossCurrencyBasisSwap(*args, **kw)


def scale_of(spec):
    return max(abs(spec["fxBaseNominal"]), abs(spec["fxQuoteNominal"]), 1.0)


def make_engine(spec):
    return xcs.make_engine(spec, ql.DiscountingMtMCrossCurrencyBasisSwapEngine)


def results(sw, scale):
    """The MtM swap's results with the engine set on it, or the refused pricing, the one output "calculate"."""
    out = xcs.results(sw, scale, 2, in_ccy_bps=False)
    if "calculate" in out:
        return out
    r = v.result
    out.update({"fairFxBaseSpread": r(sw.fairFxBaseSpread), "fairFxQuoteSpread": r(sw.fairFxQuoteSpread),
                "fairPaySpread": r(sw.fairPaySpread), "fairRecSpread": r(sw.fairRecSpread),
                "fxResetRates": list(sw.fxResetRates()),
                "fxResetNotionals": [v.amount(x, scale) for x in sw.fxResetNotionals()]})
    return out


def leg_outputs(prefix, leg, scale):
    resets = [ql.as_fx_reset_coupon(cf) for cf in leg]
    out = xcs.leg_outputs(prefix, leg, scale)
    out.update({prefix + "FxResetCoupons": [c is not None for c in resets],
                prefix + "FxResetExchanges": [ql.as_fx_reset_notional_exchange(cf) is not None for cf in leg],
                prefix + "FxResetDates": [None if c is None else c.fxResetDate().serialNumber() for c in resets],
                prefix + "FxResetValueDates": [None if c is None else c.fxResetValueDate().serialNumber()
                                               for c in resets]})
    return out


def inspectors(sw):
    out = {"type": int(sw.type()), "paysFxBaseCurrency": sw.paysFxBaseCurrency()}
    for prefix, nominal, ccy, spread, gearing, index in (
            ("fxBase", sw.fxBaseNominal, sw.fxBaseCurrency, sw.fxBaseSpread, sw.fxBaseGearing, sw.fxBaseIndex),
            ("fxQuote", sw.fxQuoteNominal, sw.fxQuoteCurrency, sw.fxQuoteSpread, sw.fxQuoteGearing, sw.fxQuoteIndex)):
        out.update({prefix + "Nominal": nominal(), prefix + "Currency": xcs.code_or_null(ccy()),
                    prefix + "Spread": spread(), prefix + "Gearing": gearing(), prefix + "IndexName": index().name()})
    cal = sw.fxResetFixingCalendar()
    out.update({"payNominal": sw.payNominal(), "payCurrency": xcs.code_or_null(sw.payCurrency()),
                "paySpread": sw.paySpread(), "payGearing": sw.payGearing(), "payIndexName": sw.payIndex().name(),
                "paySchedule": fvf.schedule_dates(sw.paySchedule()), "recNominal": sw.recNominal(),
                "recCurrency": xcs.code_or_null(sw.recCurrency()), "recSpread": sw.recSpread(),
                "recGearing": sw.recGearing(), "recIndexName": sw.recIndex().name(),
                "recSchedule": fvf.schedule_dates(sw.recSchedule()),
                "isFxBaseCurrencyLegResettable": sw.isFxBaseCurrencyLegResettable(),
                "resettingLegIndex": sw.resettingLegIndex(), "constantLegIndex": sw.constantLegIndex(),
                "constantLegNotional": sw.constantLegNotional(), "fxResetFixingDays": sw.fxResetFixingDays(),
                "fxResetFixingCalendar": None if cal.empty() else cal.name(),
                "fxBasePaymentConvention": int(sw.fxBasePaymentConvention()),
                "fxQuotePaymentConvention": int(sw.fxQuotePaymentConvention())})
    return out


def swaps():
    """(name, swap, engine, fixings, exchange rates) for the cases."""
    spot = xcs.SPOT
    two3 = xcs.sched(spot, D(17, 1, 2027), (3, M))
    one1, one3 = xcs.sched(spot, D(17, 1, 2026), (1, Y)), xcs.sched(spot, D(17, 1, 2026), (3, M))
    stub3 = xcs.sched(spot, D(17, 12, 2025), (3, M))
    seasoned = xcs.sched(D(17, 10, 2024), D(17, 10, 2025), (3, M))
    old = xcs.sched(D(15, 1, 2020), D(15, 1, 2021), (3, M))
    e3m, usd3m = xcs.E3M, xcs.USD3M
    eng = xcs.engine()
    base = (1e6, "EUR", two3, e3m, 0.0, 1.0)
    quote = (1.1e6, "USD", two3, usd3m, 0.0, 1.0)
    seasoned_fixings = [(e3m, [(D(15, 10, 2024), 0.0312)]), (usd3m, [(D(15, 10, 2024), 0.0471)])]
    seasoned_rates = [(ql.ExchangeRate(EUR, USD, 1.0857), D(17, 10, 2024), D(17, 10, 2024))]
    return [
        ("paying EUR, the USD leg resettable", swap(PAY, base, quote, False), eng, None, None),
        ("paying EUR, the EUR leg resettable", swap(PAY, base, quote, True), eng, None, None),
        ("receiving EUR, spreads and gearings", swap(RECEIVE, (1e6, "EUR", two3, e3m, -0.0012, 1.0),
                                                     (1.1e6, "USD", two3, usd3m, 0.0004, 0.9), False), eng, None,
         None),
        ("FX resets two days before, on TARGET", swap(PAY, base, quote, False, 2, xcs.TARGET), eng, None, None),
        ("FX resets two days before, on the schedule's calendar", swap(PAY, base, quote, True, 2), eng, None, None),
        ("FX resets two days before, on the US calendar", swap(RECEIVE, base, quote, False, 2, US), eng, None, None),
        ("payment lags and conventions", swap(PAY, base, quote, False, lags=(2, 1), conventions=(MF, P)), eng, None,
         None),
        ("SOFR compounded against ESTR averaged",
         swap(PAY, (1e6, "EUR", one1, xcs.ESTR, 0.0, 1.0), (1.1e6, "USD", one1, xcs.SOFR, 0.0, 1.0), False,
              base_on=(False, None, False, 0, SIMPLE), quote_on=(True, 2, False, 2, COMPOUND)), eng, None, None),
        ("indexed coupons with an interpolated stub index",
         swap(PAY, (1e6, "EUR", stub3, e3m, 0.0, 1.0), (1.1e6, "USD", stub3, usd3m, 0.0, 1.0), True, indexed=True,
              base_stub=xcs.stub(ql.StubIndexSelection.Interpolated, [E1M, e3m])), eng, None, None),
        ("a year, quarterly against annual", swap(RECEIVE, (1e6, "EUR", one3, e3m, 0.0, 1.0),
                                                  (1.1e6, "USD", one1, xcs.libor("USDLibor", 12, xcs.USD_FORECAST),
                                                   0.0, 1.0), True), eng, None, None),
        ("seasoned, a past reset and fixings", swap(PAY, (1e6, "EUR", seasoned, e3m, 0.0, 1.0),
                                                    (1.1e6, "USD", seasoned, usd3m, 0.0, 1.0), False), eng,
         seasoned_fixings, seasoned_rates),
        ("expired", swap(PAY, (1e6, "EUR", old, e3m, 0.0, 1.0), (1.1e6, "USD", old, usd3m, 0.0, 1.0), False), eng,
         None, None),
        ("a zero constant-leg notional", swap(PAY, (0.0, "EUR", two3, e3m, 0.0, 1.0), quote, False), eng, None, None),
        ("a leg in a currency the engine doesn't name", swap(PAY, base, quote, False),
         xcs.engine("USD", xcs.USD_DISCOUNT, "GBP", xcs.GBP_DISCOUNT, 1.27), None, None),
    ]


def cases():
    out = []
    for name, spec, engine_spec, fixings, rates in swaps():
        scale = scale_of(spec)
        written = None if fixings is None else [(xcs.make_index(i), pairs) for i, pairs in fixings]
        with v.case_settings(TODAY, fixings=written, exchange_rates=rates) as st:
            try:
                sw = make_swap(spec)
            except RuntimeError:
                expected = {"build": dict(v.THROWS)}
            else:
                expected = inspectors(sw)
                for j in range(2):
                    expected.update(leg_outputs(f"leg{j + 1}", sw.leg(j), scale))
                sw.setPricingEngine(make_engine(engine_spec))
                expected.update(results(sw, scale))
                for leg in ("fxBase", "fxQuote"):
                    if spec[leg + "CompoundSpread"]:
                        paid = (leg == "fxBase") == sw.paysFxBaseCurrency()
                        expected.pop("fair" + leg[0].upper() + leg[1:] + "Spread", None)
                        expected.pop("fairPaySpread" if paid else "fairRecSpread", None)
            out.append({"name": name, "inputs": {"settings": st, "swap": spec, "engine": engine_spec},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/fx/mtmcrosscurrencybasisswap.hpp", cases)
