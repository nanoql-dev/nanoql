# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Burley2020SobolRsg (ql/math/randomnumbers/burley2020sobolrsg.hpp, .cpp): the Sobol sequence without the
Gray code, its sample index and each group of four dimensions' integers scrambled by Burley's nested uniform scramble.
Nine generators: Jaeckel's direction integers at 1, 3, 4, 5 and 9 dimensions (the scramble's groups of four ending
inside and at a group) with QuantLib's default seeds 42 and 43, seeds 1 and 1, and seeds 42 and 4294967295; Jaeckel's
at 34 dimensions, past its 32 tabulated ones, so the last two are initialized from the seed 4294967295; JoeKuoD7's at
1,902 with seeds 1 and 7, likewise; Kuo's at 50 and Unit at 6. Each records its dimension, its first five
nextInt32Sequence draws (per draw the integers of its last six dimensions and the SHA-256 of all of them as
little-endian unsigned 32-bit integers), then five nextSequence draws (each one's last six values and weight) and
lastSequence's last six values. The wheel's seeds are signed 32-bit BigIntegers, so 4294967295 reaches each as -1;
each case records the seeds themselves and the set as the enum's integer. The wheel's calls, by QuantLib-SWIG 1.44's
randomnumbers.i: Burley2020SobolRsg(dimensionality, seed, directionIntegers, scrambleSeed), dimension,
nextInt32Sequence, nextSequence's value and weight, lastSequence's value. Read back by
tests/NanoQL.Tests/Vectors/Numerics/RandomNumbers/Burley2020SobolRsgVectorTests.cs."""
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

GENERATORS = [(ql.SobolRsg.Jaeckel, 1, 42, 43), (ql.SobolRsg.Jaeckel, 3, 42, 43), (ql.SobolRsg.Jaeckel, 4, 42, 43),
              (ql.SobolRsg.Jaeckel, 5, 1, 1), (ql.SobolRsg.Jaeckel, 9, 42, 4294967295),
              (ql.SobolRsg.Jaeckel, 34, 4294967295, 43), (ql.SobolRsg.JoeKuoD7, 1902, 1, 7),
              (ql.SobolRsg.Kuo, 50, 42, 43), (ql.SobolRsg.Unit, 6, 42, 43)]
DRAWS = 5
SEQUENCES = 5
LAST = 6


def wheel_seed(seed):
    """The seed as the wheel's signed 32-bit BigInteger carries it."""
    return seed - 2**32 if seed > 2**31 - 1 else seed


def digest(integers):
    return hashlib.sha256(struct.pack(f"<{len(integers)}I", *integers)).hexdigest()


def cases():
    out = []
    st = v.settings(ql.Date(15, ql.October, 2026))
    for integers, dimension, seed, scramble in GENERATORS:
        rsg = ql.Burley2020SobolRsg(dimension, wheel_seed(seed), integers, wheel_seed(scramble))
        draws = [list(rsg.nextInt32Sequence()) for _ in range(DRAWS)]
        values, weights = [], []
        for _ in range(SEQUENCES):
            s = rsg.nextSequence()
            values.append(list(s.value()))
            weights.append(s.weight())
        last = list(rsg.lastSequence().value())
        out.append({"name": f"directionIntegers {int(integers)}, dimension {dimension}, seeds {seed} and {scramble}",
                    "inputs": {"settings": st, "dimension": dimension, "seed": seed,
                               "directionIntegers": int(integers), "scrambleSeed": scramble, "draws": DRAWS,
                               "sequences": SEQUENCES, "last": LAST},
                    "expected": {"dimension": rsg.dimension(),
                                 "drawIntegers": [d[-LAST:] for d in draws],
                                 "drawDigests": [digest(d) for d in draws],
                                 "sequences": [v.enc(s[-LAST:]) for s in values],
                                 "weights": v.enc(weights),
                                 "lastSequence": v.enc(last[-LAST:])}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/burley2020sobolrsg.hpp", cases)
