# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticComplexChooserEngine (ql/pricingengines/exotic/analyticcomplexchooserengine.hpp,
.cpp), on the evaluation date 15 October 2026: complex choosers choosing in one or three months between a call struck
at 95 or 105 and a put struck at 95 or 105, expiring in six, nine or twelve months, on
analyticcompoundoptionengine.py's flat markets: the NPV.

nanoql takes QuantLib's fix after v1.44 (#2891, bc3a54105; the S-exo plan's ER6 (ii)): the maturities measured from
the evaluation date, where 1.44 measures them from the choosing date and its critical value from twice it. On a flat
market 1.44 gives the fixed value for maturities placed the choosing time later, as its own test case does (ported
on a // Moved: line): each case's wheel option is built so, its inputs the true maturities. QuantLib's engine reads
Drezner's bivariate normal, nanoql's West's (ER7), so every value carries §3.3's override: Drezner's accuracy, |Dr78 -
We04DP| <= 6e-7 times the bivariate terms' coefficients (EP39), relative to the value. Flat markets only: on term
structures nanoql's ER4 form reads by date (tests/NanoQL.Tests/Divergences/ComplexChooserTermStructureTests.cs).

A case holds analyticcompoundoptionengine.py's process spec and an option {"choosingDate", "strikeCall", "strikePut",
"exerciseCall", "exercisePut"}. The wheel's calls, by QuantLib-SWIG 1.44's options.i: ComplexChooserOption(
choosingDate, strikeCall, strikePut, exerciseCall, exercisePut), AnalyticComplexChooserEngine(process), and the
option's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Exotic/AnalyticComplexChooserEngineVectorTests.cs."""
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
        s = pspec["x0"]
        for choosing in (30, 91):
            for kc, kp in ((95.0, 105.0), (105.0, 95.0)):
                for tc, tp in ((182, 273), (365, 182), (273, 365)):
                    ospec = {"choosingDate": (R + choosing).serialNumber(), "strikeCall": kc, "strikePut": kp,
                             "exerciseCall": compound.exercise_spec(tc), "exercisePut": compound.exercise_spec(tp)}
                    # 1.44's maturities from the choosing date: each placed the choosing time later
                    o = ql.ComplexChooserOption(ql.Date(ospec["choosingDate"]), kc, kp,
                                                ql.EuropeanExercise(R + tc + choosing),
                                                ql.EuropeanExercise(R + tp + choosing))
                    o.setPricingEngine(ql.AnalyticComplexChooserEngine(p))
                    out.append({"name": f"{mname}, choosing {choosing}, call {kc:g} at {tc}, put {kp:g} at {tp}",
                                "inputs": {"settings": st, "option": ospec, "process": pspec},
                                "expected": {"NPV": compound.override(o.NPV(), 6e-7 * (2.0 * s + kc + kp))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/exotic/analyticcomplexchooserengine.hpp", cases)
