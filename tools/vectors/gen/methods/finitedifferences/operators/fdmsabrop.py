# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmSabrOp (ql/methods/finitedifferences/operators/fdmsabrop.hpp, .cpp): the SABR model's
backward operator on a mesher of the forward by the log volatility, its two directions' diffusion and drift, its
correlation term and its discounting at half the curve's forward rate in each direction, on the evaluation date
15 October 2026.

Three SABR models (testFdmSabrCevPricing's f0 1.2, alpha 0.35, beta 0.1 and 0.9, at nu 0.4, rho 0.25; and
testFdmSabrVsVolApproximation's f0 100, alpha 0.35, beta 0.85, nu 0.75, rho -0.85) on flat curves at 5%, 0 and -1%
Actual/365 (Fixed), on the engine's mesher: the CEV mesher at the volatility's upper quartile
(tools/vectors/gen/methods/finitedifferences/meshers/fdmcev1dmesher.py's spec) by a concentrating mesher of the log
volatility over its eps-quantiles. For each, at three (t1, t2), tools/vectors/gen/methods/finitedifferences/operators/
fdmcevop.py's outputs.

The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: FdmSabrOp(mesher, rTS, f0, alpha, beta, nu, rho), the C++
constructor, and its members, the Arrays built from lists. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/FdmSabrOpVectorTests.cs."""
import importlib.util
import math
import os
import random
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


cm = _load("fdmcev1dmesher", "methods/finitedifferences/meshers/fdmcev1dmesher.py")
cevop = _load("fdmcevop", "methods/finitedifferences/operators/fdmcevop.py")
g2op = cevop.g2op
bsp = cevop.bsp
R = cm.R

MODELS = [("beta 0.1", 1.2, 0.35, 0.1, 0.4, 0.25, 0.05), ("beta 0.9", 1.2, 0.35, 0.9, 0.4, 0.25, 0.0),
          ("testFdmSabrVsVolApproximation's", 100.0, 0.35, 0.85, 0.75, -0.85, -0.01)]


def engine_mesher(f0, alpha, beta, nu, maturity, f_grid, x_grid, strike, eps=1e-4, scaling=1.0):
    """FdSabrVanillaEngine's mesher (fdsabrvanillaengine.cpp:76-103)."""
    upper_alpha = alpha * math.exp(nu * math.sqrt(maturity) * ql.InverseCumulativeNormal()(0.75))
    norm_inv_eps = ql.InverseCumulativeNormal()(1 - eps)
    log_drift = -0.5 * nu * nu * maturity
    vol_range = nu * math.sqrt(maturity) * norm_inv_eps * scaling
    x_min = math.log(alpha) + log_drift - vol_range
    x_max = math.log(alpha) + log_drift + vol_range
    return {"meshers": [cm.cev_spec(f_grid, f0, upper_alpha, beta, maturity, eps, scaling, (strike, 0.025)),
                        {"type": "Concentrating", "start": x_min, "end": x_max, "size": x_grid,
                         "cPoint": [math.log(alpha), 0.1], "requireCPoint": False}]}


def cases():
    st = v.settings(R)
    rng = random.Random(20261025)
    out = []
    for name, f0, alpha, beta, nu, rho, rate in MODELS:
        curve_spec = bsp.flat_spec(R, rate)
        curve = bsp.curve(curve_spec)
        comp = engine_mesher(f0, alpha, beta, nu, 1.0, 15, 7, f0 * 1.1)
        mesher, keep = cm.composite(comp)
        n = mesher.layout().size()
        rs = [g2op.seeded(rng, n), g2op.seeded(rng, n)]
        op = ql.FdmSabrOp(mesher, curve, f0, alpha, beta, nu, rho)
        out.append({"name": f"{name}, the engine's mesher",
                    "inputs": {"settings": st, "f0": f0, "alpha": alpha, "beta": beta, "nu": nu, "rho": rho,
                               "rTS": curve_spec, "mesher": comp, "r": rs, "times": cevop.TIMES,
                               "splitting": g2op.SPLITTING, "preconditioner": g2op.PRECONDITIONER},
                    "expected": cevop.op_outputs(op, rs, 2)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/fdmsabrop.hpp", cases)
