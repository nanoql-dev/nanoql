# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MTBrownianGenerator and MTBrownianGeneratorFactory
(ql/models/marketmodels/browniangenerators/mtbrowniangenerator.hpp, .cpp): Gaussian variates from the Mersenne
Twister through the inverse cumulative normal, one step's factors at a time.

Five (factors, steps, seed) sets, from one factor and one step to three factors and 12 steps, seeds 1, 42, 1234 and
4294967295: for each, four paths, each nextPath()'s weight and every step's nextStep weight and variates; and the same
paths from the factory's generator (MTBrownianGeneratorFactory(seed).create(factors, steps)), and numberOfFactors
and numberOfSteps.

Left out: the seed 0, which QuantLib's Mersenne Twister replaces by one from the clock and nanoql refuses (seeds are
explicit, in [1, 4294967295]; tests/NanoQL.Tests/Conventions/BrownianGeneratorContractTests.cs).

The specs are {"factors", "steps", "seed"}. The wheel's calls, by QuantLib-SWIG 1.44: MTBrownianGenerator(factors,
steps, seed), nextPath(), nextStep(DoubleVector), numberOfFactors(), numberOfSteps(), and
MTBrownianGeneratorFactory(seed).create(factors, steps). Read back by
tests/NanoQL.Tests/Vectors/Models/MarketModels/MTBrownianGeneratorVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

R = ql.Date(15, ql.October, 2026)
SETS = [(1, 1, 42), (2, 3, 1), (3, 12, 1234), (1, 10, 4294967295), (2, 5, 42)]
PATHS = 4


def draws(g, factors, steps, prefix):
    """Each path's weight, and each step's weight and variates."""
    path_weights, step_weights, variates = [], [], []
    out_vector = ql.DoubleVector(factors)
    for _ in range(PATHS):
        path_weights.append(g.nextPath())
        weights, values = [], []
        for _ in range(steps):
            weights.append(g.nextStep(out_vector))
            values.append(v.enc(list(out_vector)))
        step_weights.append(weights)
        variates.append(values)
    return {prefix + "PathWeights": path_weights, prefix + "StepWeights": step_weights, prefix + "Variates": variates}


def cases():
    st = v.settings(R)
    out = []
    for factors, steps, seed in SETS:
        g = ql.MTBrownianGenerator(factors, steps, seed)
        f = ql.MTBrownianGeneratorFactory(seed).create(factors, steps)
        out.append({"name": f"{factors} factors, {steps} steps, seed {seed}",
                    "inputs": {"settings": st, "generator": {"factors": factors, "steps": steps, "seed": seed}},
                    "expected": {"numberOfFactors": g.numberOfFactors(), "numberOfSteps": g.numberOfSteps(),
                                 **draws(g, factors, steps, "generator"), **draws(f, factors, steps, "factory")}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/browniangenerators/mtbrowniangenerator.hpp", cases)
