# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for SobolRsg (ql/math/randomnumbers/sobolrsg.hpp) with the Gray code, the wheel's only form: twenty
generators. The eight sets of direction integers whose table ends below the 21,200 dimensions of the primitive
polynomials (Jaeckel's 32, SobolLevitan's 40, SobolLevitanLemieux's 360, JoeKuoD5's 2,000, JoeKuoD7's 1,900, Kuo's
4,926, Kuo2's 3,947 and Kuo3's 4,587 dimensions) are each taken at their tabulated dimensions plus 2, so their last two
dimensions are initialized from the seed, with seeds 1 and 4294967295; Unit (no table: the seed is never read) at 40
dimensions, JoeKuoD6 (whose table reaches 21,200, so no dimension is seeded) at 21,200 and 1,111, and Jaeckel, the
default set, at 8, with seed 1. Each records its dimension, its first four nextInt32Sequence draws, and after each of
four skipTo calls (1,023, 1,024, 2^20 and 10^6 + 3: the counter reaches a power of two) one nextInt32Sequence and one
nextSequence draw: per integer draw the integers of its last six dimensions and the SHA-256 of all of them as
little-endian unsigned 32-bit integers, and per sequence its last six values. The wheel's SobolRsg is a SWIG wrapper
of the C++ class (its nextInt32Sequence copies the integers into a list, its skipTo drops the result), with the Gray
code always on; its seed is a signed 32-bit BigInteger, so 4294967295 reaches it as -1. Each case records the seed
itself and the set as the enum's integer. Read back by
tests/NanoQL.Tests/Vectors/Numerics/RandomNumbers/SobolRsgVectorTests.cs."""
import hashlib
import os
import struct
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

# Each set's tabulated dimensions: its table's entries plus the degenerate first dimension.
TABULATED = [(ql.SobolRsg.Jaeckel, 32), (ql.SobolRsg.SobolLevitan, 40), (ql.SobolRsg.SobolLevitanLemieux, 360),
             (ql.SobolRsg.JoeKuoD5, 2000), (ql.SobolRsg.JoeKuoD7, 1900), (ql.SobolRsg.Kuo, 4926),
             (ql.SobolRsg.Kuo2, 3947), (ql.SobolRsg.Kuo3, 4587)]
GENERATORS = ([(integers, dimension + 2, seed) for integers, dimension in TABULATED for seed in (1, 4294967295)]
              + [(ql.SobolRsg.Unit, 40, 1), (ql.SobolRsg.JoeKuoD6, 21200, 1), (ql.SobolRsg.JoeKuoD6, 1111, 1),
                 (ql.SobolRsg.Jaeckel, 8, 1)])
DRAWS = 4
SKIPS = [1023, 1024, 2**20, 10**6 + 3]
LAST = 6


def wheel_seed(seed):
    """The seed as the wheel's signed 32-bit BigInteger carries it."""
    return seed - 2**32 if seed > 2**31 - 1 else seed


def digest(integers):
    return hashlib.sha256(struct.pack(f"<{len(integers)}I", *integers)).hexdigest()


def cases():
    assert len(GENERATORS) == 20
    out = []
    for integers, dimension, seed in GENERATORS:
        st = v.settings(ql.Date(15, ql.October, 2026))
        rsg = ql.SobolRsg(dimension, wheel_seed(seed), integers)
        draws = [list(rsg.nextInt32Sequence()) for _ in range(DRAWS)]
        skipped, sequences = [], []
        for n in SKIPS:
            rsg.skipTo(n)
            skipped.append(list(rsg.nextInt32Sequence()))
            sequences.append(list(rsg.nextSequence().value()))
        out.append({"name": f"directionIntegers {integers}, dimension {dimension}, seed {seed}",
                    "inputs": {"settings": st, "dimension": dimension, "seed": seed, "directionIntegers": integers,
                               "draws": DRAWS, "skips": SKIPS, "last": LAST},
                    "expected": {"dimension": rsg.dimension(),
                                 "drawIntegers": [d[-LAST:] for d in draws],
                                 "drawDigests": [digest(d) for d in draws],
                                 "skipIntegers": [d[-LAST:] for d in skipped],
                                 "skipDigests": [digest(d) for d in skipped],
                                 "skipSequences": [v.enc(s[-LAST:]) for s in sequences]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/sobolrsg.hpp", cases)
