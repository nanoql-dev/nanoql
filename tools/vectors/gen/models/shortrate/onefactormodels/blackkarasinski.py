# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Black-Karasinski model (ql/models/shortrate/onefactormodels/blackkarasinski.hpp, .cpp),
through the members the wheel binds and the tree it builds.

Six models on tools/vectors/gen/pricingengines/swaption/treeswaptionengine.py's discount curve (a flat 2.7% on
Actual/365 (Fixed)) and on a flat 5% Actual/360 curve, with reversions from 0.01 to 1 and volatilities from 0.02 to 0.3:
params() ([a, sigma]) and termStructure()'s discounts; then setParams on a fresh model (QuantLib's model changes in
place, nanoql's SetParams returns a new one), its refusals of too few and too many parameters compared alone. Then the
tree, the model's only pricing route: twelve of treeswaptionengine.py's swaptions, European, Bermudan and American, a
started swap and an expired one among them, priced by TreeSwaptionEngine with 20 and 60 time steps on each of three
models, each engine building its tree at its calculation (QuantLib's steps constructor), so that the tree priced on is
the last one fitted, where QuantLib's trees of one model share its fitting parameter
(tests/NanoQL.Tests/Divergences/BlackKarasinskiTreeFittingTests.cs, where a tree built before another is priced on).
Four of the twelve are their unlagged twins, as that file prices them: their lagged swaps' values nanoql's discretized
swaption moves. Then the constructor's refusals of a zero, negative or NaN reversion or volatility ("<value>: invalid
value").

The wheel's calls, by QuantLib-SWIG's shortratemodels.i: ql.BlackKarasinski(YieldTermStructureHandle, a, sigma),
params(), termStructure() and setParams(Array); ql.TreeSwaptionEngine(model, timeSteps). Each case's inputs hold its
settings, its "kind" ("model", "setParams", "tree" or "refused"), the "model" ({"type": "BlackKarasinski", "curve",
"a", "sigma", "params"}, the curve a FlatForward spec), and for a tree case the swaption (instruments/swaption.py's
spec) and the steps. Amounts are compared at the swaption's nominal. Read back by
tests/NanoQL.Tests/Vectors/Models/ShortRate/OneFactorModels/BlackKarasinskiVectorTests.cs, with
tests/NanoQL.Tests/Vectors/ModelInputs.cs."""
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


tse = _load("wheel_treeswaptionengine", "pricingengines/swaption/treeswaptionengine.py")
swp, fvf, irx = tse.swp, tse.fvf, tse.irx

TODAY = tse.TODAY
A365 = {"type": "Actual365Fixed"}
A360 = {"type": "Actual360"}
CONTINUOUS, ANNUAL = int(ql.Continuous), int(ql.Annual)


def flat(forward, dc):
    return {"type": "FlatForward", "referenceDate": tse.DISCOUNT["referenceDate"], "forward": forward,
            "dayCounter": dc, "compounding": CONTINUOUS, "frequency": ANNUAL}


def bk(curve, a, sigma):
    return {"type": "BlackKarasinski", "curve": curve, "a": a, "sigma": sigma, "params": None}


DISCOUNT = flat(0.027, A365)
MODELS = [
    bk(DISCOUNT, 0.1, 0.1),
    bk(DISCOUNT, 0.05, 0.25),
    bk(DISCOUNT, 1.0, 0.02),
    bk(flat(0.05, A360), 0.01, 0.3),
    bk(flat(0.05, A360), 0.3, 0.15),
    bk(DISCOUNT, 0.2, 0.05),
]
TREE_MODELS = [0, 1, 4]
CURVE_TIMES = [0.5, 3.0, 12.0]
STEPS = [20, 60]

SET_PARAMS = [("to a = 0.2, sigma = 0.2", [0.2, 0.2]), ("too few", [0.2]), ("too many", [0.2, 0.2, 0.5])]

TREE_SWAPTIONS = ["European / 1Y on 5Y / 1 / 0.02480", "Bermudan / 1Y on 5Y / 1 / 0.03100",
                  "European / 1Y on 5Y / -1 / 0.03720", "Bermudan / 2Y on 3Y / -1 / 0.02480",
                  "European / 6M on 2Y / -1 / 0.03100", "Bermudan / 5Y on 5Y / 1 / 0.03100",
                  "Bermudan moved -10 days", "Bermudan quarterly", "American over the first year",
                  "1M on 1Y", "started swap, Bermudan", "expired"]

REFUSED = [("a zero", 0.0, 0.1), ("a negative", -0.1, 0.1), ("a NaN", math.nan, 0.1), ("sigma zero", 0.1, 0.0),
           ("sigma negative", 0.1, -0.1), ("sigma NaN", 0.1, math.nan)]


def make_curve(spec):
    return ql.FlatForward(ql.Date(spec["referenceDate"]), spec["forward"], irx.day_counter(spec["dayCounter"]),
                          spec["compounding"], spec["frequency"])


def make_model(spec):
    """The wheel's model, and the curve it needs kept alive."""
    curve = make_curve(spec["curve"])
    return ql.BlackKarasinski(ql.YieldTermStructureHandle(curve), spec["a"], spec["sigma"]), curve


def model_cases():
    out = []
    for k, spec in enumerate(MODELS):
        with v.case_settings(TODAY) as st:
            m, _ = make_model(spec)
            ts = m.termStructure()
            out.append({"name": f"model {k}",
                        "inputs": {"settings": st, "kind": "model", "model": spec, "curveTimes": CURVE_TIMES},
                        "expected": {"params": v.enc(list(m.params())),
                                     "termStructureDiscount": [ts.discount(t) for t in CURVE_TIMES]}})
    for name, params in SET_PARAMS:
        with v.case_settings(TODAY) as st:
            m, _ = make_model(MODELS[0])
            expected = {"paramsBefore": v.enc(list(m.params()))}
            try:
                m.setParams(ql.Array(params))
            except RuntimeError:
                expected["setParams"] = dict(v.THROWS)
            else:
                expected["setParams"] = True
                expected["params"] = v.enc(list(m.params()))
            out.append({"name": f"model 0: setParams {name}",
                        "inputs": {"settings": st, "kind": "setParams", "model": MODELS[0], "params": v.enc(params)},
                        "expected": expected})
    return out


def tree_cases():
    # the tree swaption file's kept swaptions, a moved one's unlagged twin under its own name
    rows = {name.removesuffix(" / unlagged"): (name, spec, settings) for name, spec, settings in tse.tree_rows()}
    out = []
    for k in TREE_MODELS:
        for base in TREE_SWAPTIONS:
            name, spec, settings = rows[base]
            with v.case_settings(TODAY, **settings) as st:
                scale = fvf.scale_of(spec["swap"])
                swaption = tse.make_swaption(spec)
                m, _ = make_model(MODELS[k])
                npvs = []
                for steps in STEPS:
                    swaption.setPricingEngine(ql.TreeSwaptionEngine(m, steps))
                    npvs.append(fvf.ibc.scaled(swaption.NPV, scale))
                out.append({"name": f"model {k}: tree / {name}",
                            "inputs": {"settings": st, "kind": "tree", "model": MODELS[k], "swaption": spec,
                                       "steps": STEPS},
                            "expected": {"treeNPV": npvs}})
    return out


def refusal_cases():
    st = v.settings(TODAY)
    out = []
    for name, a, sigma in REFUSED:
        spec = bk(DISCOUNT, a, sigma)
        out.append({"name": "refused: " + name,
                    "inputs": {"settings": st, "kind": "refused", "model": v.enc(spec)},
                    "expected": {"built": v.attempt(lambda spec=spec: bool(make_model(spec)[0]))}})
    return out


def cases():
    return model_cases() + tree_cases() + refusal_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/models/shortrate/onefactormodels/blackkarasinski.hpp", cases)
