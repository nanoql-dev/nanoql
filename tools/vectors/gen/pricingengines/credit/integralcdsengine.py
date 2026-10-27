# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's integral CDS engine (ql/pricingengines/credit/integralcdsengine.hpp, .cpp), through the
wheel's IntegralCdsEngine on tools/vectors/gen/instruments/creditdefaultswap.py's swaps.

Each swap is priced on midpointcdsengine.py's four credit curves (a flat 2.5% hazard rate, a hazard-rate curve, a
default-density curve and a survival-probability curve, dated on the evaluation date), at integration steps of 1 day,
1 week and 1 month, under two evaluation dates (15 January 2024, and 20 March 2024, a coupon date), at a recovery of
40% with includeReferenceDateEvents false, and of 25% with it true. The engine's includeSettlementDateFlows is the
wheel's default, false, as the vector test passes it. The discount curve is flat, 3% continuous Actual/365, on the
evaluation date. A case's outputs are the engine's results, null where QuantLib's are "not available", amounts at the
swap's notional. Read back by tests/NanoQL.Tests/Vectors/PricingEngines/Credit/IntegralCdsEngineVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_creditdefaultswap",
                                               os.path.join(v.GEN_ROOT, "instruments", "creditdefaultswap.py"))
cds_gen = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cds_gen)
dts = cds_gen.dts

_spec = importlib.util.spec_from_file_location("wheel_midpointcdsengine",
                                               os.path.join(v.GEN_ROOT, "pricingengines", "credit",
                                                            "midpointcdsengine.py"))
midpoint = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(midpoint)

STEPS = [(1, ql.Days), (1, ql.Weeks), (1, ql.Months)]


def make_cases():
    out = []
    k = 0
    for label, spec in cds_gen.SWAPS:
        for ev in cds_gen.EVALUATIONS:
            for credit_label, credit in midpoint.credit_specs(ev):
                for n, unit in STEPS:
                    for recovery, ref_events in ((0.4, False), (0.25, True)):
                        with v.case_settings(ev, include_reference_date_events=ref_events) as st:
                            cds = cds_gen.make_cds(spec)
                            disc = dts.discount_spec(ev, 0.03)
                            cds.setPricingEngine(ql.IntegralCdsEngine(ql.Period(n, unit), dts.credit_handle(credit),
                                                                      recovery, dts.discount_handle(disc)))
                            out.append({"name": f"{label} / {ev.ISO()} / {credit_label} / {n} {unit} / {recovery} / {k}",
                                        "inputs": {"settings": st, "swap": spec, "credit": credit, "discount": disc,
                                                   "recoveryRate": recovery, "step": [n, int(unit)]},
                                        "expected": cds_gen.results(cds, spec["notional"])})
                        k += 1
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/credit/integralcdsengine.hpp", make_cases)
