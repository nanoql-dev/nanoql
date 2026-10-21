# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ConstNotionalCrossCurrencyBasisSwap (ql/instruments/constnotionalcrosscurrencybasisswap.hpp):
swaps on the evaluation date 15 January 2025 of two floating legs in two currencies, each with its notional
exchanged: Euribor 3M against USD LIBOR 3M, with spreads and gearings, payment lags on the coupons alone and on the
notional exchanges too, indexed coupons, interpolated and closest stub indexes on a broken front period (and the
refusal of one on at-par coupons, "stub index conventions require indexed coupons"), SONIA compounded with a
lookback, a lockout and an observation shift against SOFR with a lookback compounding its spread, SOFR averaged against
ESTR, a seasoned swap reading stored fixings, and a swap that has expired. Left out: legs whose BPS is zero, whose fair
spread QuantLib gives as an infinity (the S-rx plan's SRXR3 (xviii)); telescopic value dates, which nanoql builds in
full (tests/NanoQL.Tests/Divergences/TelescopicValueDatesTests.cs); a spread compounded daily where the telescopic
formula applies, which QuantLib drops (the C3 review's B1-1; the SOFR leg's lookback keeps it off the formula); and
the fair spread of a leg compounding its spread, which nanoql solves on the swap's pricing where QuantLib's BPS route
is inexact (B1-2; tests/NanoQL.Tests/Divergences/CrossCurrencySwapCompoundedSpreadFairSpreadTests.cs).

Each case's outputs are whether the swap builds, QuantLib's refusal recorded as its throw; then the swap's inspectors
(each leg's nominal, currency code, spread and gearing, its index's name and its schedule's dates), each leg's payment
dates and amounts, and DiscountingConstNotionalCrossCurrencySwapEngine's results with USD domestic: those of
instruments/constnotionalcrosscurrencyswap.py, with the fair pay and receive spreads. NPVs and amounts are compared at
the largest notional, BPS at it times 1e-4.

Each case's inputs hold its settings, "swap" ({"payNominal", "payCurrency", "paySchedule", "payIndex", "paySpread",
"payGearing", the same six for "rec", "payPaymentLag", "recPaymentLag", "payCompoundSpread", "payLookbackDays",
"payObservationShift", "payLockoutDays", "payAveragingMethod", the same five for "rec", "useIndexedCoupons",
"paymentLagOnNotionalExchanges", "payStubIndexSelection", "recStubIndexSelection"}, a null lookback or coupon choice
QuantLib's default) and "engine", in instruments/constnotionalcrosscurrencyswap.py's specs. The wheel's constructor
takes the arguments after the receiving gearing by name. Read back by
tests/NanoQL.Tests/Vectors/Instruments/ConstNotionalCrossCurrencyBasisSwapVectorTests.cs."""
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
E1M = fvf.preset("Euribor1M", xcs.EUR_FORECAST)


def swap(pay, rec, pay_lag=0, rec_lag=0, pay_on=None, rec_on=None, indexed=None, lag_on_exchanges=False,
         pay_stub=None, rec_stub=None):
    """pay and rec are (nominal, currency, schedule, index, spread, gearing); an overnight leg's options are
    (compoundSpread, lookbackDays, observationShift, lockoutDays, averagingMethod)."""
    on = (False, None, False, 0, COMPOUND)
    out = {}
    for prefix, (nominal, ccy, schedule, index, spread, gearing) in (("pay", pay), ("rec", rec)):
        out.update({prefix + "Nominal": nominal, prefix + "Currency": ccy, prefix + "Schedule": schedule,
                    prefix + "Index": index, prefix + "Spread": spread, prefix + "Gearing": gearing})
    out.update({"payPaymentLag": pay_lag, "recPaymentLag": rec_lag})
    for prefix, options in (("pay", pay_on or on), ("rec", rec_on or on)):
        out.update({prefix + "CompoundSpread": options[0], prefix + "LookbackDays": options[1],
                    prefix + "ObservationShift": options[2], prefix + "LockoutDays": options[3],
                    prefix + "AveragingMethod": options[4]})
    out.update({"useIndexedCoupons": indexed, "paymentLagOnNotionalExchanges": lag_on_exchanges,
                "payStubIndexSelection": pay_stub, "recStubIndexSelection": rec_stub})
    return out


def make_swap(spec):
    kw = {"payPaymentLag": spec["payPaymentLag"], "recPaymentLag": spec["recPaymentLag"],
          "useIndexedCoupons": spec["useIndexedCoupons"],
          "paymentLagOnNotionalExchanges": spec["paymentLagOnNotionalExchanges"],
          "payStubIndexSelection": xcs.make_stub(spec["payStubIndexSelection"]),
          "recStubIndexSelection": xcs.make_stub(spec["recStubIndexSelection"])}
    for prefix in ("pay", "rec"):
        kw.update({prefix + "CompoundSpread": spec[prefix + "CompoundSpread"],
                   prefix + "ObservationShift": spec[prefix + "ObservationShift"],
                   prefix + "LockoutDays": spec[prefix + "LockoutDays"],
                   prefix + "AveragingMethod": spec[prefix + "AveragingMethod"]})
        if spec[prefix + "LookbackDays"] is not None:
            kw[prefix + "LookbackDays"] = spec[prefix + "LookbackDays"]
    args = []
    for prefix in ("pay", "rec"):
        args += [spec[prefix + "Nominal"], xcs.currency(spec[prefix + "Currency"]),
                 fvf.schedule(spec[prefix + "Schedule"]), xcs.make_index(spec[prefix + "Index"]),
                 spec[prefix + "Spread"], spec[prefix + "Gearing"]]
    return ql.ConstNotionalCrossCurrencyBasisSwap(*args, **kw)


def swaps():
    """(name, swap, engine, fixings) for the cases."""
    spot = xcs.SPOT
    two3 = xcs.sched(spot, D(17, 1, 2027), (3, M))
    three6 = xcs.sched(spot, D(17, 1, 2028), (6, M))
    one1 = xcs.sched(spot, D(17, 1, 2026), (1, Y))
    stub3 = xcs.sched(spot, D(17, 12, 2025), (3, M))
    seasoned = xcs.sched(D(17, 10, 2024), D(17, 10, 2026), (3, M))
    old = xcs.sched(D(15, 1, 2020), D(15, 1, 2022), (3, M))
    eur, usd = xcs.engine(), xcs.engine()
    gbp = xcs.engine("USD", xcs.USD_DISCOUNT, "GBP", xcs.GBP_DISCOUNT, 1.27)
    e3m, usd3m = xcs.E3M, xcs.USD3M
    seasoned_fixings = [(e3m, [(D(15, 10, 2024), 0.0312)]), (usd3m, [(D(15, 10, 2024), 0.0471)])]
    return [
        ("Euribor 3M paid against USD LIBOR 3M", swap((1e6, "EUR", two3, e3m, 0.0, 1.0),
                                                      (1.1e6, "USD", two3, usd3m, 0.0, 1.0)), eur, None),
        ("spreads and gearings", swap((1e6, "EUR", two3, e3m, -0.0015, 1.0),
                                      (1.1e6, "USD", two3, usd3m, 0.0005, 0.95)), eur, None),
        ("USD LIBOR 6M paid against Euribor 6M", swap((1.1e6, "USD", three6, xcs.USD6M, 0.0, 1.0),
                                                      (1e6, "EUR", three6, xcs.E6M, -0.002, 1.0)), usd, None),
        ("payment lags on the coupons", swap((1e6, "EUR", two3, e3m, 0.0, 1.0), (1.1e6, "USD", two3, usd3m, 0.0, 1.0),
                                             pay_lag=2, rec_lag=1), eur, None),
        ("payment lags on the notional exchanges too",
         swap((1e6, "EUR", two3, e3m, 0.0, 1.0), (1.1e6, "USD", two3, usd3m, 0.0, 1.0), pay_lag=2, rec_lag=1,
              lag_on_exchanges=True), eur, None),
        ("indexed coupons", swap((1e6, "EUR", two3, e3m, 0.001, 1.0), (1.1e6, "USD", two3, usd3m, 0.0, 1.0),
                                 indexed=True), eur, None),
        ("at-par coupons", swap((1e6, "EUR", two3, e3m, 0.001, 1.0), (1.1e6, "USD", two3, usd3m, 0.0, 1.0),
                                indexed=False), eur, None),
        ("an interpolated stub index on a broken front period",
         swap((1e6, "EUR", stub3, e3m, 0.0, 1.0), (1.1e6, "USD", stub3, usd3m, 0.0, 1.0), indexed=True,
              pay_stub=xcs.stub(ql.StubIndexSelection.Interpolated, [E1M, e3m])), eur, None),
        ("the closest stub index on the receiving leg",
         swap((1e6, "EUR", stub3, e3m, 0.0, 1.0), (1.1e6, "USD", stub3, usd3m, 0.0, 1.0), indexed=True,
              rec_stub=xcs.stub(ql.StubIndexSelection.ClosestIndex, [xcs.libor("USDLibor", 1, xcs.USD_FORECAST),
                                                                       usd3m])), eur, None),
        ("a stub index on at-par coupons",
         swap((1e6, "EUR", stub3, e3m, 0.0, 1.0), (1.1e6, "USD", stub3, usd3m, 0.0, 1.0), indexed=False,
              pay_stub=xcs.stub(ql.StubIndexSelection.Interpolated, [E1M, e3m])), eur, None),
        ("SONIA with a lookback, a lockout and a shift against SOFR with a lookback compounding its spread",
         swap((0.79e6, "GBP", one1, xcs.SONIA, 0.0, 1.0), (1e6, "USD", one1, xcs.SOFR, 0.0012, 1.0),
              pay_on=(False, 2, True, 2, COMPOUND), rec_on=(True, 2, False, 0, COMPOUND)), gbp, None),
        ("SOFR averaged against ESTR", swap((1.1e6, "USD", one1, xcs.SOFR, 0.0, 1.0),
                                            (1e6, "EUR", one1, xcs.ESTR, -0.001, 1.0),
                                            pay_on=(False, None, False, 0, SIMPLE),
                                            rec_on=(False, None, False, 0, SIMPLE)), usd, None),
        ("seasoned, reading stored fixings", swap((1e6, "EUR", seasoned, e3m, 0.0, 1.0),
                                                  (1.1e6, "USD", seasoned, usd3m, 0.0, 1.0)), eur, seasoned_fixings),
        ("expired", swap((1e6, "EUR", old, e3m, 0.0, 1.0), (1.1e6, "USD", old, usd3m, 0.0, 1.0)), eur, None),
    ]


def inspectors(sw):
    out = {}
    for prefix, nominal, ccy, spread, gearing, index in (
            ("pay", sw.payNominal, sw.payCurrency, sw.paySpread, sw.payGearing, sw.payIndex),
            ("rec", sw.recNominal, sw.recCurrency, sw.recSpread, sw.recGearing, sw.recIndex)):
        out.update({prefix + "Nominal": nominal(), prefix + "Currency": xcs.code_or_null(ccy()),
                    prefix + "Spread": spread(), prefix + "Gearing": gearing(), prefix + "IndexName": index().name()})
    out.update({"paySchedule": fvf.schedule_dates(sw.paySchedule()),
                "recSchedule": fvf.schedule_dates(sw.recSchedule())})
    return out


def cases():
    out = []
    for name, spec, engine_spec, fixings in swaps():
        scale = max(abs(spec["payNominal"]), abs(spec["recNominal"]), 1.0)
        written = None if fixings is None else [(xcs.make_index(i), pairs) for i, pairs in fixings]
        with v.case_settings(TODAY, fixings=written) as st:
            try:
                sw = make_swap(spec)
            except RuntimeError:
                expected = {"build": dict(v.THROWS)}
            else:
                expected = inspectors(sw)
                for j in range(2):
                    expected.update(xcs.leg_outputs(f"leg{j + 1}", sw.leg(j), scale))
                sw.setPricingEngine(xcs.make_engine(engine_spec))
                expected.update(xcs.results(sw, scale, 2))
                if "calculate" not in expected:
                    # a leg compounding its spread: its fair spread left out (B1-2)
                    for prefix, fair in (("pay", sw.fairPaySpread), ("rec", sw.fairRecSpread)):
                        if not spec[prefix + "CompoundSpread"]:
                            expected["fair" + prefix.capitalize() + "Spread"] = v.result(fair)
            out.append({"name": name, "inputs": {"settings": st, "swap": spec, "engine": engine_spec},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/constnotionalcrosscurrencybasisswap.hpp", cases)
