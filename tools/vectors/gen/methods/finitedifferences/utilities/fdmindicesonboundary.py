# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmIndicesOnBoundary (ql/methods/finitedifferences/utilities/fdmindicesonboundary.hpp,
.cpp): the indices of a layout's points on one side of one direction.

Layouts of one to four dimensions, each direction on its lower and upper side and on no side (no index). A direction
outside the layout, where QuantLib writes past its vector's end, is left out
(tests/NanoQL.Tests/Divergences/FdmBoundaryDirectionTests.cs).

A case's inputs are the layout's "dim", the "direction" and the "side" (QuantLib's FdmDirichletBoundary::Side: NoSide
0, Upper 1, Lower 2); its output the "indices". The wheel's calls, by QuantLib-SWIG 1.44's fdm.i:
FdmIndicesOnBoundary(layout, direction, side) and its getIndices(). Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Utilities/FdmIndicesOnBoundaryVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

LAYOUTS = [[5], [2], [5, 4], [3, 1], [3, 4, 2], [2, 3, 4, 2]]
SIDES = [("no side", int(ql.FdmDirichletBoundary.NoSide)), ("upper", int(ql.FdmDirichletBoundary.Upper)),
         ("lower", int(ql.FdmDirichletBoundary.Lower))]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for dim in LAYOUTS:
        layout = ql.FdmLinearOpLayout(dim)
        for direction in range(len(dim)):
            for sname, side in SIDES:
                indices = ql.FdmIndicesOnBoundary(layout, direction, side).getIndices()
                out.append({"name": f"layout {dim}, direction {direction}, {sname}",
                            "inputs": {"settings": st, "dim": dim, "direction": direction, "side": side},
                            "expected": {"indices": [int(i) for i in indices]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/fdmindicesonboundary.hpp", cases)
