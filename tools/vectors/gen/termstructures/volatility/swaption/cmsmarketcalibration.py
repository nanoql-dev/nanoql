# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CmsMarketCalibration (ql/termstructures/volatility/swaption/cmsmarketcalibration.hpp, .cpp) on
test-suite/cms.cpp's testCmsMarketCalibration setup, on the evaluation date and curve of
tools/vectors/gen/termstructures/volatility/swaption/swaptionvolcube.py (16 June 2025, a flat 5%): its SABR cube with
beta fixed and no at-the-money calibration, extrapolating; a CMS market of 1, 5, 10 and 20 years over the 2-year swap
index, with cms.cpp's bid and ask spreads and an analytic Hagan pricer on the at-the-money matrix; unit weights.

Each case calibrates a fresh cube and market, since QuantLib's calibration recalibrates its cube in place: the free
reversion with Levenberg-Marquardt (1e-8, 1e-8, 1e-8) and cms.cpp's end criteria (20, 10, 1e-6, 1e-6, 1e-6) from the
guess (0.5, 0.01), on spreads and on prices, on the analytic Hagan pricer's standard model, and on spreads on an
ATM-calibrated and on a single-pass cube; and on spreads the matrix guess (a beta of 0.5 per swap length) and the
parametric guess (0.5, 0.5, 0.1), on the standard model, each with the reversion fixed at 1%. The standard model's
prices don't read the reversion, which no pricer here can read unmoved by a fix or a refusal (below). The outputs are
compute()'s result, flattened row by row, error() and endCriteria(), and the members compute() leaves on the
calibration, which the wheel reaches through the cube and market it recalibrated and repriced in place: the cube's
sparseSabrParameters() and the market's browse(), each flattened row by row, and the cube's denseSabrParameters() where
it has them, the ATM-calibrated and the single-pass cubes, cms.cpp's cube having none. Left out, nanoql's fixes: the
vector guess with the reversion fixed, whose betas QuantLib's guess leaves untransformed; a fixed negative reversion;
and the Simplex fits, whose reversion QuantLib's transform returns as NaN, as it does for a free reversion that the
prices read, whose Levenberg-Marquardt steps reach y below 0 (the linear TSR pricer's fit visits 32 such points in
nanoql's port); the analytic Hagan pricer's shift models, whose prices the normalisation moves, where cms.cpp's probes
read the non-parallel shifts; and the linear TSR pricer, whose sections on the SABR cube carry the cube's interpolated
forward. QuantLib's numeric Hagan pricer on the non-parallel shifts refuses this market at a reversion of 1% ("root not
bracketed", its g-function's shift solve on the 10-year swaps).

The outputs take the stability filter: each case is calibrated again with each of the eight bid and ask quotes moved one
ulp up, then down, and an output is written only where every move leaves it within 1e-12 x max(|a|, |b|, 1); the dropped
indexes per output are in the case's inputs "dropped".

Each case's inputs hold its settings, the cube (a SABR spec of swaptionvolcube.py's), the swap lengths, the index, the
bid and ask spreads, the pricer ({"type": "analytic", "model", "meanReversion"}, on the cube's at-the-money matrix), the
weights, the calibration type, the method ("guess": "vector" | "matrix" | "parametric"), the guess, whether the
reversion is fixed and the reversion's guess. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/CmsMarketCalibrationVectorTests.cs."""
import importlib.util
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _dir)
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_swaptionvolcube", os.path.join(v.GEN_ROOT, "termstructures", "volatility", "swaption", "swaptionvolcube.py"))
svc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(svc)
Y = svc.Y

CURVE = svc.flat_curve(svc.TODAY)
CUBE = svc.sabr_spec(svc.atm_floating(), CURVE, fixed=svc.BETA_FIXED, atm_calibrated=False, extrapolate=True)
CUBE_ATM = svc.sabr_spec(svc.atm_floating(), CURVE, fixed=svc.BETA_FIXED, atm_calibrated=True, extrapolate=True)
CUBE_SINGLE = svc.sabr_spec(svc.atm_floating(), CURVE, fixed=svc.BETA_FIXED, atm_calibrated=False, extrapolate=True,
                            single_pass=True)
LENGTHS = [[1, Y], [5, Y], [10, Y], [20, Y]]
INDEX = svc.euribor([2, Y], CURVE)
SPREADS = [[0.01, 0.011], [0.012, 0.013], [0.014, 0.015], [0.016, 0.017]]
WEIGHTS = [[1.0], [1.0], [1.0], [1.0]]
TYPES = {"OnSpread": ql.CmsMarketCalibration.OnSpread, "OnPrice": ql.CmsMarketCalibration.OnPrice,
         "OnForwardCmsPrice": ql.CmsMarketCalibration.OnForwardCmsPrice}
MODELS = {"Standard": ql.GFunctionFactory.Standard}


def matrix(rows):
    m = ql.Matrix(len(rows), len(rows[0]))
    for i, row in enumerate(rows):
        for j, x in enumerate(row):
            m[i][j] = x
    return m


