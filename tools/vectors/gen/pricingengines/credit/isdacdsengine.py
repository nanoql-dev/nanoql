# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ISDA CDS engine (ql/pricingengines/credit/isdacdsengine.hpp), through the wheel's
IsdaCdsEngine on tools/vectors/gen/instruments/creditdefaultswap.py's swaps.

Each swap is priced under two evaluation dates (15 January 2024, and 20 March 2024, a coupon date) on a discount
curve flat at 3% or log-linear on yearly nodes to 30 years, and a credit curve flat at 2% or backward-flat on nodes
to 20 years, each Actual/365 (Fixed) on the evaluation date, at a recovery of 40%, with four of the engine's option
sets: QuantLib's defaults; no numerical fix with the half-day bias and flat forwards; Taylor without the bias,
piecewise; and none without the bias, flat. The wheel passes includeSettlementDateFlows with the options, false or
true by turns; the harness's cxx/pricingengines/credit/isdacdsengine leaves it unset. The curves' nodes run past
every maturity, so the protection leg's end is QuantLib's (tests/NanoQL.Tests/Divergences/
IsdaProtectionLegMaturityTests.cs). The swaps the engine refuses (accrual not settled, 30/360 coupons) and the
curves it refuses (Actual/360, a curve dated the next day, a linear survival curve) are refused.

A case's outputs are the engine's results, null where QuantLib's are "not available", amounts at the swap's notional;
a refused case's results are each {"throws": true}. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Credit/IsdaCdsEngineVectorTests.cs."""
import importlib.util
import math
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

# the wheel names QuantLib's IsdaCdsEngine::None NoFix, None being Python's
TAYLOR, NONE = int(ql.IsdaCdsEngine.Taylor), int(ql.IsdaCdsEngine.NoFix)
HALF_DAY, NO_BIAS = int(ql.IsdaCdsEngine.HalfDayBias), int(ql.IsdaCdsEngine.NoBias)
FLAT, PIECEWISE = int(ql.IsdaCdsEngine.Flat), int(ql.IsdaCdsEngine.Piecewise)
OPTIONS = [(TAYLOR, HALF_DAY, PIECEWISE), (NONE, HALF_DAY, FLAT), (TAYLOR, NO_BIAS, PIECEWISE), (NONE, NO_BIAS, FLAT)]


def discount_specs(ev):
    dates = [ev] + [ev + ql.Period(n, ql.Years) for n in range(1, 31)]
    dc = ql.Actual365Fixed()
    dfs = [math.exp(-(0.02 + 0.0005 * n) * dc.yearFraction(ev, d)) for n, d in enumerate(dates)]
    return [("flat", dts.discount_spec(ev, 0.03)), ("nodes", dts.discount_curve_spec(dates, dfs))]


def credit_specs(ev):
    nodes = [ev, ev + ql.Period(6, ql.Months), ev + ql.Period(2, ql.Years), ev + ql.Period(5, ql.Years),
             ev + ql.Period(20, ql.Years)]
    return [("flat", dts.flat_spec(ev, 0.02)),
            ("hazard rates", dts.curve_spec("HazardRateCurve", nodes, [0.01, 0.01, 0.015, 0.025, 0.03]))]


def refused_specs(ev):
    nodes = [ev, ev + ql.Period(2, ql.Years), ev + ql.Period(20, ql.Years)]
    return [("discount Actual/360", dts.discount_spec(ev, 0.03, dts.A360), dts.flat_spec(ev, 0.02)),
            ("credit dated the next day", dts.discount_spec(ev, 0.03), dts.flat_spec(ev + 1, 0.02)),
            ("linear survival curve", dts.discount_spec(ev, 0.03),
             dts.curve_spec("SurvivalProbabilityCurve", nodes, [1.0, 0.97, 0.6]))]


def price(spec, ev, disc, credit, options, include, ref_events):
    with v.case_settings(ev, include_reference_date_events=ref_events) as st:
        cds = cds_gen.make_cds(spec)
        fix, bias, fwd = options
        cds.setPricingEngine(ql.IsdaCdsEngine(dts.credit_handle(credit), 0.4, dts.discount_handle(disc), include,
                                              fix, bias, fwd))
        return st, cds_gen.results(cds, spec["notional"])


def make_cases():
    out = []
    k = 0
    for label, spec in cds_gen.SWAPS:
        for ev in cds_gen.EVALUATIONS:
            for disc_label, disc in discount_specs(ev):
                for credit_label, credit in credit_specs(ev):
                    options = OPTIONS[k % len(OPTIONS)]
                    include = k % 2 == 1
                    st, expected = price(spec, ev, disc, credit, options, include, False)
                    out.append({"name": f"{label} / {ev.ISO()} / {disc_label} / {credit_label} / {k}",
                                "inputs": {"settings": st, "swap": spec, "credit": credit, "discount": disc,
                                           "recoveryRate": 0.4, "includeSettlementDateFlows": include,
                                           "numericalFix": options[0], "accrualBias": options[1],
                                           "forwardsInCouponPeriod": options[2]},
                                "expected": expected})
                    k += 1
    label, spec = cds_gen.SWAPS[0]
    for refused_label, disc, credit in refused_specs(cds_gen.TODAY):
        st, expected = price(spec, cds_gen.TODAY, disc, credit, OPTIONS[0], False, False)
        out.append({"name": f"refused {refused_label} / {k}",
                    "inputs": {"settings": st, "swap": spec, "credit": credit, "discount": disc, "recoveryRate": 0.4,
                               "includeSettlementDateFlows": False, "numericalFix": TAYLOR, "accrualBias": HALF_DAY,
                               "forwardsInCouponPeriod": PIECEWISE},
                    "expected": expected})
        k += 1
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/credit/isdacdsengine.hpp", make_cases)
