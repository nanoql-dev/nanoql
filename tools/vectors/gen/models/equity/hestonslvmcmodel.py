# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's HestonSLVMCModel (ql/models/equity/hestonslvmcmodel.hpp, .cpp), on the evaluation date 15
October 2026: the Monte Carlo calibration of a Heston stochastic-local volatility model's leverage function to a local
volatility, binning each step's paths by spot.

tools/vectors/gen/processes/hestonprocess.py's first parameter set on flat curves, to a year: on a constant local
volatility of 25% with the Mersenne-Twister factory (seed 1234) and with the Sobol factory (diagonal ordering, seed
1234, Joe-Kuo D7, QuantLib's hestonslvmodel test's), on a local volatility smile constant in time, at mixing factor
0.5, with a mandatory date, and on zero curves; 8,192 paths, 12 steps a year and 21 bins. Each case's leverage
function at strikes 80, 90, 100, 110 and 125: between the time grid's nodes (1/24, 7/24, 13/24 and 19/24 of a year),
where QuantLib 1.44's surface holds a strike beyond a time's strikes at the nearest, and at the nodes 0.5 and 1.

Left out: a time-dependent local volatility, which nanoql reads at a step's end where 1.44 reads its start (#2860;
tests/NanoQL.Tests/TestSuite/HestonSlvModelTests.cs's upstream case); structures dated or day-counted apart
(tests/NanoQL.Tests/Divergences/HestonSlvMismatchedTimesTests.cs).

Filtered by the stability filter: each case is refit with every input moved one ulp up, then down (the Heston
parameters and spot, the local volatility's values, the flat curves' rates), and each Heston parameter and the spot
alone; an output is written only where every refit leaves it within 1e-12 x max(|a|, |b|, 1). The kept points are the
case's "points"; the dropped count is printed per case.

The specs are {"heston": hestonprocess.py's, "localVol": fdhestonvanillaengine.py's leverage spec, "generator":
{"type": "MT", "seed"} or {"type": "Sobol", "ordering", "seed", "directionIntegers"}, "endDate", "timeStepsPerYear",
"nBins", "calibrationPaths", "mandatoryDates", "mixingFactor"}. The wheel's calls, by QuantLib-SWIG 1.44:
HestonSLVMCModel(localVol, hestonModel, brownianGeneratorFactory, endDate, timeStepsPerYear, nBins,
calibrationPaths, mandatoryDates, mixingFactor).leverageFunction().localVol(t, strike, True). Read back by
tests/NanoQL.Tests/Vectors/Models/Equity/HestonSLVMCModelVectorTests.cs."""
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


fhv = _load("fdhestonvanillaengine", "pricingengines/vanilla/fdhestonvanillaengine.py")
flv = _load("fixedlocalvolsurface", "termstructures/volatility/equityfx/fixedlocalvolsurface.py")
hp, bsp = fhv.hp, fhv.bsp
R = fhv.R
HESTON_SCALARS = ["s0", "v0", "kappa", "theta", "sigma", "rho"]
CONSTANT_LV = {"type": "LocalConstantVol", "referenceDate": R.serialNumber(), "volatility": 0.25,
               "dayCounter": {"type": "Actual365Fixed"}}
SMILE_STRIKES = [40.0, 70.0, 90.0, 100.0, 110.0, 140.0, 250.0]
SMILE = [0.36, 0.29, 0.255, 0.24, 0.23, 0.225, 0.25]
FLAT_SMILE_LV = flv.surface_spec("times", [0.25, 0.5, 1.0, 2.0], SMILE_STRIKES, [[x] * 4 for x in SMILE])
STRIKES = [80.0, 90.0, 100.0, 110.0, 125.0]
TIMES = [1.0 / 24, 7.0 / 24, 13.0 / 24, 19.0 / 24, 0.5, 1.0]
MT = {"type": "MT", "seed": 1234}
SOBOL = {"type": "Sobol", "ordering": int(ql.SobolBrownianGenerator.Diagonal), "seed": 1234,
         "directionIntegers": int(ql.SobolRsg.JoeKuoD7)}


def factory(spec):
    if spec["type"] == "MT":
        return ql.MTBrownianGeneratorFactory(spec["seed"])
    return ql.SobolBrownianGeneratorFactory(spec["ordering"], spec["seed"], spec["directionIntegers"])


def ulp(x, direction):
    return math.nextafter(x, direction) if x != 0.0 else x


def moved_curve(spec, direction):
    spec = copy.deepcopy(spec)
    if "forward" in spec:
        spec["forward"] = ulp(spec["forward"], direction)
    return spec


def moved_local_vol(spec, direction):
    spec = copy.deepcopy(spec)
    if "volatility" in spec:
        spec["volatility"] = ulp(spec["volatility"], direction)
    else:
        spec["vols"] = [[ulp(x, direction) for x in row] for row in spec["vols"]]
    return spec


def moved_all(spec, direction):
    """The case with every input it can move moved one ulp towards direction."""
    spec = copy.deepcopy(spec)
    h = spec["heston"]
    for k in HESTON_SCALARS:
        h[k] = ulp(h[k], direction)
    h["riskFree"], h["dividend"] = moved_curve(h["riskFree"], direction), moved_curve(h["dividend"], direction)
    spec["localVol"] = moved_local_vol(spec["localVol"], direction)
    return spec


def moved_one(spec, key, direction):
    spec = copy.deepcopy(spec)
    spec["heston"][key] = ulp(spec["heston"][key], direction)
    return spec


def refit_specs(spec):
    out = [moved_all(spec, d) for d in (math.inf, -math.inf)]
    out += [moved_one(spec, k, d) for k in HESTON_SCALARS for d in (math.inf, -math.inf)]
    return out


def stable(a, b):
    return abs(a - b) <= 1e-12 * max(abs(a), abs(b), 1.0)


def filtered(name, spec, points, leverage_values):
    """The case's points and values kept under the stability filter, and the dropped count printed."""
    base = leverage_values(spec, points)
    refits = [leverage_values(s, points) for s in refit_specs(spec)]
    kept = [i for i in range(len(points)) if all(stable(base[i], r[i]) for r in refits)]
    print(f"{name}: dropped {len(points) - len(kept)} of {len(points)} outputs", file=sys.stderr)
    return [points[i] for i in kept], [base[i] for i in kept]


