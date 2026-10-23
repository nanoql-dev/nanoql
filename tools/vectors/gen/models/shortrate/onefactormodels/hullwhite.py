# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Hull-White model (ql/models/shortrate/onefactormodels/hullwhite.hpp, .cpp) and the affine
members it inherits, through the members the wheel binds.

Twenty models: ten on flat curves (rates from -0.5% to 7%, three day counters, continuous, compounded and simple
rates) and ten on bootstrapped curves (tools/vectors/gen/termstructures/yield/piecewiseyieldcurve.py's fixture
rate helpers on Monday 15 January 2024, with a fixed reference date two TARGET days later, or on two TARGET settlement
days: log-linear, log-cubic, Kruger log and monotonic log-cubic discounts, linear and Kruger cubic zero rates,
backward-flat and linear forwards, linear simple zero rates), with mean reversions from 1e-9 (below
sqrt(QL_EPSILON), QuantLib's small-reversion branches) to 2. For each: params() ([a, sigma]), termStructure()'s
discounts at three times, discount(t) at fifteen times (through the dynamics' analytical fitting at the process's
start), discountBond(now, maturity, rate) and discountBond(now, maturity, factors) at twelve points (zero and
negative tenors among them), and the four-argument discountBondOption at sixteen points, calls and puts, on the
curve's discounts, a zero maturity among them (Hull-White has no zero-maturity branch, unlike Vasicek).

Then setParams, each on a fresh model (the wheel survey's caveat 4: QuantLib's model changes in place, nanoql's
SetParams returns a new one), on a flat and a bootstrapped model: the parameters before and, after a call that
succeeds, the parameters, discounts, discount bonds and options after it (a negative sigma, which setParams doesn't
check, makes the options refuse, blackFormula's); and its refusals of too few, too many and no parameters ("parameter
array too small", "parameter array too big!"), whose model the case doesn't read again (QuantLib's has changed in
part, nanoql's hasn't, MX3). Then the static convexityBias at fifteen points (a reversion of 0 and below QL_EPSILON,
QuantLib's own branch, and T equal to t among them) and its five refusals, with NaN inputs; and the constructor's
refusals of a zero or negative reversion or volatility, and a NaN one ("<value>: invalid value").

Left out where nanoql's forms, QuantLib's master's B (#2882) and the members written in its cancellation-free forms
(the S-sr plan's SSR3 and SSR6 (iii)), move the outputs beyond the tolerance: the three models at a = 1e-9 and 1.4e-8
and the two setParams cases to a = 1e-9, generated and left out (QuantLib 1.44's forms there take the a = 0 limit or
cancel); the convexity bias at a = 1e-9, which cancels in 1.44's (1 - exp(-a*x))/a; and, in the two setParams cases to
a negative sigma, the option of maturity 1e-17, whose variance 1.44's 1 - exp(-2*a*T) rounds to 0, so its price is the
intrinsic 0 where nanoql's variance is T and the negative sigma meets blackFormula's refusal. Their checks are
QuantLib's closed forms at ordinary a and its second routes at small a
(tests/NanoQL.Tests/Divergences/SmallReversionTests.cs). Elsewhere the forms move the last bits only.

The wheel's calls, by QuantLib-SWIG's shortratemodels.i: ql.HullWhite(YieldTermStructureHandle, a, sigma), params(),
termStructure() (a handle, whose discount(t) is the curve's), discount(t), discountBond(now, maturity, rate) and
(now, maturity, Array), discountBondOption(type, strike, maturity, bondMaturity), setParams(Array) and the static
convexityBias(futurePrice, t, T, sigma, a), each forwarding to the C++ member of that name. Each case's inputs hold
its settings, its "kind" ("model", "setParams", "convexityBias" or "refused"), the "model" (the spec of
tools/cxx/src/models/modelspec.hpp, its curve a FlatForward spec or {"type": "PiecewiseYieldCurve", "curve",
"helpers"} with piecewiseyieldcurve.py's curve spec and rate helper specs), and the points. Read back by
tests/NanoQL.Tests/Vectors/Models/ShortRate/OneFactorModels/HullWhiteVectorTests.cs, with
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

_spec = importlib.util.spec_from_file_location(
    "wheel_piecewiseyieldcurve", os.path.join(v.GEN_ROOT, "termstructures", "yield", "piecewiseyieldcurve.py"))
pyc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(pyc)
rh, irx = pyc.rh, pyc.irx

CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
TODAY = pyc.EVALUATION_DATES[0]
A365, A360, ACT_ISDA = pyc.A365, pyc.A360, {"type": "ActualActual", "convention": int(ql.ActualActual.ISDA)}
CONTINUOUS, COMPOUNDED, SIMPLE = int(ql.Continuous), int(ql.Compounded), int(ql.Simple)
ANNUAL, SEMIANNUAL = int(ql.Annual), int(ql.Semiannual)
REFERENCE = ql.TARGET().advance(TODAY, 2, ql.Days)


def flat(forward, dc=A365, compounding=CONTINUOUS, frequency=ANNUAL):
    return {"type": "FlatForward", "referenceDate": REFERENCE.serialNumber(), "forward": forward, "dayCounter": dc,
            "compounding": compounding, "frequency": frequency}


def bootstrapped(traits, interpolator, dc, moving=False):
    return {"type": "PiecewiseYieldCurve", "curve": pyc.curve_spec(traits, interpolator, dc, TODAY, moving),
            "helpers": pyc.fixture()}


def hull_white(curve, a, sigma):
    return {"type": "HullWhite", "curve": curve, "a": a, "sigma": sigma, "params": None}


MODELS = [
    hull_white(flat(0.04), 0.1, 0.01),
    hull_white(flat(0.03, A360, COMPOUNDED, ANNUAL), 0.03, 0.012),
    hull_white(flat(0.02, A365, SIMPLE), 1e-9, 0.01),
    hull_white(flat(-0.005), 0.5, 0.008),
    hull_white(flat(0.07, ACT_ISDA, COMPOUNDED, SEMIANNUAL), 2.0, 0.02),
    hull_white(flat(0.0), 1.4e-8, 0.01),
    hull_white(flat(0.05, A360), 0.05, 0.005),
    hull_white(flat(0.035), 0.25, 0.015),
    hull_white(flat(0.01), 0.15, 0.03),
    hull_white(flat(0.06, A360, SIMPLE), 1.0, 0.01),
    hull_white(bootstrapped("Discount", pyc.LOG_LINEAR, A360), 0.1, 0.01),
    hull_white(bootstrapped("ForwardRate", pyc.BACKWARD_FLAT, A360), 0.05, 0.012),
    hull_white(bootstrapped("ZeroYield", pyc.LINEAR, A365), 0.2, 0.008),
    hull_white(bootstrapped("ZeroYield", pyc.KRUGER_CUBIC, A360), 1e-9, 0.01),
    hull_white(bootstrapped("Discount", pyc.cubic(pyc.CI.Spline, log=True), A360), 0.03, 0.02),
    hull_white(bootstrapped("Discount", pyc.LOG_LINEAR, A360, moving=True), 0.5, 0.005),
    hull_white(bootstrapped("ForwardRate", pyc.LINEAR, A365), 0.1, 0.015),
    hull_white(bootstrapped("SimpleZeroYield", pyc.LINEAR, A360), 0.08, 0.01),
    hull_white(bootstrapped("Discount", pyc.KRUGER_LOG, A365), 0.3, 0.012),
    hull_white(bootstrapped("Discount", pyc.MONOTONIC_LOG_CUBIC, A360), 0.12, 0.009),
]

CURVE_TIMES = [0.5, 3.0, 12.0]
TIMES = [0.0, 0.1, 0.25, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 5.0, 7.5, 10.0, 15.0, 20.0, 25.0]
BOND_POINTS = [(0.0, 1.0, 0.04), (0.5, 2.0, 0.03), (1.0, 1.0, 0.05), (2.0, 7.0, -0.01), (0.0, 25.0, 0.06),
               (0.25, 0.5, 0.0), (5.0, 10.0, 0.08), (3.0, 1.0, 0.04), (1.5, 4.0, 0.025), (0.0, 0.0, 0.05),
               (10.0, 20.0, 0.03), (0.75, 1.25, -0.02)]
OPTIONS = [(CALL, 0.95, 1.0, 3.0), (PUT, 0.95, 1.0, 3.0), (CALL, 0.7, 2.0, 5.0), (PUT, 0.9, 0.5, 1.5),
           (CALL, 0.98, 1e-17, 1.0), (PUT, 0.85, 4.0, 10.0), (CALL, 0.5, 0.25, 20.0), (PUT, 1.2, 3.0, 3.5),
           (CALL, 0.9, 0.5, 1.5), (PUT, 0.7, 2.0, 5.0), (CALL, 1.1, 1.0, 1.25), (PUT, 0.99, 0.1, 0.2),
           (CALL, 0.6, 5.0, 15.0), (PUT, 0.5, 10.0, 25.0), (CALL, 0.95, 0.0, 3.0), (PUT, 0.95, 0.0, 3.0)]

SET_PARAMS = [
    ("to a = 0.2, sigma = 0.02", [0.2, 0.02]),
    ("to a = 1e-9", [1e-9, 0.01]),
    ("to a negative sigma", [0.1, -0.01]),
    ("to a NaN reversion", [math.nan, 0.01]),
    ("too few", [0.2]),
    ("too many", [0.2, 0.02, 0.5]),
    ("none", []),
]

CONVEXITY = [(95.0, 1.0, 1.25, 0.01, 0.1), (95.0, 1.0, 1.25, 0.01, 0.0), (95.0, 1.0, 1.0, 0.01, 0.1),
             (98.5, 0.25, 0.5, 0.012, 0.03), (100.0, 0.0, 0.25, 0.01, 0.1), (0.0, 2.0, 2.25, 0.02, 0.5),
             (97.0, 5.0, 5.25, 0.0, 0.1), (96.0, 3.0, 3.25, 0.015, 1e-17), (99.0, 10.0, 10.5, 0.008, 2.0), (101.0, 1.5, 1.75, 0.01, 0.05), (95.0, 1.0, 1.0 + 1e-17, 0.01, 0.1),
             (90.0, 0.5, 4.0, 0.03, 0.2), (95.0, 20.0, 20.25, 0.01, 0.01), (94.0, 7.0, 7.25, 0.02, 0.0)]
CONVEXITY_REFUSALS = [(-1.0, 1.0, 1.25, 0.01, 0.1), (math.nan, 1.0, 1.25, 0.01, 0.1), (95.0, -0.5, 1.25, 0.01, 0.1),
                      (95.0, math.nan, 1.25, 0.01, 0.1), (95.0, 1.0, 0.5, 0.01, 0.1), (95.0, 1.0, math.nan, 0.01, 0.1),
                      (95.0, 1.0, 1.25, -0.01, 0.1), (95.0, 1.0, 1.25, math.nan, 0.1), (95.0, 1.0, 1.25, 0.01, -0.1),
                      (95.0, 1.0, 1.25, 0.01, math.nan)]

REFUSED = [("a zero", 0.0, 0.01), ("a negative", -0.1, 0.01), ("a NaN", math.nan, 0.01), ("sigma zero", 0.1, 0.0),
           ("sigma negative", 0.1, -0.01), ("sigma NaN", 0.1, math.nan)]


def make_curve(spec):
    if spec["type"] == "FlatForward":
        return ql.FlatForward(ql.Date(spec["referenceDate"]), spec["forward"], irx.day_counter(spec["dayCounter"]),
                              spec["compounding"], spec["frequency"])
    helpers = [rh.make_value_helper(h) for h in spec["helpers"]]
    return pyc.make_curve(spec["curve"], helpers), helpers


def make_model(spec):
    """The wheel's model, and the objects it needs kept alive (the curve, the rate helpers)."""
    built = make_curve(spec["curve"])
    curve, keep = built if isinstance(built, tuple) else (built, None)
    return ql.HullWhite(ql.YieldTermStructureHandle(curve), spec["a"], spec["sigma"]), (curve, keep)


# The models and setParams cases SSR3's forms move beyond the tolerance, generated and left out; and the option
# left out of the setParams cases to a negative sigma.
LEFT_OUT = {2, 5, 13}
LEFT_OUT_SET_PARAMS = {"to a = 1e-9"}
NEGATIVE_SIGMA_OPTIONS = [o for o in OPTIONS if o[2] != 1e-17]


def affine_outputs(m, options=OPTIONS):
    return {"discount": [v.attempt(lambda t=t: m.discount(t)) for t in TIMES],
            "discountBond": v.enc([m.discountBond(now, t, r) for now, t, r in BOND_POINTS]),
            "discountBondFactors": v.enc([m.discountBond(now, t, ql.Array([r])) for now, t, r in BOND_POINTS]),
            "discountBondTwoFactors": v.enc([m.discountBond(now, t, ql.Array([r, 9.0])) for now, t, r in BOND_POINTS]),
            "discountBondOption": [v.attempt(lambda o=o: m.discountBondOption(*o)) for o in options]}


def points(options=OPTIONS):
    return {"times": TIMES, "bondPoints": BOND_POINTS, "options": options}


def cases():
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
    for base in (0, 10):
        for name, params in SET_PARAMS:
            options = NEGATIVE_SIGMA_OPTIONS if name == "to a negative sigma" else OPTIONS
            with v.case_settings(TODAY) as st:
                m, _ = make_model(MODELS[base])
                expected = {"paramsBefore": v.enc(list(m.params()))}
                try:
                    m.setParams(ql.Array(params))
                except RuntimeError:
                    expected["setParams"] = dict(v.THROWS)
                else:
                    expected["setParams"] = True
                    expected["params"] = v.enc(list(m.params()))
                    expected.update(affine_outputs(m, options))
                if name in LEFT_OUT_SET_PARAMS:
                    continue
                out.append({"name": f"model {base}: setParams {name}",
                            "inputs": dict({"settings": st, "kind": "setParams", "model": MODELS[base],
                                            "params": v.enc(params)}, **points(options)),
                            "expected": expected})
    st = v.settings(TODAY)
    out.append({"name": "convexityBias",
                "inputs": {"settings": st, "kind": "convexityBias", "points": CONVEXITY + CONVEXITY_REFUSALS},
                "expected": {"convexityBias": [v.attempt(lambda p=p: ql.HullWhite.convexityBias(*p))
                                               for p in CONVEXITY + CONVEXITY_REFUSALS]}})
    out[-1]["inputs"]["points"] = v.enc(out[-1]["inputs"]["points"])
    for name, a, sigma in REFUSED:
        spec = hull_white(flat(0.04), a, sigma)
        out.append({"name": "refused: " + name,
                    "inputs": {"settings": st, "kind": "refused", "model": v.enc(spec)},
                    "expected": {"built": v.attempt(lambda spec=spec: bool(make_model(spec)[0]))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/shortrate/onefactormodels/hullwhite.hpp", cases)
