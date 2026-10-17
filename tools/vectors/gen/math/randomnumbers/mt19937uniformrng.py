# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for MersenneTwisterUniformRng (ql/math/randomnumbers/mt19937uniformrng.hpp): for ten seeds across the
32-bit range, the first 1,000 draws of next(), past the generator's first twist at its 625th draw, and the last
draw's weight. The wheel's MersenneTwisterUniformRng is a SWIG wrapper of the C++ class with no logic of its own; its
seed is a BigInteger, a signed 32-bit long on Windows, so a seed above 2147483647 reaches it as its 32-bit two's
complement (-1 for 4294967295, -2147483648 for 2147483648), which the C++ constructor's unsigned long reads back as
the seed. Each case records the seed itself. Read back by
tests/NanoQL.Tests/Vectors/Numerics/RandomNumbers/MT19937UniformRngVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

SEEDS = [1, 42, 4357, 5489, 19650218, 2147483647, 2147483648, 3000000000, 4294967294, 4294967295]
DRAWS = 1000


def wheel_seed(seed):
    """The seed as the wheel's signed 32-bit BigInteger carries it."""
    return seed - 2**32 if seed > 2**31 - 1 else seed


def cases():
    out = []
    for seed in SEEDS:
        st = v.settings(ql.Date(15, ql.October, 2026))
        rng = ql.MersenneTwisterUniformRng(wheel_seed(seed))
        samples = [rng.next() for _ in range(DRAWS)]
        out.append({"name": f"seed {seed}",
                    "inputs": {"settings": st, "seed": seed, "draws": DRAWS},
                    "expected": {"next": [v.enc(s.value()) for s in samples],
                                 "lastWeight": v.enc(samples[-1].weight())}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/mt19937uniformrng.hpp", cases)
