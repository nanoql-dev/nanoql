# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's EquityTotalReturnSwap (ql/instruments/equitytotalreturnswap.hpp, .cpp) priced by the
DiscountingSwapEngine, on the evaluation date 27 January 2023 and test-suite/equitytotalreturnswap.cpp's market: a USD
equity index ("eqIndex", spot 8700) with stored fixings, SOFR with the case's stored fixings and USD Libor 3M with one,
all on one interest curve, the swap discounted on it.

Receiver and payer swaps against Libor and SOFR, on that case's flat curves (3.75% interest, 0.5% dividend, Actual/365
(Fixed)) and on linear zero curves, quarterly schedules on the US government-bond calendar from a past start (a stored
equity fixing), from a start four days ahead and from a forward start, over three months, a year and two years, with
margins, gearings, payment delays and nominals: the NPV, the equity and interest legs' NPVs, the fair margin, the start
and maturity dates, and each leg's payment dates and amounts. Then QuantLib's check of a negative nominal.

A case holds its settings with the stored fixings, the market {"interest", "dividend" (blackscholesprocess.py's curve
specs), "spot"}, and one swap {"type" (QuantLib's Receiver -1, Payer 1), "nominal", "schedule" (cashflows.py's
schedule spec), "index" ("Libor" | "Sofr"), "margin", "gearing", "paymentDelay"}, its day counter Actual/365 (Fixed),
its payment calendar the schedule's and its convention Following. The wheel's calls, by QuantLib-SWIG 1.44's
swap.i and indexes.i: EquityIndex("eqIndex", UnitedStates(GovernmentBond), USDCurrency(), interest, dividend, spot),
Sofr(interest), USDLibor(Period(3, Months), interest), EquityTotalReturnSwap(type, nominal, schedule, equityIndex,
index, Actual365Fixed(), margin, gearing, schedule.calendar(), Following, paymentDelay),
DiscountingSwapEngine(interest); NPV, equityLegNPV, interestRateLegNPV, fairMargin, startDate, maturityDate,
equityLeg and interestRateLeg the swap's. Read back by
tests/NanoQL.Tests/Vectors/Instruments/EquityTotalReturnSwapVectorTests.cs."""
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


bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
cfs = _load("cashflows", "cashflows/cashflows.py")

D = ql.Date
TODAY = D(27, ql.January, 2023)
A365 = bsp.A365
US_GOVT = {"type": "UnitedStates", "market": int(ql.UnitedStates.GovernmentBond)}
EQUITY_FIXINGS = [(D(5, ql.January, 2023), 9010.0), (TODAY, 8690.0)]
SOFR_FIXINGS = [(D(3, 1, 2023), 0.03)] + [(D(d, 1, 2023), 0.031) for d in (4, 5, 6)] + [(D(9, 1, 2023), 0.032)] + \
    [(D(d, 1, 2023), 0.033) for d in (10, 11, 12, 13, 17)] + [(D(d, 1, 2023), 0.034) for d in (18, 19, 20, 23, 24, 25, 26)]
LIBOR_FIXINGS = [(D(3, ql.January, 2023), 0.035)]

MARKETS = [
    ("flat", {"interest": bsp.flat_spec(TODAY, 0.0375), "dividend": bsp.flat_spec(TODAY, 0.005), "spot": 8700.0}),
    ("zero curves", {"interest": bsp.zero_spec(TODAY, [0, 182, 365, 730, 1461], [0.034, 0.036, 0.038, 0.039, 0.041]),
                     "dividend": bsp.zero_spec(TODAY, [0, 365, 1461], [0.004, 0.006, 0.008]), "spot": 8700.0}),
]

# (name, start, end, margin, gearing, payment delay, nominal)
SETUPS = [
    ("past start, three months", D(5, 1, 2023), D(5, 4, 2023), 0.0, 1.0, 0, 1.0e7),
    ("past start, three months, margin", D(5, 1, 2023), D(5, 4, 2023), 0.01, 1.0, 0, 1.0e7),
    ("past start, three months, no gearing", D(5, 1, 2023), D(5, 4, 2023), 0.0, 0.0, 0, 1.0e7),
    ("near start, negative margin, delay", D(31, 1, 2023), D(30, 4, 2023), -0.005, 1.0, 2, 1.0e7),
    ("past start, a year, geared", D(5, 1, 2023), D(5, 1, 2024), 0.0025, 1.5, 0, 2.5e6),
    ("forward start, two years, delay", D(15, 3, 2023), D(15, 3, 2025), 0.005, 1.0, 1, 1.0e7),
]


def swap_spec(swap_type, start, end, index, margin, gearing, delay, nominal):
    return {"type": int(swap_type), "nominal": nominal,
            "schedule": cfs.schedule_spec(start, end, ql.Period(3, ql.Months), US_GOVT, ql.Following,
                                          ql.DateGeneration.Backward, False),
            "index": index, "margin": margin, "gearing": gearing, "paymentDelay": delay}


def legs(leg):
    return [v.enc(cf.date()) for cf in leg], [cf.amount() for cf in leg]


def priced(market, s):
    interest = ql.YieldTermStructureHandle(bsp.curve(market["interest"]))
    dividend = ql.YieldTermStructureHandle(bsp.curve(market["dividend"]))
    spot = ql.QuoteHandle(ql.SimpleQuote(market["spot"]))
    equity = ql.EquityIndex("eqIndex", ql.UnitedStates(ql.UnitedStates.GovernmentBond), ql.USDCurrency(), interest,
                            dividend, spot)
    index = ql.Sofr(interest) if s["index"] == "Sofr" else ql.USDLibor(ql.Period(3, ql.Months), interest)
    fixings = [(equity, EQUITY_FIXINGS), (index, SOFR_FIXINGS if s["index"] == "Sofr" else LIBOR_FIXINGS)]
    names = ["NPV", "equityLegNPV", "interestRateLegNPV", "fairMargin", "startDate", "maturityDate", "equityLegDates",
             "equityLegAmounts", "interestRateLegDates", "interestRateLegAmounts"]
    with v.case_settings(TODAY, fixings=fixings) as st:
        schedule = cfs.schedule(s["schedule"])
        try:
            swap = ql.EquityTotalReturnSwap(s["type"], s["nominal"], schedule, equity, index, ql.Actual365Fixed(),
                                            s["margin"], s["gearing"], schedule.calendar(), ql.Following,
                                            s["paymentDelay"])
        except RuntimeError:
            return st, {name: dict(v.THROWS) for name in names}
        swap.setPricingEngine(ql.DiscountingSwapEngine(interest))
        equity_dates, equity_amounts = legs(swap.equityLeg())
        rate_dates, rate_amounts = legs(swap.interestRateLeg())
        return st, {"NPV": swap.NPV(), "equityLegNPV": swap.equityLegNPV(),
                    "interestRateLegNPV": swap.interestRateLegNPV(), "fairMargin": swap.fairMargin(),
                    "startDate": v.enc(swap.startDate()), "maturityDate": v.enc(swap.maturityDate()),
                    "equityLegDates": equity_dates, "equityLegAmounts": equity_amounts,
                    "interestRateLegDates": rate_dates, "interestRateLegAmounts": rate_amounts}


def cases():
    out = []
    for mname, market in MARKETS:
        for sname, start, end, margin, gearing, delay, nominal in SETUPS:
            for index in ("Libor", "Sofr"):
                for swap_type, tname in [(ql.Swap.Receiver, "receiver"), (ql.Swap.Payer, "payer")]:
                    s = swap_spec(swap_type, start, end, index, margin, gearing, delay, nominal)
                    st, expected = priced(market, s)
                    out.append({"name": f"{mname}, {sname}, {index}, {tname}",
                                "inputs": {"settings": st, "market": market, "swap": s}, "expected": expected})
    s = swap_spec(ql.Swap.Receiver, D(5, 1, 2023), D(5, 4, 2023), "Libor", 0.0, 1.0, 0, -1.0e7)
    st, expected = priced(MARKETS[0][1], s)
    out.append({"name": "a negative nominal", "inputs": {"settings": st, "market": MARKETS[0][1], "swap": s},
                "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/equitytotalreturnswap.hpp", cases)
