# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AbcdMathFunction (ql/math/abcdmathfunction.hpp, .cpp), f(t) = (a + b t) e^(-c t) + d: on
nine coefficient sets (QuantLib's defaults, AbcdFunction's, b positive and negative with the stationary point before
and after 0, b zero with a positive and negative, d zero), the value, derivative and primitive at nine times (a
negative one among them, where QuantLib returns 0), definite integrals over four intervals, the coefficients, the
derivative's, the rolling-window definite integral's and derivative's coefficients over three windows, the long-term
value; and validate's four refusals, each as {"throws": true}.

Left out where 1.44 and nanoql differ, each a documented divergence (AbcdMaximumTests): maximumLocation for b < 0
(QuantLib's master's #2841, the S-g1d plan's G1R5 (iii)), and maximumValue wherever 1.44's returns d for b = 0 or
a <= 0 or reads 1.44's location (G1R6 (ix)); each kept where the two agree. Each case's input "abcd" is the four
coefficients. Read back by tests/NanoQL.Tests/Vectors/Numerics/AbcdMathFunctionVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

TODAY = ql.Date(15, ql.January, 2025)
TIMES = [-1.0, 0.0, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0, 30.0]
INTERVALS = [[0.0, 1.0], [0.5, 2.0], [1.0, 10.0], [2.0, 30.0]]
WINDOWS = [[0.0, 1.0], [1.0, 3.0], [5.0, 5.5]]
SETS = [
    ("QuantLib's defaults", [0.002, 0.001, 0.16, 0.0005]),
    ("AbcdFunction's defaults", [-0.06, 0.17, 0.54, 0.17]),
    ("b positive, maximum after 0", [0.1, 0.4, 0.8, 0.05]),
    ("b positive, maximum before 0", [0.5, 0.05, 0.3, 0.1]),
    ("b negative, minimum after 0, a positive", [0.1, -0.02, 0.3, 0.15]),
    ("b negative, minimum before 0", [-0.05, -0.01, 0.5, 0.2]),
    ("b zero, a positive", [0.1, 0.0, 0.4, 0.05]),
    ("b zero, a negative", [-0.04, 0.0, 0.4, 0.05]),
    ("d zero", [0.2, 0.1, 1.2, 0.0]),
]
REFUSALS = [
    ("refused: c not positive", [0.1, 0.2, -0.3, 0.4]),
    ("refused: d negative", [0.1, 0.2, 0.3, -0.4]),
    ("refused: a + d negative", [-0.5, 0.2, 0.3, 0.4]),
    ("refused: negative at the stationary point", [0.1, -2.0, 0.5, 0.01]),
]


def master_maximum_location(a, b, c):
    # QuantLib's master's maximumLocation (#2841)
    if b == 0.0:
        return 0.0 if a >= 0.0 else sys.float_info.max
    t_star = 1.0 / c - a / b
    if b > 0.0:
        return t_star if t_star > 0.0 else 0.0
    if t_star <= 0.0:
        return sys.float_info.max
    return 0.0 if a >= 0.0 else sys.float_info.max


def fixed_maximum_value(f, a, b, c, d):
    loc = master_maximum_location(a, b, c)
    return d if loc == sys.float_info.max else f(loc)


def case(name, abcd):
    with v.case_settings(TODAY) as st:
        inputs = {"settings": st, "abcd": abcd, "times": TIMES, "intervals": INTERVALS, "windows": WINDOWS}
        try:
            f = ql.AbcdMathFunction(*abcd)
        except RuntimeError:
            return {"name": name, "inputs": inputs, "expected": {"construct": dict(v.THROWS)}}
        a, b, c, d = abcd
        expected = {
            "value": [v.enc(f(t)) for t in TIMES],
            "derivative": [v.enc(f.derivative(t)) for t in TIMES],
            "primitive": [v.enc(f.primitive(t)) for t in TIMES],
            "definiteIntegral": [v.enc(f.definiteIntegral(t1, t2)) for t1, t2 in INTERVALS],
            "coefficients": v.enc(list(f.coefficients())),
            "derivativeCoefficients": v.enc(list(f.derivativeCoefficients())),
            "definiteIntegralCoefficients": [v.enc(list(f.definiteIntegralCoefficients(t, t2))) for t, t2 in WINDOWS],
            "definiteDerivativeCoefficients": [v.enc(list(f.definiteDerivativeCoefficients(t, t2))) for t, t2 in WINDOWS],
            "longTermValue": v.enc(f.longTermValue()),
        }
        if f.maximumLocation() == master_maximum_location(a, b, c):
            expected["maximumLocation"] = v.enc(f.maximumLocation())
        if f.maximumValue() == fixed_maximum_value(f, a, b, c, d):
            expected["maximumValue"] = v.enc(f.maximumValue())
        return {"name": name, "inputs": inputs, "expected": expected}


def cases():
    return [case(name, abcd) for name, abcd in SETS + REFUSALS]


if __name__ == "__main__":
    v.main(__file__, "ql/math/abcdmathfunction.hpp", cases)
