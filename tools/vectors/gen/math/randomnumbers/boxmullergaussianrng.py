# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for BoxMullerGaussianRng (ql/math/randomnumbers/boxmullergaussianrng.hpp) through the wheel's four
compositions of it, BoxMuller{MersenneTwister,Knuth,Lecuyer,Xoshiro256StarStar}GaussianRng, each a SWIG
%template of the C++ class: for seeds 1 and 1234, the first 501 draws of next() (an odd count, so the last is
a pair's first) and the last draw's weight. Read back by
tests/NanoQL.Tests/Vectors/Numerics/RandomNumbers/BoxMullerGaussianRngVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

UNIFORMS = [("MersenneTwister", ql.MersenneTwisterUniformRng), ("Knuth", ql.KnuthUniformRng),
            ("Lecuyer", ql.LecuyerUniformRng), ("Xoshiro256StarStar", ql.Xoshiro256StarStarUniformRng)]
SEEDS = [1, 1234]
DRAWS = 501


def cases():
    out = []
    for uniform, rng in UNIFORMS:
        name = f"BoxMuller{uniform}GaussianRng"
        composition = getattr(ql, name)
        for seed in SEEDS:
            st = v.settings(ql.Date(15, ql.October, 2026))
            g = composition(rng(seed))
            samples = [g.next() for _ in range(DRAWS)]
            out.append({"name": f"{name}, seed {seed}",
                        "inputs": {"settings": st, "composition": name, "seed": seed, "draws": DRAWS},
                        "expected": {"next": [v.enc(s.value()) for s in samples],
                                     "lastWeight": v.enc(samples[-1].weight())}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/boxmullergaussianrng.hpp", cases)
