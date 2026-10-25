# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmHestonVarianceMesher and FdmHestonLocalVolatilityVarianceMesher
(ql/methods/finitedifferences/meshers/fdmhestonvariancemesher.hpp, .cpp), on the evaluation date 15 October 2026: the
variance grid on the quantiles of the square-root process's distribution, averaged over the steps to the maturity, and
the volatility estimate the engines size their log-spot mesher on.

FdmHestonVarianceMesher on tools/vectors/gen/processes/hestonprocess.py's three parameter sets (their sigma/kappa at
most 1.5) on flat curves, at 9, 25 and 51 points, maturities of a quarter, a year and five years, QuantLib's 10
averaging steps and epsilon 1e-4; then on the first set, 3 and 30 averaging steps and epsilon 1e-5 and 1e-3. The
local volatility mesher on the first set, at 25 points and the three maturities, with
tools/vectors/gen/pricingengines/vanilla/fdhestonvanillaengine.py's constant and fixed surface leverage functions.
Outputs: locations, dplus, dminus (QuantLib's null at the ends as null) and volaEstimate.

Left out (the S-hes plan's §2.10): variance distributions on which the quantile grid falls back, which QuantLib's
master and nanoql test before building it (HR8, tests/NanoQL.Tests/Divergences/
FdmHestonVarianceMesherFallbackTests.cs: none of these sets is narrow for its grid or falls fast); a sigma over kappa
above 2, where nanoql caps the skew factor (HR9); a leverage function off the risk-free curve's day counter or
reference date; and the leverage surface read outside its strikes past its last time, where QuantLib's interpolator
extrapolates and nanoql holds the volatility (the S-van plan's VR4 (xviii)): its strikes span every forward the mesher
averages over. The meshers' mixing factor, which the wheel doesn't take, is the engines' (fdhestonvanillaengine.py).

A case holds the "process" (hestonprocess.py's spec), the "size", "maturity", "tAvgSteps" and "epsilon", and the
"leverage" (fdhestonvanillaengine.py's spec) for the local volatility mesher, absent for the other. The wheel's calls,
by QuantLib-SWIG 1.44's fdm.i: FdmHestonVarianceMesher(size, process, maturity, tAvgSteps, epsilon) and
FdmHestonLocalVolatilityVarianceMesher(size, process, leverageFct, maturity, tAvgSteps, epsilon); locations, dplus,
dminus and volaEstimate the C++ members. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Meshers/FdmHestonVarianceMesherVectorTests.cs."""
import importlib.util
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
hp, bsm = fhv.hp, fhv.bsm
R = fhv.R


def outputs(m):
    expected = bsm.outputs(m)
    expected["volaEstimate"] = m.volaEstimate()
    return expected


def cases():
    st = v.settings(R)
    out = []

    def add(name, p, size, maturity, t_avg=10, eps=1e-4, leverage=None):
        process = hp.process(p)
        if leverage is None:
            m = ql.FdmHestonVarianceMesher(size, process, maturity, t_avg, eps)
        else:
            m = ql.FdmHestonLocalVolatilityVarianceMesher(size, process, fhv.leverage(leverage), maturity, t_avg, eps)
        inputs = {"settings": st, "process": p, "size": size, "maturity": maturity, "tAvgSteps": t_avg,
                  "epsilon": eps}
        if leverage is not None:
            inputs["leverage"] = leverage
        out.append({"name": name, "inputs": inputs, "expected": outputs(m)})

    for params in hp.PARAMETERS:
        p = fhv.heston_spec(params)
        for size in (9, 25, 51):
            for maturity in (0.25, 1.0, 5.0):
                add(f"{params[0]}, {size} points, {maturity:g} years", p, size, maturity)
    p = fhv.heston_spec(hp.PARAMETERS[0])
    for t_avg in (3, 30):
        add(f"{t_avg} averaging steps", p, 25, 1.0, t_avg)
    for eps in (1e-5, 1e-3):
        add(f"epsilon {eps:g}", p, 25, 1.0, eps=eps)
    for lname, leverage in (("constant", fhv.CONSTANT_LEVERAGE), ("surface", fhv.SURFACE_LEVERAGE)):
        for maturity in (0.25, 1.0, 5.0):
            add(f"local volatility, {lname} leverage, {maturity:g} years", p, 25, maturity, leverage=leverage)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/meshers/fdmhestonvariancemesher.hpp", cases)
