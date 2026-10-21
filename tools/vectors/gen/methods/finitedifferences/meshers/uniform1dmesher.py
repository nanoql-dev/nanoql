# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Uniform1dMesher (ql/methods/finitedifferences/meshers/uniform1dmesher.hpp): the locations,
dplus and dminus of meshers of 2 to 101 points over positive, negative, log-spot and tiny intervals (the last dplus and
the first dminus QuantLib's Null<Real>, written as null), and the refusals of an end at or below the start ("end must be
larger than start"). Sizes 0 and 1, where QuantLib writes past an empty mesher's arrays and leaves a one-point mesher's
distances null, are nanoql's refusals (tests/NanoQL.Tests/Divergences/FdmUndefinedPathRefusalTests.cs).

A mesher is tools/vectors/gen/methods/finitedifferences/meshers/fdmblackscholesmesher.py's "Uniform" spec. The wheel's
calls, by QuantLib-SWIG 1.44's fdm.i: Uniform1dMesher(start, end, size), the C++ constructor; locations, dplus and
dminus the C++ members. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Meshers/Uniform1dMesherVectorTests.cs."""
import importlib.util
import math
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

INTERVALS = [(0.0, 1.0), (-1.0, 1.0), (-5.0, -2.0), (math.log(50.0), math.log(200.0)), (0.1, 0.3), (1e-8, 2e-8),
             (-123.456, 789.012)]
SIZES = [2, 3, 7, 10, 51, 101]


def spec(start, end, size):
    return {"type": "Uniform", "start": start, "end": end, "size": size}


def cases():
    st = v.settings(bsm.R)
    out = []
    for start, end in INTERVALS:
        for size in SIZES:
            s = spec(start, end, size)
            out.append({"name": f"[{start!r}, {end!r}], {size} points", "inputs": {"settings": st, "mesher": s},
                        "expected": bsm.outputs(bsm.mesher_1d(s))})
    for start, end in ((1.0, 1.0), (1.0, 0.5)):
        s = spec(start, end, 10)
        out.append({"name": f"refused [{start!r}, {end!r}]", "inputs": {"settings": st, "mesher": s},
                    "expected": {"mesher": v.attempt(lambda s=s: bsm.outputs(bsm.mesher_1d(s)))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/meshers/uniform1dmesher.hpp", cases)
