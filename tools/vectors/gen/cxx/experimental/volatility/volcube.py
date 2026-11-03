# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's VolatilityCube (ql/experimental/volatility/volcube.hpp, .cpp), which the wheel doesn't expose,
each output computed by the C++ harness: what the cube checks at construction, on the evaluation date Friday 15 May 2026,
its surfaces SABR surfaces on Euribor 6M over abcd curves and its curves abcd curves, each with its settlement days: two
and three surfaces on one reference date, with and without curves, the counts of its surfaces and curves; then
QuantLib's refusals: one surface, a surface on another reference date, and a curve on another.

No output is left out: the cube has no departure. Nothing is fitted, so no output is filtered.

The cube is tools/cxx/src/experimental/volatility/volcube.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Volatility/VolatilityCubeCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = ql.Date(15, ql.May, 2026).serialNumber()


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for name, surfaces, curves in [("two surfaces, no curve", [0, 0], []),
                                       ("three surfaces and two curves", [0, 0, 0], [0, 0]),
                                       ("two surfaces two days out and a curve", [2, 2], [2])]:
            spec = {"surfaceSettlementDays": surfaces, "curveSettlementDays": curves}
            out.append({"name": name, "inputs": {"settings": st, "cube": spec},
                        "expected": H.call("VolatilityCube::build", surfaces, curves)})
        for name, surfaces, curves in [("one surface (refused)", [0], []),
                                       ("a surface on another reference date (refused)", [0, 2], []),
                                       ("a curve on another reference date (refused)", [0, 0], [2])]:
            try:
                H.call("VolatilityCube::build", surfaces, curves)
                raise SystemExit(f"{name}: QuantLib built it")
            except RuntimeError:
                spec = {"surfaceSettlementDays": surfaces, "curveSettlementDays": curves}
                out.append({"name": name, "inputs": {"settings": st, "cube": spec},
                            "expected": {"surfaces": {"throws": True}}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/volatility/volcube.hpp", cases)
