# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's mid-point CDS engine (ql/pricingengines/credit/midpointcdsengine.hpp), through the wheel's
MidPointCdsEngine on tools/vectors/gen/instruments/creditdefaultswap.py's swaps.

Each swap is priced on four credit curves (a flat 2.5% hazard rate, a hazard-rate curve, a default-density curve and
a survival-probability curve, tools/vectors/gen/termstructures/defaulttermstructure.py's, dated on the evaluation
date), at recoveries of 40% and 25%, under two evaluation dates (15 January 2024, and 20 March 2024, a coupon date)
and with includeReferenceDateEvents false or true. The engine's includeSettlementDateFlows, which the wheel can't
pass, is null, so the context's flags decide. The discount curve is flat, 3% continuous Actual/365, on the
evaluation date. A case's outputs are the engine's results, null where QuantLib's are "not available", amounts at the
swap's notional. Read back by tests/NanoQL.Tests/Vectors/PricingEngines/Credit/MidPointCdsEngineVectorTests.cs."""
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


def credit_specs(ev):
    nodes = [ev, ev + ql.Period(1, ql.Years), ev + ql.Period(3, ql.Years), ev + ql.Period(7, ql.Years),
             ev + ql.Period(15, ql.Years)]
    return [("flat", dts.flat_spec(ev, 0.025)),
            ("hazard rates", dts.curve_spec("HazardRateCurve", nodes, [0.01, 0.01, 0.02, 0.03, 0.035])),
            ("densities", dts.curve_spec("DefaultDensityCurve", nodes, [0.01, 0.015, 0.02, 0.018, 0.012])),
            ("probabilities", dts.curve_spec("SurvivalProbabilityCurve", nodes, [1.0, 0.985, 0.95, 0.85, 0.7]))]


def make_cases():
    out = []
    k = 0
    for label, spec in cds_gen.SWAPS:
        for ev in cds_gen.EVALUATIONS:
            for credit_label, credit in credit_specs(ev):
                for recovery, ref_events in ((0.4, False), (0.25, True)):
                    with v.case_settings(ev, include_reference_date_events=ref_events) as st:
                        cds = cds_gen.make_cds(spec)
                        disc = dts.discount_spec(ev, 0.03)
                        cds.setPricingEngine(ql.MidPointCdsEngine(dts.credit_handle(credit), recovery,
                                                                  dts.discount_handle(disc)))
                        out.append({"name": f"{label} / {ev.ISO()} / {credit_label} / {recovery} / {k}",
                                    "inputs": {"settings": st, "swap": spec, "credit": credit, "discount": disc,
                                               "recoveryRate": recovery},
                                    "expected": cds_gen.results(cds, spec["notional"])})
                    k += 1
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/credit/midpointcdsengine.hpp", make_cases)
