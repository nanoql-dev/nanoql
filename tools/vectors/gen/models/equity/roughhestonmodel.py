# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's RoughHestonModel (ql/models/equity/roughhestonmodel.hpp, .cpp), on the evaluation date 15
October 2026: the rough Heston model, the Heston parameters and the variance's Hurst exponent.

Three models (Hurst exponents 0, 0.12 and 0.5) on flat and linear zero curves: θ, κ, σ, ρ, v₀, the Hurst exponent and
the parameter vector, and the parameters after setParams. And a calibration of a model to twenty Heston
helpers (five strikes at four maturities, implied volatilities from the Padé engine on QuantLib's own calibration
case's parameters) by Levenberg-Marquardt through AnalyticRoughHestonEngine's Padé route at Gauss-Laguerre order 64 and
256 time steps, from QuantLib's case's start: the calibrated parameters and the helpers' errors, under the S7 plan's
S7R6 filter: the calibration rerun with each market volatility moved one ulp, up and down, each output written only
where every rerun leaves it within 1e-10 x max(|a|, |b|, 1), a dropped one null and its index in the case's input
"dropped"; the generator prints "dropped n of m outputs".

A case holds one model {"process" (tools/vectors/gen/processes/hestonprocess.py's spec), "hurst"}, for the members
the parameters setParams gives, and for the calibration its helpers {"maturities" (days), "strikes", "vols"} and its
start. The wheel's calls, by QuantLib-SWIG 1.44's calibratedmodel.i: RoughHestonModel(HestonProcess(...), hurst), its
theta, kappa, sigma, rho, v0, hurst, params and setParams; HestonModelHelper(maturity, calendar, s0, strike,
vol, riskFreeRate, dividendYield, ImpliedVolError), its setPricingEngine and calibrationError; calibrate with
LevenbergMarquardt(1e-8, 1e-8, 1e-8) and EndCriteria(400, 40, 1e-8, 1e-8, 1e-8). Read back by
tests/NanoQL.Tests/Vectors/Models/Equity/RoughHestonModelVectorTests.cs."""
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


bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
hp = _load("hestonprocess", "processes/hestonprocess.py")
R = bsp.R
HURSTS = [0.0, 0.12, 0.5]
NEW_PARAMS = [0.05, 0.9, 0.3, -0.55, 0.03, 0.2]
HELPER_DAYS = [91, 182, 365, 730]
HELPER_STRIKES = [80.0, 90.0, 100.0, 110.0, 120.0]
TRUE = {"v0": 0.0225, "kappa": 0.6, "theta": 0.04, "sigma": 0.35, "rho": -0.65, "hurst": 0.12}
START = {"v0": 0.04, "kappa": 1.0, "theta": 0.02, "sigma": 0.25, "rho": -0.4, "hurst": 0.2}


def model(spec):
    return ql.RoughHestonModel(hp.process({**spec["process"], "discretization": 0}), spec["hurst"])


def spec_of(rf, q, p):
    return {"process": {"riskFree": rf, "dividend": q, "s0": 100.0, "v0": p["v0"], "kappa": p["kappa"],
                        "theta": p["theta"], "sigma": p["sigma"], "rho": p["rho"]}, "hurst": p["hurst"]}


def engine(m):
    return ql.AnalyticRoughHestonEngine(m, 64, 256, ql.AnalyticRoughHestonEngine.Pade)


def market_vols(rf, q):
    m = model(spec_of(rf, q, TRUE))
    e = engine(m)
    rts, qts = bsp.curve(rf), bsp.curve(q)
    vols = []
    for days in HELPER_DAYS:
        d = R + days
        fwd = 100.0 * qts.discount(d) / rts.discount(d)
        t = ql.Actual365Fixed().yearFraction(R, d)
        for k in HELPER_STRIKES:
            o = ql.VanillaOption(ql.PlainVanillaPayoff(ql.Option.Call, k), ql.EuropeanExercise(d))
            o.setPricingEngine(e)
            vols.append(ql.blackFormulaImpliedStdDev(ql.Option.Call, k, fwd, o.NPV(), rts.discount(d)) / math.sqrt(t))
    return vols


def calibrate(rf, q, vols):
    m = model(spec_of(rf, q, START))
    rts, qts = (ql.YieldTermStructureHandle(bsp.curve(x)) for x in (rf, q))
    e = engine(m)
    helpers = []
    i = 0
    for days in HELPER_DAYS:
        for k in HELPER_STRIKES:
            h = ql.HestonModelHelper(ql.Period(days, ql.Days), ql.NullCalendar(), 100.0, k,
                                     ql.QuoteHandle(ql.SimpleQuote(vols[i])), rts, qts,
                                     ql.BlackCalibrationHelper.ImpliedVolError)
            h.setPricingEngine(e)
            helpers.append(h)
            i += 1
    m.calibrate(helpers, ql.LevenbergMarquardt(1e-8, 1e-8, 1e-8), ql.EndCriteria(400, 40, 1e-8, 1e-8, 1e-8))
    return list(m.params()) + [h.calibrationError() for h in helpers]


def reruns(rf, q, vols):
    """The calibration with each market volatility moved one ulp, up and down (S7R6); None where a rerun fails."""
    for i in range(len(vols)):
        for d in (math.inf, -math.inf):
            moved = list(vols)
            moved[i] = math.nextafter(moved[i], d)
            try:
                yield calibrate(rf, q, moved)
            except RuntimeError:
                yield None


def cases():
    st = v.settings(R)
    out = []
    for hurst in HURSTS:
        for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
            spec = spec_of(rf, q, {**TRUE, "hurst": hurst})
            m = model(spec)
            expected = {"theta": m.theta(), "kappa": m.kappa(), "sigma": m.sigma(), "rho": m.rho(), "v0": m.v0(),
                        "hurst": m.hurst(), "params": list(m.params())}
            m.setParams(ql.Array(NEW_PARAMS))
            expected["afterSetParams"] = [m.theta(), m.kappa(), m.sigma(), m.rho(), m.v0(), m.hurst()]
            out.append({"name": f"H {hurst}, {cname} curves",
                        "inputs": {"settings": st, "kind": "members", "model": spec, "newParams": NEW_PARAMS},
                        "expected": expected})
    for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
        vols = market_vols(rf, q)
        base = calibrate(rf, q, vols)
        keep = [True] * len(base)
        for again in reruns(rf, q, vols):
            if again is None:
                keep = [False] * len(base)
                continue
            keep = [k and abs(a - b) <= 1e-10 * max(abs(a), abs(b), 1.0) for k, a, b in zip(keep, base, again)]
        print(f"{cname}: dropped {keep.count(False)} of {len(base)} outputs")
        params = [x if k else None for x, k in zip(base[:6], keep[:6])]
        errors = [x if k else None for x, k in zip(base[6:], keep[6:])]
        out.append({"name": f"calibration through the Pade engine, {cname} curves",
                    "inputs": {"settings": st, "kind": "calibration", "model": spec_of(rf, q, START),
                               "helpers": {"maturities": HELPER_DAYS, "strikes": HELPER_STRIKES, "vols": vols},
                               "dropped": {"params": [i for i, x in enumerate(params) if x is None],
                                           "errors": [i for i, x in enumerate(errors) if x is None]}},
                    "expected": {"params": params, "errors": errors}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/equity/roughhestonmodel.hpp", cases)
