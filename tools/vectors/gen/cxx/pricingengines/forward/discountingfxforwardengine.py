# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DiscountingFxForwardEngine (ql/pricingengines/forward/discountingfxforwardengine.hpp, .cpp),
each output computed by the C++ harness: the additional results the wheel doesn't expose, "spotFx",
"sourceCurrencyDiscountFactor", "targetCurrencyDiscountFactor", "sourceCurrencySettlementDiscountFactor",
"targetCurrencySettlementDiscountFactor", "sourceCurrencyPV" and "targetCurrencyPV", of USD/SGD forwards built from
their nominals, paying either currency, settling in 0 or 2 days on no calendar or TARGET, maturing on the evaluation
date, in a week, six months and two years, on flat forwards on Act/365F and Act/360 dated on the evaluation date, a
day later and after settlement (which the engine refuses), with spot rates above and below 1. An expired forward is
left out: its results hold no additional results, which the harness's read can't tell from a missing key (its
std::out_of_range stops the generator); the wheel's file gives its NPV.

A case holds {"forward": {"sourceNominal", "targetNominal", "maturityDate", "paySourceCurrency", "settlementDays",
"calendar" (null or "TARGET")}, "sourceCurve", "targetCurve": {"referenceDate", "rate", "dayCounter"}, "spotFx"} and
its settings; each output is {"throws": true} where the engine refuses. Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/DiscountingFxForwardEngineCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310  # 15 October 2026, a Thursday
KEYS = ["spotFx", "sourceCurrencyDiscountFactor", "targetCurrencyDiscountFactor",
        "sourceCurrencySettlementDiscountFactor", "targetCurrencySettlementDiscountFactor", "sourceCurrencyPV",
        "targetCurrencyPV"]
DAY_COUNTERS = {"Actual365Fixed": {"type": "Actual365Fixed"}, "Actual360": {"type": "Actual360"}}


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        maturities = [("today", R), ("1W", R + 7), ("6M", 46492), ("2Y", 47041)]
        curves = [("today", R, 0.05, 0.035, "Actual365Fixed"), ("a day later", R + 1, -0.005, 0.02, "Actual360"),
                  ("after settlement", R + 6, 0.05, 0.035, "Actual365Fixed")]
        for mat_label, maturity in maturities:
            for pay in (True, False):
                for days, cal in ((0, None), (2, "TARGET")):
                    for curve_label, ref, rs, rt, dc in curves:
                        for spot in (1.35, 0.74):
                            forward = {"sourceNominal": 1000000.0, "targetNominal": 1350000.0,
                                       "maturityDate": maturity, "paySourceCurrency": pay, "settlementDays": days,
                                       "calendar": cal}
                            source = {"referenceDate": R, "rate": rs, "dayCounter": dc}
                            target = {"referenceDate": ref, "rate": rt, "dayCounter": dc}
                            try:
                                results = H.call("DiscountingFxForwardEngine::additionalResults", 1000000.0,
                                                 1350000.0, maturity, pay, days, cal is not None, R, rs,
                                                 DAY_COUNTERS[dc], ref, rt, DAY_COUNTERS[dc], spot)
                                expected = {key: results[key] for key in KEYS}
                            except RuntimeError:
                                expected = {key: dict(v.THROWS) for key in KEYS}
                            out.append({"name": f"{mat_label}, pay {'source' if pay else 'target'}, {days} days, "
                                                f"target curve {curve_label}, spot {spot}",
                                        "inputs": {"forward": forward, "sourceCurve": source, "targetCurve": target,
                                                   "spotFx": spot, "settings": st},
                                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/forward/discountingfxforwardengine.hpp", cases)
