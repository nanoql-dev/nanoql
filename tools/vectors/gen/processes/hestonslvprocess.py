# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's HestonSLVProcess (ql/processes/hestonslvprocess.hpp, .cpp), on the evaluation date 15
October 2026: the Heston stochastic-local volatility process, its spot's volatility the Heston variance's root scaled by
a leverage function and its variance's volatility by a mixing factor.

tools/vectors/gen/processes/hestonprocess.py's three parameter sets on flat curves and the first on zero curves, with
tools/vectors/gen/pricingengines/vanilla/fdhestonvanillaengine.py's constant and surface leverage functions, at mixing
factors 1 and 0.5 (the first set on flat curves only). For each: size, factors and initialValues; drift and diffusion
at times 0 and 0.5 on three states; and evolve from those times over steps of 0.05, 0.25 and 1 on the states with two
draws each (the quadratic-exponential variance's two branches among them).

Left out (the S-hes plan's §2.10): every structure is on the risk-free curve's day counter and reference date (on
others QuantLib reads the leverage at the risk-free curve's times, tests/NanoQL.Tests/Divergences/
HestonSlvMismatchedTimesTests.cs); expectation, stdDeviation and covariance, which dereference QuantLib's null
discretization (the process has none; nanoql refuses them, StochasticProcess's remarks).

The specs are a "heston" hestonprocess.py's spec, a "leverage" fdhestonvanillaengine.py's and a "mixingFactor". The
wheel's calls, by QuantLib-SWIG 1.44: HestonSLVProcess(hestonProcess, leverageFct, mixingFactor) and the
StochasticProcess members. Read back by tests/NanoQL.Tests/Vectors/Processes/HestonSLVProcessVectorTests.cs."""
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
hp, bsp = fhv.hp, fhv.bsp
R = fhv.R
TIMES = [0.0, 0.5]
STATES = [[100.0, 0.04], [80.0, 0.09], [120.0, 0.0004]]
STEPS = [0.05, 0.25, 1.0]
DRAWS = [[0.3, -1.2], [-2.0, 0.5]]
LEVERAGES = [("constant leverage", fhv.CONSTANT_LEVERAGE), ("surface leverage", fhv.SURFACE_LEVERAGE)]


def process(spec):
    return ql.HestonSLVProcess(hp.process(spec["heston"]), fhv.leverage(spec["leverage"]), spec["mixingFactor"])


def flat_matrix(m):
    return [m[i][j] for i in range(m.rows()) for j in range(m.columns())]


def specs():
    out = []
    sets = [(params[0] + ", flat curves", fhv.heston_spec(params)) for params in hp.PARAMETERS]
    sets.append((hp.PARAMETERS[0][0] + ", zero curves", fhv.heston_spec(hp.PARAMETERS[0], bsp.ZERO_R, bsp.ZERO_Q)))
    for sname, h in sets:
        for lname, lev in LEVERAGES:
            out.append((f"{sname}, {lname}", {"heston": h, "leverage": lev, "mixingFactor": 1.0}))
    for lname, lev in LEVERAGES:
        out.append((f"{sets[0][0]}, {lname}, mixing 0.5", {"heston": sets[0][1], "leverage": lev, "mixingFactor": 0.5}))
    return out


def cases():
    st = v.settings(R)
    out = []
    for name, spec in specs():
        p = process(spec)
        expected = {"size": p.size(), "factors": p.factors(), "initialValues": list(p.initialValues())}
        expected["drift"] = [v.attempt(lambda: list(p.drift(t, ql.Array(x)))) for t in TIMES for x in STATES]
        expected["diffusion"] = [v.attempt(lambda: flat_matrix(p.diffusion(t, ql.Array(x))))
                                 for t in TIMES for x in STATES]
        evolves = [(t, x, dt, dw) for t in TIMES for x in STATES for dt in STEPS for dw in DRAWS]
        expected["evolve"] = [v.attempt(lambda: list(p.evolve(t, ql.Array(x), dt, ql.Array(dw))))
                              for t, x, dt, dw in evolves]
        out.append({"name": name,
                    "inputs": {"settings": st, "process": spec, "times": TIMES, "states": STATES,
                               "evolve": [[t, x, dt, dw] for t, x, dt, dw in evolves]},
                    "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/hestonslvprocess.hpp", cases)
