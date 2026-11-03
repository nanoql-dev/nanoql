# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GJRGARCHProcess (ql/processes/gjrgarchprocess.hpp, .cpp), on the evaluation date 15 October
2026: the continuous-time GJR-GARCH(1,1) process of the spot and its daily variance.

Two parameter sets (QuantLib's gjrgarchmodel test's omega 2e-6, alpha 0.024, beta 0.93, gamma 0.059, lambda 0.1 at 365
days a year from its stationary variance, and a set at 252 days with a larger leverage and risk premium) on flat and
linear zero curves, each under the three discretizations: size, factors and initial values; the drift and diffusion at
four states (the variance positive, small and negative) and two times; and evolve from three states over three steps (a
day, 0.05 and 0.25) with three draws. The reader checks the in-place evolve and initial values on the same cases
(tests/NanoQL.Tests/Conventions/StochasticProcessInPlaceTests.cs compares the two forms).

Left out: every structure is on the risk-free curve's day counter and reference date.

A case holds one process {"riskFree", "dividend", "s0", "v0", "omega", "alpha", "beta", "gamma", "lambda",
"daysPerYear", "discretization" (QuantLib's integer)}, the curves tools/vectors/gen/processes/blackscholesprocess.py's
specs. The wheel's calls, by QuantLib-SWIG 1.44's stochasticprocess.i: GJRGARCHProcess(riskFreeRate, dividendYield,
s0, v0, omega, alpha, beta, gamma, lambda, daysPerYear, discretization), size, factors, initialValues, drift, diffusion
and evolve. Read back by tests/NanoQL.Tests/Vectors/Processes/GJRGARCHProcessVectorTests.cs."""
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
SCHEMES = ["PartialTruncation", "FullTruncation", "Reflection"]


def stationary_variance(omega, alpha, beta, gamma, lam):
    """QuantLib's gjrgarchmodel test's v0: omega over one less the variance's mean reversion factor m1."""
    m1 = (beta + (alpha + gamma * ql.CumulativeNormalDistribution()(lam)) * (1.0 + lam * lam)
          + gamma * lam * math.exp(-lam * lam / 2.0) / math.sqrt(2.0 * math.pi))
    return omega / (1.0 - m1)


# name, s0, v0, omega, alpha, beta, gamma, lambda, days per year
PARAMETERS = [
    ("gjrgarchmodel's set", 50.0, stationary_variance(2.0e-6, 0.024, 0.93, 0.059, 0.1),
     2.0e-6, 0.024, 0.93, 0.059, 0.1, 365.0),
    ("trading days, larger leverage", 100.0, 2.0e-4, 5.0e-6, 0.05, 0.85, 0.1, 0.3, 252.0),
]
STATES = [[50.0, 2.0e-4], [40.0, 4.0e-4], [60.0, 1.0e-6], [50.0, -1.0e-5]]
TIMES = [0.0, 0.5]
STEPS = [1.0 / 365.0, 0.05, 0.25]
DRAWS = [[0.3, -1.2], [-2.0, 0.5], [0.0, 0.0]]


def process(spec):
    return ql.GJRGARCHProcess(ql.YieldTermStructureHandle(bsp.curve(spec["riskFree"])),
                              ql.YieldTermStructureHandle(bsp.curve(spec["dividend"])),
                              ql.QuoteHandle(ql.SimpleQuote(spec["s0"])), spec["v0"], spec["omega"], spec["alpha"],
                              spec["beta"], spec["gamma"], spec["lambda"], spec["daysPerYear"],
                              spec["discretization"])


def process_spec(params, rf, q, discretization):
    _, s0, v0, omega, alpha, beta, gamma, lam, days = params
    return {"riskFree": rf, "dividend": q, "s0": s0, "v0": v0, "omega": omega, "alpha": alpha, "beta": beta,
            "gamma": gamma, "lambda": lam, "daysPerYear": days, "discretization": discretization}


def cases():
    st = v.settings(R)
    out = []
    for params in PARAMETERS:
        for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
            for scheme in SCHEMES:
                spec = process_spec(params, rf, q, int(getattr(ql.GJRGARCHProcess, scheme)))
                p = process(spec)
                expected = {"size": p.size(), "factors": p.factors(), "initialValues": list(p.initialValues())}
                expected["drift"] = [v.attempt(lambda: list(p.drift(t, ql.Array(x)))) for t in TIMES for x in STATES]
                expected["diffusion"] = [v.attempt(lambda: hp.flat_matrix(p.diffusion(t, ql.Array(x))))
                                         for t in TIMES for x in STATES]
                evolves = [(x, dt, dw) for x in STATES[:3] for dt in STEPS for dw in DRAWS]
                expected["evolve"] = [v.attempt(lambda: list(p.evolve(0.0, ql.Array(x), dt, ql.Array(dw))))
                                      for x, dt, dw in evolves]
                out.append({"name": f"{params[0]}, {cname} curves, {scheme}",
                            "inputs": {"settings": st, "process": spec, "times": TIMES, "states": STATES,
                                       "evolve": [[x, dt, dw] for x, dt, dw in evolves]},
                            "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/gjrgarchprocess.hpp", cases)
