# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DiscountingConstNotionalCrossCurrencySwapEngine
(ql/pricingengines/swap/discountingconstnotionalcrosscurrencyswapengine.hpp): constant-notional cross-currency swaps
priced on the evaluation date 15 January 2025 under the engine's arguments: either currency domestic; the settlement
date's flows counted or not, by the engine's flag or by the evaluation context's, on a swap with a coupon paid on the
evaluation date; a later settlement date; a later NPV date; an explicit FX settlement date, two days on, and one on the
curves' reference date, which QuantLib reads as unset; and the engine's refusals: curves with different reference dates,
and a settlement, NPV or FX settlement date before the reference date. Left out: an explicit FX settlement date, the
reference date's included, with an NPV date away from the reference date, where nanoql converts the spot to the NPV date
(tests/NanoQL.Tests/Divergences/CrossCurrencySwapNpvDateFxTests.cs).

Each case's outputs are the engine's results, those of instruments/constnotionalcrosscurrencyswap.py, or the refused
pricing, the one output "calculate". NPVs are compared at the largest notional, BPS at it times 1e-4.

Each case's inputs hold its settings, "swap" and "engine", in instruments/constnotionalcrosscurrencyswap.py's specs.
Read back by tests/NanoQL.Tests/Vectors/PricingEngines/DiscountingConstNotionalCrossCurrencySwapEngineVectorTests.cs."""
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
fvf = xcs.fvf

TODAY, D, M, Y = xcs.TODAY, xcs.D, xcs.M, xcs.Y
USD, EUR = xcs.USD_DISCOUNT, xcs.EUR_DISCOUNT


def cases():
    spot = xcs.SPOT
    two1, two3 = xcs.sched(spot, D(17, 1, 2027), (1, Y)), xcs.sched(spot, D(17, 1, 2027), (3, M))
    paying_today = xcs.sched(D(15, 1, 2024), D(15, 1, 2027), (1, Y))
    live = xcs.swap_spec("pair", [xcs.fixed_leg(two1, 1e6, 0.03), xcs.ibor_leg(two3, 1.1e6, xcs.USD3M)],
                         [True, False], ["EUR", "USD"])
    today = xcs.swap_spec("pair", [xcs.fixed_leg(paying_today, 1e6, 0.03), xcs.fixed_leg(paying_today, 1.1e6, 0.045)],
                          [True, False], ["EUR", "USD"])
    later = D(17, 7, 2025)
    rows = [
        ("USD domestic", live, xcs.engine(), False),
        ("EUR domestic", live, xcs.engine("EUR", EUR, "USD", USD, 1 / 1.1), False),
        ("a coupon today, the context's flag (false)", today, xcs.engine(), False),
        ("a coupon today, the context's flag (true)", today, xcs.engine(), True),
        ("a coupon today, the engine's flag true", today, xcs.engine(include=True), False),
        ("a coupon today, the engine's flag false", today, xcs.engine(include=False), True),
        ("a later settlement date", today, xcs.engine(settlement=D(17, 1, 2025)), False),
        ("a later NPV date", live, xcs.engine(npv=later), False),
        ("a later settlement and NPV date", today, xcs.engine(include=True, settlement=later, npv=later), False),
        ("an FX settlement date two days on", live, xcs.engine(fx_settle=spot), False),
        ("an FX settlement date two days on, EUR domestic", live,
         xcs.engine("EUR", EUR, "USD", USD, 1 / 1.1, fx_settle=spot), False),
        ("an FX settlement date on the reference date", live, xcs.engine(fx_settle=TODAY), False),
        ("curves with different reference dates", live,
         xcs.engine(foreign_curve=fvf.curve(0.025, reference=TODAY + 1)), False),
        ("a settlement date before the reference date", live, xcs.engine(settlement=TODAY - 1), False),
        ("an NPV date before the reference date", live, xcs.engine(npv=TODAY - 1), False),
        ("an FX settlement date before the reference date", live, xcs.engine(fx_settle=TODAY - 1), False),
    ]
    out = []
    for name, spec, engine_spec, include_events in rows:
        scale = xcs.scale_of(spec["legs"])
        with v.case_settings(TODAY, include_reference_date_events=include_events) as st:
            sw = xcs.make_swap(spec)
            sw.setPricingEngine(xcs.make_engine(engine_spec))
            out.append({"name": name, "inputs": {"settings": st, "swap": spec, "engine": engine_spec},
                        "expected": xcs.results(sw, scale, sw.numberOfLegs())})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/swap/discountingconstnotionalcrosscurrencyswapengine.hpp", cases)
