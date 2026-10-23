# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's NinePointLinearOp (ql/methods/finitedifferences/operators/ninepointlinearop.hpp, .cpp), on the
evaluation date 15 January 2025.

The wheel builds the operator alone, whose coefficients QuantLib leaves uninitialized
(tests/NanoQL.Tests/Conventions/NinePointLinearOpTests.cs), and wraps apply only. So the operator is read through the
two operators QuantLib builds on it: its apply through tools/vectors/gen/methods/finitedifferences/operators/
secondordermixedderivativeop.py's operator, which is its one derived class, and its mult through FdmG2Op's mixed term,
the mixed derivative multiplied by rho·sigma·eta at every point (fdmg2op.cpp), read by apply_mixed. On the two G2 models
of tools/vectors/gen/methods/finitedifferences/operators/fdmg2op.py and its three meshers, each operator applied to two
seeded vectors. Then the refusals of the constructor, "inconsistent derivative directions": both directions the same,
and a direction past the mesher's.

The specs are fdmg2op.py's. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: NinePointLinearOp(d0, d1, mesher),
SecondOrderMixedDerivativeOp(d0, d1, mesher) and FdmG2Op(mesher, model, d0, d1), the C++ constructors; apply and
apply_mixed the C++ members. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Operators/NinePointLinearOpVectorTests.cs."""
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


g2op = _load("fdmg2op", "methods/finitedifferences/operators/fdmg2op.py")
spm, mc = g2op.spm, g2op.mc


def cases():
    st = v.settings(g2op.TODAY)
    rng = random.Random(20261025)
    out = []
    for k, model in enumerate(g2op.G2S):
        m = g2op.make_model(model)
        for mname, mspec, (d0, d1) in g2op.meshers(model):
            mesher, keep = spm.composite(mspec)
            n = mesher.layout().size()
            rs = [g2op.seeded(rng, n), g2op.seeded(rng, n)]
            mixed = ql.SecondOrderMixedDerivativeOp(d0, d1, mesher)
            op = ql.FdmG2Op(mesher, m, d0, d1)
            op.setTime(0.0, 0.1)
            out.append({"name": f"model {k}, {mname} mesher",
                        "inputs": {"settings": st, "model": model, "mesher": mspec, "directions": [d0, d1], "r": rs},
                        "expected": {"apply": [g2op.arr(mixed.apply(ql.Array(r))) for r in rs],
                                     "mult": [g2op.arr(op.apply_mixed(ql.Array(r))) for r in rs]}})
    two = mc.composite_spec(mc.U5, mc.C6)
    for name, d0, d1 in (("both directions 0", 0, 0), ("a direction past the mesher's", 0, 2)):
        mesher, keep = spm.composite(two)
        out.append({"name": f"refused: {name}",
                    "inputs": {"settings": st, "mesher": two, "directions": [d0, d1]},
                    "expected": {"construct": v.attempt(lambda: ql.NinePointLinearOp(d0, d1, mesher))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/operators/ninepointlinearop.hpp", cases)
