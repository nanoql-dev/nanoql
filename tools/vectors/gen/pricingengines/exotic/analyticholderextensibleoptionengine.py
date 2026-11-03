# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticHolderExtensibleOptionEngine
(ql/pricingengines/exotic/analyticholderextensibleoptionengine.hpp, .cpp), on the evaluation date 15 October 2026:
holder-extensible calls struck at 90, 100 or 110, expiring in six months or nine, extendable for a premium of 1 or 2 to
a year at 105 or 115, on flat markets without dividends: the NPV.

nanoql takes QuantLib's fix after v1.44 (#2892, 453e6605c), which changes the put everywhere, the call with dividends
and the call whose extension region is empty (where extending never beats exercising); all three are left out
(tests/NanoQL.Tests/Divergences/HolderExtensibleCriticalPricesTests.cs), the last by the largest gain of extending over
exercising at the first expiry, scanned over spots to three times the strike, kept above 0.05; the calls here are
1.44's and the fix's alike. QuantLib's engine reads Drezner's bivariate normal, nanoql's West's, so every value carries
an override (docs/porting-playbook.md §7.3): Drezner's accuracy, |Dr78 - We04DP| <= 6e-7 times the bivariate terms'
coefficients, relative to the value. Flat markets only: on term structures nanoql's form reads by date
(HolderExtensibleTermStructureTests.cs).

A case holds a tools/vectors/gen/processes/blackscholesprocess.py process spec and an option {"type", "premium",
"secondExpiryDate", "secondStrike", "payoff", "exercise"}. The wheel's calls, by QuantLib-SWIG 1.44's options.i:
HolderExtensibleOption(type, premium, secondExpiryDate, secondStrike, payoff, exercise),
AnalyticHolderExtensibleOptionEngine(process), and the option's NPV. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Exotic/AnalyticHolderExtensibleOptionEngineVectorTests.cs."""
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


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


compound = _load("analyticcompoundoptionengine", "pricingengines/exotic/analyticcompoundoptionengine.py")
bsp, bc = compound.bsp, compound.bc
R = compound.R
CALL = compound.CALL


def extension_gain(pspec, k1, k2, premium, tau):
    """The largest gain of extending over exercising at the first expiry: the extended call less the premium and the
    intrinsic value, over spots to three times the first strike."""
    r, vol = pspec["riskFree"]["forward"], pspec["volatility"]["volatility"]
    best = -premium
    for i in range(1, 3001):
        s = k1 * i / 1000.0
        extended = ql.blackFormula(ql.Option.Call, k2, s * math.exp(r * tau), vol * math.sqrt(tau), math.exp(-r * tau))
        best = max(best, extended - premium - max(s - k1, 0.0))
    return best


MARKETS = [
    ("spot 100, r 8%, vol 25%", compound.flat(100.0, 0.08, 0.0, 0.25)),
    ("spot 90, r 3%, vol 35%", compound.flat(90.0, 0.03, 0.0, 0.35)),
]


def cases():
    st = v.settings(R)
    out = []
    for mname, pspec in MARKETS:
        p = bsp.process(pspec)
        s = pspec["x0"]
        for k1 in (90.0, 100.0, 110.0):
            for t1 in (182, 273):
                for premium, k2 in ((1.0, 105.0), (2.0, 115.0)):
                    if extension_gain(pspec, k1, k2, premium, (365 - t1) / 365.0) <= 0.05:
                        continue
                    ospec = {"type": CALL, "premium": premium, "secondExpiryDate": (R + 365).serialNumber(),
                             "secondStrike": k2, "payoff": bc.payoff_spec("PlainVanilla", CALL, k1),
                             "exercise": compound.exercise_spec(t1)}
                    o = ql.HolderExtensibleOption(CALL, premium, ql.Date(ospec["secondExpiryDate"]), k2,
                                                  bc.payoff(ospec["payoff"]), compound.exercise(ospec["exercise"]))
                    o.setPricingEngine(ql.AnalyticHolderExtensibleOptionEngine(p))
                    out.append({"name": f"{mname}, call {k1:g} at {t1}, premium {premium:g} to {k2:g}",
                                "inputs": {"settings": st, "option": ospec, "process": pspec},
                                "expected": {"NPV": compound.override(o.NPV(), 6e-7 * (2.0 * s + k1 + k2))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/exotic/analyticholderextensibleoptionengine.hpp", cases)