def calibrate(spec, quotes, cube_spec):
    """compute()'s outputs on a fresh cube and market, the market's quotes those given."""
    cube = svc.make_cube(cube_spec)
    atm = ql.SwaptionVolatilityStructureHandle(svc.make_atm(cube_spec["atm"]))
    index = svc.make_index(INDEX)
    pricers = ql.CmsCouponPricerVector()
    reversion = ql.QuoteHandle(ql.SimpleQuote(spec["pricer"]["meanReversion"]))
    pricers.append(ql.AnalyticHaganPricer(atm, MODELS[spec["pricer"]["model"]], reversion))
    bid_ask = [[ql.QuoteHandle(ql.SimpleQuote(q)) for q in row] for row in quotes]
    market = ql.CmsMarket([svc.period(p) for p in LENGTHS], [index], index.iborIndex(), bid_ask, pricers,
                          index.forwardingTermStructure())
    calibration = ql.CmsMarketCalibration(ql.SwaptionVolatilityStructureHandle(cube), market, matrix(WEIGHTS),
                                          TYPES[spec["calibrationType"]])
    ec = ql.EndCriteria(20, 10, 1e-6, 1e-6, 1e-6)
    lm = ql.LevenbergMarquardt(1e-8, 1e-8, 1e-8)
    fixed = spec["isMeanReversionFixed"]
    if spec["guessKind"] == "vector":
        r = calibration.compute(ec, lm, ql.Array(spec["guess"]), fixed)
        result = list(r)
    elif spec["guessKind"] == "matrix":
        r = calibration.compute(ec, lm, matrix(spec["guess"]), fixed, spec["meanReversionGuess"])
        result = [r[i][j] for i in range(r.rows()) for j in range(r.columns())]
    else:
        r = calibration.computeParametric(ec, lm, matrix(spec["guess"]), fixed, spec["meanReversionGuess"])
        result = [r[i][j] for i in range(r.rows()) for j in range(r.columns())]
    sparse = cube.sparseSabrParameters()
    browse = market.browse()
    out = {"result": result, "error": [calibration.error()], "endCriteria": [int(calibration.endCriteria())],
           "sparseSabrParameters": [sparse[i][j] for i in range(sparse.rows()) for j in range(sparse.columns())],
           "browse": [browse[i][j] for i in range(browse.rows()) for j in range(browse.columns())]}
    if cube_spec["isAtmCalibrated"] or cube_spec["singlePassCalibration"]:
        dense = cube.denseSabrParameters()
        out["denseSabrParameters"] = [dense[i][j] for i in range(dense.rows()) for j in range(dense.columns())]
    return out


def moved_quotes():
    for i in range(len(SPREADS)):
        for j in range(len(SPREADS[0])):
            for d in (math.inf, -math.inf):
                q = [row[:] for row in SPREADS]
                q[i][j] = math.nextafter(q[i][j], d)
                yield q


def case(name, spec, cube_spec=CUBE):
    st = v.settings(svc.TODAY)
    base = calibrate(spec, SPREADS, cube_spec)
    refits = [calibrate(spec, q, cube_spec) for q in moved_quotes()]
    expected, dropped = {}, {}
    for key, values in base.items():
        keep = [k for k in range(len(values))
                if all(svc.is_stable(values[k], r[key][k]) for r in refits)]
        expected[key] = [values[k] for k in keep]
        if len(keep) < len(values):
            dropped[key] = [k for k in range(len(values)) if k not in keep]
    print(f"{name}: dropped {sum(len(d) for d in dropped.values())} of {sum(len(x) for x in base.values())} outputs")
    inputs = {"settings": st, "cube": cube_spec, "swapLengths": LENGTHS, "index": INDEX, "bidAskSpreads": SPREADS,
              "weights": WEIGHTS, **spec, "dropped": dropped}
    return {"name": name, "inputs": inputs,
            "expected": expected}


def cases():
    out = []
    standard = {"type": "analytic", "model": "Standard", "meanReversion": 0.0}
    for ctype in ("OnSpread", "OnPrice"):
        out.append(case(f"Standard, {ctype}, free reversion", {
            "pricer": standard, "calibrationType": ctype, "guessKind": "vector",
            "guess": [0.5, 0.01], "isMeanReversionFixed": False, "meanReversionGuess": None}))
    out.append(case("Standard, OnSpread, matrix guess, fixed reversion", {
        "pricer": standard, "calibrationType": "OnSpread", "guessKind": "matrix", "guess": [[0.5]] * 4,
        "isMeanReversionFixed": True, "meanReversionGuess": 0.01}))
    out.append(case("Standard, OnSpread, parametric guess, fixed reversion", {
        "pricer": standard, "calibrationType": "OnSpread", "guessKind": "parametric", "guess": [[0.5], [0.5], [0.1]],
        "isMeanReversionFixed": True, "meanReversionGuess": 0.01}))
    for cube_spec, label in ((CUBE_ATM, "ATM-calibrated cube"), (CUBE_SINGLE, "single-pass cube")):
        out.append(case(f"Standard, OnSpread, free reversion, {label}", {
            "pricer": standard, "calibrationType": "OnSpread", "guessKind": "vector",
            "guess": [0.5, 0.01], "isMeanReversionFixed": False, "meanReversionGuess": None}, cube_spec))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/swaption/cmsmarketcalibration.hpp", cases)
