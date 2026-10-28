# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Boost.Math's beta family (boost/math/special_functions/beta.hpp, Boost 1.90) for double under the default
policy, as QuantLib's Student t quantile calls it, each output computed by the C++ harness: beta over parameters from
1e-300 to 1e10; ibeta, ibetac and ibeta_derivative over a grid of parameters from 0.1 to 1e6 and arguments from 1e-300
to 1 - 1e-12, which reaches each of ibeta_imp's routes (the arcsine and unit-parameter cases, the series, the a-step
and BGRAT sidesteps, the binomial sum and its underflowing start, the continued fraction, the large symmetric
expansion); the Student quantile's own calls, at half its integer degrees of freedom and 1/2; and the refused
arguments. Read back by tests/NanoQL.Tests/Vectors/Cxx/Boost/IncompleteBetaCxxVectorTests.cs through nanoql's
IncompleteBetaFunctions."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
NAN = float("nan")
INF = float("inf")

BETA_PARAMETERS = [1e-300, 1e-20, 1e-5, 0.1, 0.5, 0.9, 1.0, 1.5, 2.0, 5.0, 15.5, 20.0, 39.5, 40.0, 50.0, 100.0,
                   150.0, 1000.0, 1e5, 1e10]
PARAMETERS = [0.1, 0.25, 0.5, 0.9, 1.0, 1.5, 2.0, 3.5, 5.0, 10.0, 15.0, 15.5, 20.0, 39.0, 40.0, 50.0, 100.0, 1000.0,
              1e5, 1e6]
ARGUMENTS = [1e-300, 1e-20, 1e-8, 0.001, 0.01, 0.1, 0.25, 0.3, 0.35, 0.5, 0.65, 0.7, 0.9, 0.99, 0.999999, 1 - 1e-12]
# the binomial sum's underflowing first term, and the large symmetric expansion
SPECIAL = ([(600.0, 3.0, x) for x in (0.05, 0.1, 0.3, 0.9)] + [(400.0, 20.0, x) for x in (1e-3, 0.2, 0.95)]
           + [(1e11, 1e11, x) for x in (0.4999, 0.49999999, 0.5, 0.50000001, 0.5001)]
           + [(1e11, 1.5e11, x) for x in (0.39, 0.4, 0.41)]
           + [(a, b, x) for a in (0.0, 2.0) for b in (0.0, 3.0) for x in (0.0, 0.3, 1.0) if not (a == 0 and b == 0)])
STUDENT_DF = [3, 5, 7, 9, 10, 15, 19, 20, 30, 100, 1000, 10**6]
STUDENT_X = [1e-300, 1e-12, 1e-6, 0.01, 0.1, 0.3, 0.5, 0.7, 0.9, 0.999, 1 - 1e-9]
REFUSED_BETA = [(0.0, 1.0), (-1.0, 1.0), (1.0, 0.0), (NAN, 1.0)]
REFUSED_IBETA = [(0.0, 0.0, 0.5), (-1.0, 2.0, 0.5), (2.0, -1.0, 0.5), (2.0, 3.0, -0.1), (2.0, 3.0, 1.1),
                 (NAN, 2.0, 0.5), (2.0, NAN, 0.5), (2.0, 3.0, NAN), (INF, 2.0, 0.5), (2.0, INF, 0.5)]


def triples():
    out = [(a, b, x) for a in PARAMETERS for b in PARAMETERS for x in ARGUMENTS] + SPECIAL
    for df in STUDENT_DF:
        out += [(df / 2, 0.5, x) for x in STUDENT_X] + [(0.5, df / 2, x) for x in STUDENT_X]
    return out + REFUSED_IBETA


def cases():
    with cxx.connect() as H:
        st = H.settings(R)

        def two(method, pairs):
            return [v.attempt(lambda a=a, b=b: H.call(method, a, b)) for a, b in pairs]

        def three(method, ts):
            return [v.attempt(lambda a=a, b=b, x=x: H.call(method, a, b, x)) for a, b, x in ts]

        pairs = [(a, b) for a in BETA_PARAMETERS for b in BETA_PARAMETERS] + REFUSED_BETA
        abx = triples()
        return [
            {"name": "beta",
             "inputs": {"settings": st, "a": v.enc([a for a, _ in pairs]), "b": v.enc([b for _, b in pairs])},
             "expected": {"beta": two("beta::beta", pairs)}},
            {"name": "the incomplete beta",
             "inputs": {"settings": st, "a": v.enc([a for a, _, _ in abx]), "b": v.enc([b for _, b, _ in abx]),
                        "x": v.enc([x for _, _, x in abx])},
             "expected": {"ibeta": three("beta::ibeta", abx), "ibetac": three("beta::ibetac", abx),
                          "ibetaDerivative": three("beta::ibeta_derivative", abx)}},
        ]


if __name__ == "__main__":
    v.main(__file__, "boost/math/special_functions/beta.hpp", cases)
