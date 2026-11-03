# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's zero-coupon swap (ql/instruments/zerocouponswap.hpp), through the wheel's ZeroCouponSwap and
DiscountingSwapEngine, on the evaluation date 15 January 2025, a Euribor 6M index on a flat 3.1% curve with the stored
past fixings of cashflows/iborcoupon.py, and a flat 2.5% discounting curve.

Each "swap" case is a payer or a receiver, built from a fixed payment or from a fixed rate and its day counter, starting
in the past, today or later, with a payment delay, calendar and convention: NPV, fixedLegNPV, floatingLegNPV,
fixedPayment, the legs' payment dates and amounts, startDate and maturityDate, and fairFixedPayment and fairFixedRate
(on Actual/365 (Fixed) and 30/360) where QuantLib's are right. QuantLib discounts the fair payment at its fixed leg's
maturity date (zerocouponswap.cpp:147-153), the fixed-rate coupon's accrual end, so for a swap built from a fixed rate
whose payment date isn't its maturity date, by a payment delay or an adjusted maturity, the fair payment and rate are
left out (tests/NanoQL.Tests/Divergences/ZeroCouponSwapFairPaymentDelayTests.cs).

A case's inputs hold its settings with the stored fixings, the index (a spec of indexes/interestrateindex.py), the
discounting curve, and the swap's arguments ("fixedPayment" or "fixedRate" with "fixedDayCounter"). Read back by
tests/NanoQL.Tests/Vectors/Instruments/ZeroCouponSwapVectorTests.cs."""
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


irx = _load("wheel_interestrateindex", "indexes/interestrateindex.py")
cf = _load("wheel_cashflows", "cashflows/cashflows.py")
ibc = _load("wheel_iborcoupon", "cashflows/iborcoupon.py")

TODAY = irx.TODAY
D = ql.Date
TARGET, UK = cf.TARGET, cf.UK_SETTLEMENT
A360, A365, THIRTY360_BB = cf.A360, cf.A365, cf.THIRTY360_BB
E6M, E3M = ibc.E6M, ibc.E3M
DISCOUNT = ibc.DISCOUNT
PAYER, RECEIVER = int(ql.Swap.Payer), int(ql.Swap.Receiver)

# (label, type, nominal, start, maturity, ("payment", amount) or ("rate", rate, dc), index, calendar, convention,
#  delay)
SWAPS = [
    ("payment, 5Y, receiver", RECEIVER, 1e8, D(17, ql.January, 2025), D(19, ql.January, 2030), ("payment", 1.7e7),
     E6M, TARGET, ql.Following, 0),
    ("payment, 5Y, payer, delay 2", PAYER, 1e8, D(17, ql.January, 2025), D(19, ql.January, 2030), ("payment", 1.7e7),
     E6M, TARGET, ql.Following, 2),
    ("payment, 10Y, delay 10 on another calendar", RECEIVER, 5e7, D(15, ql.July, 2025), D(15, ql.July, 2035),
     ("payment", 1.8e7), E6M, UK, ql.ModifiedFollowing, 10),
    ("payment, started", PAYER, 1e8, D(17, ql.April, 2024), D(17, ql.April, 2029), ("payment", 1.6e7), E6M, TARGET,
     ql.Following, 0),
    ("payment, 3M index", RECEIVER, 1e7, D(20, ql.March, 2025), D(20, ql.March, 2028), ("payment", 9.5e5), E3M,
     TARGET, ql.ModifiedFollowing, 0),
    ("rate, 5Y, receiver", RECEIVER, 1e8, D(17, ql.January, 2025), D(17, ql.January, 2030), ("rate", 0.032, A365),
     E6M, TARGET, ql.Following, 0),
    ("rate, 7Y, payer, 30/360", PAYER, 1e8, D(15, ql.April, 2025), D(15, ql.April, 2032), ("rate", 0.03, THIRTY360_BB),
     E6M, TARGET, ql.Following, 0),
    ("rate, started", RECEIVER, 2e7, D(16, ql.October, 2024), D(16, ql.October, 2028), ("rate", 0.029, A360), E3M,
     TARGET, ql.Following, 0),
    ("rate, delay 2", RECEIVER, 1e8, D(17, ql.January, 2025), D(17, ql.January, 2030), ("rate", 0.032, A365), E6M,
     TARGET, ql.Following, 2),
    ("rate, maturity on a Saturday", PAYER, 1e8, D(17, ql.January, 2025), D(17, ql.January, 2032),
     ("rate", 0.031, A365), E6M, TARGET, ql.Following, 0),
]


def make_swap(swap_type, nominal, start, maturity, fixed, index, calendar, convention, delay):
    if fixed[0] == "payment":
        return ql.ZeroCouponSwap(swap_type, nominal, start, maturity, fixed[1], index, calendar, convention, delay)
    return ql.ZeroCouponSwap(swap_type, nominal, start, maturity, fixed[1], cf.day_counter(fixed[2]), index, calendar,
                             convention, delay)


def cases():
    out = []
    for k, (label, swap_type, nominal, start, maturity, fixed, index_spec, cal, convention, delay) in enumerate(SWAPS):
        with v.case_settings(TODAY, fixings=ibc.fixings_for(index_spec)) as st:
            index = irx.make_index(index_spec)
            swap = make_swap(swap_type, nominal, start, maturity, fixed, index, cf.calendar(cal), convention, delay)
            swap.setPricingEngine(ql.DiscountingSwapEngine(irx.handle(DISCOUNT)))
            fixed_flow, floating_flow = swap.fixedLeg()[0], swap.floatingLeg()[0]
            expected = {"NPV": v.attempt(swap.NPV), "fixedLegNPV": v.attempt(swap.fixedLegNPV),
                        "floatingLegNPV": v.attempt(swap.floatingLegNPV), "fixedPayment": swap.fixedPayment(),
                        "fixedDate": v.enc(fixed_flow.date()), "fixedAmount": v.attempt(fixed_flow.amount),
                        "floatingDate": v.enc(floating_flow.date()), "floatingAmount": v.attempt(floating_flow.amount),
                        "startDate": v.enc(swap.startDate()), "maturityDate": v.enc(swap.maturityDate())}
            if fixed[0] == "payment" or fixed_flow.date() == swap.maturityDate():
                expected.update({"fairFixedPayment": v.attempt(swap.fairFixedPayment),
                                 "fairFixedRateA365": v.attempt(lambda: swap.fairFixedRate(ql.Actual365Fixed())),
                                 "fairFixedRate30360": v.attempt(
                                     lambda: swap.fairFixedRate(ql.Thirty360(ql.Thirty360.BondBasis)))})
            inputs = {"settings": st, "index": index_spec, "discountCurve": DISCOUNT, "type": swap_type,
                      "baseNominal": nominal, "startDate": v.enc(start), "maturityDate": v.enc(maturity),
                      "paymentCalendar": cal, "paymentConvention": int(convention), "paymentDelay": delay}
            if fixed[0] == "payment":
                inputs["fixedPayment"] = fixed[1]
            else:
                inputs.update({"fixedRate": fixed[1], "fixedDayCounter": fixed[2]})
            out.append({"name": f"swap {label} / {k}", "inputs": inputs, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/zerocouponswap.hpp", cases)
