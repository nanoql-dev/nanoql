# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's extended Cox-Ingersoll-Ross model (ql/models/shortrate/onefactormodels/
extendedcoxingersollross.hpp, .cpp), the Cox-Ingersoll-Ross model shifted by its analytical fitting parameter, and
the affine members it inherits, through the members the wheel binds.

Ten models on flat curves (rates from 0.5% to 7%, Actual/365 (Fixed) and Actual/360, continuous and compounded
rates), each inside the Feller region the wheel's constructor requires, with speeds from 0.05 to 3. For each:
params() ([theta, k, sigma, x0]), termStructure()'s discounts at three times, discount(t) at fifteen times (through the
dynamics' short rate, the fitting parameter's shift of x0), discountBond(now, maturity, rate) and (now, maturity,
factors) at twelve points, and the four-argument discountBondOption at sixteen points, calls and puts, a maturity below
QL_EPSILON and a zero strike among them; an option whose non-central chi-squared series leaves the range of doubles is
generated and left out of its model's points (coxingersollross.py's chi2_refused, on extendedcoxingersollross.cpp's
arguments; tests/NanoQL.Tests/Divergences/ChiSquaredSeriesRefusalTests.cs).

Then setParams on a fresh model, its refusals compared alone; calibrations to tools/vectors/gen/models/model.py's
swaption helper sets through its machinery and stability filter, the model on the helpers' curve, with theta and k
fixed, where QuantLib's Feller test and nanoql's agree at every trial, those that free them generated and left out, as
the tree prices are (nanoql's tree is QuantLib's before 1.23, on the square root of the short rate, its numerical
fitting the fitted tree constructor's; tests/NanoQL.Tests/Divergences/CirFellerConstraintTests.cs,
CirTreeConvergenceTests.cs). Then the constructor's refusals.

