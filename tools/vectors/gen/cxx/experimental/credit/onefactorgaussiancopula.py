# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's OneFactorGaussianCopula (ql/experimental/credit/onefactorgaussiancopula.hpp), which the
wheel doesn't expose, each output computed by the C++ harness (tools/cxx/src/experimental/credit/onefactorcopula.cpp):
the densities, the distributions of Z and Y and Y's inverse, the conditional probabilities, the integrals of a
probability, and the test integral of Y's distribution, at correlations from 0 to 1 (the comonotonic step among them)
and on the factor grid's default and given ends. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/OneFactorGaussianCopulaCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 38960  # 31 August 2006, the evaluation date, which no output reads
MS = [-4.0, -1.5, 0.0, 0.7, 3.2]
ZS = [-3.0, -0.5, 0.0, 1.2, 2.5]
YS = [-3.5, -1.0, 0.0, 0.4, 2.8]
PS = [0.001, 0.05, 0.3, 0.5, 0.92]
PROBABILITIES = [0.01, 0.05, 0.2, 0.6]
COPULAS = [
    {"kind": "Gaussian", "correlation": 0.0},
    {"kind": "Gaussian", "correlation": 0.3},
    {"kind": "Gaussian", "correlation": 0.75},
    {"kind": "Gaussian", "correlation": 0.9999},
    {"kind": "Gaussian", "correlation": 1.0},
    {"kind": "Gaussian", "correlation": 0.3, "maximum": 6.0, "integrationSteps": 120},
]


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        st = H.settings(R)
        for c in COPULAS:
            out.append({"name": f"Gaussian at {c['correlation']}" + (" on a given grid" if "maximum" in c else ""),
                        "inputs": {"settings": st, "copula": c, "ms": MS, "zs": ZS, "ys": YS, "ps": PS,
                                   "probabilities": PROBABILITIES},
                        "expected": {"members": call("OneFactorCopula::members", c, MS, ZS, YS, PS, PROBABILITIES)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/onefactorgaussiancopula.hpp", cases)
