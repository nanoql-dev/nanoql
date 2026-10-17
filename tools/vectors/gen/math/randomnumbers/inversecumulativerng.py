# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for InverseCumulativeRng (ql/math/randomnumbers/inversecumulativerng.hpp) through the wheel's two
compositions of it over MersenneTwisterUniformRng, each a SWIG %template of the C++ class with no logic of its own:
InvCumulativeMersenneTwisterGaussianRng (InverseCumulativeNormal) and MoroInvCumulativeMersenneTwisterGaussianRng
(MoroInverseCumulativeNormal), whose inverse cumulative is its default construction, never assigned. For seeds 1, 1234
and 4294967295, each records its first 500 draws of next() and the last draw's weight. The wheel's
MersenneTwisterUniformRng takes the seed as a signed 32-bit BigInteger, so 4294967295 reaches it as -1. Read back by
tests/NanoQL.Tests/Vectors/Numerics/RandomNumbers/InverseCumulativeRngVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

SEEDS = [1, 1234, 4294967295]
DRAWS = 500
COMPOSITIONS = [("InvCumulativeMersenneTwisterGaussianRng", ql.InvCumulativeMersenneTwisterGaussianRng),
                ("MoroInvCumulativeMersenneTwisterGaussianRng", ql.MoroInvCumulativeMersenneTwisterGaussianRng)]


def wheel_seed(seed):
    """The seed as the wheel's signed 32-bit BigInteger carries it."""
    return seed - 2**32 if seed > 2**31 - 1 else seed


def cases():
    out = []
    for name, composition in COMPOSITIONS:
        for seed in SEEDS:
            st = v.settings(ql.Date(15, ql.October, 2026))
            rng = composition(ql.MersenneTwisterUniformRng(wheel_seed(seed)))
            samples = [rng.next() for _ in range(DRAWS)]
            out.append({"name": f"{name}, seed {seed}",
                        "inputs": {"settings": st, "composition": name, "seed": seed, "draws": DRAWS},
                        "expected": {"next": [v.enc(s.value()) for s in samples],
                                     "lastWeight": v.enc(samples[-1].weight())}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/inversecumulativerng.hpp", cases)
