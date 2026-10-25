# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SquareRootProcessRNDCalculator
(ql/methods/finitedifferences/utilities/squarerootprocessrndcalculator.hpp, .cpp): the density, cumulative distribution
and inverse of the square-root process dv = κ(θ − v)dt + σ√v dW, a scaled non-central chi-squared distribution, and its
stationary gamma distribution.

QuantLib's three parameter sets (testSquareRootProcessRND's) and a fourth near the Feller boundary: pdf and cdf at
eight values and three times, invcdf at five probabilities and three times, and the stationary pdf, cdf and inverse at
those values and probabilities. Every set's shape α = 2κθ/σ² is at most 48, so the stationary product stays finite and
is QuantLib's (the S-hes plan's HR7 (xiii); §2.10 leaves out α past about 170). The wheel's calls, by QuantLib-SWIG
1.44: SquareRootProcessRNDCalculator(v0, kappa, theta, sigma), pdf, cdf, invcdf, stationary_pdf, stationary_cdf and
stationary_invcdf. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Utilities/SquareRootProcessRNDCalculatorVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

PARAMETERS = [(0.17, 1.0, 0.09, 0.5), (1.0, 0.6, 0.1, 0.75), (0.005, 0.6, 0.1, 0.05), (0.04, 2.0, 0.04, 0.4)]
TIMES = [0.1, 0.75, 5.0]
VALUES = [1e-4, 0.01, 0.03, 0.06, 0.1, 0.2, 0.5, 1.2]
PROBABILITIES = [0.001, 0.1, 0.5, 0.9, 0.999]


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for v0, kappa, theta, sigma in PARAMETERS:
        calc = ql.SquareRootProcessRNDCalculator(v0, kappa, theta, sigma)
        inputs = {"settings": st, "v0": v0, "kappa": kappa, "theta": theta, "sigma": sigma, "times": TIMES, "values": VALUES,
                  "probabilities": PROBABILITIES}
        out.append({"name": f"v0 {v0}, kappa {kappa}, theta {theta}, sigma {sigma}",
                    "inputs": inputs,
                    "expected": {
                        "pdf": [[v.attempt(lambda t=t, x=x: calc.pdf(x, t)) for x in VALUES] for t in TIMES],
                        "cdf": [[v.attempt(lambda t=t, x=x: calc.cdf(x, t)) for x in VALUES] for t in TIMES],
                        "invcdf": [[v.attempt(lambda t=t, p=p: calc.invcdf(p, t)) for p in PROBABILITIES]
                                   for t in TIMES],
                        "stationaryPdf": [v.attempt(lambda x=x: calc.stationary_pdf(x)) for x in VALUES],
                        "stationaryCdf": [v.attempt(lambda x=x: calc.stationary_cdf(x)) for x in VALUES],
                        "stationaryInvCdf": [v.attempt(lambda p=p: calc.stationary_invcdf(p)) for p in PROBABILITIES]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/squarerootprocessrndcalculator.hpp", cases)
