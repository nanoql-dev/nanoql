# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's TripleBandLinearOp (ql/methods/finitedifferences/operators/triplebandlinearop.hpp, .cpp): its
arithmetic on the first and second derivative operators of four meshers (one, two and three dimensions; uniform,
concentrating and Black-Scholes meshers) in each direction. An operator is read through apply on a seeded vector, since
the wheel has no accessor for the bands (and a TripleBandLinearOp as constructed holds uninitialized bands, so it is
only read after axpyb has set all three).

For each mesher and direction, with x the first and y the second derivative, u and r seeded vectors of the layout's
size: x.mult(u), x.multR(u), y.add(x), x.add(u), and axpyb into a new operator with a and b full, a of one element and b
empty, a empty and b of one element, both empty, and a full and b empty, each applied to r; y.mult(u)'s
solve_splitting(r, a, b) at two (a, b); and the refusals of apply on a vector of another size ("inconsistent length of
r"), of multR on one ("inconsistent size of rhs"), of solve_splitting on one ("inconsistent size of rhs") and of
solve_splitting with a = b = 0 ("division by zero"). Bands of different sizes, which QuantLib reads past, are nanoql's
refusals (tests/NanoQL.Tests/Divergences/FdmUndefinedPathRefusalTests.cs).

The specs, which the derivative operators' generators share, are an operator {"kind": "First" | "Second", "direction",
"mesher"}, the mesher tools/vectors/gen/methods/finitedifferences/meshers/fdmmeshercomposite.py's composite. The wheel's
calls, by QuantLib-SWIG 1.44's fdm.i: TripleBandLinearOp(direction, mesher), FirstDerivativeOp and SecondDerivativeOp
the C++ constructors; apply, solve_splitting, mult, multR, add (both overloads) and axpyb the C++ members, the Arrays
built from lists. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/TripleBandLinearOpVectorTests.cs."""
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

MESHERS = [
    ("uniform 7", mc.composite_spec({"type": "Uniform", "start": -1.0, "end": 2.0, "size": 7})),
    ("Black-Scholes 9", mc.composite_spec(mc.BS)),
    ("concentrating by uniform", mc.composite_spec(mc.C6, mc.U4)),
    ("three directions", mc.composite_spec(mc.U4, mc.P7, mc.C3)),
]


def band_op(kind, direction, mesher):
    return (ql.FirstDerivativeOp if kind == "First" else ql.SecondDerivativeOp)(direction, mesher)


def seeded(rng, n):
    return [rng.uniform(-1.0, 1.0) for _ in range(n)]


def arr(a):
    return v.enc([a[i] for i in range(len(a))])


def cases():
    st = v.settings(bsm.R)
    rng = random.Random(20261020)
    out = []
    for name, mspec in MESHERS:
        mesher, keep = mc.composite(mspec)
        n = mesher.layout().size()
        for d in range(len(mspec["meshers"])):
            x, y = band_op("First", d, mesher), band_op("Second", d, mesher)
            u, r, a, b = seeded(rng, n), seeded(rng, n), seeded(rng, n), seeded(rng, n)
            ra, ua = ql.Array(r), ql.Array(u)
            expected = {"mult": arr(x.mult(ua).apply(ra)), "multR": arr(x.multR(ua).apply(ra)),
                        "addOp": arr(y.add(x).apply(ra)), "addArray": arr(x.add(ua).apply(ra)), "axpyb": []}
            for aa, bb in ((a, b), (a[:1], []), ([], b[:1]), ([], []), (a, [])):
                z = ql.TripleBandLinearOp(d, mesher)
                z.axpyb(ql.Array(aa), x, y, ql.Array(bb))
                expected["axpyb"].append(arr(z.apply(ra)))
            m = y.mult(ua)
            expected["solveSplitting"] = [arr(m.solve_splitting(ra, sa, sb)) for sa, sb in ((-0.3, 1.0), (0.7, 2.0))]
            short = ql.Array(r[:-1])
            expected["applyShort"] = v.attempt(lambda: arr(x.apply(short)))
            expected["multRShort"] = v.attempt(lambda: arr(x.multR(short).apply(ra)))
            expected["solveShort"] = v.attempt(lambda: arr(m.solve_splitting(short, 0.5, 1.0)))
            expected["solveZero"] = v.attempt(lambda: arr(m.solve_splitting(ra, 0.0, 0.0)))
            out.append({"name": f"{name}, direction {d}",
                        "inputs": {"settings": st, "mesher": mspec, "direction": d, "u": u, "r": r, "a": a, "b": b,
                                   "axpyb": [[n, n], [1, 0], [0, 1], [0, 0], [n, 0]],
                                   "splitting": [[-0.3, 1.0], [0.7, 2.0]]},
                        "expected": expected})
    return out


def derivative_cases(kind):
    """The derivative operator's cases: for each mesher and direction, apply on two seeded vectors and solve_splitting
    on the first at two (a, b)."""
    st = v.settings(bsm.R)
    rng = random.Random(20261021 if kind == "First" else 20261022)
    out = []
    for name, mspec in MESHERS:
        mesher, keep = mc.composite(mspec)
        n = mesher.layout().size()
        for d in range(len(mspec["meshers"])):
            op = band_op(kind, d, mesher)
            rs = [seeded(rng, n), seeded(rng, n)]
            out.append({"name": f"{name}, direction {d}",
                        "inputs": {"settings": st, "mesher": mspec, "direction": d, "r": rs,
                                   "splitting": [[-0.3, 1.0], [0.7, 2.0]]},
                        "expected": {"apply": [arr(op.apply(ql.Array(r))) for r in rs],
                                     "solveSplitting": [arr(op.solve_splitting(ql.Array(rs[0]), a, b))
                                                        for a, b in ((-0.3, 1.0), (0.7, 2.0))]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/triplebandlinearop.hpp", cases)
