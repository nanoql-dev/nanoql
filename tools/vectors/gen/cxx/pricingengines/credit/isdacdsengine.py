# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ISDA CDS engine (ql/pricingengines/credit/isdacdsengine.hpp) with includeSettlementDateFlows
unset, which the wheel can't pass with the engine's options, each output computed by the C++ harness.

- "price" cases: tools/vectors/gen/instruments/creditdefaultswap.py's swaps the engine accepts, under two evaluation
  dates (15 January 2024, and 20 March 2024, a coupon date), with includeReferenceDateEvents and
  includeTodaysCashFlows set as each case gives, on tools/vectors/gen/pricingengines/credit/isdacdsengine.py's curves
  and with each of the engine's eight option sets in turn: the engine's results, null where QuantLib's are "not
  available"; an expired swap's accrual-rebate NPV, which QuantLib leaves unset and reads uninitialized
  (tests/NanoQL.Tests/Divergences/CdsExpiredAccrualRebateTests.cs), isn't written.
- "option" cases: an option outside its enum: refused.

A case's inputs hold its settings, the swap, the credit and discount curves, the recovery and the options, as
tools/cxx/src/pricingengines/credit/isdacdsengine.cpp reads them. Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/IsdaCdsEngineCxxVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location(
    "wheel_isdacdsengine", os.path.join(v.GEN_ROOT, "pricingengines", "credit", "isdacdsengine.py"))
ice = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ice)
cds_gen = ice.cds_gen

OPTIONS = [(fix, bias, fwd) for fix in (ice.NONE, ice.TAYLOR) for bias in (ice.HALF_DAY, ice.NO_BIAS)
           for fwd in (ice.FLAT, ice.PIECEWISE)]
ACCEPTED = [s for s in cds_gen.SWAPS if s[0] not in ("accrual not settled, paid at the period's end",
                                                     "30/360 coupons")]
FLAGS = [(False, None), (True, None), (False, True), (True, False)]


def cases():
    out = []
    k = 0
    with cxx.connect() as H:
        for label, spec in ACCEPTED:
            for ev in cds_gen.EVALUATIONS:
                for disc_label, disc in ice.discount_specs(ev):
                    for credit_label, credit in ice.credit_specs(ev):
                        ref_events, todays = FLAGS[k % len(FLAGS)]
                        options = OPTIONS[k % len(OPTIONS)]
                        with H.case_settings(v.enc(ev), include_reference_date_events=ref_events,
                                             include_todays_cash_flows=todays) as st:
                            results = H.call("IsdaCdsEngine::results", spec, credit, disc, 0.4, None, *options)
                        with v.case_settings(ev):  # the wheel's swap, for its expiry alone
                            expired = cds_gen.make_cds(spec).isExpired()
                        expected = {name: (value if value is None or name in ("fairSpread", "fairUpfront")
                                           else v.amount(value, spec["notional"]))
                                    for name, value in results.items()
                                    if not (expired and name == "accrualRebateNPV")}
                        out.append({"name": f"price {label} / {ev.ISO()} / {disc_label} / {credit_label} / {k}",
                                    "inputs": {"settings": st, "kind": "price", "swap": spec, "credit": credit,
                                               "discount": disc, "recoveryRate": 0.4,
                                               "includeSettlementDateFlows": None, "numericalFix": options[0],
                                               "accrualBias": options[1], "forwardsInCouponPeriod": options[2]},
                                    "expected": expected})
                        k += 1
        label, spec = ACCEPTED[0]
        disc, credit = ice.discount_specs(cds_gen.TODAY)[0][1], ice.credit_specs(cds_gen.TODAY)[0][1]
        for name, options in (("numericalFix", (5, ice.HALF_DAY, ice.PIECEWISE)),
                              ("accrualBias", (ice.TAYLOR, 5, ice.PIECEWISE)),
                              ("forwardsInCouponPeriod", (ice.TAYLOR, ice.HALF_DAY, 5))):
            with H.case_settings(v.enc(cds_gen.TODAY)) as st:
                out.append({"name": f"option {name} outside its enum / {k}",
                            "inputs": {"settings": st, "kind": "option", "swap": spec, "credit": credit,
                                       "discount": disc, "recoveryRate": 0.4, "includeSettlementDateFlows": None,
                                       "numericalFix": options[0], "accrualBias": options[1],
                                       "forwardsInCouponPeriod": options[2]},
                            "expected": {"NPV": v.attempt(
                                lambda: H.call("IsdaCdsEngine::results", spec, credit, disc, 0.4, None, *options))}})
            k += 1
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/credit/isdacdsengine.hpp", cases)
