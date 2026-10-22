# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmDirichletBoundary (ql/methods/finitedifferences/utilities/fdmdirichletboundary.hpp,
.cpp): the values a Dirichlet boundary writes.

On the composite meshers of tools/vectors/gen/methods/finitedifferences/meshers/fdmmeshercomposite.py, each direction
on its lower and upper side at boundary values 0.25 and -1.5: the array after applyAfterApplying and after
applyAfterSolving, and the scalar applyAfterApplying(x, value) at the direction's first and last locations, between
them and beyond each. A boundary on no side is QuantLib's refusal ("internal error"). A direction outside the layout,
where QuantLib reads past its vector's end, is left out (the S-rx plan's SRXR3 (xiv);
tests/NanoQL.Tests/Divergences/FdmBoundaryDirectionTests.cs).

A case's inputs are the "mesher" (that generator's composite spec), the "valueOnBoundary", the "direction", the "side"
(QuantLib's FdmDirichletBoundary::Side: NoSide 0, Upper 1, Lower 2), the "values" the arrays start from and the
"points" the scalar form reads, each an (x, value) pair. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i:
FdmDirichletBoundary(mesher, valueOnBoundary, direction, side), its applyAfterApplying(Array), which writes the array
in place, as applyAfterSolving does, and applyAfterApplying(x, value). Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Utilities/FdmDirichletBoundaryVectorTests.cs."""
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


mc = _load("fdmmeshercomposite", "methods/finitedifferences/meshers/fdmmeshercomposite.py")
SIDES = [("upper", int(ql.FdmDirichletBoundary.Upper)), ("lower", int(ql.FdmDirichletBoundary.Lower))]


def cases():
    st = v.settings(mc.bsm.R)
    out = []
    for mname, mspec in mc.COMPOSITES:
        mesher, _keep = mc.composite(mspec)
        size = mesher.layout().size()
        dims = list(mesher.layout().dim())
        values = [1.0 + 0.1 * i for i in range(size)]
        for direction in range(len(dims)):
            locations = list(mesher.getFdm1dMeshers()[direction].locations())
            first, last = locations[0], locations[-1]
            points = [[first - 0.1, 7.0], [first, 7.0], [0.5 * (first + last), 7.0], [last, 7.0], [last + 0.1, 7.0]]
            for sname, side in SIDES:
                for value in (0.25, -1.5):
                    b = ql.FdmDirichletBoundary(mesher, value, direction, side)
                    applied, solved = ql.Array(values), ql.Array(values)
                    b.applyAfterApplying(applied)
                    b.applyAfterSolving(solved)
                    out.append({"name": f"{mname}, direction {direction}, {sname}, {value:g}",
                                "inputs": {"settings": st, "mesher": mspec, "valueOnBoundary": value,
                                           "direction": direction, "side": side, "values": values, "points": points},
                                "expected": {"applyAfterApplying": v.enc(list(applied)),
                                             "applyAfterSolving": v.enc(list(solved)),
                                             "applyAfterApplyingAt": [b.applyAfterApplying(x, y) for x, y in points]}})
    mspec = mc.COMPOSITES[0][1]
    mesher, _keep = mc.composite(mspec)
    try:
        ql.FdmDirichletBoundary(mesher, 0.25, 0, ql.FdmDirichletBoundary.NoSide)
        refused = False
    except RuntimeError:
        refused = True
    assert refused
    out.append({"name": "no side", "inputs": {"settings": st, "mesher": mspec, "valueOnBoundary": 0.25,
                                              "direction": 0, "side": int(ql.FdmDirichletBoundary.NoSide),
                                              "values": [1.0] * 5, "points": []},
                "expected": {"build": dict(v.THROWS)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/fdmdirichletboundary.hpp", cases)
