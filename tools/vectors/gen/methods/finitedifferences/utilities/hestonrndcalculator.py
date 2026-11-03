# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's HestonRNDCalculator (ql/methods/finitedifferences/utilities/hestonrndcalculator.hpp, .cpp):
the density, cumulative distribution and inverse of the log spot under the Heston model.

Two Heston parameter sets (tools/vectors/gen/processes/hestonprocess.py's equity and high-vol-of-variance sets) on flat
and linear zero curves, at QuantLib's default accuracy (1e-6) and at 1e-8, at three times: the density and distribution
at log spots from 2.5 standard deviations of the average variance below the log forward to 2.5 above, the inverse at
probabilities from 0.01 to 0.99. Every structure is dated 15 October 2026 on Actual/365 (Fixed), so the dividend curve
read at the risk-free curve's times is read at its instants, and every κt is far above the floor where nanoql's form
departs. The wheel's calls, by QuantLib-SWIG 1.44: HestonRNDCalculator(process, integrationEps,
maxIntegrationIterations) and its pdf, cdf and invcdf. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Utilities/HestonRNDCalculatorVectorTests.cs."""
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
TIMES = [0.25, 1.0, 3.0]
SIGMAS = [-2.5, -1.0, 0.0, 1.0, 2.5]
PROBABILITIES = [0.01, 0.25, 0.5, 0.75, 0.99]
ACCURACIES = [("default accuracy", {"eps": 1e-6, "maxIterations": 10000}), ("1e-8", {"eps": 1e-8, "maxIterations": 10000})]


def cases():
    st = v.settings(R)
    out = []
    for pname, s0, v0, kappa, theta, sigma, rho in (hp.PARAMETERS[0], hp.PARAMETERS[1]):
        for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
            spec = {"riskFree": rf, "dividend": q, "s0": s0, "v0": v0, "kappa": kappa, "theta": theta, "sigma": sigma,
                    "rho": rho}
            process = hp.process({**spec, "discretization": 0})
            rts, qts = bsp.curve(rf), bsp.curve(q)
            for aname, aspec in ACCURACIES:
                calc = ql.HestonRNDCalculator(process, aspec["eps"], aspec["maxIterations"])
                points = []
                for t in TIMES:
                    avg = ((1 - math.exp(-kappa * t)) * (v0 - theta) / (kappa * t) + theta) * t
                    fwd = s0 * qts.discount(t) / rts.discount(t)
                    points += [[t, math.log(fwd) + s * math.sqrt(avg)] for s in SIGMAS]
                out.append({"name": f"{pname}, {cname} curves, {aname}",
                            "inputs": {"settings": st, "process": spec, "calculator": aspec, "points": points,
                                       "times": TIMES, "probabilities": PROBABILITIES},
                            "expected": {
                                "pdf": [v.attempt(lambda t=t, x=x: calc.pdf(x, t)) for t, x in points],
                                "cdf": [v.attempt(lambda t=t, x=x: calc.cdf(x, t)) for t, x in points],
                                "invcdf": [[v.attempt(lambda t=t, p=p: calc.invcdf(p, t)) for p in PROBABILITIES]
                                           for t in TIMES]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/hestonrndcalculator.hpp", cases)
