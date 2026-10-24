# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ChebyshevInterpolation (ql/math/interpolations/chebyshevinterpolation.hpp, .cpp): the nodes
of both kinds for 1 to 20 points (a single node of the second kind is QuantLib's 0/0, NaN); interpolations of four
named functions on 3 to 25 nodes of each kind, built from the function and from its values at the nodes, each read at
its nodes, at points within a few machine epsilons of them, at 21 points across [-1, 1], and past the ends: the
value, with the extrapolation flag set outside the range except at the two farthest points, where QuantLib refuses to
extrapolate. Then the refusals of no node.

The functions, FUNCTIONS below, are written the same way in
tests/NanoQL.Tests/Vectors/Numerics/Interpolations/ChebyshevInterpolationVectorTests.cs, and the values built from
the function are computed in the generator at the nodes QuantLib gives. The wheel's calls, by QuantLib-SWIG 1.44's
interpolation.i: ChebyshevInterpolation(f, pointsType) on an Array of values and ChebyshevInterpolation(n, fct,
pointsType) on a Python function, the C++ constructors; nodes(n, pointsType), the static member; and the call
operator with its extrapolation flag. Its updateY, which the wheel doesn't expose, is nanoql's WithY
(tests/NanoQL.Tests/Conventions/ChebyshevInterpolationWithYTests.cs). Read back by
tests/NanoQL.Tests/Vectors/Numerics/Interpolations/ChebyshevInterpolationVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

EPS = 2.220446049250313e-16
C = ql.ChebyshevInterpolation
KINDS = [("first kind", C.FirstKind), ("second kind", C.SecondKind)]
FUNCTIONS = {
    "x x x - x": lambda x: x * x * x - x,
    "1/(1+25 x x)": lambda x: 1.0 / (1.0 + 25.0 * x * x),
    "sqrt(x+1)": lambda x: math.sqrt(x + 1.0),
    "|x|": abs,
}
SIZES = (3, 8, 25)


def sample(nodes):
    points = [(x, False) for x in nodes]
    points += [(x + k * EPS * max(abs(x), 1.0), False) for x in nodes[1:-1] for k in (-3, 3)]
    points += [(-1.0 + 0.1 * i, False) for i in range(21)]
    lo, hi = nodes[0], nodes[-1]
    points += [(lo - 0.05, True), (hi + 0.05, True), (lo - 1.0, False), (hi + 1.0, False)]
    return points


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for kname, kind in KINDS:
        out.append({"name": f"nodes, {kname}", "inputs": {"settings": st, "pointsType": int(kind)},
                    "expected": {"nodes": [[v.enc(x) for x in C.nodes(n, kind)] for n in range(1, 21)]}})
    for kname, kind in KINDS:
        for fname, f in FUNCTIONS.items():
            for n in SIZES:
                for built in ("function", "values"):
                    nodes = list(C.nodes(n, kind))
                    values = [f(x) for x in nodes]
                    interpolation = C(n, f, kind) if built == "function" else C(ql.Array(values), kind)
                    points = sample(nodes)
                    inputs = {"settings": st, "pointsType": int(kind), "points": [[x, flag] for x, flag in points]}
                    inputs.update({"function": fname, "n": n} if built == "function" else {"values": values})
                    out.append({"name": f"{fname}, {n} nodes, {kname}, from the {built}", "inputs": inputs,
                                "expected": {"value": [v.attempt(lambda: interpolation(x, flag))
                                                       for x, flag in points]}})
    for kname, kind in KINDS:
        out.append({"name": f"no node, {kname}", "inputs": {"settings": st, "pointsType": int(kind), "values": [],
                                                           "points": [[0.0, False]]},
                    "expected": {"value": [v.attempt(lambda: C(ql.Array([]), kind)(0.0))]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/chebyshevinterpolation.hpp", cases)
