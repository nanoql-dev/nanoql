# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmMesherComposite (ql/methods/finitedifferences/meshers/fdmmeshercomposite.hpp, .cpp): a
composite of one to four 1-D meshers (uniform, concentrating and Black-Scholes ones), built from the meshers, which size
the layout, and from an explicit layout of the meshers' sizes; for each: the layout's dimensions and size, the locations
in each direction over the whole layout, the locations of each of getFdm1dMeshers' meshers, and at every layout point,
in each direction, the location, dplus and dminus (QuantLib's Null<Real> written as null). Then the refusal of a layout
whose size in a direction isn't its mesher's ("size of 1d mesher ... does not fit to layout"). More meshers than the
layout's dimensions, where QuantLib reads past the layout's sizes, are nanoql's refusal
(tests/NanoQL.Tests/Divergences/FdmUndefinedPathRefusalTests.cs).

A composite is {"meshers": [1-D mesher specs], "layout": the dimensions, or null to size it from the meshers}, a 1-D
spec tools/vectors/gen/methods/finitedifferences/meshers/fdmblackscholesmesher.py's. The wheel's calls, by QuantLib-SWIG
1.44's fdm.i: FdmMesherComposite(meshers) and FdmMesherComposite(layout, meshers), the C++ constructors; layout,
locations(direction), getFdm1dMeshers, location, dplus and dminus (at a layout iterator) the C++ members, the iterator
walked by the layout's begin and increment. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Meshers/FdmMesherCompositeVectorTests.cs."""
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


def composite_spec(*meshers, layout=None):
    return {"meshers": list(meshers), "layout": layout}


def composite(spec):
    """The wheel's composite mesher for a spec, and its 1-D meshers (to keep them alive)."""
    meshers = [bsm.mesher_1d(m) for m in spec["meshers"]]
    if spec["layout"] is not None:
        return ql.FdmMesherComposite(ql.FdmLinearOpLayout(spec["layout"]), meshers), meshers
    if len(meshers) == 1:
        return ql.FdmMesherComposite(meshers[0]), meshers
    return ql.FdmMesherComposite(meshers), meshers


def outputs(m):
    layout = m.layout()
    dims = list(layout.dim())
    out = {"dim": dims, "size": layout.size(), "locations": [], "location": [], "dplus": [], "dminus": [],
           "fdm1dMeshers": [v.enc(list(m1.locations())) for m1 in m.getFdm1dMeshers()]}
    for d in range(len(dims)):
        out["locations"].append(v.enc(list(m.locations(d))))
        loc, dp, dm = [], [], []
        it = layout.begin()
        for _ in range(layout.size()):
            loc.append(m.location(it, d))
            dp.append(m.dplus(it, d))
            dm.append(m.dminus(it, d))
            it.increment()
        out["location"].append(v.enc(loc))
        out["dplus"].append(v.enc(dp))
        out["dminus"].append(v.enc(dm))
    return out


U5 = {"type": "Uniform", "start": -1.0, "end": 1.0, "size": 5}
U4 = {"type": "Uniform", "start": 0.0, "end": 3.0, "size": 4}
C6 = {"type": "Concentrating", "start": 0.0, "end": 2.0, "size": 6, "cPoint": [0.5, 0.1], "requireCPoint": True}
C3 = {"type": "Concentrating", "start": 1.0, "end": 4.0, "size": 3, "cPoint": [None, None], "requireCPoint": False}
P7 = {"type": "ConcentratingPoints", "start": 0.0, "end": 1.0, "size": 7, "points": [[0.4, 0.1, True]], "tol": 1e-8}
BS = bsm.bs_spec(9, bsm.MERTON_FLAT, 1.0, 100.0)
BS_SURFACE = bsm.bs_spec(8, bsm.MERTON_ZERO_SURFACE, 2.0, 90.0, c_point=(90.0, 0.1))

COMPOSITES = [
    ("one uniform", composite_spec(U5)),
    ("one Black-Scholes", composite_spec(BS)),
    ("Black-Scholes by uniform", composite_spec(BS, U4)),
    ("concentrating by Black-Scholes", composite_spec(C6, BS_SURFACE)),
    ("three meshers", composite_spec(U5, C6, P7)),
    ("four meshers", composite_spec(U4, C3, U5, C3)),
    ("one uniform, explicit layout", composite_spec(U5, layout=[5])),
    ("two meshers, explicit layout", composite_spec(C6, U4, layout=[6, 4])),
    ("three meshers, explicit layout", composite_spec(U4, P7, C3, layout=[4, 7, 3])),
]
REFUSED = [("layout too large", composite_spec(U5, layout=[6])),
           ("second direction too small", composite_spec(U5, U4, layout=[5, 3]))]


def cases():
    st = v.settings(bsm.R)
    out = []
    for name, spec in COMPOSITES:
        m, _ = composite(spec)
        out.append({"name": name, "inputs": {"settings": st, "mesher": spec}, "expected": outputs(m)})
    for name, spec in REFUSED:
        out.append({"name": "refused: " + name, "inputs": {"settings": st, "mesher": spec},
                    "expected": {"mesher": v.attempt(lambda spec=spec: outputs(composite(spec)[0]))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/meshers/fdmmeshercomposite.hpp", cases)
