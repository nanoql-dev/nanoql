# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Burley2020SobolBrownianGenerator and Burley2020SobolBrownianGeneratorFactory
(ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.hpp, .cpp), which the wheel doesn't bind, each
output computed by the C++ harness: Gaussian variates from Burley's scrambled Sobol sequence through the inverse
cumulative normal, the steps' variates built by a Brownian bridge, assigned to the factors and steps by the factors,
steps or diagonal ordering.

Each ordering on three (factors, steps) sets (one factor and eight steps, two factors and five steps, three factors and
four steps), seeds 42 and 43 (QuantLib's defaults) and Jaeckel's direction integers; and the diagonal ordering on two
factors and 91 steps with Joe-Kuo D7 (seeds 1234 and 5678), and on two factors and six steps with Kuo's (seeds 7 and
11): for each, five paths, each nextPath()'s weight and every step's nextStep weight and variates, from the generator
and from the factory's, and the generator's numberOfFactors and numberOfSteps.

The harness call is tools/cxx/src/models/marketmodels/browniangenerators/sobolbrowniangenerator.cpp's
"Burley2020SobolBrownianGenerator.paths". Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/Burley2020SobolBrownianGeneratorCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
ORDERINGS = [("factors", 0), ("steps", 1), ("diagonal", 2)]
SIZES = [(1, 8), (2, 5), (3, 4)]
JAECKEL, JOE_KUO_D7, KUO = 1, 6, 7
PATHS = 5


def specs():
    out = [(f"{oname}, {factors} factors, {steps} steps", factors, steps, o, 42, JAECKEL, 43)
           for oname, o in ORDERINGS for factors, steps in SIZES]
    out += [("diagonal, 2 factors, 91 steps, Joe-Kuo D7", 2, 91, 2, 1234, JOE_KUO_D7, 5678),
            ("diagonal, 2 factors, 6 steps, Kuo", 2, 6, 2, 7, KUO, 11)]
    return out


def flatten(draws, prefix):
    return {prefix + "PathWeights": [p["weight"] for p in draws["paths"]],
            prefix + "StepWeights": [[s["weight"] for s in p["steps"]] for p in draws["paths"]],
            prefix + "Variates": [[s["variates"] for s in p["steps"]] for p in draws["paths"]]}


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for name, factors, steps, ordering, seed, integers, scramble in specs():
            r = H.call("Burley2020SobolBrownianGenerator.paths", factors, steps, ordering, seed, integers, scramble,
                       PATHS)
            out.append({"name": name,
                        "inputs": {"settings": st, "factors": factors, "steps": steps, "ordering": ordering,
                                   "seed": seed, "directionIntegers": integers, "scrambleSeed": scramble,
                                   "paths": PATHS},
                        "expected": {"numberOfFactors": r["generator"]["numberOfFactors"],
                                     "numberOfSteps": r["generator"]["numberOfSteps"],
                                     **flatten(r["generator"], "generator"), **flatten(r["factory"], "factory")}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.hpp", cases)
