# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Fdm2dBlackScholesOp (ql/methods/finitedifferences/operators/fdm2dblackscholesop.hpp, .cpp),
on the evaluation date 15 October 2026: the two-dimensional Black-Scholes operator after setTime(t1, t2), on two
Black-Scholes meshers of 9 and 7 points, for flat curves and constant volatilities, zero curves with a surface, and the
local volatility (with and without an illegal-local-volatility overwrite), at correlations of 0.5 and -0.7.

For each and for (t1, t2) of (0, 0.25), (0.5, 0.75) and (0.9, 1): apply, apply_mixed (the correlation term and the
rate added back), apply_direction and solve_splitting at s = -0.01 in both directions, preconditioner at s = -0.01, each
on a seeded vector; apply_direction in a third direction, which QuantLib refuses ("direction is too large"); and
to_sparse_matrix, the sum of toMatrixDecomp, as its stored elements' rows, columns and values in storage order.
Every structure is on Act/365 (Fixed) from the evaluation date and the two processes share one risk-free curve: the
mixed term adds back the first process's rate where nanoql's adds back the second's
(tests/NanoQL.Tests/Divergences/Fd2dTwoRiskFreeCurvesTests.cs), and on structures dated or day-counted apart
QuantLib reads the second process's at the first's times (tests/NanoQL.Tests/Divergences/Fd2dMismatchedTimesTests.cs).

The specs are an operator {"mesher" (tools/vectors/gen/methods/finitedifferences/meshers/fdmmeshercomposite.py's),
"processes" (tools/vectors/gen/processes/blackscholesprocess.py's), "correlation", "maturity", "localVol",
"illegalLocalVolOverwrite"}. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: Fdm2dBlackScholesOp(mesher, p1, p2,
correlation, maturity, localVol, illegalLocalVolOverwrite), the C++ constructor; setTime, apply, apply_mixed,
apply_direction, solve_splitting and preconditioner the C++ members; to_sparse_matrix toMatrix. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/Fdm2dBlackScholesOpVectorTests.cs."""
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
fd2 = _load("fd2dblackscholesvanillaengine", "pricingengines/basket/fd2dblackscholesvanillaengine.py")
bsm = mc.bsm
bsp = bsm.bsp
TIMES = [(0.0, 0.25), (0.5, 0.75), (0.9, 1.0)]
S = -0.01


def op_spec(processes, correlation, local_vol=False, overwrite=None, maturity=1.0):
    mesher = mc.composite_spec(bsm.bs_spec(9, processes[0], maturity, processes[0]["x0"]),
                               bsm.bs_spec(7, processes[1], maturity, processes[1]["x0"]))
    return {"mesher": mesher, "processes": processes, "correlation": correlation, "maturity": maturity,
            "localVol": local_vol, "illegalLocalVolOverwrite": overwrite}


def op_of(spec):
    mesher, keep = mc.composite(spec["mesher"])
    p1, p2 = (bsp.process(p) for p in spec["processes"])
    overwrite = -bsm.NULL if spec["illegalLocalVolOverwrite"] is None else spec["illegalLocalVolOverwrite"]
    op = ql.Fdm2dBlackScholesOp(mesher, p1, p2, spec["correlation"], spec["maturity"], spec["localVol"], overwrite)
    return op, mesher, (keep, p1, p2)


def arr(a):
    return v.enc([a[i] for i in range(len(a))])


def outputs(spec, r):
    op, mesher, keep = op_of(spec)
    out = {k: [] for k in ("apply", "applyMixed", "applyDirection0", "applyDirection1", "solveSplitting0",
                           "solveSplitting1", "preconditioner", "matrixRows", "matrixColumns", "matrixValues")}
    ra = ql.Array(r)
    for t1, t2 in TIMES:
        op.setTime(t1, t2)
        out["apply"].append(arr(op.apply(ra)))
        out["applyMixed"].append(arr(op.apply_mixed(ra)))
        for d in (0, 1):
            out[f"applyDirection{d}"].append(arr(op.apply_direction(d, ra)))
            out[f"solveSplitting{d}"].append(arr(op.solve_splitting(d, ra, S)))
        out["preconditioner"].append(arr(op.preconditioner(ra, S)))
        m = op.to_sparse_matrix()
        out["matrixRows"].append([int(i) for i in m.row_idx])
        out["matrixColumns"].append([int(j) for j in m.col_idx])
        out["matrixValues"].append(v.enc(list(m.data)))
    out["applyDirection2"] = v.attempt(lambda: op.apply_direction(2, ra))
    return out


def cases():
    st = v.settings(bsm.R)
    rng = random.Random(20261025)
    setups = [("flat", op_spec(fd2.FLAT_PAIR, 0.5)),
              ("flat, negative correlation", op_spec(fd2.FLAT_PAIR, -0.7)),
              ("zero curves, surface", op_spec(fd2.ZERO_PAIR, 0.5)),
              ("local volatility", op_spec(fd2.SURFACE_PAIR, 0.5, local_vol=True)),
              ("local volatility, overwrite 0.2", op_spec(fd2.SURFACE_PAIR, -0.7, local_vol=True, overwrite=0.2))]
    out = []
    for name, spec in setups:
        r = [rng.uniform(-1.0, 1.0) for _ in range(63)]
        out.append({"name": name, "inputs": {"settings": st, "op": spec, "r": r, "times": [list(t) for t in TIMES],
                                             "s": S},
                    "expected": outputs(spec, r)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/fdm2dblackscholesop.hpp", cases)
