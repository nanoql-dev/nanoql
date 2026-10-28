# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for ZigguratGaussianRng (ql/math/randomnumbers/zigguratgaussianrng.hpp) through the wheel's
ZigguratXoshiro256StarStarGaussianRng and its RandomSequenceGenerator, ZigguratXoshiro256StarStarGaussianRsg: for
seeds 1, 42 and 2147483647 (the wheel's seed is a signed 32-bit BigInteger; the 64-bit seeds are the harness's),
the first 2,000 draws of next(), and 100 five-dimensional sequences, their last weight and dimension. Read back by
tests/NanoQL.Tests/Vectors/Numerics/RandomNumbers/ZigguratGaussianRngVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

SEEDS = [1, 42, 2147483647]
DRAWS = 2000
DIM = 5
SEQUENCES = 100


def cases():
    out = []
    for seed in SEEDS:
        st = v.settings(ql.Date(15, ql.October, 2026))
        g = ql.ZigguratXoshiro256StarStarGaussianRng(ql.Xoshiro256StarStarUniformRng(seed))
        out.append({"name": f"Rng, seed {seed}", "inputs": {"settings": st, "generator": "Rng", "seed": seed,
                                                            "draws": DRAWS},
                    "expected": {"next": [v.enc(g.next().value()) for _ in range(DRAWS)]}})
        st = v.settings(ql.Date(15, ql.October, 2026))
        rsg = ql.ZigguratXoshiro256StarStarGaussianRsg(
            DIM, ql.ZigguratXoshiro256StarStarGaussianRng(ql.Xoshiro256StarStarUniformRng(seed)))
        # nextSequence returns a reference to the generator's buffer, QuantLib's const sample_type&, which the next
        # draw overwrites: each draw is read as it comes.
        drawn, weight = [], None
        for _ in range(SEQUENCES):
            sample = rsg.nextSequence()
            drawn.append(list(sample.value()))
            weight = sample.weight()
        out.append({"name": f"Rsg, seed {seed}",
                    "inputs": {"settings": st, "generator": "Rsg", "seed": seed, "dimension": DIM,
                               "sequences": SEQUENCES},
                    "expected": {"nextSequence": [v.enc(s) for s in drawn],
                                 "lastWeight": v.enc(weight), "dimension": rsg.dimension()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/zigguratgaussianrng.hpp", cases)
