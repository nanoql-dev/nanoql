# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's RiskyBondEngine (ql/pricingengines/bond/riskybondengine.hpp), through the Bond it prices
(ql/instruments/bond.hpp): each future cash flow weighted by its survival probability, and each coupon period's
recovery on its nominal paid at the period's midpoint.

Four bonds (tools/vectors/gen/instruments/bond.py's specs, as tools/vectors/gen/pricingengines/bond/
discountingbondengine.py's: a semiannual fixed-rate bond with 2 settlement days, an annual one with none, a
zero-coupon bond and an amortizing bond), each under 3 evaluation dates around one of its payment dates (the payment
date, the day before it and the day after it), on a flat hazard rate of 2% or a hazard-rate curve rising from 1% to
4% (tools/vectors/gen/termstructures/defaulttermstructure.py's specs, dated on the evaluation date), at recoveries of
40% and 0. The risk-free curve is flat, 3.2% continuous Actual/365, on the evaluation date.

A case's outputs are the NPV, the settlement value and the valuation date, and the clean and dirty prices the bond
reads from them; NPVs and settlement values are compared at the bond's face amount. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/RiskyBondEngineVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, *rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bd = _load("wheel_bond", "instruments", "bond.py")
dbe = _load("wheel_discountingbondengine", "pricingengines", "bond", "discountingbondengine.py")
dts = _load("wheel_defaulttermstructure", "termstructures", "defaulttermstructure.py")


def credit_specs(ev):
    nodes = [ev, ev + ql.Period(1, ql.Years), ev + ql.Period(3, ql.Years), ev + ql.Period(10, ql.Years)]
    return [("flat 2%", dts.flat_spec(ev, 0.02)),
            ("hazard rates 1% to 4%", dts.curve_spec("HazardRateCurve", nodes, [0.01, 0.01, 0.025, 0.04]))]


def cases():
    out = []
    for name, spec, scale, payment in dbe.bonds():
        for ev in [payment, payment - 1, payment + 1]:
            for credit_label, credit in credit_specs(ev):
                for recovery in (0.4, 0.0):
                    with v.case_settings(ev) as st:
                        curve = bd.curve_spec(ev, bd.CURVE_RATE)
                        bond = bd.make_bond(spec)
                        bond.setPricingEngine(ql.RiskyBondEngine(dts.credit_handle(credit), recovery,
                                                                 ql.YieldTermStructureHandle(bd.make_curve(curve))))
                        expected = {"NPV": bd.scaled(bond.NPV, scale),
                                    "settlementValue": bd.scaled(bond.settlementValue, scale),
                                    "valuationDate": v.attempt(lambda: v.result(bond.valuationDate)),
                                    "cleanPrice": v.attempt(bond.cleanPrice),
                                    "dirtyPrice": v.attempt(bond.dirtyPrice)}
                        out.append({"name": f"{name} / {ev.ISO()} / {credit_label} / recovery {recovery}",
                                    "inputs": {"settings": st, "bond": spec, "curve": curve, "credit": credit,
                                               "recoveryRate": recovery},
                                    "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/bond/riskybondengine.hpp", cases)
