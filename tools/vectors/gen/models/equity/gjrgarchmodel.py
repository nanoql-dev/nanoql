# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GJRGARCHModel (ql/models/equity/gjrgarchmodel.hpp, .cpp), on the evaluation date 15 October
2026: the GJR-GARCH(1,1) model's six parameters and a calibration.

tools/vectors/gen/processes/gjrgarchprocess.py's two processes on flat and linear zero curves: params (omega, alpha,
beta, gamma, lambda and v0, in that order), each accessor; then the parameters set to other values, read back. Then
calibrations from both processes, on a flat risk-free curve and no dividend yield, to nine helpers (maturities of three
months, six months and a year, strikes at 0.9, 1 and 1.1 of the spot, quoted at volatilities of a skew), each helper
priced by the analytic GJR-GARCH engine on the model, by Levenberg-Marquardt at QuantLib's defaults and QuantLib's DAX
case's end criteria: the calibrated params, the helpers' calibration errors, the end criteria and the function
evaluations, through the S7R6 filter of the master plan: each calibration is refit with every nonzero real input (the
vols, the spot, the curves' rates and the starting parameters) moved one ulp, up and down together, and an output is
written only where both refits keep it within 1e-12 x max(|a|, |b|, 1) (an integer, where they give the same); the
generator prints "dropped n of m outputs".

Left out (the S-hes plan's §2.10): every structure is on the risk-free curve's day counter and reference date; the
calibrations hold no dividend yield, where nanoql's analytic engine prices on r − q (HR7 (xxix),
tests/NanoQL.Tests/Divergences/AnalyticGjrGarchDividendTests.cs).

A case holds one process, gjrgarchprocess.py's spec, and for a calibration its helpers [{"months", "strike", "vol"}].
The wheel's calls, by QuantLib-SWIG 1.44's calibratedmodel.i and calibrationhelpers.i: GJRGARCHModel(GJRGARCHProcess(
...)), params, omega, alpha, beta, gamma, lambda_parameter, v0, setParams, calibrate, endCriteria, functionEvaluation,
HestonModelHelper(period, NullCalendar, s0, strike, vol, riskFree, dividend) with
setPricingEngine(AnalyticGJRGARCHEngine(model)) and calibrationError. Read back by
tests/NanoQL.Tests/Vectors/Models/GJRGARCHModelVectorTests.cs."""
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
gp = _load("gjrgarchprocess", "processes/gjrgarchprocess.py")
R = bsp.R
FULL_TRUNCATION = int(ql.GJRGARCHProcess.FullTruncation)
NO_DIVIDEND = bsp.flat_spec(R, 0.0)
NEW = [3.0e-6, 0.03, 0.9, 0.05, 0.2, 1.5e-4]
SKEW = ((3, 0.9, 0.3327), (3, 1.0, 0.3108), (3, 1.1, 0.2919), (6, 0.9, 0.3155), (6, 1.0, 0.3027),
        (6, 1.1, 0.2919), (12, 0.9, 0.2999), (12, 1.0, 0.2870), (12, 1.1, 0.2751))
STABLE = 1e-12


def helpers_for(s0):
    return [{"months": m, "strike": k * s0, "vol": vol} for m, k, vol in SKEW]


def read(m):
    return {"params": list(m.params()), "omega": m.omega(), "alpha": m.alpha(), "beta": m.beta(),
            "gamma": m.gamma(), "lambda": m.lambda_parameter(), "v0": m.v0()}


def calibrated(spec, helper_specs):
    """The calibration's outputs, each list element its own."""
    p = gp.process(spec)
    model = ql.GJRGARCHModel(p)
    engine = ql.AnalyticGJRGARCHEngine(model)
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
    for params in gp.PARAMETERS:
        for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
            spec = gp.process_spec(params, rf, q, FULL_TRUNCATION)
            m = ql.GJRGARCHModel(gp.process(spec))
            expected = read(m)
            m.setParams(ql.Array(NEW))
            expected.update({"after" + k[0].upper() + k[1:]: x for k, x in read(m).items()})
            out.append({"name": f"{params[0]}, {cname} curves",
                        "inputs": {"settings": st, "process": spec, "newParams": NEW}, "expected": expected})
    for params in gp.PARAMETERS:
        spec = gp.process_spec(params, bsp.FLAT_R, NO_DIVIDEND, FULL_TRUNCATION)
        helpers = helpers_for(spec["s0"])
        outputs = calibrated(spec, helpers)
        refits = [calibrated(moved(copy.deepcopy(spec), d), moved(copy.deepcopy(helpers), d))
                  for d in (math.inf, -math.inf)]
        kept = {k: x for k, x in outputs.items() if all(stable(x, r[k]) for r in refits)}
        print(f"calibration from {params[0]}: dropped {len(outputs) - len(kept)} of {len(outputs)} outputs")
        out.append({"name": f"calibration from {params[0]}",
                    "inputs": {"settings": st, "process": spec, "helpers": helpers}, "expected": kept})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/equity/gjrgarchmodel.hpp", cases)
