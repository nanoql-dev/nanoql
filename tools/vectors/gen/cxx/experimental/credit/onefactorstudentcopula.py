# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's OneFactorStudentCopula, OneFactorGaussianStudentCopula and OneFactorStudentGaussianCopula
(ql/experimental/credit/onefactorstudentcopula.hpp), which the wheel doesn't expose, each output computed by the C++
harness (tools/cxx/src/experimental/credit/onefactorcopula.cpp): the densities, the distributions of Z and Y (Y's from
the table each constructor computes) and Y's inverse, the conditional probabilities and the integrals of a
probability, at degrees of freedom from 3 to 12 and correlations from 0 to 1. The densities and distributions are
S-core's Student t (its CR7 (xxii), (xxiii)), so the tables read the last bits of their fixes (the kickoff's Student
cdf class, within R7). The integrals of a probability are null: QuantLib's Student classes integrate the factor over
[-5, 10], losing its left tail, where nanoql integrates over [-maximum, maximum] (the S-xcr code review's C3 O1-4;
Divergences/OneFactorStudentCopulaRangeTests.cs). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/OneFactorStudentCopulaCxxVectorTests.cs."""
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
    {"kind": "StudentT", "nz": 5, "nm": 5, "correlation": 0.3},
    {"kind": "StudentT", "nz": 3, "nm": 12, "correlation": 0.6},
    {"kind": "StudentT", "nz": 7, "nm": 4, "correlation": 0.0},
    {"kind": "StudentT", "nz": 6, "nm": 6, "correlation": 1.0},
    {"kind": "GaussianStudent", "nz": 5, "correlation": 0.3},
    {"kind": "GaussianStudent", "nz": 9, "correlation": 0.8},
    {"kind": "StudentGaussian", "nm": 5, "correlation": 0.3},
    {"kind": "StudentGaussian", "nm": 4, "correlation": 0.55, "maximum": 8.0, "integrationSteps": 150},
]


def members(r):
    """The members but the integrals, which the factor's range changes (nulled)."""
    if isinstance(r, list):
        r[6] = None
    return r


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        st = H.settings(R)
        for c in COPULAS:
            dof = ", ".join(f"{k} {c[k]}" for k in ("nz", "nm") if k in c)
            out.append({"name": f"{c['kind']} ({dof}) at {c['correlation']}",
                        "inputs": {"settings": st, "copula": c, "ms": MS, "zs": ZS, "ys": YS, "ps": PS,
                                   "probabilities": PROBABILITIES},
                        "expected": {"members": members(call("OneFactorCopula::members", c, MS, ZS, YS, PS,
                                                             PROBABILITIES))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/onefactorstudentcopula.hpp", cases)
