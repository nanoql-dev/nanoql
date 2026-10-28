# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmOrnsteinUhlenbeckOp (ql/methods/finitedifferences/operators/fdmornsteinuhlenbeckop.hpp,
.cpp), on the evaluation date 15 October 2026: the operator of an Ornstein-Uhlenbeck process after setTime(t1, t2), on
one- and two-dimensional meshers (uniform and predefined), in each direction, discounted on a flat and on a zero curve.

For each and for (t1, t2) of (0, 0.25), (0.5, 0.75) and (0.9, 1): apply, apply_mixed (zeros), apply_direction and
solve_splitting at s = -0.01 in each direction (zeros and the vector itself off the operator's direction),
preconditioner at s = -0.01, each on a seeded vector; to_sparse_matrix as its stored elements; and size, the mesher's
number of directions.

The specs are an operator {"mesher" (tools/vectors/gen/methods/finitedifferences/meshers/fdmmeshercomposite.py's),
"process" ({"speed", "vol", "x0", "level"}), "curve" (tools/vectors/gen/processes/blackscholesprocess.py's),
"direction"}. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: FdmOrnsteinUhlenbeckOp(mesher, process, rTS, direction),
the C++ constructor; OrnsteinUhlenbeckProcess(speed, vol, x0, level); setTime, apply, apply_mixed, apply_direction,
solve_splitting and preconditioner the C++ members; to_sparse_matrix toMatrix. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/FdmOrnsteinUhlenbeckOpVectorTests.cs."""
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


wiener = _load("fdmwienerop", "methods/finitedifferences/operators/fdmwienerop.py")
mc, bsm, bsp = wiener.mc, wiener.bsm, wiener.bsp
TIMES = wiener.TIMES
S = wiener.S
uniform, arr = wiener.uniform, wiener.arr


def op_of(spec):
    mesher, keep = mc.composite(spec["mesher"])
    curve = bsp.curve(spec["curve"])
    p = spec["process"]
    process = ql.OrnsteinUhlenbeckProcess(p["speed"], p["vol"], p["x0"], p["level"])
    op = ql.FdmOrnsteinUhlenbeckOp(mesher, process, curve, spec["direction"])
    return op, mesher, (keep, curve, process)


def outputs(spec, r):
    op, mesher, keep = op_of(spec)
    n = len(spec["mesher"]["meshers"])
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
    out["size"] = op.size()
    return out


def cases():
    st = v.settings(bsm.R)
    rng = random.Random(20261027)
    one = mc.composite_spec(uniform(-1.0, 1.5, 11))
    two = mc.composite_spec(uniform(-1.0, 1.0, 9), {"type": "Predefined", "x": [-0.8, -0.5, -0.1, 0.0, 0.2, 0.6, 1.1]})
    slow = {"speed": 0.5, "vol": 0.2, "x0": 0.1, "level": 0.05}
    fast = {"speed": 4.0, "vol": 0.6, "x0": -0.2, "level": 0.3}
    setups = [("one dimension, flat curve", {"mesher": one, "process": slow, "curve": bsp.FLAT_R, "direction": 0}),
              ("one dimension, zero curve", {"mesher": one, "process": fast, "curve": bsp.ZERO_R, "direction": 0}),
              ("two dimensions, direction 0", {"mesher": two, "process": slow, "curve": bsp.ZERO_R, "direction": 0}),
              ("two dimensions, direction 1", {"mesher": two, "process": fast, "curve": bsp.FLAT_R, "direction": 1})]
    out = []
    for name, spec in setups:
        mesher, _ = mc.composite(spec["mesher"])
        r = [rng.uniform(-1.0, 1.0) for _ in range(mesher.layout().size())]
        out.append({"name": name, "inputs": {"settings": st, "op": spec, "r": r, "times": [list(t) for t in TIMES],
                                             "s": S},
                    "expected": outputs(spec, r)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/fdmornsteinuhlenbeckop.hpp", cases)
