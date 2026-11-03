# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's G2 model (ql/models/shortrate/twofactormodels/g2.hpp, .cpp), its two-factor base
(ql/models/shortrate/twofactormodel.hpp, .cpp: the dynamics and the short-rate tree) and the two-dimensional tree
lattice it builds (ql/methods/lattices/lattice2d.hpp, .cpp), through the members the wheel binds.

Fourteen models on flat curves (rates from -0.5% to 7%, three day counters, continuous, compounded and simple rates),
with reversions from 1e-9 to 2, volatilities from 0.002 to 0.03 and correlations from -1 to 1 (a = b among them). For
each: params() ([a, sigma, b, eta, rho]), termStructure()'s discounts at three times, discount(t) at fifteen times,
discountBond(now, maturity, [x, y]) at twelve points (a zero tenor among them), and the four-argument
discountBondOption at sixteen points, calls and puts, a zero maturity among them.

Then setParams on a fresh model (QuantLib's model changes in place, nanoql's SetParams returns a new one), the
parameters before and, after a call that succeeds, the parameters and the affine outputs; its refusals of too few and
too many parameters, whose model the case doesn't read again. Then the tree: eighteen of
tools/vectors/gen/pricingengines/swaption/treeswaptionengine.py's swaptions, European, Bermudan and American, a started
swap and an expired one among them, priced by TreeSwaptionEngine on a G2 (a = 0.07, sigma = 0.011, b = 0.3, eta = 0.008,
rho = -0.6) on that file's discount curve with 10 and 25 time steps, seven of them their unlagged twins, as that file
prices them, their lagged swaps' values nanoql's discretized swaption moves; QuantLib's G2 tree is its only public route
to TwoFactorModel's ShortRateTree and TreeLattice2D. Then the constructor's refusals of a reversion or volatility that
is zero, negative or NaN, or a correlation outside [-1, 1] ("<value>: invalid value").

The models at a = 1e-9, b = 1e-9 and a = 1e-4 are generated and left out: QuantLib 1.44's sigmaP and its V, A and B
cancel there (to 9e-9 relative in the bonds at a = 1e-4, against a 60-digit evaluation of the same formulas), and
nanoql's are QuantLib's master's (#2885), which move their outputs beyond the tolerance; upstream's new test lines and
those references check them (tests/NanoQL.Tests/Divergences/G2CancellationTests.cs). Elsewhere the forms move the last
bits only.

