# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmG2Op (ql/methods/finitedifferences/operators/fdmg2op.hpp, .cpp): the G2 model's
two-factor operator, its two directions' drift and diffusion, its correlation term and its short-rate discounting, on
the evaluation date 15 January 2025.

Two G2 models (a = 0.07, sigma = 0.011, b = 0.3, eta = 0.008, rho = -0.6; and a = 0.1, sigma = 0.01, b = 0.1,
eta = 0.01, rho = -0.75) on a flat 2.7% Actual/365 (Fixed) curve, on two meshers: the swaption engine's, each factor's
Ornstein-Uhlenbeck quantiles to 5 years (tools/vectors/gen/methods/finitedifferences/meshers/fdmsimpleprocess1dmesher.py's
spec, 15 and 12 points), and a uniform by concentrating one; and on a three-direction mesher, the model in its first and
third directions. For each, at three (t1, t2): size, then on seeded vectors apply, apply_mixed, apply_direction in each
direction, solve_splitting in each direction at s = -0.3 and 0.7, and preconditioner at s = 0.4.

The specs, which tools/vectors/gen/methods/finitedifferences/operators/fdmhullwhiteop.py shares, are a model {"type":
"G2" | "HullWhite", "curve" (tools/vectors/gen/instruments/fixedvsfloatingswap.py's curve spec), and its parameters},
a mesher fdmsimpleprocess1dmesher.py's composite, and the operator's directions. The wheel's calls, by QuantLib-SWIG
1.44's fdm.i: FdmG2Op(mesher, model, direction1, direction2), the C++ constructor; setTime, size, apply, apply_mixed,
apply_direction, solve_splitting and preconditioner the C++ members, the Arrays built from lists. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/FdmG2OpVectorTests.cs."""
import importlib.util
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


spm = _load("fdmsimpleprocess1dmesher", "methods/finitedifferences/meshers/fdmsimpleprocess1dmesher.py")
swp = _load("wheel_swaption", "instruments/swaption.py")
mc = spm.mc
irx = swp.irx
TODAY = swp.TODAY
DISCOUNT = swp.DISCOUNT
G2S = [{"type": "G2", "curve": DISCOUNT, "a": 0.07, "sigma": 0.011, "b": 0.3, "eta": 0.008, "rho": -0.6},
       {"type": "G2", "curve": DISCOUNT, "a": 0.1, "sigma": 0.01, "b": 0.1, "eta": 0.01, "rho": -0.75}]
HULL_WHITES = [{"type": "HullWhite", "curve": DISCOUNT, "a": 0.05, "sigma": 0.01},
               {"type": "HullWhite", "curve": DISCOUNT, "a": 0.2, "sigma": 0.015}]
TIMES = [[0.0, 0.1], [1.0, 1.25], [4.9, 5.0]]
SPLITTING = [-0.3, 0.7]
PRECONDITIONER = 0.4


def make_model(spec):
    curve = irx.handle(spec["curve"])
    if spec["type"] == "G2":
        return ql.G2(curve, spec["a"], spec["sigma"], spec["b"], spec["eta"], spec["rho"])
    return ql.HullWhite(curve, spec["a"], spec["sigma"])


def factor_meshers(model, sizes, maturity=5.0, eps=1e-5):
    """The engines' meshers: each factor's Ornstein-Uhlenbeck quantiles to the maturity, one averaging step."""
    if model["type"] == "G2":
        procs = [spm.ou_spec(model["a"], model["sigma"]), spm.ou_spec(model["b"], model["eta"])]
    else:
        procs = [spm.ou_spec(model["a"], model["sigma"])]
    return [spm.mesher_spec(n, p, maturity, 1, eps) for n, p in zip(sizes, procs)]


def arr(a):
    return v.enc([a[i] for i in range(len(a))])


def seeded(rng, n):
    return [rng.uniform(-1.0, 1.0) for _ in range(n)]


def op_outputs(op, rs, dims):
    out = {"size": op.size(), "apply": [], "applyMixed": [], "applyDirection": [], "solveSplitting": [],
           "preconditioner": []}
    for t1, t2 in TIMES:
        op.setTime(t1, t2)
        ra = [ql.Array(r) for r in rs]
        out["apply"].append([arr(op.apply(r)) for r in ra])
        out["applyMixed"].append([arr(op.apply_mixed(r)) for r in ra])
        out["applyDirection"].append([arr(op.apply_direction(d, ra[0])) for d in range(dims)])
        out["solveSplitting"].append([[arr(op.solve_splitting(d, ra[0], s)) for s in SPLITTING] for d in range(dims)])
        out["preconditioner"].append(arr(op.preconditioner(ra[0], PRECONDITIONER)))
    return out


def meshers(model):
    if model["type"] == "G2":
        engine = mc.composite_spec(*factor_meshers(model, (15, 12)))
        return [("engine's", engine, (0, 1)), ("uniform by concentrating", mc.composite_spec(mc.U5, mc.C6), (0, 1)),
                ("three directions, first and third", mc.composite_spec(mc.U4, mc.P7, mc.C3), (0, 2))]
    engine = mc.composite_spec(*factor_meshers(model, (25,)))
    return [("engine's", engine, (0,)), ("uniform", mc.composite_spec(mc.U5), (0,)),
            ("second of two directions", mc.composite_spec(mc.U4, mc.C6), (1,))]


def cases(models, seed):
    st = v.settings(TODAY)
    rng = random.Random(seed)
    out = []
    for k, model in enumerate(models):
        m = make_model(model)
        for mname, mspec, directions in meshers(model):
            mesher, keep = spm.composite(mspec)
            n = mesher.layout().size()
            dims = len(mspec["meshers"])
            rs = [seeded(rng, n), seeded(rng, n)]
            if model["type"] == "G2":
                op = ql.FdmG2Op(mesher, m, directions[0], directions[1])
            else:
                op = ql.FdmHullWhiteOp(mesher, m, directions[0])
            out.append({"name": f"model {k}, {mname} mesher",
                        "inputs": {"settings": st, "model": model, "mesher": mspec, "directions": list(directions),
                                   "r": rs, "times": TIMES, "splitting": SPLITTING,
                                   "preconditioner": PRECONDITIONER},
                        "expected": op_outputs(op, rs, dims)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/fdmg2op.hpp", lambda: cases(G2S, 20261022))
