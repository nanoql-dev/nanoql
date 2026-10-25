# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmSquareRootFwdOp (ql/methods/finitedifferences/operators/fdmsquarerootfwdop.hpp, .cpp):
the square-root process's forward (Fokker-Planck) operator with its zero-flow boundaries, on the evaluation date 15
October 2026.

Four parameter sets (kappa, theta, sigma): QuantLib's hestonslvmodel test's zero-flow set (1, 0.4, 0.8) and its
stationary-density sets at sigma 0.5 and 1.5 (kappa 2.5, theta 0.2), and its Dirac-start set (1.2, 0.4, 0.7); each
under the three transformations (plain, power, log) on a uniform 1-D mesher of 12 points (from 0.002 to 1.5, or from
log(0.001) to log(1.5) for the log transformation) and on an uneven predefined mesher of 9 points; and the first set
on a two-dimensional mesher with the variance in direction 1. For each and for (t1, t2) of (0, 0.25), (0.5, 0.75) and
(0.9, 1): the operator outputs tools/vectors/gen/methods/finitedifferences/operators/fdmblackscholesfwdop.py lists.

The specs are an operator {"mesher", "kappa", "theta", "sigma", "direction", "type"}, the mesher
fdmmeshercomposite.py's, the type QuantLib's TransformationType (0 plain, 1 power, 2 log). The wheel's calls, by
QuantLib-SWIG 1.44's fdm.i: FdmSquareRootFwdOp(mesher, kappa, theta, sigma, direction, type), and the operator
members fdmblackscholesfwdop.py's generator reads. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/FdmSquareRootFwdOpVectorTests.cs."""
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


fwd = _load("fdmblackscholesfwdop", "methods/finitedifferences/operators/fdmblackscholesfwdop.py")
mc = fwd.mc
R = fwd.bsm.R
PLAIN, POWER, LOG = int(ql.FdmSquareRootFwdOp.Plain), int(ql.FdmSquareRootFwdOp.Power), int(ql.FdmSquareRootFwdOp.Log)
TYPES = [("plain", PLAIN), ("power", POWER), ("log", LOG)]
PARAMETERS = [("zero flow", 1.0, 0.4, 0.8), ("stationary, sigma 0.5", 2.5, 0.2, 0.5),
              ("stationary, sigma 1.5", 2.5, 0.2, 1.5), ("Dirac start", 1.2, 0.4, 0.7)]
UNEVEN = [0.002, 0.01, 0.03, 0.07, 0.12, 0.2, 0.35, 0.6, 1.1]


def mesher_spec(transform, uneven=False):
    if uneven:
        x = [math.log(u) for u in UNEVEN] if transform == LOG else UNEVEN
        return {"type": "Predefined", "x": x}
    if transform == LOG:
        return {"type": "Uniform", "start": math.log(0.001), "end": math.log(1.5), "size": 12}
    return {"type": "Uniform", "start": 0.002, "end": 1.5, "size": 12}


def op_spec(mesher, kappa, theta, sigma, transform, direction=0):
    return {"mesher": mesher, "kappa": kappa, "theta": theta, "sigma": sigma, "direction": direction,
            "type": transform}


def specs():
    out = []
    for pname, kappa, theta, sigma in PARAMETERS:
        for tname, t in TYPES:
            out.append((f"{pname}, {tname}", op_spec(mc.composite_spec(mesher_spec(t)), kappa, theta, sigma, t)))
            out.append((f"{pname}, {tname}, uneven", op_spec(mc.composite_spec(mesher_spec(t, True)), kappa, theta,
                                                             sigma, t)))
    _, kappa, theta, sigma = PARAMETERS[0]
    out.append(("zero flow, plain, two dimensions",
                op_spec(mc.composite_spec(mc.U4, mesher_spec(PLAIN)), kappa, theta, sigma, PLAIN, direction=1)))
    return out


def cases():
    st = v.settings(R)
    rng = random.Random(20261025)
    out = []
    for name, spec in specs():
        mesher, _keep = mc.composite(spec["mesher"])
        op = ql.FdmSquareRootFwdOp(mesher, spec["kappa"], spec["theta"], spec["sigma"], spec["direction"],
                                   spec["type"])
        r = [rng.uniform(-1.0, 1.0) for _ in range(mesher.layout().size())]
        out.append({"name": name, "inputs": {"settings": st, "op": spec, "r": r,
                                             "times": [list(t) for t in fwd.TIMES], "s": fwd.S},
                    "expected": fwd.op_outputs(op, spec, r)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/fdmsquarerootfwdop.hpp", cases)
