# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BatesModel (ql/models/equity/batesmodel.hpp, .cpp), on the evaluation date 15 October 2026:
the Heston model with log-normal jumps, its eight parameters and a calibration.

Three Bates processes on flat and linear zero curves: params (theta, kappa, sigma, rho, v0, nu, delta and lambda, in
that order), each accessor; then the parameters set to other values, read back. The constructor's refusals of a delta
and a lambda at zero (QuantLib's ConstantParameter, ": invalid value"). Then calibrations of two of the models to nine
helpers (maturities of three months, a year and two years, strikes 90, 100 and 110, quoted at QuantLib's DAX case's
volatilities), each helper priced by a BatesEngine (Gauss-Laguerre 64) on the model, by Levenberg-Marquardt at
QuantLib's defaults and the DAX case's end criteria: the calibrated params, the helpers' calibration errors, the end
criteria and the function evaluations, through the S7R6 filter of the master plan: each calibration is refit with every
nonzero real input (the vols, the spot, the curves' rates and the starting parameters) moved one ulp, up and down
together, and an output is written only where both refits keep it within 1e-12 x max(|a|, |b|, 1) (an integer, where
they give the same); the generator prints "dropped n of m outputs".

A case holds one process {"riskFree", "dividend", "s0", "v0", "kappa", "theta", "sigma", "rho", "lambda", "nu",
"delta"}, the curves tools/vectors/gen/processes/blackscholesprocess.py's specs, and for a calibration its helpers
[{"months", "strike", "vol"}]. The wheel's calls, by QuantLib-SWIG 1.44's calibratedmodel.i and calibrationhelpers.i:
BatesModel(BatesProcess(...)), params, theta, kappa, sigma, rho, v0, nu, delta, lambda_parameter, setParams, calibrate,
endCriteria, functionEvaluation, HestonModelHelper(period, NullCalendar, s0, strike, vol, riskFree, dividend) with
setPricingEngine(BatesEngine(model, 64)) and calibrationError. Read back by
tests/NanoQL.Tests/Vectors/Models/BatesModelVectorTests.cs."""
import copy
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
bp = _load("batesprocess", "processes/batesprocess.py")
R = bsp.R
PARAMETERS = [  # s0, v0, kappa, theta, sigma, rho, lambda, nu, delta
    ("DAX start", 100.0, 0.0433, 1.0, 0.0433, 1.0, 0.0, 1.1098, -0.1285, 0.1702),
    ("equity", 100.0, 0.04, 1.5, 0.04, 0.3, -0.7, 2.0, -0.2, 0.1),
    ("rare large jumps", 80.0, 0.09, 0.8, 0.06, 0.6, -0.5, 0.3, 0.05, 0.3),
]
NEW = [0.05, 2.5, 0.45, -0.3, 0.07, -0.05, 0.2, 0.8]
HELPERS = [{"months": m, "strike": k, "vol": vol} for m, k, vol in
           ((3, 90.0, 0.3327), (3, 100.0, 0.3108), (3, 110.0, 0.2919), (12, 90.0, 0.2999), (12, 100.0, 0.2870),
            (12, 110.0, 0.2751), (24, 90.0, 0.2781), (24, 100.0, 0.2722), (24, 110.0, 0.2661))]
STABLE = 1e-12


def read(m):
    return {"params": list(m.params()), "theta": m.theta(), "kappa": m.kappa(), "sigma": m.sigma(), "rho": m.rho(),
            "v0": m.v0(), "nu": m.nu(), "delta": m.delta(), "lambda": m.lambda_parameter()}


def calibrated(spec, helper_specs):
    """The calibration's outputs, each list element its own."""
    p = bp.process(spec)
    model = ql.BatesModel(p)
    engine = ql.BatesEngine(model, 64)
    helpers = []
    for h in helper_specs:
        helper = ql.HestonModelHelper(ql.Period(h["months"], ql.Months), ql.NullCalendar(), spec["s0"], h["strike"],
                                      ql.QuoteHandle(ql.SimpleQuote(h["vol"])), p.riskFreeRate(), p.dividendYield())
        helper.setPricingEngine(engine)
        helpers.append(helper)
    model.calibrate(helpers, ql.LevenbergMarquardt(), ql.EndCriteria(400, 40, 1.0e-8, 1.0e-8, 1.0e-8))
    out = {"endCriteria": int(model.endCriteria()), "functionEvaluation": model.functionEvaluation()}
    out.update({f"params[{i}]": x for i, x in enumerate(model.params())})
    out.update({f"calibrationErrors[{i}]": h.calibrationError() for i, h in enumerate(helpers)})
    return out


def moved(x, direction):
    if isinstance(x, dict):
        return {k: moved(e, direction) for k, e in x.items()}
    if isinstance(x, list):
        return [moved(e, direction) for e in x]
    if isinstance(x, float) and x != 0.0:
        return math.nextafter(x, direction)
    return x


def stable(base, refit):
    if isinstance(base, int) or isinstance(refit, int):
        return base == refit
    return abs(base - refit) <= STABLE * max(abs(base), abs(refit), 1.0)


def cases():
    st = v.settings(R)
    out = []
    for name, s0, v0, kappa, theta, sigma, rho, lam, nu, delta in PARAMETERS:
        for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
            spec = {"riskFree": rf, "dividend": q, "s0": s0, "v0": v0, "kappa": kappa, "theta": theta, "sigma": sigma,
                    "rho": rho, "lambda": lam, "nu": nu, "delta": delta}
            m = ql.BatesModel(bp.process(spec))
            expected = read(m)
            m.setParams(ql.Array(NEW))
            expected.update({"after" + k[0].upper() + k[1:]: x for k, x in read(m).items()})
            out.append({"name": f"{name}, {cname} curves", "inputs": {"settings": st, "process": spec, "newParams": NEW},
                        "expected": expected})
    base = {"riskFree": bsp.FLAT_R, "dividend": bsp.FLAT_Q, "s0": 100.0, "v0": 0.04, "kappa": 1.5, "theta": 0.04,
            "sigma": 0.3, "rho": -0.7, "lambda": 2.0, "nu": -0.2, "delta": 0.1}
    for key, value in (("delta", 0.0), ("lambda", 0.0)):
        spec = {**base, key: value}
        out.append({"name": f"{key} at {value}", "inputs": {"settings": st, "process": spec},
                    "expected": {"params": v.attempt(lambda: list(ql.BatesModel(bp.process(spec)).params()))}})
    for name, s0, v0, kappa, theta, sigma, rho, lam, nu, delta in PARAMETERS[:2]:
        spec = {"riskFree": bsp.FLAT_R, "dividend": bsp.FLAT_Q, "s0": s0, "v0": v0, "kappa": kappa, "theta": theta,
                "sigma": sigma, "rho": rho, "lambda": lam, "nu": nu, "delta": delta}
        outputs = calibrated(spec, HELPERS)
        refits = [calibrated(moved(copy.deepcopy(spec), d), moved(copy.deepcopy(HELPERS), d))
                  for d in (math.inf, -math.inf)]
        kept = {k: x for k, x in outputs.items() if all(stable(x, r[k]) for r in refits)}
        print(f"calibration from {name}: dropped {len(outputs) - len(kept)} of {len(outputs)} outputs")
        out.append({"name": f"calibration from {name}",
                    "inputs": {"settings": st, "process": spec, "helpers": HELPERS}, "expected": kept})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/equity/batesmodel.hpp", cases)