The wheel's calls, by QuantLib-SWIG's shortratemodels.i: ql.G2(YieldTermStructureHandle, a, sigma, b, eta, rho),
params(), termStructure(), discount(t), discountBond(now, maturity, Array), discountBondOption(type, strike, maturity,
bondMaturity) and setParams(Array), each forwarding to the C++ member of that name; ql.TreeSwaptionEngine(model,
timeSteps). Each case's inputs hold its settings, its "kind" ("model", "setParams", "tree" or "refused"), the
"model" ({"type": "G2", "curve", "a", "sigma", "b", "eta", "rho", "params"}, its curve a FlatForward spec), and the
points; a tree case, the swaption (instruments/swaption.py's spec), the discount curve and the steps. Amounts are
compared at the swaption's nominal. Read back by tests/NanoQL.Tests/Vectors/Models/ShortRate/TwoFactorModels/
G2VectorTests.cs, with tests/NanoQL.Tests/Vectors/ModelInputs.cs."""
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

CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
TODAY = ql.Date(15, ql.January, 2024)
REFERENCE = ql.TARGET().advance(TODAY, 2, ql.Days)
A365 = {"type": "Actual365Fixed"}
A360 = {"type": "Actual360"}
ACT_ISDA = {"type": "ActualActual", "convention": int(ql.ActualActual.ISDA)}
CONTINUOUS, COMPOUNDED, SIMPLE = int(ql.Continuous), int(ql.Compounded), int(ql.Simple)
ANNUAL, SEMIANNUAL = int(ql.Annual), int(ql.Semiannual)


def flat(forward, dc=A365, compounding=CONTINUOUS, frequency=ANNUAL):
    return {"type": "FlatForward", "referenceDate": REFERENCE.serialNumber(), "forward": forward, "dayCounter": dc,
            "compounding": compounding, "frequency": frequency}


def g2(curve, a, sigma, b, eta, rho):
    return {"type": "G2", "curve": curve, "a": a, "sigma": sigma, "b": b, "eta": eta, "rho": rho, "params": None}


MODELS = [
    g2(flat(0.04), 0.1, 0.01, 0.1, 0.01, -0.75),
    g2(flat(0.03, A360, COMPOUNDED, ANNUAL), 0.05, 0.012, 0.3, 0.008, -0.5),
    g2(flat(0.02, A365, SIMPLE), 1e-9, 0.01, 0.2, 0.01, -0.3),
    g2(flat(-0.005), 0.5, 0.008, 0.05, 0.006, 0.2),
    g2(flat(0.07, ACT_ISDA, COMPOUNDED, SEMIANNUAL), 2.0, 0.02, 0.4, 0.015, -0.9),
    g2(flat(0.0), 0.2, 0.005, 0.2, 0.005, 0.0),
    g2(flat(0.05, A360), 0.08, 0.009, 1e-9, 0.007, 0.5),
    g2(flat(0.035), 0.25, 0.015, 0.02, 0.01, -1.0),
    g2(flat(0.01), 0.15, 0.03, 0.6, 0.02, 1.0),
    g2(flat(0.06, A360, SIMPLE), 1.0, 0.01, 0.1, 0.012, -0.2),
    g2(flat(0.045), 0.03, 0.002, 0.7, 0.004, 0.9),
    g2(flat(0.025, A365), 0.12, 0.011, 0.12, 0.011, -0.6),
    g2(flat(0.03), 1e-4, 0.01, 0.3, 0.009, -0.4),
    g2(flat(0.04, A360), 0.07, 0.011, 0.3, 0.008, -0.6),
]

CURVE_TIMES = [0.5, 3.0, 12.0]
TIMES = [0.0, 0.1, 0.25, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 5.0, 7.5, 10.0, 15.0, 20.0, 25.0]
BOND_POINTS = [(0.0, 1.0, 0.0, 0.0), (0.5, 2.0, 0.01, -0.005), (1.0, 1.0, 0.02, 0.01), (2.0, 7.0, -0.01, 0.003),
               (0.0, 25.0, 0.005, 0.005), (0.25, 0.5, 0.0, -0.02), (5.0, 10.0, 0.03, -0.01), (1.5, 4.0, 0.002, 0.0),
               (10.0, 20.0, -0.005, 0.01), (0.75, 1.25, -0.02, -0.01), (3.0, 3.5, 0.1, 0.05),
               (0.0, 5.0, -0.03, 0.02)]
OPTIONS = [(CALL, 0.95, 1.0, 3.0), (PUT, 0.95, 1.0, 3.0), (CALL, 0.7, 2.0, 5.0), (PUT, 0.9, 0.5, 1.5),
           (CALL, 0.98, 0.0, 1.0), (PUT, 0.85, 4.0, 10.0), (CALL, 0.5, 0.25, 20.0), (PUT, 1.2, 3.0, 3.5),
           (CALL, 0.9, 0.5, 1.5), (PUT, 0.7, 2.0, 5.0), (CALL, 1.1, 1.0, 1.25), (PUT, 0.99, 0.1, 0.2),
           (CALL, 0.6, 5.0, 15.0), (PUT, 0.5, 10.0, 25.0), (CALL, 0.85, 1.0, 5.0), (PUT, 0.9, 2.0, 7.0)]

SET_PARAMS = [
    ("to a = 0.2, b = 0.05", [0.2, 0.015, 0.05, 0.01, -0.4]),
    ("to rho = 1", [0.1, 0.01, 0.2, 0.01, 1.0]),
    ("too few", [0.2, 0.015, 0.05, 0.01]),
    ("too many", [0.2, 0.015, 0.05, 0.01, -0.4, 0.3]),
]

TREE_MODEL = MODELS[-1]
TREE_STEPS = [10, 25]
TREE_SWAPTIONS = {"European / 1Y on 5Y / 1 / 0.02480", "Bermudan / 1Y on 5Y / 1 / 0.02480",
                  "European / 1Y on 5Y / 1 / 0.03100", "Bermudan / 1Y on 5Y / 1 / 0.03100",
                  "European / 1Y on 5Y / -1 / 0.03720", "Bermudan / 1Y on 5Y / -1 / 0.03720",
                  "European / 2Y on 3Y / 1 / 0.03100", "Bermudan / 2Y on 3Y / -1 / 0.02480",
                  "European / 6M on 2Y / -1 / 0.03100", "Bermudan / 6M on 2Y / 1 / 0.03720",
                  "Bermudan moved -10 days", "Bermudan moved 3 days", "Bermudan quarterly",
                  "American over the first year", "overnight-indexed European / 1", "1M on 1Y",
                  "started swap, Bermudan", "expired"}

REFUSED = [("a zero", (0.0, 0.01, 0.1, 0.01, -0.75)), ("sigma negative", (0.1, -0.01, 0.1, 0.01, -0.75)),
           ("b NaN", (0.1, 0.01, math.nan, 0.01, -0.75)), ("eta zero", (0.1, 0.01, 0.1, 0.0, -0.75)),
           ("rho above 1", (0.1, 0.01, 0.1, 0.01, 1.5)), ("rho below -1", (0.1, 0.01, 0.1, 0.01, -1.000001)),
           ("rho NaN", (0.1, 0.01, 0.1, 0.01, math.nan))]

# The models the cancellation-free forms move beyond the tolerance, generated and left out.
LEFT_OUT = {2, 6, 12}


def make_curve(spec):
    return ql.FlatForward(ql.Date(spec["referenceDate"]), spec["forward"], irx.day_counter(spec["dayCounter"]),
                          spec["compounding"], spec["frequency"])


def make_model(spec):
    """The wheel's model, and the curve it needs kept alive."""
    curve = make_curve(spec["curve"])
    return ql.G2(ql.YieldTermStructureHandle(curve), spec["a"], spec["sigma"], spec["b"], spec["eta"],
                 spec["rho"]), curve


def affine_outputs(m):
    return {"discount": [v.attempt(lambda t=t: m.discount(t)) for t in TIMES],
            "discountBond": [v.attempt(lambda p=p: m.discountBond(p[0], p[1], ql.Array([p[2], p[3]])))
                             for p in BOND_POINTS],
            "discountBondOption": [v.attempt(lambda o=o: m.discountBondOption(*o)) for o in OPTIONS]}


def points():
    return {"times": TIMES, "bondPoints": BOND_POINTS, "options": OPTIONS}


def model_cases():
    out = []
    for k, spec in enumerate(MODELS):
        with v.case_settings(TODAY) as st:
            m, _ = make_model(spec)
            ts = m.termStructure()
            expected = {"params": v.enc(list(m.params())),
                        "termStructureDiscount": [ts.discount(t) for t in CURVE_TIMES]}
            expected.update(affine_outputs(m))
            if k in LEFT_OUT:
                continue
            out.append({"name": f"model {k}",
                        "inputs": dict({"settings": st, "kind": "model", "model": spec, "curveTimes": CURVE_TIMES},
                                       **points()),
                        "expected": expected})
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
                expected.update(affine_outputs(m))
            out.append({"name": f"model 0: setParams {name}",
                        "inputs": dict({"settings": st, "kind": "setParams", "model": MODELS[0],
                                        "params": v.enc(params)}, **points()),
                        "expected": expected})
    return out


def tree_cases():
    out = []
    # the tree swaption file's kept swaptions, a moved one's unlagged twin under its own name
    for name, spec, settings in tse.tree_rows():
        if name.removesuffix(" / unlagged") not in TREE_SWAPTIONS:
            continue
        with v.case_settings(tse.TODAY, **settings) as st:
            scale = fvf.scale_of(spec["swap"])
            swaption = tse.make_swaption(spec)
            curve = irx.handle(tse.DISCOUNT)
            model = ql.G2(curve, TREE_MODEL["a"], TREE_MODEL["sigma"], TREE_MODEL["b"], TREE_MODEL["eta"],
                          TREE_MODEL["rho"])
            npvs = []
            for steps in TREE_STEPS:
                swaption.setPricingEngine(ql.TreeSwaptionEngine(model, steps))
                npvs.append(fvf.ibc.scaled(swaption.NPV, scale))
            out.append({"name": "tree / " + name,
                        "inputs": {"settings": st, "kind": "tree", "swaption": spec, "discountCurve": tse.DISCOUNT,
                                   "g2": {k: TREE_MODEL[k] for k in ("a", "sigma", "b", "eta", "rho")},
                                   "steps": TREE_STEPS},
                        "expected": {"treeNPV": npvs}})
    assert len(out) == len(TREE_SWAPTIONS), [c["name"] for c in out]
    return out


def refusal_cases():
    st = v.settings(TODAY)
    out = []
    for name, args in REFUSED:
        spec = g2(flat(0.04), *args)
        out.append({"name": "refused: " + name,
                    "inputs": {"settings": st, "kind": "refused", "model": v.enc(spec)},
                    "expected": {"built": v.attempt(lambda spec=spec: bool(make_model(spec)[0]))}})
    return out


def cases():
    return model_cases() + tree_cases() + refusal_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/models/shortrate/twofactormodels/g2.hpp", cases)
