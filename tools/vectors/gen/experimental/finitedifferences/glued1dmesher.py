# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Glued1dMesher (ql/experimental/finitedifferences/glued1dmesher.hpp, .cpp), a mesher joining
two: two meshers meeting at a common point (uniform and uniform; two concentrating ones, ZABR's full finite-difference
forward mesher's glue), two with a gap between them, a one-point mesher glued on, and a left mesher ending past the
right one's start, refused as {"throws": true}. For each, the size, the locations, dplus and dminus, QuantLib's
Null<Real>() at the edges.

Each case's inputs "left" and "right" are tools/vectors/gen/methods/finitedifferences/meshers/fdmblackscholesmesher.py's
1-D mesher specs. The wheel's Glued1dMesher(left, right) is the C++ constructor. Read back by
tests/NanoQL.Tests/Vectors/Experimental/FiniteDifferences/Glued1dMesherVectorTests.cs."""
import importlib.util
import os
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


bsm = _load("fdmblackscholesmesher", "methods/finitedifferences/meshers/fdmblackscholesmesher.py")
TODAY = ql.Date(15, ql.January, 2025)


def uniform(start, end, size):
    return {"type": "Uniform", "start": start, "end": end, "size": size}


def concentrating(start, end, size, point, density):
    return {"type": "Concentrating", "start": start, "end": end, "size": size, "cPoint": [point, density],
            "requireCPoint": True}


PAIRS = [
    ("uniform meeting uniform", uniform(0.0, 1.0, 5), uniform(1.0, 3.0, 4)),
    ("ZABR's forward glue", concentrating(0.001, 0.035, 40, 0.03, 0.1), concentrating(0.035, 0.2, 61, 0.04, 0.1)),
    ("a gap between them", uniform(-1.0, 0.0, 3), concentrating(0.5, 2.0, 6, 1.0, 0.2)),
    ("a one-point mesher on the right", uniform(0.0, 2.0, 3), uniform(2.0, 3.0, 2)),
    ("refused: the left mesher ends past the right one's start", uniform(0.0, 2.0, 3), uniform(1.0, 3.0, 3)),
]


def arr(xs):
    return v.enc([xs[i] for i in range(len(xs))])


def case(name, left, right):
    with v.case_settings(TODAY) as st:
        inputs = {"settings": st, "left": left, "right": right}
        lm, rm = bsm.mesher_1d(left), bsm.mesher_1d(right)
        try:
            m = ql.Glued1dMesher(lm, rm)
        except RuntimeError:
            return {"name": name, "inputs": inputs, "expected": {"construct": dict(v.THROWS)}}
        n = m.size()
        return {"name": name, "inputs": inputs,
                "expected": {"size": n, "locations": arr(m.locations()),
                             "dplus": [v.enc(m.dplus(i)) for i in range(n)],
                             "dminus": [v.enc(m.dminus(i)) for i in range(n)]}}


def cases():
    return [case(*p) for p in PAIRS]


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/finitedifferences/glued1dmesher.hpp", cases)
