# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Cox-Ingersoll-Ross model (ql/models/shortrate/onefactormodels/coxingersollross.hpp, .cpp)
and the affine members it inherits, through the members the wheel binds.

Twelve models, each inside the Feller region the wheel's constructor requires (sigma^2 < 2 k theta; the wheel binds no
withFellerConstraint argument, so QuantLib's default, true), with speeds from 0.01 to 5, levels from 0.5% to 10% and
short rates from 0.1% to 15%. For each: params() ([theta, k, sigma, r0]), discount(t) at fifteen times,
discountBond(now, maturity, rate) and discountBond(now, maturity, factors) at twelve points (a zero tenor among them),
and the four-argument discountBondOption at sixteen points, calls and puts, a maturity below QL_EPSILON (its intrinsic
branch) and a zero strike ("strike must be positive") among them. An option whose non-central chi-squared series
leaves the range of doubles, its Poisson weight or its first term below the normal range, where QuantLib's series runs
on subnormals and zeros and its price comes out negative and nanoql's distribution refuses
(tests/NanoQL.Tests/Divergences/ChiSquaredSeriesRefusalTests.cs), is generated and left out of its model's points, by
the test of chi2_refused below on the formula's arguments (coxingersollross.cpp's discountBondOption).

Then setParams on a fresh model (QuantLib's model changes in place, nanoql's SetParams returns a new one), to a point
inside the Feller region and to one outside it, which setParams doesn't check, and its refusals of too few and too
many parameters, compared alone. Then calibrations to tools/vectors/gen/models/model.py's swaption helper sets, each
helper priced by a JamshidianSwaptionEngine on the model and the helper's curve, through model.py's machinery and its
S7R6 filter (each case's outputs those a one-ulp move of every input keeps): Levenberg-Marquardt and Simplex with theta
and k fixed, where QuantLib's Feller test, on the constructor's k and theta (coxingersollross.cpp:24-37), and nanoql's,
on each trial vector's (the S-sr plan's SSR7, K1), agree at every trial. The calibrations that free k or theta are
generated and left out, since the two tests can disagree along them; the tree prices are generated and left out too,
since nanoql's tree is QuantLib's before 1.23, on the square root of the short rate (K2), whose prices converge to
QuantLib's own analytic ones where QuantLib's tree stalls (tests/NanoQL.Tests/Divergences/CirFellerConstraintTests.cs,
CirTreeConvergenceTests.cs). Then the constructor's refusals: a level, speed or short rate zero, negative or NaN, and a
volatility on or above the Feller bound ("<value>: invalid value").

