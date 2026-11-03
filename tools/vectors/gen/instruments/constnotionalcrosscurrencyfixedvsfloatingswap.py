# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ConstNotionalCrossCurrencyFixedVsFloatingSwap
(ql/instruments/constnotionalcrosscurrencyfixedvsfloatingswap.hpp): swaps on the evaluation date 15 January 2025 of a
fixed leg against a floating leg in another currency, each with its notional exchanged: payer and receiver, EUR fixed
against USD LIBOR 3M and GBP fixed against Euribor 6M, a spread, payment conventions, lags and calendars, indexed
coupons, an interpolated stub index on a broken front period, SOFR compounded with a lookback, a lockout and its
spread, SOFR averaged (and its refusal of a lookback, "Cannot price an overnight coupon with simple averaging with
lookback or lockout."), a seasoned swap reading a stored fixing, and a swap that has expired. Left out: legs whose BPS
is zero, whose fair rate or spread QuantLib gives as an infinity; telescopic value dates, which nanoql builds in full
(tests/NanoQL.Tests/Divergences/TelescopicValueDatesTests.cs); QuantLib's empty payment calendars, which the wheel
can't build; and the fair spread of a floating leg compounding its spread, which nanoql solves on the swap's pricing
where QuantLib's BPS route is inexact
(tests/NanoQL.Tests/Divergences/CrossCurrencySwapCompoundedSpreadFairSpreadTests.cs).

Each case's outputs are whether the swap builds, QuantLib's refusal recorded as its throw; then the swap's inspectors
(type; the fixed leg's nominal, currency code, rate, day counter and payment convention, lag and calendar; the floating
leg's nominal, currency code, index name, spread, payment convention, lag and calendar, and its overnight options; each
schedule's dates), each leg's payment dates and amounts, and DiscountingConstNotionalCrossCurrencySwapEngine's results:
those of instruments/constnotionalcrosscurrencyswap.py, with the fair fixed rate and the fair spread. NPVs and amounts
are compared at the largest notional, BPS at it times 1e-4.

Each case's inputs hold its settings, "swap" ({"type", "fixedNominal", "fixedCurrency", "fixedSchedule", "fixedRate",
"fixedDayCount", "fixedPaymentBdc", "fixedPaymentLag", "fixedPaymentCalendar", "floatNominal", "floatCurrency",
"floatSchedule", "floatIndex", "floatSpread", "floatPaymentBdc", "floatPaymentLag", "floatPaymentCalendar",
"floatCompoundSpread", "floatLookbackDays", "floatObservationShift", "floatLockoutDays", "floatAveragingMethod",
"useIndexedCoupons", "floatStubIndexSelection"}, a null lookback or coupon choice QuantLib's default) and "engine", in
instruments/constnotionalcrosscurrencyswap.py's specs. The wheel's constructor takes the arguments after the floating
payment calendar by name. Read back by
tests/NanoQL.Tests/Vectors/Instruments/ConstNotionalCrossCurrencyFixedVsFloatingSwapVectorTests.cs."""
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
PAYER, RECEIVER = int(ql.Swap.Payer), int(ql.Swap.Receiver)
F, MF, P = int(ql.Following), int(ql.ModifiedFollowing), int(ql.Preceding)
US = {"type": "UnitedStates", "market": int(ql.UnitedStates.Settlement)}
E1M = fvf.preset("Euribor1M", xcs.EUR_FORECAST)


def swap(swap_type, fixed, floating, fixed_payment=(F, 0, xcs.TARGET), float_payment=(F, 0, xcs.TARGET), on=None,
         indexed=None, stub=None):
    """fixed is (nominal, currency, schedule, rate, day counter), floating (nominal, currency, schedule, index,
    spread); a payment is (convention, lag, calendar); on is (compoundSpread, lookbackDays, observationShift,
    lockoutDays, averagingMethod)."""
    on = on or (False, None, False, 0, COMPOUND)
    return {"type": swap_type, "fixedNominal": fixed[0], "fixedCurrency": fixed[1], "fixedSchedule": fixed[2],
            "fixedRate": fixed[3], "fixedDayCount": fixed[4], "fixedPaymentBdc": fixed_payment[0],
            "fixedPaymentLag": fixed_payment[1], "fixedPaymentCalendar": fixed_payment[2],
            "floatNominal": floating[0], "floatCurrency": floating[1], "floatSchedule": floating[2],
            "floatIndex": floating[3], "floatSpread": floating[4], "floatPaymentBdc": float_payment[0],
            "floatPaymentLag": float_payment[1], "floatPaymentCalendar": float_payment[2],
            "floatCompoundSpread": on[0], "floatLookbackDays": on[1], "floatObservationShift": on[2],
            "floatLockoutDays": on[3], "floatAveragingMethod": on[4], "useIndexedCoupons": indexed,
            "floatStubIndexSelection": stub}


def make_swap(spec):
    kw = {"floatCompoundSpread": spec["floatCompoundSpread"], "floatObservationShift": spec["floatObservationShift"],
          "floatLockoutDays": spec["floatLockoutDays"], "floatAveragingMethod": spec["floatAveragingMethod"],
          "useIndexedCoupons": spec["useIndexedCoupons"],
          "floatStubIndexSelection": xcs.make_stub(spec["floatStubIndexSelection"])}
    if spec["floatLookbackDays"] is not None:
        kw["floatLookbackDays"] = spec["floatLookbackDays"]
    return ql.ConstNotionalCrossCurrencyFixedVsFloatingSwap(
        spec["type"], spec["fixedNominal"], xcs.currency(spec["fixedCurrency"]), fvf.schedule(spec["fixedSchedule"]),
        spec["fixedRate"], irx.day_counter(spec["fixedDayCount"]), spec["fixedPaymentBdc"], spec["fixedPaymentLag"],
        irx.calendar(spec["fixedPaymentCalendar"]), spec["floatNominal"], xcs.currency(spec["floatCurrency"]),
        fvf.schedule(spec["floatSchedule"]), xcs.make_index(spec["floatIndex"]), spec["floatSpread"],
        spec["floatPaymentBdc"], spec["floatPaymentLag"], irx.calendar(spec["floatPaymentCalendar"]), **kw)


def swaps():
    """(name, swap, engine, fixings) for the cases."""
    spot = xcs.SPOT
    three1, three3 = xcs.sched(spot, D(17, 1, 2028), (1, Y)), xcs.sched(spot, D(17, 1, 2028), (3, M))
    five1, five6 = xcs.sched(spot, D(17, 1, 2030), (1, Y)), xcs.sched(spot, D(17, 1, 2030), (6, M))
    one1 = xcs.sched(spot, D(17, 1, 2026), (1, Y))
    stub1, stub3 = xcs.sched(spot, D(17, 12, 2025), (1, Y)), xcs.sched(spot, D(17, 12, 2025), (3, M))
    s1, s3 = xcs.sched(D(17, 10, 2024), D(17, 10, 2026), (1, Y)), xcs.sched(D(17, 10, 2024), D(17, 10, 2026), (3, M))
    o1, o3 = xcs.sched(D(15, 1, 2020), D(15, 1, 2022), (1, Y)), xcs.sched(D(15, 1, 2020), D(15, 1, 2022), (3, M))
    a360, a365, t360 = irx.A360, irx.A365, irx.THIRTY360_BB
    usd3m = xcs.USD3M
    eur = xcs.engine()
    gbp = xcs.engine("EUR", xcs.EUR_DISCOUNT, "GBP", xcs.GBP_DISCOUNT, 1.17)
    return [
        ("payer, EUR fixed against USD LIBOR 3M", swap(PAYER, (1e6, "EUR", three1, 0.028, a360),
                                                       (1.1e6, "USD", three3, usd3m, 0.0)), eur, None),
        ("receiver, 30/360 fixed against a spread", swap(RECEIVER, (1e6, "EUR", three1, 0.027, t360),
                                                         (1.1e6, "USD", three3, usd3m, 0.0015)), eur, None),
        ("payment conventions, lags and a US calendar",
         swap(PAYER, (1e6, "EUR", three1, 0.028, a360), (1.1e6, "USD", three3, usd3m, 0.0), (MF, 2, xcs.TARGET),
              (P, 1, US)), eur, None),
        ("GBP fixed against Euribor 6M", swap(RECEIVER, (0.86e6, "GBP", five1, 0.036, a365),
                                              (1e6, "EUR", five6, xcs.E6M, -0.001)), gbp, None),
        ("SOFR compounded with a lookback, a lockout and its spread",
         swap(PAYER, (1e6, "EUR", one1, 0.026, a360), (1.1e6, "USD", one1, xcs.SOFR, 0.001), float_payment=(F, 2, US),
              on=(True, 2, False, 2, COMPOUND)), eur, None),
        ("SOFR averaged", swap(PAYER, (1e6, "EUR", one1, 0.026, a360), (1.1e6, "USD", one1, xcs.SOFR, 0.0),
                               float_payment=(F, 2, US), on=(False, None, False, 0, SIMPLE)), eur, None),
        ("SOFR averaged with a lookback", swap(PAYER, (1e6, "EUR", one1, 0.026, a360),
                                               (1.1e6, "USD", one1, xcs.SOFR, 0.0), float_payment=(F, 2, US),
                                               on=(False, 2, False, 0, SIMPLE)), eur, None),
        ("indexed coupons", swap(PAYER, (1e6, "EUR", three1, 0.028, a360), (1.1e6, "USD", three3, usd3m, 0.001),
                                 indexed=True), eur, None),
        ("an interpolated stub index on a broken front period",
         swap(PAYER, (1e6, "EUR", stub1, 0.028, a360), (1e6, "EUR", stub3, xcs.E3M, 0.0), indexed=True,
              stub=xcs.stub(ql.StubIndexSelection.Interpolated, [E1M, xcs.E3M])),
         xcs.engine("EUR", xcs.EUR_DISCOUNT, "USD", xcs.USD_DISCOUNT, 0.91), None),
        ("seasoned, reading a stored fixing", swap(PAYER, (1e6, "EUR", s1, 0.028, a360),
                                                   (1.1e6, "USD", s3, usd3m, 0.0)), eur,
         [(usd3m, [(D(15, 10, 2024), 0.0471)])]),
        ("expired", swap(PAYER, (1e6, "EUR", o1, 0.01, a360), (1.1e6, "USD", o3, usd3m, 0.0)), eur, None),
    ]


def name_or_null(x):
    return None if x.empty() else x.name()


def inspectors(sw):
    return {"type": int(sw.type()), "fixedNominal": sw.fixedNominal(),
            "fixedCurrency": xcs.code_or_null(sw.fixedCurrency()), "fixedRate": sw.fixedRate(),
            "fixedDayCount": sw.fixedDayCount().name(), "fixedPaymentBdc": int(sw.fixedPaymentBdc()),
            "fixedPaymentLag": sw.fixedPaymentLag(), "fixedPaymentCalendar": name_or_null(sw.fixedPaymentCalendar()),
            "floatNominal": sw.floatNominal(), "floatCurrency": xcs.code_or_null(sw.floatCurrency()),
            "floatIndexName": sw.floatIndex().name(), "floatSpread": sw.floatSpread(),
            "floatPaymentBdc": int(sw.floatPaymentBdc()), "floatPaymentLag": sw.floatPaymentLag(),
            "floatPaymentCalendar": name_or_null(sw.floatPaymentCalendar()),
            "floatCompoundSpread": sw.floatCompoundSpread(), "floatLookbackDays": v.enc(sw.floatLookbackDays()),
            "floatLockoutDays": sw.floatLockoutDays(), "floatAveragingMethod": int(sw.floatAveragingMethod()),
            "fixedSchedule": fvf.schedule_dates(sw.fixedSchedule()),
            "floatSchedule": fvf.schedule_dates(sw.floatSchedule())}


def cases():
    out = []
    for name, spec, engine_spec, fixings in swaps():
        scale = max(abs(spec["fixedNominal"]), abs(spec["floatNominal"]), 1.0)
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
                    expected["fairRate"] = v.result(sw.fairRate)
                    if not spec["floatCompoundSpread"]:
                        expected["fairSpread"] = v.result(sw.fairSpread)  # left out when compounded
            out.append({"name": name, "inputs": {"settings": st, "swap": spec, "engine": engine_spec},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/constnotionalcrosscurrencyfixedvsfloatingswap.hpp", cases)
