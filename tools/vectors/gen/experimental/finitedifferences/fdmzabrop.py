# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmZabrOp (ql/experimental/finitedifferences/fdmzabrop.hpp, .cpp), the ZABR operator on a
mesher of the forward and the volatility: three parameter sets (beta 0.7, nu 0.2, rho -0.3, gamma 1, SABR; beta 0.5,
nu 0.4, rho 0.2, gamma 0.7; beta 1, nu 0.3, rho 0, gamma 1.3) on two meshers (uniform by uniform, and concentrating
at the forward and the volatility, fullFdPrice's form). For each, at three (t1, t2): the size, apply, apply_mixed,
apply_direction in both directions, solve_splitting in both at s = -0.3 and 0.7, and preconditioner at s = 0.4, on
seeded vectors (seed 20261023).

The inputs are tools/vectors/gen/methods/finitedifferences/operators/fdmg2op.py's, its mesher spec
fdmsimpleprocess1dmesher.py's composite, and "beta", "nu", "rho", "gamma". The wheel's FdmZabrOp(mesher, beta, nu, rho,
gamma) is the C++ constructor. Read back by tests/NanoQL.Tests/Vectors/Experimental/FiniteDifferences/FdmZabrOpVectorTests.cs."""
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


g2 = _load("fdmg2op", "methods/finitedifferences/operators/fdmg2op.py")
mc = g2.mc
spm = g2.spm
TODAY = ql.Date(15, ql.January, 2025)
PARAMETERS = [(0.7, 0.2, -0.3, 1.0), (0.5, 0.4, 0.2, 0.7), (1.0, 0.3, 0.0, 1.3)]
MESHERS = [
    ("uniform by uniform", mc.composite_spec({"type": "Uniform", "start": 0.005, "end": 0.1, "size": 9},
                                            {"type": "Uniform", "start": 0.02, "end": 0.3, "size": 7})),
    ("concentrating", mc.composite_spec(
        {"type": "Concentrating", "start": 0.002, "end": 0.15, "size": 12, "cPoint": [0.03, 0.1], "requireCPoint": True},
        {"type": "Concentrating", "start": 0.03, "end": 0.25, "size": 10, "cPoint": [0.08, 0.1], "requireCPoint": True})),
]


def cases():
    st = v.settings(TODAY)
    rng = random.Random(20261023)
    out = []
    for k, (beta, nu, rho, gamma) in enumerate(PARAMETERS):
        for name, mspec in MESHERS:
            mesher, keep = spm.composite(mspec)
            n = mesher.layout().size()
            rs = [g2.seeded(rng, n), g2.seeded(rng, n)]
            op = ql.FdmZabrOp(mesher, beta, nu, rho, gamma)
            out.append({"name": f"parameters {k}, {name} mesher",
                        "inputs": {"settings": st, "mesher": mspec, "beta": beta, "nu": nu, "rho": rho, "gamma": gamma,
                                   "r": rs, "times": g2.TIMES, "splitting": g2.SPLITTING,
                                   "preconditioner": g2.PRECONDITIONER},
                        "expected": g2.op_outputs(op, rs, 2)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/finitedifferences/fdmzabrop.hpp", cases)
