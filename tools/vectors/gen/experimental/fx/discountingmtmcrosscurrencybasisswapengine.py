# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DiscountingMtMCrossCurrencyBasisSwapEngine
(ql/experimental/fx/discountingmtmcrosscurrencybasisswapengine.hpp): mark-to-market cross-currency basis swaps, EUR
against USD, priced on the evaluation date 15 January 2025 under the engine's arguments: either currency domestic;
the settlement date's flows counted or not, by the engine's flag or by the evaluation context's, on a seasoned swap
whose coupons, notional exchanges and FX reset fall on the evaluation date; a later settlement date; a later NPV date;
an FX settlement date two days on, with and without a later NPV date, and one on the reference date; and the engine's
refusals: curves with different reference dates, and a settlement, NPV or FX settlement date before the reference
date.

Each case's outputs are the engine's results, those of experimental/fx/mtmcrosscurrencybasisswap.py, or the refused
pricing, the one output "calculate". NPVs are compared at the largest notional, BPS at it times 1e-4.

Each case's inputs hold its settings (with the fixings and FX fixings the seasoned swap reads), "swap" and "engine",
in experimental/fx/mtmcrosscurrencybasisswap.py's and instruments/constnotionalcrosscurrencyswap.py's specs. Read back
by tests/NanoQL.Tests/Vectors/Experimental/FX/DiscountingMtMCrossCurrencyBasisSwapEngineVectorTests.cs."""
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
    "wheel_mtmcrosscurrencybasisswap",
    os.path.join(v.GEN_ROOT, "experimental", "fx", "mtmcrosscurrencybasisswap.py"))
mtm = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(mtm)
xcs = mtm.xcs
fvf = xcs.fvf

TODAY, D, M = xcs.TODAY, xcs.D, xcs.M
USD, EUR = xcs.USD_DISCOUNT, xcs.EUR_DISCOUNT


def cases():
    spot = xcs.SPOT
    two3 = xcs.sched(spot, D(17, 1, 2027), (3, M))
    seasoned = xcs.sched(D(15, 10, 2024), D(15, 10, 2025), (3, M))
    e3m, usd3m = xcs.E3M, xcs.USD3M
    live = mtm.swap(mtm.PAY, (1e6, "EUR", two3, e3m, 0.0, 1.0), (1.1e6, "USD", two3, usd3m, 0.0, 1.0), False)
    base_resettable = mtm.swap(mtm.RECEIVE, (1e6, "EUR", two3, e3m, 0.0, 1.0), (1.1e6, "USD", two3, usd3m, 0.0, 1.0),
                               True)
    today = mtm.swap(mtm.PAY, (1e6, "EUR", seasoned, e3m, 0.0, 1.0), (1.1e6, "USD", seasoned, usd3m, 0.0, 1.0), False)
    fixings = [(e3m, [(D(11, 10, 2024), 0.0318), (D(13, 1, 2025), 0.0284)]),
               (usd3m, [(D(11, 10, 2024), 0.0468), (D(13, 1, 2025), 0.0442)])]
    rates = [(ql.ExchangeRate(ql.EURCurrency(), ql.USDCurrency(), 1.0921), D(15, 10, 2024), D(15, 10, 2024)),
             (ql.ExchangeRate(ql.EURCurrency(), ql.USDCurrency(), 1.0302), TODAY, TODAY)]
    later = D(17, 7, 2025)
    rows = [
        ("USD domestic", live, xcs.engine(), False),
        ("EUR domestic", live, xcs.engine("EUR", EUR, "USD", USD, 1 / 1.1), False),
        ("USD domestic, the EUR leg resettable", base_resettable, xcs.engine(), False),
        ("flows today, the context's flag (false)", today, xcs.engine(), False),
        ("flows today, the context's flag (true)", today, xcs.engine(), True),
        ("flows today, the engine's flag true", today, xcs.engine(include=True), False),
        ("flows today, the engine's flag false", today, xcs.engine(include=False), True),
        ("a later settlement date", today, xcs.engine(settlement=D(17, 1, 2025)), False),
        ("a later NPV date", live, xcs.engine(npv=later), False),
        ("a later settlement and NPV date", today, xcs.engine(include=True, settlement=later, npv=later), False),
        ("an FX settlement date two days on", live, xcs.engine(fx_settle=spot), False),
        ("an FX settlement date two days on, a later NPV date", live, xcs.engine(npv=later, fx_settle=spot), False),
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
        scale = mtm.scale_of(spec)
        written = [(xcs.make_index(i), pairs) for i, pairs in fixings] if spec is today else None
        with v.case_settings(TODAY, include_reference_date_events=include_events, fixings=written,
                             exchange_rates=rates if spec is today else None) as st:
            sw = mtm.make_swap(spec)
            sw.setPricingEngine(mtm.make_engine(engine_spec))
            out.append({"name": name, "inputs": {"settings": st, "swap": spec, "engine": engine_spec},
                        "expected": mtm.results(sw, scale)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/fx/discountingmtmcrosscurrencybasisswapengine.hpp", cases)
