# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CEVRNDCalculator (ql/methods/finitedifferences/utilities/cevrndcalculator.hpp, .cpp): the
mass at zero, the density, the cumulative distribution and its inverse of the CEV forward df = alpha f^beta dW, on
forwards of 100 and 2.1, elasticities from 0.1 to 2 on both sides of 1 (δ below 2, where the forward reaches zero, and
above), volatility parameters alpha = sigma f0^(1 - beta) at sigma 10% and 30%, and times from 0.1 to 5 years; the
density and distribution at forwards from 0.3 to 2.5 times f0, the inverse at probabilities from 1e-3 to 0.99, among
them some below the mass at zero (0). |β - 1| < 1e-3, where nanoql departs (the S-van plan's VR10,
tests/NanoQL.Tests/Divergences/CevNearOneTests.cs), is none of the grid's. The inverse leaves out the points where
QuantLib's second Brent steps below zero, where X(f) is NaN or negative and Boost refuses the non-centrality, which
nanoql prices (tests/NanoQL.Tests/Divergences/CevDensityBelowZeroTests.cs); its points are listed as (t, p) pairs. The
wheel's calls, by QuantLib-SWIG 1.44: CEVRNDCalculator(f0, alpha, beta) and its massAtZero, pdf, cdf and invcdf. Read
back by tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Utilities/CEVRNDCalculatorVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

R = ql.Date(15, ql.October, 2026)
BETAS = [0.1, 0.3, 0.5, 0.7, 0.9, 0.99, 1.01, 1.1, 1.6, 2.0]
SIGMAS = [0.1, 0.3]
TIMES = [0.1, 1.0, 2.75, 5.0]
RELATIVE = [0.3, 0.6, 0.9, 1.0, 1.1, 1.5, 2.5]
PROBABILITIES = [1e-3, 0.01, 0.1, 0.25, 0.5, 0.75, 0.9, 0.99]


def inverse_of(calc, t, p):
    """QuantLib's invcdf, its throw recorded, or None where its second Brent steps below zero."""
    try:
        return v.enc(calc.invcdf(p, t))
    except RuntimeError as e:
        if "Non centrality parameter is" in str(e):
            return None
        return dict(v.THROWS)


def cases():
    st = v.settings(R)
    out = []
    for f0 in (100.0, 2.1):
        for beta in BETAS:
            for sigma in SIGMAS:
                alpha = sigma * f0 ** (1.0 - beta)
                calc = ql.CEVRNDCalculator(f0, alpha, beta)
                xs = [f0 * r for r in RELATIVE]
                inverse = [(t, p, inverse_of(calc, t, p)) for t in TIMES for p in PROBABILITIES]
                inverse = [(t, p, y) for t, p, y in inverse if y is not None]
                out.append({"name": f"f0 {f0} beta {beta} sigma {sigma}",
                            "inputs": {"settings": st, "f0": f0, "alpha": alpha, "beta": beta, "t": TIMES, "x": xs,
                                       "invcdfPoints": [[t, p] for t, p, _ in inverse]},
                            "expected": {
                                "massAtZero": [v.attempt(lambda t=t: calc.massAtZero(t)) for t in TIMES],
                                "pdf": [v.attempt(lambda t=t, x=x: calc.pdf(x, t)) for t in TIMES for x in xs],
                                "cdf": [v.attempt(lambda t=t, x=x: calc.cdf(x, t)) for t in TIMES for x in xs],
                                "invcdf": [y for _, _, y in inverse]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/cevrndcalculator.hpp", cases)
