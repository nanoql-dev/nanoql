# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmBlackScholesFwdOp (ql/methods/finitedifferences/operators/fdmblackscholesfwdop.hpp, .cpp),
on the evaluation date 15 October 2026: the Black-Scholes forward (Fokker-Planck) operator in the log spot after
setTime(t1, t2), over six processes (flat and zero curves, constant and surface volatilities; the Merton, generalized,
Black-Scholes and Black processes, each on Act/365 throughout), with the Black forward variance and with the local
volatility (with and without an illegal-local-volatility overwrite), on a Black-Scholes mesher of 21 points, and on a
two-dimensional mesher with the underlying in direction 1.

For each and for (t1, t2) of (0, 0.25), (0.5, 0.75) and (0.9, 1): apply, apply_direction in the underlying's direction
(and in the other one, which returns zeros), apply_mixed (zeros), solve_splitting at s = -0.01 (in the other direction,
the vector itself) and preconditioner at s = -0.01, each on a seeded vector; and to_sparse_matrix's stored elements.
Every structure is on the risk-free curve's day counter and reference date: on others QuantLib reads them at the
risk-free curve's times, where nanoql reads them at their instants (the S-van plan's VR4 (xxiii);
tests/NanoQL.Tests/Divergences/LocalVolMismatchedTimesTests.cs).

The specs are tools/vectors/gen/methods/finitedifferences/operators/fdmblackscholesop.py's, without a quanto helper.
The wheel's calls, by QuantLib-SWIG 1.44: FdmBlackScholesFwdOp(mesher, process, strike, localVol,
illegalLocalVolOverwrite, direction), and the operator members FdmBlackScholesOp's generator reads. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/FdmBlackScholesFwdOpVectorTests.cs."""
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


bso = _load("fdmblackscholesop", "methods/finitedifferences/operators/fdmblackscholesop.py")
mc, bsm, bsp = bso.mc, bso.bsm, bso.bsp
TIMES, S = bso.TIMES, bso.S


def op_outputs(op, spec, r):
    d = spec["direction"]
    other = 1 - d if len(spec["mesher"]["meshers"]) == 2 else None
    out = {k: [] for k in ("apply", "applyDirection", "applyOther", "applyMixed", "solveSplitting", "solveOther",
                           "preconditioner", "matrixRows", "matrixColumns", "matrixValues")}
    ra = ql.Array(r)
    for t1, t2 in TIMES:
        op.setTime(t1, t2)
        out["apply"].append(bso.arr(op.apply(ra)))
        out["applyDirection"].append(bso.arr(op.apply_direction(d, ra)))
        out["applyMixed"].append(bso.arr(op.apply_mixed(ra)))
        out["solveSplitting"].append(bso.arr(op.solve_splitting(d, ra, S)))
        out["preconditioner"].append(bso.arr(op.preconditioner(ra, S)))
        m = op.to_sparse_matrix()
        out["matrixRows"].append([int(i) for i in m.row_idx])
        out["matrixColumns"].append([int(j) for j in m.col_idx])
        out["matrixValues"].append(v.enc(list(m.data)))
        if other is not None:
            out["applyOther"].append(bso.arr(op.apply_direction(other, ra)))
            out["solveOther"].append(bso.arr(op.solve_splitting(other, ra, S)))
    if other is None:
        del out["applyOther"], out["solveOther"]
    return out


def fwd_op(spec):
    mesher, keep = mc.composite(spec["mesher"])
    process = bsp.process(spec["process"])
    overwrite = -bsm.NULL if spec["illegalLocalVolOverwrite"] is None else spec["illegalLocalVolOverwrite"]
    op = ql.FdmBlackScholesFwdOp(mesher, process, spec["strike"], spec["localVol"], overwrite, spec["direction"])
    return op, mesher, (keep, process)


def specs():
    out = []
    for i in (0, 1, 3, 4, 5, 7):
        pname, p = bsp.PROCESSES[i]
        mesher = mc.composite_spec(bsm.bs_spec(21, p, 1.0, 100.0))
        out += [(f"{pname}", bso.op_spec(mesher, p)),
                (f"{pname}, local volatility", bso.op_spec(mesher, p, local_vol=True))]
        if i in (0, 3):
            out += [(f"{pname}, local volatility, overwrite 0.2",
                     bso.op_spec(mesher, p, local_vol=True, overwrite=0.2)),
                    (f"{pname}, strike 80", bso.op_spec(mesher, p, strike=80.0))]
    p = bsp.PROCESSES[0][1]
    two = mc.composite_spec(mc.U4, bsm.bs_spec(9, p, 1.0, 100.0))
    out += [("two dimensions", bso.op_spec(two, p, direction=1)),
            ("two dimensions, local volatility", bso.op_spec(two, p, local_vol=True, direction=1))]
    return out


def cases():
    st = v.settings(bsm.R)
    rng = random.Random(20261024)
    out = []
    for name, spec in specs():
        op, mesher, keep = fwd_op(spec)
        r = [rng.uniform(-1.0, 1.0) for _ in range(mesher.layout().size())]
        out.append({"name": name, "inputs": {"settings": st, "op": spec, "r": r, "times": [list(t) for t in TIMES],
                                             "s": S},
                    "expected": op_outputs(op, spec, r)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/fdmblackscholesfwdop.hpp", cases)
