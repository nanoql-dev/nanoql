# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's VarianceGammaModel (ql/experimental/variancegamma/variancegammamodel.hpp, .cpp), which the
wheel doesn't expose, each output computed by the C++ harness: the model's sigma, nu and theta and its params on the
variance gamma generator's four processes (tools/vectors/gen/experimental/variancegamma/variancegammaengine.py).

No output is left out. The process and model are tools/cxx/src/experimental/variancegamma/variancegammamodel.cpp's.
Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/VarianceGamma/VarianceGammaModelCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


vge = _load("variancegammaengine", "experimental/variancegamma/variancegammaengine.py")
R = vge.R.serialNumber()
KEYS = ("s0", "dividendYield", "riskFreeRate", "sigma", "nu", "theta")


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for name, p in vge.PROCESSES:
            out.append({"name": name, "inputs": {"settings": st, "process": p},
                        "expected": H.call("VarianceGammaModel", *[p[k] for k in KEYS])})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/variancegamma/variancegammamodel.hpp", cases)
