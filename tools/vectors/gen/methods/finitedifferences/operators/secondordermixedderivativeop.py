# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SecondOrderMixedDerivativeOp (ql/methods/finitedifferences/operators/
secondordermixedderivativeop.hpp, .cpp): the mixed second derivative's nine-point differences, with their one-sided
forms at the edges and corners, on the evaluation date 15 October 2026.

On tools/vectors/gen/methods/finitedifferences/operators/triplebandlinearop.py's two- and three-direction meshers and on
two Ornstein-Uhlenbeck meshers of tools/vectors/gen/methods/finitedifferences/meshers/fdmsimpleprocess1dmesher.py, in
each ordered pair of directions, the operator applied to two seeded vectors of the layout's size.

The specs are triplebandlinearop.py's and fdmsimpleprocess1dmesher.py's composites, with the pair of directions. The
wheel's calls, by QuantLib-SWIG 1.44's fdm.i: SecondOrderMixedDerivativeOp(d0, d1, mesher), the C++ constructor; apply
the C++ member, the Arrays built from lists. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/SecondOrderMixedDerivativeOpVectorTests.cs."""
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


spm = _load("fdmsimpleprocess1dmesher", "methods/finitedifferences/meshers/fdmsimpleprocess1dmesher.py")
tb = _load("triplebandlinearop", "methods/finitedifferences/operators/triplebandlinearop.py")
mc = spm.mc
R = spm.R

OU_MESHERS = mc.composite_spec(spm.mesher_spec(11, spm.ou_spec(0.1, 0.01), 5.0, 1, 1e-5),
                               spm.mesher_spec(9, spm.ou_spec(0.3, 0.008), 5.0, 1, 1e-5))
MESHERS = [(name, spec) for name, spec in tb.MESHERS if len(spec["meshers"]) > 1] + [("Ornstein-Uhlenbeck", OU_MESHERS)]


def pairs(dims):
    return [(d0, d1) for d0 in range(dims) for d1 in range(dims) if d0 != d1]


def cases():
    st = v.settings(R)
    rng = random.Random(20261024)
    out = []
    for name, mspec in MESHERS:
        mesher, keep = spm.composite(mspec)
        n = mesher.layout().size()
        for d0, d1 in pairs(len(mspec["meshers"])):
            op = ql.SecondOrderMixedDerivativeOp(d0, d1, mesher)
            rs = [tb.seeded(rng, n), tb.seeded(rng, n)]
            out.append({"name": f"{name}, directions {d0} and {d1}",
                        "inputs": {"settings": st, "mesher": mspec, "directions": [d0, d1], "r": rs},
                        "expected": {"apply": [tb.arr(op.apply(ql.Array(r))) for r in rs]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/secondordermixedderivativeop.hpp", cases)
