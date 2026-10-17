# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for InverseCumulativeRsg (ql/math/randomnumbers/inversecumulativersg.hpp) through the wheel's six
compositions of it, each a SWIG %template of the C++ class with no logic of its own:
InvCumulativeMersenneTwisterGaussianRsg and MoroInvCumulativeMersenneTwisterGaussianRsg over
RandomSequenceGenerator<MersenneTwisterUniformRng> (the wheel's MersenneTwisterUniformRsg),
InvCumulativeHaltonGaussianRsg and MoroInvCumulativeHaltonGaussianRsg over HaltonRsg (its defaults: a random start,
no random shift), and InvCumulativeSobolGaussianRsg and MoroInvCumulativeSobolGaussianRsg over SobolRsg (its defaults:
Jaeckel's direction integers, the Gray code). For each, three 5-dimensional generators: seed 1 with the inverse
cumulative's default construction and the uniform generator built from a MersenneTwisterUniformRng; seed 42 with the
default too and, for the Mersenne Twister ones, MersenneTwisterUniformRsg's seed constructor; and seed 4294967295 with
the inverse cumulative given (average 0.5, sigma 2). Each records its first 50 nextSequence draws, the last draw's
weight and its dimension. The wheel's MersenneTwisterUniformRng and SobolRsg take the seed as a signed 32-bit
BigInteger, so 4294967295 reaches them as -1; HaltonRsg and the seed constructor take it unsigned. Read back by
tests/NanoQL.Tests/Vectors/Numerics/RandomNumbers/InverseCumulativeRsgVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

DIMENSION = 5
DRAWS = 50
# (seed, how the Mersenne Twister sequence generator is built, the inverse cumulative given or None)
GENERATORS = [(1, "rng", None), (42, "seed", None), (4294967295, "rng", {"average": 0.5, "sigma": 2.0})]
COMPOSITIONS = [
    ("InvCumulativeMersenneTwisterGaussianRsg", ql.InvCumulativeMersenneTwisterGaussianRsg, "MersenneTwister",
     ql.InverseCumulativeNormal),
    ("MoroInvCumulativeMersenneTwisterGaussianRsg", ql.MoroInvCumulativeMersenneTwisterGaussianRsg, "MersenneTwister",
     ql.MoroInverseCumulativeNormal),
    ("InvCumulativeHaltonGaussianRsg", ql.InvCumulativeHaltonGaussianRsg, "Halton", ql.InverseCumulativeNormal),
    ("MoroInvCumulativeHaltonGaussianRsg", ql.MoroInvCumulativeHaltonGaussianRsg, "Halton",
     ql.MoroInverseCumulativeNormal),
    ("InvCumulativeSobolGaussianRsg", ql.InvCumulativeSobolGaussianRsg, "Sobol", ql.InverseCumulativeNormal),
    ("MoroInvCumulativeSobolGaussianRsg", ql.MoroInvCumulativeSobolGaussianRsg, "Sobol",
     ql.MoroInverseCumulativeNormal),
]


def wheel_seed(seed):
    """The seed as the wheel's signed 32-bit BigInteger carries it."""
    return seed - 2**32 if seed > 2**31 - 1 else seed


def uniform(kind, seed, construction):
    if kind == "MersenneTwister":
        if construction == "seed":
            return ql.MersenneTwisterUniformRsg(DIMENSION, seed)
        return ql.MersenneTwisterUniformRsg(DIMENSION, ql.MersenneTwisterUniformRng(wheel_seed(seed)))
    if kind == "Halton":
        return ql.HaltonRsg(DIMENSION, seed)
    return ql.SobolRsg(DIMENSION, wheel_seed(seed))


def cases():
    out = []
    for name, composition, kind, inverse in COMPOSITIONS:
        for seed, construction, ic in GENERATORS:
            st = v.settings(ql.Date(15, ql.October, 2026))
            usg = uniform(kind, seed, construction)
            rsg = composition(usg) if ic is None else composition(usg, inverse(ic["average"], ic["sigma"]))
            # nextSequence returns a reference to the generator's buffer, QuantLib's const sample_type&, which the
            # next draw overwrites: each draw is read as it comes.
            draws, weight = [], None
            for _ in range(DRAWS):
                sample = rsg.nextSequence()
                draws.append(list(sample.value()))
                weight = sample.weight()
            out.append({"name": f"{name}, seed {seed}",
                        "inputs": {"settings": st, "composition": name, "dimension": DIMENSION, "seed": seed,
                                   "construction": construction, "inverseCumulative": ic, "draws": DRAWS},
                        "expected": {"nextSequence": v.enc(draws), "lastWeight": v.enc(weight),
                                     "dimension": rsg.dimension()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/inversecumulativersg.hpp", cases)
