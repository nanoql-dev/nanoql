# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmBlackScholesOp (ql/methods/finitedifferences/operators/fdmblackscholesop.hpp, .cpp), on the
evaluation date 15 October 2026: the Black-Scholes operator in the log spot after setTime(t1, t2), over six processes
(flat and zero curves, constant and surface volatilities; the Merton, generalized, Black-Scholes and Black processes,
each on Act/365 throughout), with the Black forward variance and with the local volatility (with and without an
illegal-local-volatility overwrite), with and without a quanto helper, on a Black-Scholes mesher of 21 points, and on a
two-dimensional mesher with the underlying in direction 1.

For each and for (t1, t2) of (0, 0.25), (0.5, 0.75) and (0.9, 1): apply, apply_direction in the underlying's direction
(and in the other one, which returns zeros), apply_mixed (zeros), solve_splitting at s = -0.01 (in the other direction,
the vector itself) and preconditioner at s = -0.01, each on a seeded vector. Structures on different day counters or
reference dates, which QuantLib reads at the risk-free curve's times, are nanoql's refusal
(tests/NanoQL.Tests/Divergences/FdmMismatchedTimesTests.cs).

The specs, which the solver and scheme generators share, are an operator {"mesher", "process", "strike", "localVol",
"illegalLocalVolOverwrite" (null for QuantLib's default, -Null<Real>()), "direction", "quanto"}, the mesher
tools/vectors/gen/methods/finitedifferences/meshers/fdmmeshercomposite.py's composite, the process
tools/vectors/gen/processes/blackscholesprocess.py's, the quanto helper fdmblackscholesmesher.py's. The wheel's calls,
by QuantLib-SWIG 1.44's fdm.i: FdmBlackScholesOp(mesher, process, strike, localVol, illegalLocalVolOverwrite, direction,
quantoHelper), the C++ constructor; setTime, apply, apply_direction, apply_mixed, solve_splitting and preconditioner the
C++ members, the Arrays built from lists. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/FdmBlackScholesOpVectorTests.cs."""
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


def op_spec(mesher, process, strike=100.0, local_vol=False, overwrite=None, direction=0, quanto=None):
    return {"mesher": mesher, "process": process, "strike": strike, "localVol": local_vol,
            "illegalLocalVolOverwrite": overwrite, "direction": direction, "quanto": quanto}


def bs_op(spec):
    """The wheel's operator for a spec, and the objects it needs alive."""
    mesher, keep = mc.composite(spec["mesher"])
    process = bsp.process(spec["process"])
    helper = bsm.quanto(spec["quanto"])
    overwrite = -bsm.NULL if spec["illegalLocalVolOverwrite"] is None else spec["illegalLocalVolOverwrite"]
    if helper is None:
        op = ql.FdmBlackScholesOp(mesher, process, spec["strike"], spec["localVol"], overwrite, spec["direction"])
    else:
        op = ql.FdmBlackScholesOp(mesher, process, spec["strike"], spec["localVol"], overwrite, spec["direction"],
                                  helper)
    return op, mesher, (keep, process, helper)


def arr(a):
    return v.enc([a[i] for i in range(len(a))])


def outputs(spec, r):
    op, mesher, keep = bs_op(spec)
    d = spec["direction"]
    other = 1 - d if len(spec["mesher"]["meshers"]) == 2 else None
    out = {k: [] for k in ("apply", "applyDirection", "applyOther", "applyMixed", "solveSplitting", "solveOther",
                           "preconditioner")}
    ra = ql.Array(r)
    for t1, t2 in TIMES:
        op.setTime(t1, t2)
        out["apply"].append(arr(op.apply(ra)))
        out["applyDirection"].append(arr(op.apply_direction(d, ra)))
        out["applyMixed"].append(arr(op.apply_mixed(ra)))
        out["solveSplitting"].append(arr(op.solve_splitting(d, ra, S)))
        out["preconditioner"].append(arr(op.preconditioner(ra, S)))
        if other is not None:
            out["applyOther"].append(arr(op.apply_direction(other, ra)))
            out["solveOther"].append(arr(op.solve_splitting(other, ra, S)))
    if other is None:
        del out["applyOther"], out["solveOther"]
    return out


def specs():
    out = []
    for i in (0, 1, 3, 4, 5, 7):
        pname, p = bsp.PROCESSES[i]
        mesher = mc.composite_spec(bsm.bs_spec(21, p, 1.0, 100.0))
        out += [(f"{pname}", op_spec(mesher, p)), (f"{pname}, local volatility", op_spec(mesher, p, local_vol=True))]
        if i in (0, 3):
            out += [(f"{pname}, local volatility, overwrite 0.2", op_spec(mesher, p, local_vol=True, overwrite=0.2)),
                    (f"{pname}, quanto", op_spec(mesher, p, quanto=bsm.FLAT_QUANTO)),
                    (f"{pname}, quanto, local volatility",
                     op_spec(mesher, p, local_vol=True, quanto=bsm.FLAT_QUANTO)),
                    (f"{pname}, strike 80", op_spec(mesher, p, strike=80.0))]
    p = bsp.PROCESSES[0][1]
    two = mc.composite_spec(mc.U4, bsm.bs_spec(9, p, 1.0, 100.0))
    out += [("two dimensions", op_spec(two, p, direction=1)),
            ("two dimensions, local volatility", op_spec(two, p, local_vol=True, direction=1))]
    return out


def cases():
    st = v.settings(bsm.R)
    rng = random.Random(20261023)
    out = []
    for name, spec in specs():
        mesher, _ = mc.composite(spec["mesher"])
        r = [rng.uniform(-1.0, 1.0) for _ in range(mesher.layout().size())]
        out.append({"name": name, "inputs": {"settings": st, "op": spec, "r": r, "times": [list(t) for t in TIMES],
                                             "s": S},
                    "expected": outputs(spec, r)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/fdmblackscholesop.hpp", cases)
