# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's NthOrderDerivativeOp (ql/methods/finitedifferences/operators/nthorderderivativeop.hpp, .cpp),
on the evaluation date 15 October 2026: the operator's apply on a seeded vector, on one- and two-dimensional meshers
(uniform, concentrating and predefined), in each direction, for derivatives of order 0 to 3 on 2 to 7 points, even and
odd; and the inputs QuantLib refuses (fewer points than two or more than the direction's size, and no more points than
the order).

The meshers are tools/vectors/gen/methods/finitedifferences/meshers/fdmmeshercomposite.py's specs. The wheel's calls, by
QuantLib-SWIG 1.44's fdm.i: NthOrderDerivativeOp(direction, order, nPoints, mesher), the C++ constructor, and apply, the
C++ member. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/NthOrderDerivativeOpVectorTests.cs."""
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
GRID = [(order, points) for points in range(2, 8) for order in range(min(points, 4))]


def uniform(start, end, size):
    return {"type": "Uniform", "start": start, "end": end, "size": size}


def apply(spec, direction, order, points, r):
    mesher, keep = mc.composite(spec)
    op = ql.NthOrderDerivativeOp(direction, order, points, mesher)
    out = op.apply(ql.Array(r))
    return v.enc([out[i] for i in range(len(out))])


def cases():
    st = v.settings(bsm.R)
    rng = random.Random(20261027)
    one = mc.composite_spec(uniform(-1.0, 2.0, 12))
    conc = mc.composite_spec({"type": "Concentrating", "start": 0.0, "end": 1.0, "size": 15, "cPoint": [0.4, 0.1],
                              "requireCPoint": False})
    two = mc.composite_spec(uniform(-1.0, 1.0, 8), {"type": "Predefined", "x": [-0.8, -0.5, -0.1, 0.0, 0.2, 0.6, 1.1]})
    out = []
    for name, spec in (("uniform", one), ("concentrating", conc), ("two dimensions", two)):
        mesher, _ = mc.composite(spec)
        r = [rng.uniform(-1.0, 1.0) for _ in range(mesher.layout().size())]
        for direction in range(len(spec["meshers"])):
            size = list(mesher.layout().dim())[direction]
            for order, points in GRID:
                if points > size:
                    continue
                out.append({"name": f"{name}, direction {direction}, order {order}, {points} points",
                            "inputs": {"settings": st, "mesher": spec, "direction": direction, "order": order,
                                       "nPoints": points, "r": r},
                            "expected": {"apply": apply(spec, direction, order, points, r)}})
    mesher, _ = mc.composite(one)
    r = [rng.uniform(-1.0, 1.0) for _ in range(mesher.layout().size())]
    for order, points in ((0, 1), (1, 13), (2, 2), (3, 3)):
        out.append({"name": f"order {order}, {points} points (refused)",
                    "inputs": {"settings": st, "mesher": one, "direction": 0, "order": order, "nPoints": points,
                               "r": r},
                    "expected": {"apply": v.attempt(lambda o=order, p=points: apply(one, 0, o, p, r))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/nthorderderivativeop.hpp", cases)
