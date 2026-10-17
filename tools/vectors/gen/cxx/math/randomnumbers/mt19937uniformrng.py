# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for MersenneTwisterUniformRng's members the wheel doesn't expose (ql/math/randomnumbers/mt19937uniformrng.hpp,
docs/porting-playbook.md §9.4), each output computed by the C++ harness: the first 700 draws of nextInt32, past the
first twist at the 625th, for six seeds across the full unsigned 32-bit range (2^31 and 4294967295 among them, which
the wheel's signed seed reaches only as negatives), and for five keys of the constructor from a key, MT19937's
init_by_array: test-suite/mersennetwister.cpp's {0x123, 0x234, 0x345, 0x456}, one word of 0, one of 4294967295, four
words at the range's edges, and 700 words (more than the 624 of the state, so the key's loop runs over the key's
length) from a fixed random sample. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/RandomNumbers/MT19937UniformRngCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

SEEDS = [1, 42, 2147483647, 2147483648, 4294967294, 4294967295]
DRAWS = 700


def keys():
    rng = random.Random(v.SEED)
    return [[0x123, 0x234, 0x345, 0x456], [0], [4294967295], [1, 4294967295, 0, 2147483648],
            [rng.randrange(2**32) for _ in range(700)]]


def cases():
    out = []
    with cxx.connect() as H:
        for seed in SEEDS:
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            out.append({"name": f"seed {seed}", "inputs": {"settings": st, "seed": seed, "draws": DRAWS},
                        "expected": {"nextInt32": H.call("MersenneTwisterUniformRng::nextInt32", {"seed": seed},
                                                         DRAWS)}})
        for i, key in enumerate(keys()):
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            out.append({"name": f"key {i}, {len(key)} words", "inputs": {"settings": st, "seeds": key, "draws": DRAWS},
                        "expected": {"nextInt32": H.call("MersenneTwisterUniformRng::nextInt32", {"seeds": key},
                                                         DRAWS)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/mt19937uniformrng.hpp", cases)
