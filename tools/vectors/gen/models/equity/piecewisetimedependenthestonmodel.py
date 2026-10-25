# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's PiecewiseTimeDependentHestonModel (ql/models/equity/piecewisetimedependenthestonmodel.hpp,
.cpp), on the evaluation date 15 October 2026: the Heston model with piecewise constant parameters on a time grid.

tools/vectors/gen/pricingengines/vanilla/analyticptdhestonengine.py's three models on flat and linear zero curves: θ,
κ, σ and ρ at eight times across and past their steps, v₀, s₀, the time grid's times and the flat parameter vector.
And a calibration of the every-parameter-piecewise model, its 13 parameters, to twenty Heston helpers (five strikes at
four maturities, one or two in each of the grid's steps, implied volatilities from the analytic Heston engine on known
parameters) by Levenberg-Marquardt through
AnalyticPTDHestonEngine at Gauss-Laguerre order 144, from parameters moved off the known ones: the calibrated
parameters and the helpers' errors, under the S7 plan's S7R6 filter: the calibration rerun with each market volatility
moved one ulp, up and down, and each output written only where every rerun leaves it within 1e-10 x max(|a|, |b|, 1),
a dropped one null and its index in the case's input "dropped"; the generator prints "dropped n of m outputs".

A case holds one model (analyticptdhestonengine.py's spec) and, for the calibration, its helpers {"maturities" (days),
"strikes", "vols"} and the start. The wheel's calls, by QuantLib-SWIG 1.44's calibratedmodel.i: the model's theta,
kappa, sigma, rho, v0, s0, timeGrid and params; HestonModelHelper(maturity, calendar, s0, strike, vol, riskFreeRate,
dividendYield, ImpliedVolError), its setPricingEngine and calibrationError; calibrate with LevenbergMarquardt(1e-8,
1e-8, 1e-8) and EndCriteria(400, 40, 1e-8, 1e-8, 1e-8). Read back by
tests/NanoQL.Tests/Vectors/Models/Equity/PiecewiseTimeDependentHestonModelVectorTests.cs."""
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
ptd = _load("analyticptdhestonengine", "pricingengines/vanilla/analyticptdhestonengine.py")
R = bsp.R
TIMES = [0.0, 0.1, 0.25, 0.5, 0.75, 1.5, 4.0, 12.0]
HELPER_DAYS = [91, 365, 730, 1095]
HELPER_STRIKES = [80.0, 90.0, 100.0, 110.0, 120.0]
TRUE = (0.04, 1.5, 0.05, 0.4, -0.6)  # v0, kappa, theta, sigma, rho of the analytic engine's surface


def market_vols(spec):
    """Implied volatilities of the analytic Heston engine on TRUE, the market the calibration fits."""
    rf, q = (ql.YieldTermStructureHandle(bsp.curve(spec[k])) for k in ("riskFree", "dividend"))
    v0, kappa, theta, sigma, rho = TRUE
    model = ql.HestonModel(ql.HestonProcess(rf, q, ql.QuoteHandle(ql.SimpleQuote(spec["s0"])), v0, kappa, theta, sigma,
                                            rho))
    engine = ql.AnalyticHestonEngine(model, 144)
    vols = []
    for days in HELPER_DAYS:
        for k in HELPER_STRIKES:
            o = ql.VanillaOption(ql.PlainVanillaPayoff(ql.Option.Call, k), ql.EuropeanExercise(R + days))
            o.setPricingEngine(engine)
            vols.append(o.impliedVolatility(o.NPV(), ql.BlackScholesMertonProcess(
                ql.QuoteHandle(ql.SimpleQuote(spec["s0"])), q, rf,
                ql.BlackVolTermStructureHandle(ql.BlackConstantVol(R, ql.NullCalendar(), 0.2, ql.Actual365Fixed()))),
                1e-12, 1000, 1e-4, 4.0))
    return vols


def calibrate(spec, vols):
    m = ptd.model(spec)
    rf, q = (ql.YieldTermStructureHandle(bsp.curve(spec[k])) for k in ("riskFree", "dividend"))
    s0 = spec["s0"]
    engine = ql.AnalyticPTDHestonEngine(m, 144)
    helpers = []
    i = 0
    for days in HELPER_DAYS:
        for k in HELPER_STRIKES:
            h = ql.HestonModelHelper(ql.Period(days, ql.Days), ql.NullCalendar(), s0, k,
                                     ql.QuoteHandle(ql.SimpleQuote(vols[i])), rf, q, ql.BlackCalibrationHelper.ImpliedVolError)
            h.setPricingEngine(engine)
            helpers.append(h)
            i += 1
    m.calibrate(helpers, ql.LevenbergMarquardt(1e-8, 1e-8, 1e-8), ql.EndCriteria(400, 40, 1e-8, 1e-8, 1e-8))
    return list(m.params()) + [h.calibrationError() for h in helpers]


def reruns(spec, vols):
    """The calibration with each market volatility moved one ulp, up and down (S7R6); None where a rerun fails."""
    for i in range(len(vols)):
        for d in (math.inf, -math.inf):
            moved = list(vols)
            moved[i] = math.nextafter(moved[i], d)
            try:
                yield calibrate(spec, moved)
            except RuntimeError:
                yield None


def cases():
    st = v.settings(R)
    out = []
    for mname, mspec in ptd.MODELS:
        for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
            spec = {"riskFree": rf, "dividend": q, **mspec}
            m = ptd.model(spec)
            out.append({"name": f"{mname}, {cname} curves",
                        "inputs": {"settings": st, "kind": "members", "model": spec, "times": TIMES},
                        "expected": {"theta": [m.theta(t) for t in TIMES], "kappa": [m.kappa(t) for t in TIMES],
                                     "sigma": [m.sigma(t) for t in TIMES], "rho": [m.rho(t) for t in TIMES],
                                     "v0": m.v0(), "s0": m.s0(), "timeGrid": list(m.timeGrid()),
                                     "params": list(m.params())}})
    # the calibration, from parameters moved off the known ones
    mname, mspec = ptd.MODELS[2]
    for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
        spec = {"riskFree": rf, "dividend": q, **mspec}
        vols = market_vols(spec)
        base = calibrate(spec, vols)
        keep = [True] * len(base)
        for again in reruns(spec, vols):
            if again is None:
                keep = [False] * len(base)
                continue
            keep = [k and abs(a - b) <= 1e-10 * max(abs(a), abs(b), 1.0) for k, a, b in zip(keep, base, again)]
        dropped = keep.count(False)
        print(f"{cname}: dropped {dropped} of {len(base)} outputs")
        n = len(list(ptd.model(spec).params()))
        params = [x if k else None for x, k in zip(base[:n], keep[:n])]
        errors = [x if k else None for x, k in zip(base[n:], keep[n:])]
        dropped = {"params": [i for i, x in enumerate(params) if x is None],
                   "errors": [i for i, x in enumerate(errors) if x is None]}
        out.append({"name": f"calibration of the {mname} model, {cname} curves",
                    "inputs": {"settings": st, "kind": "calibration", "model": spec,
                               "helpers": {"maturities": HELPER_DAYS, "strikes": HELPER_STRIKES, "vols": vols},
                               "dropped": dropped},
                    "expected": {k: val for k, val in (("params", params), ("errors", errors))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/equity/piecewisetimedependenthestonmodel.hpp", cases)
