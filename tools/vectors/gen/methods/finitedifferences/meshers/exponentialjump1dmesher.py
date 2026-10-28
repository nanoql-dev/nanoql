# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ExponentialJump1dMesher (ql/methods/finitedifferences/meshers/exponentialjump1dmesher.hpp,
.cpp): the locations, dplus and dminus of meshers of 3 to 50 points at the swing test's parameters and three others, at
the default eps and at 1e-2 and 1e-5 (the last dplus and the first dminus QuantLib's Null<Real>, written as null).

The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: ExponentialJump1dMesher(steps, beta, jumpIntensity, eta, eps), the C++
constructor; locations, dplus and dminus the C++ members. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Meshers/ExponentialJump1dMesherVectorTests.cs."""
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

PARAMETERS = [(4.0, 4.0, 5.0), (1.0, 0.5, 2.0), (10.0, 2.0, 1.0), (0.2, 6.0, 0.3)]
STEPS = [3, 10, 50]
EPS = [1e-3, 1e-2, 1e-5]


def cases():
    st = v.settings(bsm.R)
    out = []
    for beta, intensity, eta in PARAMETERS:
        for steps in STEPS:
            for eps in EPS:
                m = ql.ExponentialJump1dMesher(steps, beta, intensity, eta, eps)
                out.append({"name": f"beta {beta}, intensity {intensity}, eta {eta}, {steps} steps, eps {eps!r}",
                            "inputs": {"settings": st, "steps": steps, "beta": beta, "jumpIntensity": intensity,
                                       "eta": eta, "eps": eps},
                            "expected": bsm.outputs(m)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/meshers/exponentialjump1dmesher.hpp", cases)
