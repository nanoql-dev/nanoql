# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for HaltonRsg (ql/math/randomnumbers/haltonrsg.hpp): for each of the four combinations of a random start
and a random shift, and seeds 1, 42 and 1472677, a 7-dimensional generator's first 100 draws of nextSequence, its
lastSequence and its dimension. With seed 1472677 the random start of dimension index 2 lies within 74 of 2^32, so
QuantLib's counter + randomStart, an unsigned long of 32 bits on MSVC, wraps to 0 at draw 74 and the dimension starts
over; without a random start or shift the seed isn't read, and the three seeds give one sequence. The wheel's HaltonRsg
is a SWIG wrapper of the C++ class with no logic of its own, taking the seed as an unsigned 32-bit integer. Read back
by tests/NanoQL.Tests/Vectors/Numerics/RandomNumbers/HaltonRsgVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

OPTIONS = [(False, False), (True, False), (False, True), (True, True)]
SEEDS = [1, 42, 1472677]
DIMENSION = 7
DRAWS = 100


def cases():
    out = []
    for random_start, random_shift in OPTIONS:
        for seed in SEEDS:
            st = v.settings(ql.Date(15, ql.October, 2026))
            rsg = ql.HaltonRsg(DIMENSION, seed, random_start, random_shift)
            draws = [list(rsg.nextSequence().value()) for _ in range(DRAWS)]
            out.append({"name": f"randomStart {random_start}, randomShift {random_shift}, seed {seed}",
                        "inputs": {"settings": st, "dimension": DIMENSION, "seed": seed, "randomStart": random_start,
                                   "randomShift": random_shift, "draws": DRAWS},
                        "expected": {"nextSequence": v.enc(draws),
                                     "lastSequence": v.enc(list(rsg.lastSequence().value())),
                                     "dimension": rsg.dimension()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/haltonrsg.hpp", cases)