The wheel's calls, by QuantLib-SWIG's shortratemodels.i: ql.CoxIngersollRoss(r0, theta, k, sigma), params(),
discount(t), discountBond(now, maturity, rate) and (now, maturity, Array), discountBondOption(type, strike, maturity,
bondMaturity), setParams(Array) and calibrate, each forwarding to the C++ member of that name; ql.TreeSwaptionEngine.
Each case's inputs hold its settings, its "kind" ("model", "setParams", "calibration" or "refused"), the "model"
({"type": "CoxIngersollRoss", "r0", "theta", "k", "sigma"}) and the points, or model.py's calibration inputs. Read back
by tests/NanoQL.Tests/Vectors/Models/ShortRate/OneFactorModels/CoxIngersollRossVectorTests.cs."""
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


mdl = _load("wheel_model", "models/model.py")
tse = _load("wheel_treeswaptionengine", "pricingengines/swaption/treeswaptionengine.py")

CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
TODAY = ql.Date(15, ql.January, 2025)


def cir(r0, theta, k, sigma):
    return {"type": "CoxIngersollRoss", "r0": r0, "theta": theta, "k": k, "sigma": sigma}


MODELS = [
    cir(0.05, 0.1, 0.1, 0.1),
    cir(0.03, 0.04, 0.3, 0.1),
    cir(0.02, 0.035, 0.5, 0.05),
    cir(0.001, 0.02, 1.0, 0.15),
    cir(0.15, 0.08, 0.2, 0.17),
    cir(0.04, 0.04, 5.0, 0.5),
    cir(0.06, 0.1, 0.01, 0.04),
    cir(0.025, 0.005, 2.0, 0.1),
    cir(0.08, 0.06, 0.8, 0.3),
    cir(0.01, 0.03, 0.15, 0.08),
    cir(0.035, 0.05, 0.25, 0.155),
    cir(0.045, 0.045, 0.6, 0.02),
]

TIMES = [0.0, 0.1, 0.25, 0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 5.0, 7.5, 10.0, 15.0, 20.0, 30.0]
BOND_POINTS = [(0.0, 1.0, 0.05), (0.5, 2.0, 0.03), (1.0, 1.0, 0.05), (2.0, 7.0, 0.001), (0.0, 30.0, 0.1),
               (0.25, 0.5, 0.0), (5.0, 10.0, 0.2), (1.5, 4.0, 0.025), (0.0, 0.0, 0.05), (10.0, 30.0, 0.03),
               (0.75, 1.25, 0.02), (3.0, 5.0, 0.12)]
OPTIONS = [(CALL, 0.95, 1.0, 3.0), (PUT, 0.95, 1.0, 3.0), (CALL, 0.7, 2.0, 5.0), (PUT, 0.9, 0.5, 1.5),
           (CALL, 0.98, 1e-17, 1.0), (PUT, 0.98, 1e-17, 1.0), (CALL, 0.85, 4.0, 10.0), (PUT, 0.5, 0.25, 20.0),
           (CALL, 1.2, 3.0, 3.5), (PUT, 0.9, 0.5, 1.5), (CALL, 1.1, 1.0, 1.25), (PUT, 0.99, 0.1, 0.2),
           (CALL, 0.6, 5.0, 15.0), (PUT, 0.7, 2.0, 5.0), (CALL, 0.0, 1.0, 3.0), (PUT, 0.93, 1.0, 2.0)]

SET_PARAMS = [("inside the Feller region", [0.05, 0.4, 0.12, 0.04]), ("outside it", [0.05, 0.4, 0.25, 0.04]),
              ("too few", [0.05, 0.4, 0.12]), ("too many", [0.05, 0.4, 0.12, 0.04, 0.1])]

FIXED = [True, True, False, False]
CALIBRATIONS = [
    ("2025 / theta and k fixed", "2025", cir(0.03, 0.035, 0.3, 0.05), mdl.LM_TEST, mdl.EC_TEST, mdl.NO_CONSTRAINT,
     [], FIXED, True),
    ("2025 normal / theta and k fixed", "2025 normal", cir(0.03, 0.035, 0.3, 0.05), mdl.LM_TEST, mdl.EC_TEST,
     mdl.NO_CONSTRAINT, [], FIXED, True),
    ("2025 / theta and k fixed, Simplex", "2025", cir(0.03, 0.035, 0.3, 0.05), mdl.SIMPLEX, mdl.EC_SIMPLEX,
     mdl.NO_CONSTRAINT, [], FIXED, True),
    ("2002 / theta and k fixed, weights", "2002", cir(0.04, 0.05, 0.2, 0.06), mdl.LM_TEST, mdl.EC_TEST,
     mdl.NO_CONSTRAINT, [1.0, 2.0, 3.0, 4.0, 5.0], FIXED, True),
    # Free k and theta: QuantLib's test and nanoql's can disagree along the path, generated and left out.
    ("2025 / free", "2025", cir(0.03, 0.035, 0.3, 0.05), mdl.LM_TEST, mdl.EC_TEST, None, None, None, False),
    ("2002 / free", "2002", cir(0.04, 0.05, 0.2, 0.06), mdl.LM_TEST, mdl.EC_TEST, None, None, None, False),
]

REFUSED = [("theta zero", cir(0.03, 0.0, 0.3, 0.1)), ("k negative", cir(0.03, 0.04, -0.3, 0.1)),
           ("r0 zero", cir(0.0, 0.04, 0.3, 0.1)), ("r0 NaN", cir(math.nan, 0.04, 0.3, 0.1)),
           ("sigma on the Feller bound", cir(0.03, 0.08, 0.5, math.sqrt(2 * 0.5 * 0.08))),
           ("sigma above it", cir(0.03, 0.04, 0.3, 0.2)), ("sigma zero", cir(0.03, 0.04, 0.3, 0.0))]

TREE_SWAPTIONS = ["European / 1Y on 5Y / 1 / 0.03100", "Bermudan / 1Y on 5Y / -1 / 0.03720"]


DBL_MIN = 2.2250738585072014e-308
QL_EPSILON = 2.220446049250313e-16


def make_model(spec):
    return ql.CoxIngersollRoss(spec["r0"], spec["theta"], spec["k"], spec["sigma"])


def chi2_refused(df, ncp, x):
    """Whether QuantLib's non-central chi-squared series at x leaves the range of doubles
    (chisquaredistribution.cpp:39-55): its Poisson weight exp(-ncp/2), or above df its first term, below the normal
    range."""
    if x <= 0.0:
        return False
    if math.exp(-0.5 * ncp) < DBL_MIN:
        return True
    x2, f2 = 0.5 * x, 0.5 * df
    if f2 * QL_EPSILON > 0.125 and abs(x2 - f2) < math.sqrt(QL_EPSILON) * f2:
        t = math.exp(2.0) / math.sqrt(2.0 * math.pi * (f2 + 1.0))
    else:
        t = math.exp(f2 * math.log(x2) - x2 - math.lgamma(f2 + 1))
    return x > df and t < DBL_MIN


def option_refused(spec, option):
    """Whether coxingersollross.cpp's discountBondOption reaches a series chi2_refused refuses."""
    _, strike, t, s = option
    if strike <= 0.0 or t < QL_EPSILON:
        return False
    theta, k, sigma, x0 = spec["theta"], spec["k"], spec["sigma"], spec["r0"]
    sigma2 = sigma * sigma
    h = math.sqrt(k * k + 2.0 * sigma2)
    temp = math.exp((s - t) * h) - 1.0
    b = 2.0 * temp / (2.0 * h + (k + h) * temp)
    a = math.exp(math.log(2.0 * h * math.exp(0.5 * (k + h) * (s - t)) / (2.0 * h + (k + h) * temp))
                 * 2.0 * k * theta / sigma2)
    rho = 2.0 * h / (sigma2 * (math.exp(h * t) - 1.0))
    psi = (k + h) / sigma2
    df = 4.0 * k * theta / sigma2
    ncps = 2.0 * rho * rho * x0 * math.exp(h * t) / (rho + psi + b)
    ncpt = 2.0 * rho * rho * x0 * math.exp(h * t) / (rho + psi)
    z = math.log(a / strike) / b
    return chi2_refused(df, ncps, 2.0 * z * (rho + psi + b)) or chi2_refused(df, ncpt, 2.0 * z * (rho + psi))


