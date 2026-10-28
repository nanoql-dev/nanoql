# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's hazard-rate and default-density bases' default survival probability
(ql/termstructures/credit/hazardratestructure.hpp, defaultdensitystructure.hpp), a 48-point Gauss-Chebyshev integral
which no QuantLib curve keeps, each output computed by the C++ harness (tools/cxx/src/termstructures/credit/
hazardratestructure.cpp) on harness subclasses: hazard rates a + b t and densities c e^(-c t) (Actual/365 Fixed), at
times from a day to forty years. Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/Credit/HazardRateStructureCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TODAY = 38960  # 31 August 2006
TIMES = [1.0 / 365, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0, 20.0, 40.0]
HAZARDS = [(0.02, 0.01), (0.0, 0.0), (0.05, 0.0), (0.001, 0.003), (0.3, -0.005)]
DENSITIES = [0.03, 0.0, 0.2, 1.5]


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        with H.case_settings(TODAY) as st:
            for a, b in HAZARDS:
                args = [TODAY, a, b, TIMES]
                out.append({"name": f"hazard {a} + {b} t", "inputs": {"settings": st, "kind": "hazard", "args": args},
                            "expected": {"survival": call("HazardRateStructure::survival", *args)}})
            for c in DENSITIES:
                args = [TODAY, c, TIMES]
                out.append({"name": f"density {c} e^(-{c} t)",
                            "inputs": {"settings": st, "kind": "density", "args": args},
                            "expected": {"survival": call("DefaultDensityStructure::survival", *args)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/credit/hazardratestructure.hpp", cases)