def leverage_values(spec, points):
    process = hp.process(spec["heston"])
    model = ql.HestonModel(process)
    lv = fhv.leverage(spec["localVol"])
    mc = ql.HestonSLVMCModel(lv, model, factory(spec["generator"]), ql.Date(spec["endDate"]),
                             spec["timeStepsPerYear"], spec["nBins"], spec["calibrationPaths"],
                             [ql.Date(d) for d in spec["mandatoryDates"]], spec["mixingFactor"])
    leverage = mc.leverageFunction()
    return [leverage.localVol(t, k, True) for t, k in points]


def model_spec(heston, local_vol, generator, mandatory=(), mixing=1.0):
    return {"heston": heston, "localVol": local_vol, "generator": generator, "endDate": (R + 365).serialNumber(),
            "timeStepsPerYear": 12, "nBins": 21, "calibrationPaths": 8192, "mandatoryDates": list(mandatory),
            "mixingFactor": mixing}


def specs():
    flat = fhv.heston_spec(hp.PARAMETERS[0])
    zero = fhv.heston_spec(hp.PARAMETERS[0], bsp.ZERO_R, bsp.ZERO_Q)
    return [("constant local volatility, Mersenne Twister", model_spec(flat, CONSTANT_LV, MT)),
            ("constant local volatility, Sobol", model_spec(flat, CONSTANT_LV, SOBOL)),
            ("a smile constant in time, Sobol", model_spec(flat, FLAT_SMILE_LV, SOBOL)),
            ("constant local volatility, mixing 0.5", model_spec(flat, CONSTANT_LV, MT, mixing=0.5)),
            ("a mandatory date", model_spec(flat, CONSTANT_LV, MT, mandatory=[(R + 100).serialNumber()])),
            ("zero curves", model_spec(zero, CONSTANT_LV, MT))]


def cases():
    st = v.settings(R)
    out = []
    points = [[t, k] for t in TIMES for k in STRIKES]
    for name, spec in specs():
        kept, values = filtered(name, spec, points, leverage_values)
        out.append({"name": name, "inputs": {"settings": st, "model": spec, "points": kept},
                    "expected": {"leverage": values}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/equity/hestonslvmcmodel.hpp", cases)
