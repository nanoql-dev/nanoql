# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's counterparty-risk adjusted swap engine (ql/pricingengines/swap/cvaswapengine.hpp), each output
computed by the C++ harness, since the wheel doesn't bind the engine: vanilla swaps on Euribor 6M, payer and receiver, 5
and 10 years, fresh and seasoned (some fixed payments before the counterparty curve's reference date, which QuantLib's
loop skips, the past fixings stored), on a flat discounting curve, the exposure priced by Black swaptions on a constant
volatility or on a volatility quote, or by a Bachelier swaption engine on a normal volatility; counterparty and investor
curves flat or interpolated, and no investor curve (QuantLib's default-free one).

Each case gives the adjusted NPV and fair rate. Each swap is at its fair rate on the wheel (the stored fixings set),
where QuantLib's swaptionlets, struck at the swap's risk-free fair rate, are nanoql's for the NPV, struck at its fixed
rate, within a few ulps (C3's review, C3-O2, ruled (a); off par the two depart,
tests/NanoQL.Tests/Divergences/CvaSwaptionStrikeTests.cs); both price the fair rate on QuantLib's. A swap whose fixed
payments all fall before the counterparty curve's reference date is the S-cms plan's CMR5 (xvi)
(tests/NanoQL.Tests/Divergences/CvaEngineFixingLoopTests.cs) and isn't sampled. A case's inputs hold its settings with
the stored fixings, the swap (instruments/fixedvsfloatingswap.py's vanilla spec on a Euribor 6M preset, its payment
convention and coupon choice null), the discounting curve, the exposure ({"kind": "blackVol" | "blackQuote" |
"bachelier", "volatility"}) and the credit curves and recoveries (termstructures/defaulttermstructure.py's specs, a null
investor curve none). Read back by tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/CvaSwapEngineCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


fvf = _load("wheel_fixedvsfloatingswap", "instruments/fixedvsfloatingswap.py")
ibc = _load("wheel_iborcoupon", "cashflows/iborcoupon.py")
dts = _load("wheel_defaulttermstructure", "termstructures/defaulttermstructure.py")

TODAY = fvf.TODAY
D = ql.Date
E6M = fvf.preset("Euribor6M", fvf.FORECAST)
DISCOUNT = fvf.DISCOUNT
PAYER, RECEIVER = int(ql.Swap.Payer), int(ql.Swap.Receiver)


def vanilla(swap_type, start, years, rate, spread=0.0, nominal=1e7):
    end = D(start.dayOfMonth(), start.month(), start.year() + years)
    return fvf.vanilla(swap_type, nominal, fvf.sched(start, end, ql.Period(1, ql.Years), convention=ql.Unadjusted),
                       rate, fvf.THIRTY360_BB, fvf.sched(start, end, ql.Period(6, ql.Months)), E6M, spread)


FRESH = D(17, ql.January, 2025)
SEASONED = D(16, ql.April, 2022)
CTPTY = dts.flat_spec(TODAY, 0.02)
CTPTY_HIGH = dts.flat_spec(TODAY, 0.08)
INVESTOR = dts.flat_spec(TODAY, 0.01)
CTPTY_CURVE = dts.curve_spec("HazardRateCurve", [TODAY, TODAY + 365, TODAY + 1826, TODAY + 3653],
                             [0.01, 0.015, 0.025, 0.03])
BLACK = {"kind": "blackVol", "volatility": 0.15}
QUOTE = {"kind": "blackQuote", "volatility": 0.22}
BACHELIER = {"kind": "bachelier", "volatility": 0.008}

# (label, swap, exposure, ctpty curve, ctpty recovery, investor curve, investor recovery); each swap's rate is replaced
# by its fair rate (at_par)
CASES = [
    ("5Y payer, Black, no investor curve", vanilla(PAYER, FRESH, 5, 0.031), BLACK, CTPTY, 0.4, None, 0.999),
    ("5Y receiver, Black, no investor curve", vanilla(RECEIVER, FRESH, 5, 0.031), BLACK, CTPTY, 0.4, None, 0.999),
    ("10Y payer, a volatility quote", vanilla(PAYER, FRESH, 10, 0.025), QUOTE, CTPTY_HIGH, 0.3, None, 0.999),
    ("10Y receiver, bilateral", vanilla(RECEIVER, FRESH, 10, 0.035), BLACK, CTPTY, 0.4, INVESTOR, 0.4),
    ("5Y payer, bilateral, Bachelier", vanilla(PAYER, FRESH, 5, 0.03, 0.001), BACHELIER, CTPTY, 0.4, INVESTOR, 0.5),
    ("5Y payer, an interpolated hazard curve", vanilla(PAYER, FRESH, 5, 0.032), BLACK, CTPTY_CURVE, 0.4, INVESTOR,
     0.4),
    ("seasoned 5Y payer", vanilla(PAYER, SEASONED, 5, 0.02), BLACK, CTPTY, 0.4, None, 0.999),
    ("seasoned 5Y receiver, bilateral", vanilla(RECEIVER, SEASONED, 5, 0.02), BLACK, CTPTY_HIGH, 0.4, INVESTOR, 0.4),
]


def fixings():
    target = ibc.euribor((6, ql.Months))
    return {fvf.make_index(E6M).name(): [[d.serialNumber(), x] for d, x in
                                         ibc.history(target, D(15, ql.January, 2022), TODAY - 1, v.SEED)]}


def at_par(spec, stored):
    """The swap spec at its fair rate on the wheel's discounting engine, the stored fixings set."""
    ql.Settings.instance().evaluationDate = TODAY
    index = fvf.make_index(spec["index"])
    for name, rows in stored.items():
        assert name == index.name(), name
        for serial, x in rows:
            index.addFixing(D(serial), x, True)
    sw = fvf.make_vanilla(spec, index)
    sw.setPricingEngine(fvf.engine(DISCOUNT))
    return {**spec, "fixedRate": sw.fairRate()}


def cases():
    out = []
    stored = fixings()
    with cxx.connect() as H:
        st = H.settings(TODAY.serialNumber(), fixings=stored)
        for label, swap, exposure, ctpty, ctpty_recovery, investor, investor_recovery in CASES:
            swap = at_par(swap, stored)
            npv, fair = H.call("CounterpartyAdjSwapEngine", swap, DISCOUNT, exposure, ctpty, ctpty_recovery,
                               investor, investor_recovery)
            scale = swap["nominal"]
            out.append({"name": label,
                        "inputs": {"settings": st, "swap": swap, "discountCurve": DISCOUNT, "exposure": exposure,
                                   "ctptyCurve": ctpty, "ctptyRecoveryRate": ctpty_recovery,
                                   "investorCurve": investor, "investorRecoveryRate": investor_recovery},
                        "expected": {"NPV": v.amount(npv, scale), "fairRate": fair}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/swap/cvaswapengine.hpp", cases)
