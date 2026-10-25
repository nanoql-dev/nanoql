# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SobolBrownianGenerator and SobolBrownianGeneratorFactory
(ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.hpp, .cpp): Gaussian variates from a Sobol sequence
through the inverse cumulative normal, the steps' variates built by a Brownian bridge, assigned to the factors and steps
by the factors, steps or diagonal ordering.

Each ordering on three (factors, steps) sets (one factor and eight steps, two factors and five steps, three factors and
four steps), seed 42 and Jaeckel's direction integers, and the diagonal ordering on two factors and 91 steps with
Joe-Kuo D7, QuantLib's hestonslvmodel test's generator (seed 1234), and on Kuo's: for each, five paths, each
nextPath()'s weight and every step's nextStep weight and variates; and the same paths from the factory's generator, and
numberOfFactors and numberOfSteps.

Left out: the seed 0, which QuantLib's Sobol sequence replaces by one from the clock and nanoql refuses (seeds are
explicit, in [1, 4294967295]); Burley2020SobolBrownianGenerator and its factory, which wait for S-mm's
Burley2020SobolRsg.

The specs are {"factors", "steps", "ordering", "seed", "directionIntegers"}, the ordering QuantLib's Ordering (0
factors, 1 steps, 2 diagonal) and the direction integers its SobolRsg::DirectionIntegers. The wheel's calls, by
QuantLib-SWIG 1.44: SobolBrownianGenerator(factors, steps, ordering, seed, directionIntegers), nextPath(),
nextStep(DoubleVector), numberOfFactors(), numberOfSteps(), and SobolBrownianGeneratorFactory(ordering, seed,
directionIntegers).create(factors, steps). Read back by
tests/NanoQL.Tests/Vectors/Models/MarketModels/SobolBrownianGeneratorVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


mt = _load("mtbrowniangenerator", "models/marketmodels/browniangenerators/mtbrowniangenerator.py")
R = mt.R
ORDERINGS = [("factors", int(ql.SobolBrownianGenerator.Factors)), ("steps", int(ql.SobolBrownianGenerator.Steps)),
             ("diagonal", int(ql.SobolBrownianGenerator.Diagonal))]
SIZES = [(1, 8), (2, 5), (3, 4)]
JAECKEL, JOE_KUO_D7, KUO = int(ql.SobolRsg.Jaeckel), int(ql.SobolRsg.JoeKuoD7), int(ql.SobolRsg.Kuo)
PATHS = 5


def draws(g, factors, steps, prefix):
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


def specs():
    out = [(f"{oname}, {factors} factors, {steps} steps", {"factors": factors, "steps": steps, "ordering": o,
                                                           "seed": 42, "directionIntegers": JAECKEL})
           for oname, o in ORDERINGS for factors, steps in SIZES]
    diagonal = ORDERINGS[2][1]
    out += [("diagonal, 2 factors, 91 steps, Joe-Kuo D7", {"factors": 2, "steps": 91, "ordering": diagonal,
                                                           "seed": 1234, "directionIntegers": JOE_KUO_D7}),
            ("diagonal, 2 factors, 6 steps, Kuo", {"factors": 2, "steps": 6, "ordering": diagonal, "seed": 7,
                                                   "directionIntegers": KUO})]
    return out


def cases():
    st = v.settings(R)
    out = []
    for name, spec in specs():
        args = (spec["ordering"], spec["seed"], spec["directionIntegers"])
        g = ql.SobolBrownianGenerator(spec["factors"], spec["steps"], *args)
        f = ql.SobolBrownianGeneratorFactory(*args).create(spec["factors"], spec["steps"])
        out.append({"name": name, "inputs": {"settings": st, "generator": spec},
                    "expected": {"numberOfFactors": g.numberOfFactors(), "numberOfSteps": g.numberOfSteps(),
                                 **draws(g, spec["factors"], spec["steps"], "generator"),
                                 **draws(f, spec["factors"], spec["steps"], "factory")}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.hpp", cases)