def options_of(spec):
    return [o for o in OPTIONS if not option_refused(spec, o)]


def affine_outputs(m, options):
    return {"params": v.enc(list(m.params())),
            "discount": [v.attempt(lambda t=t: m.discount(t)) for t in TIMES],
            "discountBond": [v.attempt(lambda p=p: m.discountBond(*p)) for p in BOND_POINTS],
            "discountBondFactors": [v.attempt(lambda p=p: m.discountBond(p[0], p[1], ql.Array([p[2]])))
                                    for p in BOND_POINTS],
            "discountBondOption": [v.attempt(lambda o=o: m.discountBondOption(*o)) for o in options]}


def points(options):
    return {"times": TIMES, "bondPoints": BOND_POINTS, "options": options}


def tree_prices(spec):
    """The tree prices, generated and left out (K2)."""
    rows = {name: (s, settings) for name, s, settings in tse.swaptions()}
    prices = []
    for name in TREE_SWAPTIONS:
        s, settings = rows[name]
        with v.case_settings(tse.TODAY, **settings):
            swaption = tse.make_swaption(s)
            swaption.setPricingEngine(ql.TreeSwaptionEngine(make_model(spec), 50, tse.irx.handle(tse.DISCOUNT)))
            prices.append(swaption.NPV())
    return prices


def cases():
    out = []
    for k, spec in enumerate(MODELS):
        with v.case_settings(TODAY) as st:
            options = options_of(spec)
            out.append({"name": f"model {k}", "inputs": dict({"settings": st, "kind": "model", "model": spec},
                                                             **points(options)),
                        "expected": affine_outputs(make_model(spec), options)})
            tree_prices(spec)
    for name, params in SET_PARAMS:
        with v.case_settings(TODAY) as st:
            m = make_model(MODELS[1])
            options = options_of(dict(zip(("theta", "k", "sigma", "r0"), params))) if len(params) == 4 else OPTIONS
            expected = {"paramsBefore": v.enc(list(m.params()))}
            try:
                m.setParams(ql.Array(params))
            except RuntimeError:
                expected["setParams"] = dict(v.THROWS)
            else:
                expected["setParams"] = True
                expected.update(affine_outputs(m, options))
            out.append({"name": f"model 1: setParams {name}",
                        "inputs": dict({"settings": st, "kind": "setParams", "model": MODELS[1],
                                        "params": v.enc(params)}, **points(options)),
                        "expected": expected})
    dropped = mdl.Dropped()
    for name, set_name, spec, method, criteria, constraint, weights, fix, kept in CALIBRATIONS:
        case, _ = mdl.calibration_case(dropped, name, set_name, spec, method, criteria, constraint, weights, fix)
        if not kept:
            continue
        case["inputs"]["kind"] = "calibration"
        out.append(case)
    dropped.file("coxingersollross")
    st = v.settings(TODAY)
    for name, spec in REFUSED:
        out.append({"name": "refused: " + name,
                    "inputs": {"settings": st, "kind": "refused", "model": v.enc(spec)},
                    "expected": {"built": v.attempt(lambda spec=spec: bool(make_model(spec)))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/shortrate/onefactormodels/coxingersollross.hpp", cases)
