# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for RandomSequenceGenerator<MersenneTwisterUniformRng>'s members the wheel doesn't expose
(ql/math/randomnumbers/randomsequencegenerator.hpp, docs/porting-playbook.md §9.4), each output computed by the C++
harness: for three 5-dimensional generators (seeds 1 and 4294967295 with the seed constructor, seed 42 built from a
MersenneTwisterUniformRng), the first 100 draws of nextInt32Sequence, and the lastSequence and its weight after three
nextSequence draws of a fresh generator; and the two constructors at dimension 0, which the seed constructor accepts
and the one from a generator refuses. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/RandomNumbers/RandomSequenceGeneratorCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

DIMENSION = 5
DRAWS = 100
LAST_AFTER = 3
GENERATORS = [(1, "seed"), (42, "rng"), (4294967295, "seed")]


def spec(dimension, seed, construction):
    return {"dimension": dimension, "seed": seed} if construction == "seed" else \
        {"dimension": dimension, "rng": {"seed": seed}}


def cases():
    out = []
    with cxx.connect() as H:
        for seed, construction in GENERATORS:
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            s = spec(DIMENSION, seed, construction)
            last = H.call("RandomSequenceGenerator::lastSequence", s, LAST_AFTER)
            out.append({"name": f"seed {seed}, {construction} constructor",
                        "inputs": {"settings": st, "dimension": DIMENSION, "seed": seed, "construction": construction,
                                   "draws": DRAWS, "lastAfter": LAST_AFTER},
                        "expected": {"nextInt32Sequence": H.call("RandomSequenceGenerator::nextInt32Sequence", s,
                                                                 DRAWS),
                                     "lastSequence": v.enc(last["value"]), "lastWeight": v.enc(last["weight"])}})
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out.append({"name": "dimension 0",
                    "inputs": {"settings": st, "dimension": 0, "seed": 1},
                    "expected": {"seedConstructor": v.attempt(
                                     lambda: H.call("RandomSequenceGenerator::dimension", spec(0, 1, "seed"))),
                                 "rngConstructor": v.attempt(
                                     lambda: H.call("RandomSequenceGenerator::dimension", spec(0, 1, "rng")))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/randomsequencegenerator.hpp", cases)
