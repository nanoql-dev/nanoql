# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticSimpleChooserEngine (ql/pricingengines/exotic/analyticsimplechooserengine.hpp,
.cpp), on the evaluation date 15 October 2026: simple choosers at strikes 90, 100 and 110, choosing in one or three
months and expiring in six months or a year, on analyticcompoundoptionengine.py's flat markets: the NPV.

Flat markets only: on term structures QuantLib reads one constant rate, yield and volatility to expiry, nanoql prices
the call and put by the analytic European engine by date
(tests/NanoQL.Tests/Divergences/SimpleChooserTermStructureTests.cs), and QuantLib refuses curves on different day
counters (ExoticMismatchedTimesTests.cs).

A case holds analyticcompoundoptionengine.py's process spec and an option {"choosingDate", "strike", "exercise"}. The
wheel's calls, by QuantLib-SWIG 1.44's options.i: SimpleChooserOption(choosingDate, strike, exercise),
AnalyticSimpleChooserEngine(process), and the option's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Exotic/AnalyticSimpleChooserEngineVectorTests.cs."""
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
bsp = compound.bsp
R = compound.R


def cases():
    st = v.settings(R)
    out = []
    for mname, pspec in compound.MARKETS:
        p = bsp.process(pspec)
        for choosing, expiry in ((30, 182), (91, 365)):
            for k in (90.0, 100.0, 110.0):
                ospec = {"choosingDate": (R + choosing).serialNumber(), "strike": k,
                         "exercise": compound.exercise_spec(expiry)}
                o = ql.SimpleChooserOption(ql.Date(ospec["choosingDate"]), k, compound.exercise(ospec["exercise"]))
                o.setPricingEngine(ql.AnalyticSimpleChooserEngine(p))
                out.append({"name": f"{mname}, choosing {choosing}, expiry {expiry}, strike {k:g}",
                            "inputs": {"settings": st, "option": ospec, "process": pspec},
                            "expected": {"NPV": o.NPV()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/exotic/analyticsimplechooserengine.hpp", cases)
