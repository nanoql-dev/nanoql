# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmLinearOpLayout and FdmLinearOpIterator
(ql/methods/finitedifferences/operators/fdmlinearoplayout.hpp, .cpp, fdmlinearopiterator.hpp): layouts of one to three
dimensions, single points among them; for each: the dimensions, size and spacing; the iterator's walk from begin, its
index and coordinates at each step; the index of each point's coordinates; and at each point the neighbourhood index in
each direction for every offset of at most the direction's size less one (QuantLib reflects an offset past an edge back
inside, and an offset beyond that reads past the layout), the neighbourhood index in two directions for offset pairs,
and the index of iter_neighbourhood's iterator.

A layout is its list of dimensions. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: FdmLinearOpLayout(dim), the C++
constructor; dim, size, spacing, begin, index(coordinates), neighbourhood (both overloads) and iter_neighbourhood the
C++ members, the iterator's increment, index and coordinates its own. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/FdmLinearOpLayoutVectorTests.cs."""
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

LAYOUTS = [[1], [6], [1, 4], [3, 4], [2, 3, 4], [4, 5, 3]]
PAIRS = [(-1, 1), (1, -1), (2, 1), (-2, -1)]


def offsets(n):
    return [o for o in range(-(n - 1), n) if o != 0] or [0]


def outputs(dims):
    layout = ql.FdmLinearOpLayout(dims)
    out = {"dim": list(layout.dim()), "size": layout.size(), "spacing": list(layout.spacing()), "indices": [],
           "coordinates": [], "index": [], "neighbourhood": [], "neighbourhood2": [], "iterNeighbourhood": []}
    it = layout.begin()
    for _ in range(layout.size()):
        coords = list(it.coordinates())
        out["indices"].append(it.index())
        out["coordinates"].append(coords)
        out["index"].append(layout.index(coords))
        out["neighbourhood"].append([[layout.neighbourhood(it, d, o) for o in offsets(dims[d])]
                                     for d in range(len(dims))])
        out["iterNeighbourhood"].append([[layout.iter_neighbourhood(it, d, o).index() for o in offsets(dims[d])]
                                         for d in range(len(dims))])
        pairs = []
        for d1 in range(len(dims)):
            for d2 in range(len(dims)):
                if d1 != d2:
                    pairs.append([layout.neighbourhood(it, d1, o1, d2, o2) for o1, o2 in PAIRS
                                  if abs(o1) < dims[d1] and abs(o2) < dims[d2]])
        out["neighbourhood2"].append(pairs)
        it.increment()
    return out


def cases():
    st = v.settings(bsm.R)
    return [{"name": f"layout {dims}", "inputs": {"settings": st, "dim": dims, "offsets": [offsets(n) for n in dims],
                                                  "pairs": [list(p) for p in PAIRS]},
             "expected": outputs(dims)} for dims in LAYOUTS]


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/fdmlinearoplayout.hpp", cases)
