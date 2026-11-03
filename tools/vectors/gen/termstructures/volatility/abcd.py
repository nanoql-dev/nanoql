# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AbcdFunction (ql/termstructures/volatility/abcd.hpp, .cpp), the abcd form for instantaneous
volatility: on six coefficient sets, the short- and long-term volatilities, the instantaneous volatility, variance and
covariance at five (u, T, S) points, the three-argument covariance there, the primitive of the covariance, the
integrated covariance over five (t1, t2, T, S) intervals (one starting past the earlier fixing, which is 0), the
volatility and variance over four (tMin, tMax, T) intervals (one with tMin = tMax, the instantaneous volatility), and
the maximum volatility where 1.44 agrees with nanoql; and the refusals: reversed integration bounds, tMax before tMin,
and invalid coefficients, each as {"throws": true}.

Left out, each a documented divergence: the maximum volatility where 1.44's maximumValue returns d or reads its b < 0
location (AbcdMaximumTests); and small c, where QuantLib's primitive cancels (AbcdPrimitiveTests): every set's c is at
least 0.3. Each case's input "abcd" is the four coefficients. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/AbcdVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

TODAY = ql.Date(15, ql.January, 2025)
POINTS = [[0.0, 1.0, 1.0], [0.5, 2.0, 3.0], [1.0, 5.0, 4.0], [2.0, 10.0, 10.0], [3.0, 2.0, 5.0]]
COVARIANCES = [[0.0, 1.0, 1.0, 1.0], [0.0, 5.0, 5.0, 7.0], [2.0, 4.0, 6.0, 4.5], [0.0, 0.25, 0.25, 0.5],
               [6.0, 8.0, 5.0, 7.0]]
VOLATILITIES = [[0.0, 1.0, 1.0], [0.0, 5.0, 5.0], [1.0, 3.0, 4.0], [2.0, 2.0, 3.0]]
SETS = [
    ("QuantLib's defaults", [-0.06, 0.17, 0.54, 0.17]),
    ("b positive, maximum after 0", [0.1, 0.4, 0.8, 0.05]),
    ("b negative, minimum after 0, a positive", [0.1, -0.02, 0.3, 0.15]),
    ("b zero", [0.1, 0.0, 0.4, 0.05]),
    ("a steep hump", [0.05, 0.4, 2.5, 0.01]),
    ("the integrals case's", [0.07, 0.07, 0.5, 0.1]),
]


def master_maximum_value(f, a, b, c, d):
    # f at QuantLib's master's maximumLocation (#2841), d at +infinity
    big = sys.float_info.max
    if b == 0.0:
        loc = 0.0 if a >= 0.0 else big
    else:
        t_star = 1.0 / c - a / b
        loc = (t_star if t_star > 0.0 else 0.0) if b > 0.0 else (big if t_star <= 0.0 else (0.0 if a >= 0.0 else big))
    return d if loc == big else f(loc)


def case(name, abcd):
    with v.case_settings(TODAY) as st:
        inputs = {"settings": st, "abcd": abcd, "points": POINTS, "covariances": COVARIANCES, "volatilities": VOLATILITIES}
        f = ql.AbcdFunction(*abcd)
        expected = {
            "shortTermVolatility": v.enc(f.shortTermVolatility()),
            "longTermVolatility": v.enc(f.longTermVolatility()),
            "instantaneousVolatility": [v.enc(f.instantaneousVolatility(u, t)) for u, t, s in POINTS],
            "instantaneousVariance": [v.enc(f.instantaneousVariance(u, t)) for u, t, s in POINTS],
            "instantaneousCovariance": [v.enc(f.instantaneousCovariance(u, t, s)) for u, t, s in POINTS],
            "covarianceAt": [v.enc(f.covariance(u, t, s)) for u, t, s in POINTS],
            "primitive": [v.enc(f.primitive(u, t, s)) for u, t, s in POINTS],
            "covariance": [v.enc(f.covariance(t1, t2, t, s)) for t1, t2, t, s in COVARIANCES],
            "volatility": [v.enc(f.volatility(a, b, t)) for a, b, t in VOLATILITIES],
            "variance": [v.enc(f.variance(a, b, t)) for a, b, t in VOLATILITIES],
        }
        if f.maximumVolatility() == master_maximum_value(f, *abcd):
            expected["maximumVolatility"] = v.enc(f.maximumVolatility())
        return {"name": name, "inputs": inputs, "expected": expected}


def refusal(name, abcd, call):
    with v.case_settings(TODAY) as st:
        try:
            f = ql.AbcdFunction(*abcd)
            call(f)
        except RuntimeError:
            return {"name": name, "inputs": {"settings": st, "abcd": abcd, "call": call.__name__},
                    "expected": {"refused": dict(v.THROWS)}}
        raise AssertionError(name)


def reversed_bounds(f):
    return f.covariance(2.0, 1.0, 3.0, 4.0)


def reversed_volatility(f):
    return f.volatility(2.0, 1.0, 3.0)


def construct(f):
    return f


def cases():
    out = [case(name, abcd) for name, abcd in SETS]
    out.append(refusal("refused: integration bounds in reverse order", SETS[0][1], reversed_bounds))
    out.append(refusal("refused: tMax before tMin", SETS[0][1], reversed_volatility))
    out.append(refusal("refused: c not positive", [0.1, 0.2, -0.3, 0.4], construct))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/abcd.hpp", cases)
