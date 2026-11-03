# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmDupire1dOp (ql/experimental/finitedifferences/fdmdupire1dop.hpp, .cpp), the Dupire local
volatility operator 1/2 sigma(K)^2 d2/dK2: on three strike meshers (uniform, concentrating at a forward, ZABR's
fdPrice mesher's form) with a local volatility per point, at three (t1, t2), the size, apply, apply_direction,
solve_splitting at s = -0.3 and 0.7, and preconditioner at s = 0.4, on seeded vectors (seed 20261023).

Left out: apply_mixed, which QuantLib's returns as its input (FdmDupireApplyMixedTests). The inputs are
tools/vectors/gen/methods/finitedifferences/operators/fdmg2op.py's, its mesher spec fdmsimpleprocess1dmesher.py's
composite, and "localVolatility". The wheel's FdmDupire1dOp(mesher, localVolatility) is the C++ constructor. Read back
by tests/NanoQL.Tests/Vectors/Experimental/FiniteDifferences/FdmDupire1dOpVectorTests.cs."""
import importlib.util
import math
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


g2 = _load("fdmg2op", "methods/finitedifferences/operators/fdmg2op.py")
mc = g2.mc
spm = g2.spm
TODAY = ql.Date(15, ql.January, 2025)
MESHERS = [
    ("uniform", mc.composite_spec({"type": "Uniform", "start": 0.0, "end": 0.1, "size": 21})),
    ("concentrating at the forward", mc.composite_spec(
        {"type": "Concentrating", "start": 0.00001, "end": 0.12, "size": 40, "cPoint": [0.03, 0.1], "requireCPoint": True})),
    ("fdPrice's form, small", mc.composite_spec(
        {"type": "Concentrating", "start": 0.000005, "end": 0.10, "size": 60, "cPoint": [0.03, 0.1], "requireCPoint": True})),
]


def cases():
    st = v.settings(TODAY)
    rng = random.Random(20261023)
    out = []
    for name, mspec in MESHERS:
        mesher, keep = spm.composite(mspec)
        n = mesher.layout().size()
        locations = mesher.locations(0)
        local_vol = [0.2 + 0.05 * math.sin(10.0 * locations[i]) + 0.1 * locations[i] for i in range(n)]
        rs = [g2.seeded(rng, n), g2.seeded(rng, n)]
        op = ql.FdmDupire1dOp(mesher, ql.Array(local_vol))
        expected = g2.op_outputs(op, rs, 1)
        del expected["applyMixed"]
        out.append({"name": name,
                    "inputs": {"settings": st, "mesher": mspec, "localVolatility": v.enc(local_vol), "r": rs,
                               "times": g2.TIMES, "splitting": g2.SPLITTING, "preconditioner": g2.PRECONDITIONER},
                    "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/finitedifferences/fdmdupire1dop.hpp", cases)
