# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Concentrating1dMesher (ql/methods/finitedifferences/meshers/concentrating1dmesher.hpp, .cpp):
the locations, dplus and dminus (the last dplus and the first dminus QuantLib's Null<Real>, written as null) of both
constructors.

The single-point constructor: no critical point (a uniform mesher), a density without a point, points inside the
interval at densities 0.01, 0.1 and 1, each with and without requireCPoint, points at the start and at the end, and
sizes 2 to 101; then its refusals, of an end at or below the start, a point outside the interval, a density at or below
zero, a point without a density and a required point not given. The constructor of several points, which solves an ODE
for the mesher and a Brent root for its scaling: one point, two points, three points, required and not, at tolerances
1e-8 and 1e-6; and its refusal of an end at or below the start.

A mesher is tools/vectors/gen/methods/finitedifferences/meshers/fdmblackscholesmesher.py's "Concentrating" or
"ConcentratingPoints" spec. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: the two C++ constructors, the point a
DoublePair (Null<Real>() for one not given), the points a vector of (point, density, required) tuples; locations, dplus
and dminus the C++ members. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Meshers/Concentrating1dMesherVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bsm = _load("fdmblackscholesmesher", "methods/finitedifferences/meshers/fdmblackscholesmesher.py")


def single(start, end, size, point, density, require=False):
    return {"type": "Concentrating", "start": start, "end": end, "size": size, "cPoint": [point, density],
            "requireCPoint": require}


def several(start, end, size, points, tol=1e-8):
    return {"type": "ConcentratingPoints", "start": start, "end": end, "size": size, "points": points, "tol": tol}


def answered():
    out = [("no critical point", single(-1.0, 2.0, 11, None, None)),
           ("a density without a point", single(-1.0, 2.0, 11, None, 0.1)),
           ("point at the start", single(0.0, 1.0, 21, 0.0, 0.1, True)),
           ("point at the end", single(0.0, 1.0, 21, 1.0, 0.1, True)),
           ("point at the start, not required", single(0.0, 1.0, 21, 0.0, 0.1))]
    for density in (0.01, 0.1, 1.0):
        for require in (False, True):
            for size in (2, 3, 10, 51, 101):
                out.append((f"point 4.6, density {density}, required {require}, {size} points",
                            single(3.0, 6.5, size, 4.6, density, require)))
    out += [("point near the start, required", single(0.0, 10.0, 15, 0.2, 0.05, True)),
            ("point near the end, required", single(0.0, 10.0, 15, 9.9, 0.05, True)),
            ("negative interval", single(-3.0, -1.0, 30, -2.5, 0.2, True))]
    for tol in (1e-8, 1e-6):
        out += [(f"one point, tol {tol}", several(0.0, 1.0, 21, [[0.5, 0.1, False]], tol)),
                (f"one point required, tol {tol}", several(0.0, 1.0, 21, [[0.33, 0.1, True]], tol)),
                (f"two points, tol {tol}", several(-1.0, 1.0, 41, [[-0.5, 0.05, False], [0.3, 0.1, True]], tol)),
                (f"three points, tol {tol}",
                 several(3.0, 6.0, 51, [[3.5, 0.02, True], [4.5, 0.1, False], [5.8, 0.05, True]], tol)),
                (f"two points, few nodes, tol {tol}", several(0.0, 2.0, 6, [[0.5, 0.2, True], [1.5, 0.2, True]], tol))]
    return out


REFUSED = [("end at the start", single(1.0, 1.0, 10, None, None)),
           ("end below the start", single(1.0, 0.0, 10, 0.5, 0.1)),
           ("point outside", single(0.0, 1.0, 10, 1.5, 0.1)),
           ("zero density", single(0.0, 1.0, 10, 0.5, 0.0)),
           ("negative density", single(0.0, 1.0, 10, 0.5, -0.1)),
           ("point without a density", single(0.0, 1.0, 10, 0.5, None)),
           ("required point not given", single(0.0, 1.0, 10, None, None, True)),
           ("several points, end below the start", several(1.0, 0.0, 10, [[0.5, 0.1, False]]))]


def cases():
    st = v.settings(bsm.R)
    out = []
    for name, s in answered():
        out.append({"name": name, "inputs": {"settings": st, "mesher": s}, "expected": bsm.outputs(bsm.mesher_1d(s))})
    for name, s in REFUSED:
        out.append({"name": "refused: " + name, "inputs": {"settings": st, "mesher": s},
                    "expected": {"mesher": v.attempt(lambda s=s: bsm.outputs(bsm.mesher_1d(s)))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/meshers/concentrating1dmesher.hpp", cases)
