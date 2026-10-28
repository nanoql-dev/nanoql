# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's one-factor affine survival structure (ql/experimental/credit/onefactoraffinesurvival.hpp),
which the wheel doesn't expose, each output computed by the C++ harness (tools/cxx/src/experimental/credit/
affinehazardratecurve.cpp): on Vasicek and Cox-Ingersoll-Ross models of the default intensity, the structure's
survival probability, default density and hazard rate at times from a day to thirty years, and its conditional
survival probability from a prior time at a realized intensity. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/OneFactorAffineSurvivalStructureCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TODAY = 38960  # 31 August 2006
MODELS = [
    ("Vasicek r0 2%, b 3%", ["Vasicek", 0.02, 0.1, 0.03, 0.01]),
    ("Vasicek r0 1%, b -2%, high volatility", ["Vasicek", 0.01, 0.5, -0.02, 0.05]),
    ("CIR r0 2%", ["CIR", 0.02, 0.03, 0.5, 0.05]),
    ("CIR r0 8%, slow", ["CIR", 0.08, 0.04, 0.1, 0.08]),
]
TIMES = [1.0 / 365, 0.5, 1.0, 5.0, 10.0, 30.0]
CONDITIONALS = [[0.0, 1.0, 0.02], [1.0, 3.0, 0.025], [2.0, 2.0, 0.03], [5.0, 10.0, 0.0], [0.5, 7.0, 0.1]]


def flat(result):
    """The harness's object result, one output per key; a refused call's one output, "values"."""
    return dict(result) if isinstance(result, dict) and "throws" not in result else {"values": result}


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        with H.case_settings(TODAY) as st:
            for label, model in MODELS:
                args = [model, TODAY, TIMES, CONDITIONALS]
                out.append({"name": label, "inputs": {"settings": st, "args": args},
                            "expected": flat(call("OneFactorAffineSurvivalStructure::values", *args))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/onefactoraffinesurvival.hpp", cases)
