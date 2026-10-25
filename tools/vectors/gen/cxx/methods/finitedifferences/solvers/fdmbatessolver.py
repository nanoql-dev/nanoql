# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmBatesSolver (ql/methods/finitedifferences/solvers/fdmbatessolver.hpp, .cpp), which the
wheel doesn't bind, each output computed by the C++ harness (docs/porting-playbook.md §9.4), on the evaluation date 15
October 2026: a rollback on the Bates operator, read at points of the spot and variance plane.

European and American puts at the money on two Bates processes (tools/vectors/gen/cxx/models/equity/batesmodel.py's),
on a mesher of 25 log-spots, uniform over the spot's logarithm plus and minus 1.5, and 15 variances from 0, closer
together near 0, with 20 time steps under the Hundsdorfer scheme and QuantLib's integration order 12; then on the first
process: Dirichlet boundaries on the log-spot's two sides, order 8, the Douglas scheme, two damping steps, a quanto
helper, and a call. Each case's outputs at nine points (the spot at 0.9, 1 and 1.1 times the process's, the variance at
0.8, 1 and 1.25 times v0): valueAt, deltaAt and gammaAt; and thetaAt at those of them where the theta doesn't cancel
("thetaPoints", tools/vectors/gen/methods/finitedifferences/solvers/fdm2dimsolver.py's rule: kept where the value is
at most 1e4 times the theta's change over its snapshot interval, 0.99/365 here).

Left out (the S-hes plan's §2.10): structures off the risk-free curve's reference date and day counter; every
structure here is on them.

A case holds the "process" (batesmodel.py's harness spec), "optionType", "strike", "exercise" (["European" |
"American", date]), "spots", "variances", "boundaries" ([[valueOnBoundary, direction, side]], side QuantLib's Upper 1
or Lower 2), "tGrid", "dampingSteps", "scheme" ([type, theta, mu]), "integrationOrder", "quanto" (the harness's spec or
null) and the "points" and "thetaPoints". The harness's call: FdmBatesSolver::results
(tools/cxx/src/methods/finitedifferences/solvers/fdmbatessolver.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/FiniteDifferences/Solvers/FdmBatesSolverCxxVectorTests.cs."""
import importlib.util
import math
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
fmv = _load("cxx_fdhestonvanillaengine", "cxx/pricingengines/vanilla/fdhestonvanillaengine.py")
R = bm.R
CALL, PUT = 1, -1
UPPER, LOWER = 1, 2
CANCELLATION = 1e4
SNAPSHOT = 0.99 / 365.0
OUTPUTS = ["valueAt", "thetaAt", "deltaAt", "gammaAt"]


def spots(s0, n=25):
    x0 = math.log(s0)
    return [x0 - 1.5 + 3.0 * i / (n - 1) for i in range(n)]


def variances(n=15, top=0.5):
    return [top * (i / (n - 1)) ** 2 for i in range(n)]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)

        def add(name, process, option_type=PUT, exercise=("European", R + 365), boundaries=(), damping=0,
                scheme=None, order=12, quanto=None):
            s0, v0 = process["s0"], process["v0"]
            points = [[a * s0, b * v0] for a in (0.9, 1.0, 1.1) for b in (0.8, 1.0, 1.25)]
            inputs = {"settings": st, "process": process, "optionType": option_type, "strike": s0,
                      "exercise": list(exercise), "spots": spots(s0), "variances": variances(),
                      "boundaries": [list(b) for b in boundaries], "tGrid": 20, "dampingSteps": damping,
                      "scheme": scheme or fmv.HUNDSDORFER, "integrationOrder": order, "quanto": quanto,
                      "points": points}
            r = H.call("FdmBatesSolver::results", process, option_type, s0, inputs["exercise"], inputs["spots"],
                       inputs["variances"], inputs["boundaries"], 20, damping, inputs["scheme"], order, quanto, points)
            kept = [i for i, (value, theta) in enumerate(zip(r["valueAt"], r["thetaAt"]))
                    if abs(value) <= CANCELLATION * abs(theta * SNAPSHOT)]
            inputs["thetaPoints"] = [points[i] for i in kept]
            expected = {o: [v.enc(x) for x in r[o]] for o in OUTPUTS}
            expected["thetaAt"] = [expected["thetaAt"][i] for i in kept]
            out.append({"name": name, "inputs": inputs, "expected": expected})

        for pname, process in bm.BATES[:2]:
            add(f"{pname}, European put", process)
            add(f"{pname}, American put", process, exercise=("American", R + 365))
        first = bm.BATES[0][1]
        add("Dirichlet boundaries, European put", first, boundaries=[(0.0, 0, UPPER), (60.0, 0, LOWER)])
        add("order 8, European put", first, order=8)
        add("Douglas, American put", first, exercise=("American", R + 365), scheme=fmv.DOUGLAS)
        add("two damping steps, European put", first, damping=2)
        add("quanto, European put", first, quanto=fmv.QUANTO)
        add("European call", first, option_type=CALL)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/solvers/fdmbatessolver.hpp", cases)
