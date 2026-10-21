# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DiscountingFxForwardEngine (ql/pricingengines/forward/discountingfxforwardengine.hpp, .cpp):
an FX forward's NPV, fair forward rate and NPV in each currency.

Forwards paying either currency, built from nominals and from a rate, with 0 and 2 settlement days on no calendar and
on TARGET, maturing in a week, a month, six months and two years, on the evaluation date and the day before it
(expired); priced on flat curves of both signs on Act/365F and Act/360, dated on the evaluation date, between it and
settlement, on the settlement date and after it (which the engine refuses), with spot rates above and below 1, and
zero and negative spots (refused). The NPV's and each currency NPV's scale is the nominal in its currency.

A case holds {"forward": the instrument generator's spec (tools/vectors/gen/instruments/fxforward.py), "sourceCurve",
"targetCurve": {"referenceDate", "rate", "dayCounter" ("Actual365Fixed" or "Actual360")}, "spotFx"} and its settings.
The outputs: "NPV", "fairForwardRate", "npvSourceCurrency", "npvTargetCurrency", null where QuantLib leaves them null
(an expired forward), each {"throws": true} where the engine refuses. The additional results, which the wheel doesn't
expose, are tools/vectors/gen/cxx/pricingengines/forward/discountingfxforwardengine.py's. The wheel's calls, by
QuantLib-SWIG 1.44's fxforward.i: DiscountingFxForwardEngine(sourceCurve, targetCurve, spot), setPricingEngine, NPV(),
fairForwardRate(), npvSourceCurrency() and npvTargetCurrency(). Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/DiscountingFxForwardEngineVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

R = ql.Date(15, ql.October, 2026)  # a Thursday
CURRENCIES = {"EUR": ql.EURCurrency, "USD": ql.USDCurrency, "SGD": ql.SGDCurrency, "JPY": ql.JPYCurrency}
DAY_COUNTERS = {"Actual365Fixed": ql.Actual365Fixed, "Actual360": ql.Actual360}


def forward_of(spec):
    calendar = [] if spec["calendar"] is None else [ql.TARGET()]
    maturity = ql.Date(spec["maturityDate"])
    src, tgt = CURRENCIES[spec["sourceCurrency"]](), CURRENCIES[spec["targetCurrency"]]()
    if spec["kind"] == "nominals":
        return ql.FxForward(spec["sourceNominal"], src, spec["targetNominal"], tgt, maturity,
                            spec["paySourceCurrency"], spec["settlementDays"], *calendar)
    return ql.FxForward(spec["sourceNominal"], src, tgt, spec["forwardRate"], maturity, spec["paySourceCurrency"],
                        spec["settlementDays"], *calendar)


def curve_of(spec):
    return ql.YieldTermStructureHandle(
        ql.FlatForward(ql.Date(spec["referenceDate"]), spec["rate"], DAY_COUNTERS[spec["dayCounter"]]()))


def case(name, forward, source_curve, target_curve, spot):
    with v.case_settings(R) as s:
        f = forward_of(forward)
        f.setPricingEngine(ql.DiscountingFxForwardEngine(curve_of(source_curve), curve_of(target_curve),
                                                         ql.QuoteHandle(ql.SimpleQuote(spot))))
        src_scale = forward["sourceNominal"]
        tgt_scale = f.targetNominal()
        try:
            f.NPV()
            expected = {"NPV": v.amount(f.NPV(), src_scale),
                        "fairForwardRate": v.result(f.fairForwardRate),
                        "npvSourceCurrency": v.amount(v.result(f.npvSourceCurrency), src_scale),
                        "npvTargetCurrency": v.amount(v.result(f.npvTargetCurrency), tgt_scale)}
        except RuntimeError:
            expected = {k: dict(v.THROWS) for k in ("NPV", "fairForwardRate", "npvSourceCurrency",
                                                     "npvTargetCurrency")}
        return {"name": name, "inputs": {"forward": forward, "sourceCurve": source_curve,
                                         "targetCurve": target_curve, "spotFx": spot, "settings": s},
                "expected": expected}


def cases():
    out = []
    t2 = ql.TARGET().advance(R, 2, ql.Days)
    maturities = [("1W", R + 7), ("1M", R + ql.Period(1, ql.Months)), ("6M", R + ql.Period(6, ql.Months)),
                  ("2Y", R + ql.Period(2, ql.Years)), ("today", R), ("expired", R - 1)]
    curves = [("today", R, 0.05, 0.035, "Actual365Fixed"), ("T+1", R + 1, 0.05, 0.035, "Actual365Fixed"),
              ("settlement", t2, -0.005, 0.02, "Actual360"), ("after settlement", t2 + 1, 0.05, 0.035,
                                                                "Actual365Fixed")]
    for mat_label, maturity in maturities:
        for kind in ("nominals", "rate"):
            for pay in (True, False):
                for days, cal in ((0, None), (2, "TARGET")):
                    for curve_label, ref, rs, rt, dc in curves:
                        forward = {"kind": kind, "sourceNominal": 1000000.0, "sourceCurrency": "USD",
                                   "targetCurrency": "SGD", "maturityDate": maturity.serialNumber(),
                                   "paySourceCurrency": pay, "settlementDays": days, "calendar": cal}
                        forward["targetNominal" if kind == "nominals" else "forwardRate"] = (
                            1350000.0 if kind == "nominals" else 1.36)
                        source = {"referenceDate": R.serialNumber(), "rate": rs, "dayCounter": dc}
                        target = {"referenceDate": ref.serialNumber(), "rate": rt, "dayCounter": dc}
                        out.append(case(f"{mat_label}, {kind}, pay {'source' if pay else 'target'}, {days} days, "
                                        f"target curve {curve_label}", forward, source, target, 1.35))
    base = {"kind": "nominals", "sourceNominal": 1000000.0, "sourceCurrency": "EUR", "targetNominal": 150000000.0,
            "targetCurrency": "JPY", "maturityDate": (R + ql.Period(1, ql.Years)).serialNumber(),
            "paySourceCurrency": False, "settlementDays": 2, "calendar": "TARGET"}
    for spot in (162.5, 0.0061, 0.0, -1.0):
        for ref_label, ref in (("today", R), ("after settlement", t2 + 1)):
            source = {"referenceDate": ref.serialNumber(), "rate": 0.025, "dayCounter": "Actual360"}
            target = {"referenceDate": R.serialNumber(), "rate": -0.001, "dayCounter": "Actual365Fixed"}
            out.append(case(f"EUR/JPY, spot {spot!r}, source curve {ref_label}", base, source, target, spot))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/forward/discountingfxforwardengine.hpp", cases)
