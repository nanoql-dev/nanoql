# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's QdPlusAmericanEngine (ql/pricingengines/vanilla/qdplusamericanengine.hpp, .cpp) and the
put-call parity engine it derives from (qdplusamericanengine.hpp's QdPutCallParityEngine), on the evaluation date
15 October 2026: the NPVs of American calls and puts on plain vanilla payoffs, exercisable from the evaluation date,
over the Barone-Adesi-Whaley generator's grid (tools/vectors/gen/pricingengines/vanilla/baroneadesiwhaleyengine.py),
each priced by the engine's defaults (8 Chebyshev nodes, Halley's iteration, an accuracy of 1e-6) and by six other
settings, CONFIGS below: each solver type, more nodes at a tighter accuracy, and fewer nodes with Newton's solve
capped at ten evaluations, which refuses about one in six. Then an option whose window opened before the evaluation
date, an expired one, and the refusals of a European exercise, a payoff at expiry and a spot at zero.

The engine's exercise boundary members, putExerciseBoundaryAtTau, getPutExerciseBoundary and xMax, which the wheel
doesn't expose, are vectored through the C++ harness (tools/vectors/gen/cxx/pricingengines/vanilla/
qdplusamericanengine.py). Every structure is dated on the evaluation date and counts on Actual/365 (Fixed), and no
window opens after it, so neither the mismatched-dates class (the S-van plan's VR4 (ix),
tests/NanoQL.Tests/Divergences/QdMismatchedTimesTests.cs) nor the window (ApproximationWindowTests.cs) reaches the
file.

An engine spec is {"interpolationPoints", "solverType" (QuantLib's SolverType as an integer), "eps", "maxIter" (null
for QuantLib's default)}. The wheel's calls, by QuantLib-SWIG 1.44's options.i: QdPlusAmericanEngine(process,
interpolationPoints, solverType, eps, maxIter), the C++ constructor, VanillaOption(payoff, exercise) and its NPV.
Read back by tests/NanoQL.Tests/Vectors/PricingEngines/QdPlusAmericanEngineVectorTests.cs."""
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


GRID = _load("baroneadesiwhaleyengine", "pricingengines/vanilla/baroneadesiwhaleyengine.py")
E = ql.QdPlusAmericanEngine


def config(points=8, solver=E.Halley, eps=1e-6, max_iter=None):
    return {"interpolationPoints": points, "solverType": int(solver), "eps": eps, "maxIter": max_iter}


CONFIGS = [("defaults", config()), ("Brent", config(solver=E.Brent)), ("Newton", config(solver=E.Newton)),
           ("Ridder", config(solver=E.Ridder)), ("super-Halley", config(solver=E.SuperHalley)),
           ("16 nodes, 1e-10", config(points=16, eps=1e-10)),
           ("4 nodes, Newton capped at 10", config(points=4, solver=E.Newton, max_iter=10))]


def engine(spec):
    def make(p):
        if spec["maxIter"] is None:
            return ql.QdPlusAmericanEngine(p, spec["interpolationPoints"], spec["solverType"], spec["eps"])
        return ql.QdPlusAmericanEngine(p, spec["interpolationPoints"], spec["solverType"], spec["eps"],
                                       spec["maxIter"])
    return make


def with_engine(cases, name, spec):
    for c in cases:
        c["name"] = f"{name}: {c['name']}"
        c["inputs"]["engine"] = spec
    return cases


def cases():
    out = []
    for name, spec in CONFIGS:
        out += with_engine(GRID.grid_cases(engine(spec), lambda p, date, option_type, strike: True, []), name, spec)
    defaults = config()
    return out + with_engine(GRID.edge_cases(engine(defaults), [], []), "defaults", defaults)


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/qdplusamericanengine.hpp", cases)
