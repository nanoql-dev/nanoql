# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BatesProcess (ql/processes/batesprocess.hpp, .cpp), on the evaluation date 15 October 2026:
the Heston process with log-normal jumps.

Two jump sets (QuantLib's batesmodel test's lambda 2, nu -0.2, delta 0.1, and a rarer, larger set) on two of
tools/vectors/gen/processes/hestonprocess.py's parameter sets, on flat and linear zero curves, under QuantLib's default
discretization, full truncation, the one the wheel's constructor takes: size, factors and initial values; the drift and
diffusion at hestonprocess.py's states and times; and evolve from three states over three steps with four draws, the
third drawing the step's number of jumps through the inverse Poisson distribution and the fourth their sizes, the draws
chosen so that none, one and several jumps occur. The reader checks the in-place evolve and initial values on the same
cases; the other discretizations' steps are the Heston process's (hestonprocess.py) with the jumps applied
(tests/NanoQL.Tests/Conventions/StochasticProcessInPlaceTests.cs compares the two forms on each).

A case holds one process {"riskFree", "dividend", "s0", "v0", "kappa", "theta", "sigma", "rho", "lambda", "nu",
"delta"}, the curves tools/vectors/gen/processes/blackscholesprocess.py's specs. The wheel's calls, by QuantLib-SWIG
1.44's stochasticprocess.i: BatesProcess(riskFreeRate, dividendYield, s0, v0, kappa, theta, sigma, rho, lambda, nu,
delta), size, factors, initialValues, drift, diffusion and evolve; the wheel hides lambda, nu and delta. Read back by
tests/NanoQL.Tests/Vectors/Processes/BatesProcessVectorTests.cs."""
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


bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
hp = _load("hestonprocess", "processes/hestonprocess.py")
R = bsp.R
JUMPS = [("batesmodel's jumps", 2.0, -0.2, 0.1), ("rare large jumps", 0.3, 0.05, 0.3)]
DRAWS = [[0.3, -1.2, -1.0, 0.7], [-2.0, 0.5, 1.2, -0.4], [0.4, 0.1, 2.6, 1.5]]


def process(spec):
    return ql.BatesProcess(ql.YieldTermStructureHandle(bsp.curve(spec["riskFree"])),
                           ql.YieldTermStructureHandle(bsp.curve(spec["dividend"])),
                           ql.QuoteHandle(ql.SimpleQuote(spec["s0"])), spec["v0"], spec["kappa"], spec["theta"],
                           spec["sigma"], spec["rho"], spec["lambda"], spec["nu"], spec["delta"])


def cases():
    st = v.settings(R)
    out = []
    for pname, s0, v0, kappa, theta, sigma, rho in hp.PARAMETERS[:2]:
        for jname, lam, nu, delta in JUMPS:
            for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
                spec = {"riskFree": rf, "dividend": q, "s0": s0, "v0": v0, "kappa": kappa, "theta": theta,
                        "sigma": sigma, "rho": rho, "lambda": lam, "nu": nu, "delta": delta}
                p = process(spec)
                expected = {"size": p.size(), "factors": p.factors(), "initialValues": list(p.initialValues())}
                expected["drift"] = [v.attempt(lambda: list(p.drift(t, ql.Array(x))))
                                     for t in hp.TIMES for x in hp.STATES]
                expected["diffusion"] = [v.attempt(lambda: hp.flat_matrix(p.diffusion(t, ql.Array(x))))
                                         for t in hp.TIMES for x in hp.STATES]
                evolves = [(x, dt, dw) for x in hp.STATES[:3] for dt in hp.STEPS for dw in DRAWS]
                expected["evolve"] = [v.attempt(lambda: list(p.evolve(0.0, ql.Array(x), dt, ql.Array(dw))))
                                      for x, dt, dw in evolves]
                out.append({"name": f"{pname}, {jname}, {cname} curves",
                            "inputs": {"settings": st, "process": spec, "times": hp.TIMES, "states": hp.STATES,
                                       "evolve": [[x, dt, dw] for x, dt, dw in evolves]},
                            "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/batesprocess.hpp", cases)
