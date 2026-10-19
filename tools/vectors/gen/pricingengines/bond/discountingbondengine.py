# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DiscountingBondEngine (ql/pricingengines/bond/discountingbondengine.hpp), through the Bond
it prices (ql/instruments/bond.hpp): the cash flows on the curve's reference date and on the bond's settlement date,
which the NPV counts as the flags say and the settlement value never counts.

Four bonds (tools/vectors/gen/instruments/bond.py's specs: a semiannual fixed-rate bond with 2 settlement days, an
annual one on the null calendar with none, a zero-coupon bond and an amortizing bond), each priced under 4 evaluation
dates around one of its payment dates (the payment date itself, the day before it, the business day two before it,
so that the bond settles on it, and the day after it) and, for each, 6 of the 12 settings drawn with the seed:
includeReferenceDateEvents x includeTodaysCashFlows (null, false or true) x the curve's reference date (the
evaluation date, or a week before it). The engine's includeSettlementDateFlows, which the wheel can't pass, is null,
so the context's includeReferenceDateEvents decides; the harness's cxx/pricingengines/bond/discountingbondengine
passes it. The curve is flat, 3.2% continuous Actual/365.

A case's outputs are the NPV, the settlement value, the valuation date, the error estimate (null where QuantLib
throws "not provided"), and the clean and dirty prices the bond reads from them; an expired bond gives zeros, an error
estimate of 0 and a null valuation date. NPVs and settlement values are compared at the bond's face amount. Read back
by tests/NanoQL.Tests/Vectors/PricingEngines/DiscountingBondEngineVectorTests.cs."""
import importlib.util
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_bond", os.path.join(v.GEN_ROOT, "instruments", "bond.py"))
bd = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bd)

D = ql.Date


def bonds():
    """(name, bond spec, face amount, the payment date the evaluation dates are around) for the 4 bonds."""
    s6m = bd.schedule_spec(D(15, 3, 2024), D(15, 3, 2028), (6, ql.Months), bd.TARGET, ql.ModifiedFollowing)
    s1y = bd.schedule_spec(D(16, 6, 2025), D(16, 6, 2030), (1, ql.Years), bd.NULL_CALENDAR, ql.Unadjusted,
                           ql.DateGeneration.Forward)
    sinking, notionals = bd.schedule_spec(D(15, 1, 2025), D(15, 1, 2029), (1, ql.Years), bd.TARGET), \
        [100.0, 77.0, 52.0, 27.0]
    return [
        ("fixed 6M", bd.fixed_bond_spec(2, 100.0, s6m, [0.05], bd.A360, ql.Following, 100.0, D(15, 3, 2024)), 100.0,
         D(15, 9, 2026)),
        ("fixed 1Y, no settlement days", bd.fixed_bond_spec(0, 100.0, s1y, [0.03], bd.THIRTY360_BB, ql.Unadjusted),
         100.0, D(16, 6, 2027)),
        ("zero coupon", bd.zero_bond_spec(2, bd.TARGET, 1e6, D(16, 6, 2028)), 1e6, D(16, 6, 2028)),
        ("amortizing", bd.amortizing_bond_spec(2, notionals, sinking, [0.04], bd.A365), 100.0, D(15, 1, 2027)),
    ]


def outputs(bond, scale):
    def price(f):
        return v.attempt(f)
    return {"NPV": bd.scaled(bond.NPV, scale),
            "settlementValue": bd.scaled(lambda: v.result(lambda: bond.settlementValue()), scale),
            "valuationDate": v.attempt(lambda: v.result(bond.valuationDate)),
            "errorEstimate": v.attempt(lambda: v.result(bond.errorEstimate)),
            "cleanPrice": price(bond.cleanPrice),
            "dirtyPrice": price(bond.dirtyPrice)}


def cases():
    rnd = random.Random(v.SEED)
    out = []
    for name, spec, scale, payment in bonds():
        cal = ql.TARGET()
        for ev in [payment, payment - 1, cal.advance(payment, -2, ql.Days), payment + 1]:
            settings = [(ref_events, todays, shift) for ref_events in (False, True) for todays in (None, False, True)
                        for shift in (0, 7)]
            for ref_events, todays, shift in rnd.sample(settings, 6):
                st = v.settings(ev, ref_events, todays)
                curve = bd.curve_spec(ev - shift, bd.CURVE_RATE)
                bond = bd.make_bond(spec)
                bond.setPricingEngine(ql.DiscountingBondEngine(ql.YieldTermStructureHandle(bd.make_curve(curve))))
                out.append({"name": f"{name} / {ev.ISO()} {ref_events} {todays} / curve {shift} days before",
                            "inputs": {"settings": st, "bond": spec, "curve": curve},
                            "expected": outputs(bond, scale)})
    check_rows(out)
    return out


def check_rows(out):
    """The rows the file must hold, whatever the draws: a flow on the curve's reference date that the NPV counts and
    the settlement value doesn't."""
    assert any(c["expected"]["NPV"] != c["expected"]["settlementValue"] for c in out)
    assert any(c["expected"]["valuationDate"] is None for c in out)


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/bond/discountingbondengine.hpp", cases)
