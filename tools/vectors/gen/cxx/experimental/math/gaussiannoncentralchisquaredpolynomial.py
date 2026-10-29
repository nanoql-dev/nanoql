# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's non-central chi-squared polynomial
(ql/experimental/math/gaussiannoncentralchisquaredpolynomial.hpp, .cpp), from the C++ harness's multi-precision oracle
(tools/cxx/src/experimental/math/gaussiannoncentralchisquaredpolynomial.cpp): QuantLib's moment recursion template at
cpp_bin_float_50 on the cumulant moments in that type, its recurrence rounded once to double (the S-xrt plan's XRR8).
QuantLib's own double polynomial is wrong at its own CLV cases by up to 0.61 (Divergences/NonCentralChiSquared
PolynomialTests.cs), so it isn't the oracle.

- "values" cases: ν and λ of QuantLib's quadrature cases (4 and 1, 1 and 1), of its CLV cases (6 and 31.7, 9 and 31.7)
  and one at λ 75, where QuantLib's double rule refuses; their first n recurrence coefficients, and the n-point rule's
  nodes and weights.

Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Math/GaussNonCentralChiSquaredPolynomialCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

CASES = [(4.0, 1.0, 9), (1.0, 1.0, 14), (6.0, 31.7, 14), (9.0, 31.7, 14), (6.0, 31.7, 8), (9.0, 75.0, 14)]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(46311)
        for nu, lam, n in CASES:
            out.append({"name": f"nu {nu}, lambda {lam}, {n} nodes",
                        "inputs": {"settings": st, "nu": nu, "lambda": lam, "n": n},
                        "expected": H.call("GaussNonCentralChiSquaredPolynomial::values", nu, lam, n)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/math/gaussiannoncentralchisquaredpolynomial.hpp", cases)
