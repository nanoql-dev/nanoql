# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DiscountingBondEngine (ql/pricingengines/bond/discountingbondengine.hpp) with its
includeSettlementDateFlows, which the wheel can't pass, each output computed by the C++ harness.

The bonds are tools/vectors/gen/pricingengines/bond/discountingbondengine.py's 4, imported, each priced under its 4
evaluation dates around one of its payment dates (the payment date itself, the day before it, the business day two
before it, so that the bond settles on it, and the day after it), each with one of the wheel generator's 12 settings
drawn with the seed (includeReferenceDateEvents x includeTodaysCashFlows x the curve's reference date, the evaluation
date or a week before it); on the payment date itself includeTodaysCashFlows is null and the curve's reference date the
evaluation date, so that the engine's flag decides whether the NPV counts the flow on it. A case's outputs are, for the
engine's includeSettlementDateFlows null, false and true (one element per flag): the NPV, the settlement value, the
valuation date, the error estimate (null where QuantLib throws "not provided"), and the clean and dirty prices the bond
reads from them. NPVs and settlement values are compared at the bond's face amount.

Each case's inputs hold its settings, "bond", "curve" and "flags". Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/DiscountingBondEngineCxxVectorTests.cs."""
import importlib.util
import os
import random
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


bd = _load("wheel_bond", "instruments/bond.py")
we = _load("wheel_discountingbondengine", "pricingengines/bond/discountingbondengine.py")

FLAGS = [None, False, True]


def outputs(H, spec, curve, scale):
    def result(method, include):
        try:
            return v.result(lambda: H.call(method, spec, curve, include))
        except RuntimeError:
            return dict(v.THROWS)

    def amount(method, include):
        r = result(method, include)
        return r if isinstance(r, dict) or r is None else v.amount(r, scale)

    return {"NPV": [amount("DiscountingBondEngine::NPV", f) for f in FLAGS],
            "settlementValue": [amount("DiscountingBondEngine::settlementValue", f) for f in FLAGS],
            "valuationDate": [result("DiscountingBondEngine::valuationDate", f) for f in FLAGS],
            "errorEstimate": [result("DiscountingBondEngine::errorEstimate", f) for f in FLAGS],
            "cleanPrice": [v.attempt(lambda f=f: H.call("Bond::cleanPrice()", spec, curve, f)) for f in FLAGS],
            "dirtyPrice": [v.attempt(lambda f=f: H.call("Bond::dirtyPrice()", spec, curve, f)) for f in FLAGS]}


def cases():
    rnd = random.Random(v.SEED)
    out = []
    with cxx.connect() as H:
        for name, spec, scale, payment in we.bonds():
            cal = ql.TARGET()
            for ev in [payment, payment - 1, cal.advance(payment, -2, ql.Days), payment + 1]:
                settings = [(ref_events, todays, shift) for ref_events in (False, True)
                            for todays in (None, False, True) for shift in (0, 7)]
                if ev == payment:
                    # the flow on the curve's reference date, which only the engine's flag decides
                    settings = [(ref_events, None, 0) for ref_events in (False, True)]
                ref_events, todays, shift = rnd.choice(settings)
                with H.case_settings(v.enc(ev), include_reference_date_events=ref_events,
                                     include_todays_cash_flows=todays) as st:
                    curve = bd.curve_spec(ev - shift, bd.CURVE_RATE)
                    out.append({"name": f"{name} / {ev.ISO()} {ref_events} {todays} / curve {shift} days before",
                                "inputs": {"settings": st, "bond": spec, "curve": curve, "flags": FLAGS},
                                "expected": outputs(H, spec, curve, scale)})
    check_rows(out)
    return out


def check_rows(out):
    """The rows the file must hold, whatever the draws: a flow on the curve's reference date that the NPV counts
    under one flag and not under the other."""
    assert any(c["expected"]["NPV"][1] != c["expected"]["NPV"][2] for c in out)


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/bond/discountingbondengine.hpp", cases)
