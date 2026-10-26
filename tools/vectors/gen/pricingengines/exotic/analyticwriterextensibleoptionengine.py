# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticWriterExtensibleOptionEngine
(ql/pricingengines/exotic/analyticwriterextensibleoptionengine.hpp, .cpp), on the evaluation date 15 October 2026:
writer-extensible calls and puts struck at 90, 100 or 110, expiring in six or nine months and extended, out of the
money there, to a year at 95, 105 or 115, on analyticcompoundoptionengine.py's flat markets: the NPV. QuantLib's
engine reads West's bivariate normal, as nanoql's does.

Flat markets only: on term structures QuantLib reads one constant rate, yield and volatility per maturity, nanoql's
ER4 form the discounts and variances by date (tests/NanoQL.Tests/Divergences/WriterExtensibleTermStructureTests.cs),
and on structures dated apart QuantLib's times differ (ExoticMismatchedTimesTests.cs).

A case holds analyticcompoundoptionengine.py's process spec and an option {"payoff1", "exercise1", "payoff2",
"exercise2"}. The wheel's calls, by QuantLib-SWIG 1.44's options.i: WriterExtensibleOption(payoff1, exercise1,
payoff2, exercise2), AnalyticWriterExtensibleOptionEngine(process), and the option's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Exotic/AnalyticWriterExtensibleOptionEngineVectorTests.cs."""
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


compound = _load("analyticcompoundoptionengine", "pricingengines/exotic/analyticcompoundoptionengine.py")
bsp, bc = compound.bsp, compound.bc
R = compound.R
CALL, PUT = compound.CALL, compound.PUT


def cases():
    st = v.settings(R)
    out = []
    for mname, pspec in compound.MARKETS:
        p = bsp.process(pspec)
        for t in (CALL, PUT):
            for k1 in (90.0, 100.0, 110.0):
                for t1, dk in ((182, 5.0), (273, -5.0)):
                    k2 = k1 + dk
                    ospec = {"payoff1": bc.payoff_spec("PlainVanilla", t, k1), "exercise1": compound.exercise_spec(t1),
                             "payoff2": bc.payoff_spec("PlainVanilla", t, k2), "exercise2": compound.exercise_spec(365)}
                    o = ql.WriterExtensibleOption(bc.payoff(ospec["payoff1"]), compound.exercise(ospec["exercise1"]),
                                                  bc.payoff(ospec["payoff2"]), compound.exercise(ospec["exercise2"]))
                    o.setPricingEngine(ql.AnalyticWriterExtensibleOptionEngine(p))
                    out.append({"name": f"{mname}, {'call' if t == CALL else 'put'} {k1:g} at {t1}, then {k2:g}",
                                "inputs": {"settings": st, "option": ospec, "process": pspec},
                                "expected": {"NPV": o.NPV()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/exotic/analyticwriterextensibleoptionengine.hpp", cases)
