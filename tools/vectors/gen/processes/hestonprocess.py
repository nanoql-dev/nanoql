# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's HestonProcess (ql/processes/hestonprocess.hpp, .cpp), on the evaluation date 15 October
2026: the two-factor process and its nine discretizations.

Three processes (a typical equity set, a high volatility of variance with a positive correlation, and one far from
Feller's condition) on flat and linear zero curves, each under every discretization: size, factors and initial values;
the drift and diffusion at four states (the variance positive, small and negative) and two times; the expectation,
standard deviation and covariance of S2's Euler discretization at two states and steps, the multi-dimensional members
of tools/vectors/gen/processes/eulerdiscretization.py's (C1's review, O1-2); and evolve from those states over three
steps with three draws (Broadie and Kaya's schemes take a third). The steps keep the non-central chi-squared
variance's non-centrality below 1,416.8, from which nanoql's inverse refuses where QuantLib's series stops unconverged
(the S-eq plan's SEQR7 (xvii)). Broadie and Kaya's schemes solve for the integrated variance, by Brent's method at an
absolute accuracy of 1e-5 on an adaptively integrated distribution, so the stability filter of the S7 plan's S7R6 runs on
their evolve outputs: each is evolved again with the process's v0, kappa, theta, sigma and rho, the state, the step and
the draws moved one ulp, together up and down and each alone, and written only where every move leaves it within
1e-12 x max(|a|, |b|, 1); the generator prints "dropped n of m outputs" per case, none for the Gauss-Laguerre and
trapezoid schemes, which move by 1e-15. The Gauss-Lobatto scheme's price moves by up to 2.7e-3 under one ulp of a
parameter, and under relative moves up to 1e-12, the size of the C runtime's last-bit differences in the complex
functions, it takes two to five discrete values, its solve landing on another branch: its file holds the variance
alone ("evolveVariance"), which no solve computes, and the reader checks the price at one input where QuantLib's is
steady (HestonProcessVectorTests.BroadieKayaGaussLobattoPrice). The Fokker-Planck density, pdf, waits on HestonProcess.cs' Partial
line.

A case holds one process {"riskFree", "dividend", "s0", "v0", "kappa", "theta", "sigma", "rho", "discretization"
(QuantLib's integer)}, the curves tools/vectors/gen/processes/blackscholesprocess.py's specs. The wheel's calls, by
QuantLib-SWIG 1.44's stochasticprocess.i: HestonProcess(riskFreeRate, dividendYield, s0, v0, kappa, theta, sigma,
rho, discretization), size, factors, initialValues, drift, diffusion, expectation, stdDeviation, covariance and
evolve. Read back by tests/NanoQL.Tests/Vectors/Processes/HestonProcessVectorTests.cs."""
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
R = bsp.R
SCHEMES = ["PartialTruncation", "FullTruncation", "Reflection", "NonCentralChiSquareVariance", "QuadraticExponential",
           "QuadraticExponentialMartingale", "BroadieKayaExactSchemeLobatto", "BroadieKayaExactSchemeLaguerre",
           "BroadieKayaExactSchemeTrapezoidal"]
PARAMETERS = [
    ("equity", 100.0, 0.04, 1.5, 0.04, 0.3, -0.7),
    ("high vol of variance", 50.0, 0.09, 0.8, 0.06, 1.2, 0.4),
    ("far from Feller", 120.0, 0.02, 3.0, 0.03, 0.9, -0.2),
]
STATES = [[100.0, 0.04], [80.0, 0.09], [120.0, 0.0004], [100.0, -0.01]]
TIMES = [0.0, 0.5]
STEPS = [0.05, 0.25, 1.0]
DRAWS = [[0.3, -1.2, 0.7], [-2.0, 0.5, -0.4], [0.0, 0.0, 0.0]]


def process(spec):
    return ql.HestonProcess(ql.YieldTermStructureHandle(bsp.curve(spec["riskFree"])),
                            ql.YieldTermStructureHandle(bsp.curve(spec["dividend"])),
                            ql.QuoteHandle(ql.SimpleQuote(spec["s0"])), spec["v0"], spec["kappa"], spec["theta"],
                            spec["sigma"], spec["rho"], spec["discretization"])


def flat_matrix(m):
    return [m[i][j] for i in range(m.rows()) for j in range(m.columns())]


def within(a, b):
    return abs(a - b) <= 1e-12 * max(abs(a), abs(b), 1.0)


def stable_evolve(spec, x, dt, dw, base):
    """Whether every refit with the inputs moved one ulp, together up and down and each alone, leaves base's state
    within the filter's measure (the S7 plan's S7R6): the inputs the process's v0, kappa, theta, sigma and rho, the
    state, the step and the draws."""
    names = ["v0", "kappa", "theta", "sigma", "rho"]
    inputs = [spec[n] for n in names] + list(x) + [dt] + list(dw)
    moves = []
    for d in (math.inf, -math.inf):
        moves.append([math.nextafter(u, d) for u in inputs])
        for i in range(len(inputs)):
            m = list(inputs)
            m[i] = math.nextafter(m[i], d)
            moves.append(m)
    for m in moves:
        moved_spec = {**spec, **dict(zip(names, m[:5]))}
        try:
            moved = list(process(moved_spec).evolve(0.0, ql.Array(m[5:7]), m[7], ql.Array(m[8:])))
        except RuntimeError:
            return False
        if not all(within(a, b) for a, b in zip(base, moved)):
            return False
    return True


def cases():
    st = v.settings(R)
    out = []
    for pname, s0, v0, kappa, theta, sigma, rho in PARAMETERS:
        for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
            for scheme in SCHEMES:
                d = int(getattr(ql.HestonProcess, scheme))
                spec = {"riskFree": rf, "dividend": q, "s0": s0, "v0": v0, "kappa": kappa, "theta": theta,
                        "sigma": sigma, "rho": rho, "discretization": d}
                p = process(spec)
                factors = p.factors()
                expected = {"size": p.size(), "factors": factors, "initialValues": list(p.initialValues())}
                expected["drift"] = [v.attempt(lambda: list(p.drift(t, ql.Array(x)))) for t in TIMES for x in STATES]
                expected["diffusion"] = [v.attempt(lambda: flat_matrix(p.diffusion(t, ql.Array(x))))
                                         for t in TIMES for x in STATES]
                euler = [(t, x, dt) for t in TIMES for x in STATES[:2] for dt in STEPS[:2]]
                expected["expectation"] = [v.attempt(lambda: list(p.expectation(t, ql.Array(x), dt)))
                                           for t, x, dt in euler]
                expected["stdDeviation"] = [v.attempt(lambda: flat_matrix(p.stdDeviation(t, ql.Array(x), dt)))
                                            for t, x, dt in euler]
                expected["covariance"] = [v.attempt(lambda: flat_matrix(p.covariance(t, ql.Array(x), dt)))
                                          for t, x, dt in euler]
                evolves = [(x, dt, dw[:factors]) for x in STATES[:3] for dt in STEPS for dw in DRAWS]
                values = [v.attempt(lambda: list(p.evolve(0.0, ql.Array(x), dt, ql.Array(dw))))
                          for x, dt, dw in evolves]
                if scheme == "BroadieKayaExactSchemeLobatto":
                    # the price jumps among its solve's branches: only the variance, which no solve computes
                    expected["evolveVariance"] = [x if isinstance(x, dict) else x[1] for x in values]
                else:
                    if scheme.startswith("BroadieKaya"):
                        # the integrated variance's solve: S7R6's stability filter
                        keep = [i for i, (x, dt, dw) in enumerate(evolves)
                                if isinstance(values[i], dict) or stable_evolve(spec, x, dt, dw, values[i])]
                        print(f"{pname}, {cname} curves, {scheme}: dropped {len(evolves) - len(keep)} of "
                              f"{len(evolves)} outputs", file=sys.stderr)
                        evolves = [evolves[i] for i in keep]
                        values = [values[i] for i in keep]
                    expected["evolve"] = values
                out.append({"name": f"{pname}, {cname} curves, {scheme}",
                            "inputs": {"settings": st, "process": spec, "times": TIMES, "states": STATES,
                                       "euler": [[t, x, dt] for t, x, dt in euler],
                                       "evolve": [[x, dt, dw] for x, dt, dw in evolves]},
                            "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/hestonprocess.hpp", cases)