The wheel's calls, by QuantLib-SWIG's shortratemodels.i: ql.ExtendedCoxIngersollRoss(YieldTermStructureHandle, theta,
k, sigma, x0), params(), termStructure(), discount(t), discountBond, discountBondOption, setParams and calibrate. Each
case's inputs hold its settings, its "kind" ("model", "setParams", "calibration" or "refused"), the "model"
({"type": "ExtendedCoxIngersollRoss", "termStructure", "theta", "k", "sigma", "x0"}, the curve
indexes/interestrateindex.py's flat spec) and the points, or model.py's calibration inputs. Read back by
tests/NanoQL.Tests/Vectors/Models/ShortRate/OneFactorModels/ExtendedCoxIngersollRossVectorTests.cs."""
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


cir = _load("wheel_coxingersollross", "models/shortrate/onefactormodels/coxingersollross.py")
mdl, tse = cir.mdl, cir.tse
irx = mdl.irx

TODAY = cir.TODAY
A365 = {"type": "Actual365Fixed"}
A360 = {"type": "Actual360"}
CURVE_TIMES = [0.5, 3.0, 12.0]


def flat(forward, dc=A365, compounding=ql.Continuous, frequency=ql.Annual):
    return irx.curve_spec(TODAY, forward, dc, compounding, frequency)


def ecir(curve, theta, k, sigma, x0):
    return {"type": "ExtendedCoxIngersollRoss", "termStructure": curve, "theta": theta, "k": k, "sigma": sigma,
            "x0": x0}


MODELS = [
    ecir(flat(0.04), 0.1, 0.1, 0.1, 0.05),
    ecir(flat(0.03, A360), 0.04, 0.3, 0.1, 0.03),
    ecir(flat(0.02), 0.035, 0.5, 0.05, 0.02),
    ecir(flat(0.07, A365, ql.Compounded, ql.Semiannual), 0.08, 0.2, 0.17, 0.06),
    ecir(flat(0.005), 0.02, 1.0, 0.15, 0.001),
    ecir(flat(0.05, A360), 0.1, 0.05, 0.09, 0.04),
    ecir(flat(0.025), 0.005, 3.0, 0.15, 0.025),
    ecir(flat(0.035), 0.05, 0.25, 0.155, 0.035),
    ecir(flat(0.045, A360, ql.Compounded, ql.Annual), 0.045, 0.6, 0.02, 0.045),
    ecir(flat(0.02), 0.02, 1.0, 1e-4, 0.02),
]

SET_PARAMS = [("inside the Feller region", [0.05, 0.4, 0.12, 0.04]), ("outside it", [0.05, 0.4, 0.25, 0.04]),
              ("too few", [0.05, 0.4, 0.12]), ("too many", [0.05, 0.4, 0.12, 0.04, 0.1])]

FIXED = [True, True, False, False]
CALIBRATIONS = [
    ("2025 / theta and k fixed", "2025", ecir(None, 0.035, 0.3, 0.05, 0.03), mdl.LM_TEST, mdl.EC_TEST,
     mdl.NO_CONSTRAINT, [], FIXED, True),
    ("2025 price / theta and k fixed", "2025 price", ecir(None, 0.035, 0.3, 0.05, 0.03), mdl.LM_TEST, mdl.EC_TEST,
     mdl.NO_CONSTRAINT, [], FIXED, True),
    ("2002 / theta and k fixed, Simplex", "2002", ecir(None, 0.05, 0.2, 0.06, 0.04), mdl.SIMPLEX, mdl.EC_SIMPLEX,
     mdl.NO_CONSTRAINT, [], FIXED, True),
    ("2025 / free", "2025", ecir(None, 0.035, 0.3, 0.05, 0.03), mdl.LM_TEST, mdl.EC_TEST, None, None, None, False),
]

REFUSED = [("theta negative", ecir(flat(0.04), -0.1, 0.1, 0.1, 0.05)),
           ("k zero", ecir(flat(0.04), 0.1, 0.0, 0.1, 0.05)),
           ("x0 NaN", ecir(flat(0.04), 0.1, 0.1, 0.1, math.nan)),
           ("sigma above the Feller bound", ecir(flat(0.04), 0.04, 0.3, 0.2, 0.05))]


def make_model(spec):
    return mdl.make_model(spec)


def option_refused(m, spec, option):
    """Whether extendedcoxingersollross.cpp's discountBondOption reaches a series chi2_refused refuses."""
    _, strike, t, s = option
    if strike <= 0.0 or t < cir.QL_EPSILON:
        return False
    theta, k, sigma, x0 = spec["theta"], spec["k"], spec["sigma"], spec["x0"]
    ts = m.termStructure()
    sigma2 = sigma * sigma
    h = math.sqrt(k * k + 2.0 * sigma2)

    def b_of(t0, t1):
        temp = math.exp((t1 - t0) * h) - 1.0
        return 2.0 * temp / (2.0 * h + (k + h) * temp)

    def a_of(t0, t1):
        temp = math.exp((t1 - t0) * h) - 1.0
        return math.exp(math.log(2.0 * h * math.exp(0.5 * (k + h) * (t1 - t0)) / (2.0 * h + (k + h) * temp))
                        * 2.0 * k * theta / sigma2)

    r0 = ts.forwardRate(0.0, 0.0, ql.Continuous, ql.NoFrequency).rate()
    temp0 = 2.0 * h
    phi0 = r0 - x0 * 4.0 * h * h / (temp0 * temp0)
    b = b_of(t, s)
    rho = 2.0 * h / (sigma2 * (math.exp(h * t) - 1.0))
    psi = (k + h) / sigma2
    df = 4.0 * k * theta / sigma2
    ncps = 2.0 * rho * rho * (r0 - phi0) * math.exp(h * t) / (rho + psi + b)
    ncpt = 2.0 * rho * rho * (r0 - phi0) * math.exp(h * t) / (rho + psi)
    shift = (ts.discount(t) * a_of(0.0, s) * math.exp(-b_of(0.0, s) * x0)) / (
        ts.discount(s) * a_of(0.0, t) * math.exp(-b_of(0.0, t) * x0))
    z = (math.log(a_of(t, s) / strike) - math.log(shift)) / b
    return cir.chi2_refused(df, ncps, 2.0 * z * (rho + psi + b)) or cir.chi2_refused(df, ncpt, 2.0 * z * (rho + psi))


def affine(m, spec):
    options = [o for o in cir.OPTIONS if not option_refused(m, spec, o)]
    ts = m.termStructure()
    expected = {"termStructureDiscount": [ts.discount(t) for t in CURVE_TIMES]}
    expected.update(cir.affine_outputs(m, options))
    return expected, dict(cir.points(options), curveTimes=CURVE_TIMES)


def tree_prices(spec):
    """The tree prices, generated and left out (K2)."""
    m = make_model(spec)
    rows = {name: (s, settings) for name, s, settings in tse.swaptions()}
    for name in cir.TREE_SWAPTIONS:
        s, settings = rows[name]
        with v.case_settings(tse.TODAY, **settings):
            swaption = tse.make_swaption(s)
            swaption.setPricingEngine(ql.TreeSwaptionEngine(m, 50))
            swaption.NPV()


def cases():
    out = []
    for k, spec in enumerate(MODELS):
        with v.case_settings(TODAY) as st:
            m = make_model(spec)
            expected, pts = affine(m, spec)
            out.append({"name": f"model {k}", "inputs": dict({"settings": st, "kind": "model", "model": spec}, **pts),
                        "expected": expected})
        tree_prices(spec)
    for name, params in SET_PARAMS:
        with v.case_settings(TODAY) as st:
            m = make_model(MODELS[1])
            expected = {"paramsBefore": v.enc(list(m.params()))}
            pts = dict(cir.points(cir.OPTIONS), curveTimes=CURVE_TIMES)
            try:
                m.setParams(ql.Array(params))
            except RuntimeError:
                expected["setParams"] = dict(v.THROWS)
            else:
                expected["setParams"] = True
                changed = dict(MODELS[1], **dict(zip(("theta", "k", "sigma", "x0"), params)))
                outputs, pts = affine(m, changed)
                expected.update(outputs)
            out.append({"name": f"model 1: setParams {name}",
                        "inputs": dict({"settings": st, "kind": "setParams", "model": MODELS[1],
                                        "params": v.enc(params)}, **pts),
                        "expected": expected})
    dropped = mdl.Dropped()
    for name, set_name, spec, method, criteria, constraint, weights, fix, kept in CALIBRATIONS:
        case, _ = mdl.calibration_case(dropped, name, set_name, spec, method, criteria, constraint, weights, fix)
        if not kept:
            continue
        case["inputs"]["kind"] = "calibration"
        out.append(case)
    dropped.file("extendedcoxingersollross")
    st = v.settings(TODAY)
    for name, spec in REFUSED:
        out.append({"name": "refused: " + name,
                    "inputs": {"settings": st, "kind": "refused", "model": v.enc(spec)},
                    "expected": {"built": v.attempt(lambda spec=spec: bool(make_model(spec)))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/shortrate/onefactormodels/extendedcoxingersollross.hpp", cases)
