# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmWienerOp (ql/methods/finitedifferences/operators/fdmwienerop.hpp, .cpp), on the
evaluation date 15 October 2026: the operator of independent Wiener processes after setTime(t1, t2), on one-, two- and
three-dimensional meshers (uniform and predefined), with variance rates per direction, discounted on a flat curve, on
a zero curve, and undiscounted (no curve).

For each and for (t1, t2) of (0, 0.25), (0.5, 0.75) and (0.9, 1): apply, apply_mixed (zeros), apply_direction and
solve_splitting at s = -0.01 in each direction, preconditioner at s = -0.01, each on a seeded vector; and
to_sparse_matrix, the sum of toMatrixDecomp, as its stored elements; and a mesher of another dimension than the rates,
which QuantLib refuses. A direction past the operator's count isn't asked, where QuantLib reads past its operators
(tests/NanoQL.Tests/Divergences/FdmWienerOpDirectionTests.cs).

The specs are an operator {"mesher" (tools/vectors/gen/methods/finitedifferences/meshers/fdmmeshercomposite.py's),
"curve" (tools/vectors/gen/processes/blackscholesprocess.py's, or null), "lambdas"}. The wheel's calls, by
QuantLib-SWIG 1.44's fdm.i: FdmWienerOp(mesher, rTS, lambdas), the C++ constructor, None for the empty curve; setTime,
apply, apply_mixed, apply_direction, solve_splitting and preconditioner the C++ members; to_sparse_matrix toMatrix.
Read back by tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/FdmWienerOpVectorTests.cs."""
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


mc = _load("fdmmeshercomposite", "methods/finitedifferences/meshers/fdmmeshercomposite.py")
bsm = mc.bsm
bsp = bsm.bsp
TIMES = [(0.0, 0.25), (0.5, 0.75), (0.9, 1.0)]
S = -0.01


def uniform(start, end, size):
    return {"type": "Uniform", "start": start, "end": end, "size": size}


def op_of(spec):
    mesher, keep = mc.composite(spec["mesher"])
    curve = None if spec["curve"] is None else bsp.curve(spec["curve"])
    op = ql.FdmWienerOp(mesher, curve, ql.Array(spec["lambdas"]))
    return op, mesher, (keep, curve)


def arr(a):
    return v.enc([a[i] for i in range(len(a))])


def outputs(spec, r):
    op, mesher, keep = op_of(spec)
    n = len(spec["lambdas"])
    keys = ["apply", "applyMixed", "preconditioner", "matrixRows", "matrixColumns", "matrixValues"]
    keys += [f"applyDirection{d}" for d in range(n)] + [f"solveSplitting{d}" for d in range(n)]
    out = {k: [] for k in keys}
    ra = ql.Array(r)
    for t1, t2 in TIMES:
        op.setTime(t1, t2)
        out["apply"].append(arr(op.apply(ra)))
        out["applyMixed"].append(arr(op.apply_mixed(ra)))
        for d in range(n):
            out[f"applyDirection{d}"].append(arr(op.apply_direction(d, ra)))
            out[f"solveSplitting{d}"].append(arr(op.solve_splitting(d, ra, S)))
        out["preconditioner"].append(arr(op.preconditioner(ra, S)))
        m = op.to_sparse_matrix()
        out["matrixRows"].append([int(i) for i in m.row_idx])
        out["matrixColumns"].append([int(j) for j in m.col_idx])
        out["matrixValues"].append(v.enc(list(m.data)))
    return out


def cases():
    st = v.settings(bsm.R)
    rng = random.Random(20261025)
    one = mc.composite_spec(uniform(-1.0, 1.0, 11))
    two = mc.composite_spec(uniform(-1.0, 1.0, 9), {"type": "Predefined", "x": [-0.8, -0.5, -0.1, 0.0, 0.2, 0.6, 1.1]})
    three = mc.composite_spec(uniform(-1.0, 1.0, 5), uniform(-0.5, 0.5, 4), uniform(-0.3, 0.4, 6))
    setups = [("one dimension, flat curve", {"mesher": one, "curve": bsp.FLAT_R, "lambdas": [0.04]}),
              ("one dimension, no curve", {"mesher": one, "curve": None, "lambdas": [0.09]}),
              ("two dimensions, zero curve", {"mesher": two, "curve": bsp.ZERO_R, "lambdas": [0.05, 0.01]}),
              ("two dimensions, no curve", {"mesher": two, "curve": None, "lambdas": [0.2, 0.003]}),
              ("three dimensions, flat curve", {"mesher": three, "curve": bsp.FLAT_R, "lambdas": [0.07, 0.02, 0.004]})]
    out = []
    for name, spec in setups:
        mesher, _ = mc.composite(spec["mesher"])
        r = [rng.uniform(-1.0, 1.0) for _ in range(mesher.layout().size())]
        out.append({"name": name, "inputs": {"settings": st, "op": spec, "r": r, "times": [list(t) for t in TIMES],
                                             "s": S},
                    "expected": outputs(spec, r)})
    spec = {"mesher": two, "curve": bsp.FLAT_R, "lambdas": [0.04]}
    out.append({"name": "lambdas of another dimension (refused)", "inputs": {"settings": st, "op": spec},
                "expected": {"construction": v.attempt(lambda: op_of(spec) and 0.0)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/fdmwienerop.hpp", cases)
