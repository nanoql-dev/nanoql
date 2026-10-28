# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's OneFactorCopula (ql/experimental/credit/onefactorcopula.hpp), which the wheel doesn't
expose, each output computed by the C++ harness (tools/cxx/src/experimental/credit/onefactorcopula.cpp) on its four
forms: the moments' check at two tolerances (its result, or its refusal), the loss distribution of a bucketing
integrated over the factor, and the integral of a function of the conditional probabilities (their sum). On the
Student forms all three are null: QuantLib's Student classes integrate the factor over [-5, 10], where nanoql
integrates over [-maximum, maximum] (the S-xcr code review's C3 O1-4; Divergences/OneFactorStudentCopulaRangeTests.cs).
Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/OneFactorCopulaCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 38960  # 31 August 2006, the evaluation date, which no output reads
COPULAS = [
    {"kind": "Gaussian", "correlation": 0.3},
    {"kind": "Gaussian", "correlation": 0.9},
    {"kind": "StudentT", "nz": 5, "nm": 5, "correlation": 0.3},
    {"kind": "GaussianStudent", "nz": 6, "correlation": 0.4},
    {"kind": "StudentGaussian", "nm": 6, "correlation": 0.4},
]
TOLERANCES = [1e-2, 1e-6]
INTEGRALS = [(20, 10.0, [1.0, 2.5, 0.7, 3.2], [0.1, 0.2, 0.05, 0.3]),
             (40, 6.0, [0.6, 0.6, 1.2, 1.8, 0.3, 2.4], [0.02, 0.05, 0.01, 0.03, 0.04, 0.02])]


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        st = H.settings(R)
        for c in COPULAS:
            if c["kind"] != "Gaussian":
                out.append({"name": f"{c['kind']} at {c['correlation']}",
                            "inputs": {"settings": st, "copula": c, "tolerances": TOLERANCES,
                                       "integrals": [list(i) for i in INTEGRALS]},
                            "expected": {"checkMoments": [None] * len(TOLERANCES), "integral": [None] * len(INTEGRALS),
                                         "integralOfSum": [None] * len(INTEGRALS)}})
                continue
            out.append({"name": f"{c['kind']} at {c['correlation']}",
                        "inputs": {"settings": st, "copula": c, "tolerances": TOLERANCES,
                                   "integrals": [list(i) for i in INTEGRALS]},
                        "expected": {
                            "checkMoments": [call("OneFactorCopula::checkMoments", c, t) for t in TOLERANCES],
                            "integral": [call("OneFactorCopula::integral", c, *i) for i in INTEGRALS],
                            "integralOfSum": [call("OneFactorCopula::integralOfSum", c, i[3]) for i in INTEGRALS]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/onefactorcopula.hpp", cases)
