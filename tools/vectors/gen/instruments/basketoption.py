# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's basket payoffs and option (ql/instruments/basketoption.hpp, .cpp), on the evaluation date 15
October 2026: MinBasketPayoff, MaxBasketPayoff, AverageBasketPayoff (on given weights and on equal weights) and
SpreadBasketPayoff over plain-vanilla calls and puts and a cash-or-nothing call, each at seven single prices (the
payoff's call on a Real, its base payoff's) and on arrays of the assets' prices (the call on an Array, which
accumulates them): the wheel's FdmLogBasketInnerValue at every point of a predefined mesher of the prices' logs (three
assets, two for the spread), its innerValue the payoff at the exponentials of the point's locations.

The specs, which the basket engines' generators share: a basket payoff {"type": "Min" | "Max" | "Average" | "Spread",
"base": tools/vectors/gen/pricingengines/blackcalculator.py's payoff spec, "weights" (Average) or "n" (equal
weights)}, and a basket option {"payoff", "exercise": {"type": "European" | "American", "date", "earliestDate"}}. The
wheel's calls, by QuantLib-SWIG 1.44's options.i and fdm.i: the payoffs' C++ constructors (AverageBasketPayoff's on an
Array or a size), their __call__ on a Real, the C++ operator(); FdmLogBasketInnerValue(payoff, mesher) and innerValue
at the layout's iterator; BasketOption(payoff, exercise). The payoffs aren't called on an Array of another size than
their weights' or than two for the spread, where QuantLib reads past the shorter array or refuses
(tests/NanoQL.Tests/Divergences/AverageBasketPayoffWeightCountTests.cs). Read back by
tests/NanoQL.Tests/Vectors/Instruments/BasketOptionVectorTests.cs."""
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


bc = _load("blackcalculator", "pricingengines/blackcalculator.py")
mc = _load("fdmmeshercomposite", "methods/finitedifferences/meshers/fdmmeshercomposite.py")
bsp = mc.bsm.bsp
R = bsp.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
PRICES = [0.0, 40.0, 95.0, 100.0, 105.0, 120.0, 250.0]


def payoff_spec(kind, base, weights=None, n=None):
    spec = {"type": kind, "base": base}
    if weights is not None:
        spec["weights"] = list(weights)
    if n is not None:
        spec["n"] = n
    return spec


def payoff(spec):
    base = bc.payoff(spec["base"])
    t = spec["type"]
    if t == "Min":
        return ql.MinBasketPayoff(base)
    if t == "Max":
        return ql.MaxBasketPayoff(base)
    if t == "Spread":
        return ql.SpreadBasketPayoff(base)
    if t == "Average":
        if "weights" in spec:
            return ql.AverageBasketPayoff(base, ql.Array(spec["weights"]))
        return ql.AverageBasketPayoff(base, spec["n"])
    raise ValueError(f"unknown basket payoff {t}")


def exercise_spec(kind, offset, earliest=None):
    spec = {"type": kind, "date": (R + offset).serialNumber()}
    if earliest is not None:
        spec["earliestDate"] = (R + earliest).serialNumber()
    return spec


def exercise(spec):
    date = ql.Date(spec["date"])
    if spec["type"] == "European":
        return ql.EuropeanExercise(date)
    if spec["type"] == "American":
        return ql.AmericanExercise(ql.Date(spec.get("earliestDate", R.serialNumber())), date)
    raise ValueError(f"unknown exercise {spec['type']}")


def option_spec(payoff_s, exercise_s):
    return {"payoff": payoff_s, "exercise": exercise_s}


def option(spec):
    p, e = payoff(spec["payoff"]), exercise(spec["exercise"])
    return ql.BasketOption(p, e), (p, e)


BASES = [("call", bc.payoff_spec("PlainVanilla", CALL, 100.0)),
         ("put", bc.payoff_spec("PlainVanilla", PUT, 100.0)),
         ("cash-or-nothing call", bc.payoff_spec("CashOrNothing", CALL, 100.0))]
KINDS = [("minimum", lambda b: payoff_spec("Min", b), 3),
         ("maximum", lambda b: payoff_spec("Max", b), 3),
         ("average", lambda b: payoff_spec("Average", b, weights=[0.5, 0.3, 0.2]), 3),
         ("equal average", lambda b: payoff_spec("Average", b, n=3), 3),
         ("negative weights", lambda b: payoff_spec("Average", b, weights=[1.0, -0.5, 0.25]), 3),
         ("spread", lambda b: payoff_spec("Spread", b), 2)]
LEVELS = [[60.0, 100.0, 140.0], [80.0, 110.0, 125.0], [50.0, 95.0, 200.0]]


def basket_values(p, assets):
    mspec = mc.composite_spec(*[{"type": "Predefined", "x": [math.log(s) for s in LEVELS[i]]} for i in range(assets)])
    mesher, keep = mc.composite(mspec)
    calc = ql.FdmLogBasketInnerValue(p, mesher)
    layout = mesher.layout()
    out = []
    it = layout.begin()
    for _ in range(layout.size()):
        out.append(calc.innerValue(it, 0.0))
        it.increment()
    return mspec, v.enc(out)


def cases():
    st = v.settings(R)
    out = []
    for bname, base in BASES:
        for kname, kind, assets in KINDS:
            spec = kind(base)
            p = payoff(spec)
            mspec, basket = basket_values(p, assets)
            out.append({"name": f"{kname} of {bname}",
                        "inputs": {"settings": st, "payoff": spec, "prices": PRICES, "mesher": mspec},
                        "expected": {"value": v.enc([p(s) for s in PRICES]), "basket": basket}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/basketoption.hpp", cases)
