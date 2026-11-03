# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's HybridHestonHullWhiteProcess (ql/processes/hybridhestonhullwhiteprocess.hpp, .cpp), which
the wheel doesn't bind, each output computed by the C++ harness (docs/porting-playbook.md §9.4), on the evaluation date
15 October 2026.

tools/vectors/gen/cxx/models/equity/batesmodel.py's two Heston processes, each with two forward Hull-White processes
on its risk-free curve (reversion 0.1, volatility 0.01, the forward measure at 10 years; 0.5, 0.02 and 5 years) at the
equity-rate correlations -0.4 and 0.3, under both discretizations: size, factors, initial values and eta; the drift and
diffusion at four states (the variance positive, small and negative) and three times; evolve from three states at two
times over the steps and draws below; and the numeraire at the states and times. Then the constructor's refusal of
correlations whose squares sum past 1.

Left out: steps whose a·dt falls below the step terms' cut, 0.1, where nanoql evaluates them in double-double
(tests/NanoQL.Tests/Divergences/HybridHestonHullWhiteStepTests.cs): the steps here are 0.25, 1 and 2 years, each kept
where a·dt reaches the cut; structures off the process's reference date and day counter; and a forward process on
another curve (tests/NanoQL.Tests/Divergences/HybridHestonHullWhiteCurveTests.cs).

A case holds the "process" (tools/cxx/src/processes/hybridspec.hpp's spec), the "times", "states", "evolve" ([t0, x0,
dt, dw]) and "numeraire" points. The harness's calls: HybridHestonHullWhiteProcess::summary, ::drift, ::diffusion,
::evolve and ::numeraire (tools/cxx/src/processes/hybridhestonhullwhiteprocess.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Processes/HybridHestonHullWhiteProcessCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bm = _load("cxx_batesmodel", "cxx/models/equity/batesmodel.py")
R = bm.R
EULER, BSM_HULL_WHITE = 0, 1
HULL_WHITE = [(0.1, 0.01, 10.0), (0.5, 0.02, 5.0)]
CORRELATIONS = [-0.4, 0.3]
CUT = 0.1
STATES = [[100.0, 0.04, 0.0], [80.0, 0.09, 0.02], [120.0, 0.0004, -0.01], [100.0, -0.01, 0.01]]
TIMES = [0.0, 0.5, 2.0]
STEPS = [0.25, 1.0, 2.0]
DRAWS = [[0.3, -1.2, 0.7], [-2.0, 0.5, -0.4], [0.0, 0.0, 0.0]]


def process_spec(heston, hw, correlation, discretization):
    a, sigma, measure = hw
    return {"heston": heston, "a": a, "sigma": sigma, "forwardMeasureTime": measure, "correlation": correlation,
            "discretization": discretization}


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for hname, *params in bm.HESTON:
            heston = bm.process_spec((hname, *params))
            for hw in HULL_WHITE:
                for corr in CORRELATIONS:
                    for dname, d in (("Euler", EULER), ("BSMHullWhite", BSM_HULL_WHITE)):
                        spec = process_spec(heston, hw, corr, d)
                        summary = H.call("HybridHestonHullWhiteProcess::summary", spec)
                        points = [(t, x) for t in TIMES for x in STATES]
                        evolves = [(t0, x, dt, dw) for t0 in (0.0, 1.0) for x in STATES[:3] for dt in STEPS
                                   if hw[0] * dt >= CUT for dw in DRAWS]
                        expected = {"size": summary["size"], "factors": summary["factors"],
                                    "initialValues": v.enc(summary["initialValues"]), "eta": summary["eta"],
                                    "drift": [v.attempt(lambda t=t, x=x: v.enc(H.call(
                                        "HybridHestonHullWhiteProcess::drift", spec, t, x))) for t, x in points],
                                    "diffusion": [v.attempt(lambda t=t, x=x: v.enc(H.call(
                                        "HybridHestonHullWhiteProcess::diffusion", spec, t, x))) for t, x in points],
                                    "evolve": [v.attempt(lambda e=e: v.enc(H.call(
                                        "HybridHestonHullWhiteProcess::evolve", spec, *e))) for e in evolves],
                                    "numeraire": [v.attempt(lambda t=t, x=x: v.enc(H.call(
                                        "HybridHestonHullWhiteProcess::numeraire", spec, t, x))) for t, x in points]}
                        out.append({"name": f"{hname}, a {hw[0]}, sigma {hw[1]}, correlation {corr}, {dname}",
                                    "inputs": {"settings": st, "process": spec, "times": TIMES, "states": STATES,
                                               "evolve": [[t0, x, dt, dw] for t0, x, dt, dw in evolves]},
                                    "expected": expected})
        spec = process_spec(bm.process_spec(bm.HESTON[0]), HULL_WHITE[0], 0.8, BSM_HULL_WHITE)
        out.append({"name": "correlations whose squares sum past 1",
                    "inputs": {"settings": st, "process": spec},
                    "expected": {"size": v.attempt(lambda: H.call("HybridHestonHullWhiteProcess::summary",
                                                                  spec)["size"])}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/hybridhestonhullwhiteprocess.hpp", cases)
