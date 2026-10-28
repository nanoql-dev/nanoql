# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for KnuthUniformRng (ql/math/randomnumbers/knuthuniformrng.hpp, .cpp) through the wheel, and its
RandomSequenceGenerator, the wheel's KnuthUniformRsg: for seeds 1, 1234 and 2147483647 (the wheel's seed is a
signed 32-bit BigInteger), the first 2,100 draws of next() (past two refills of the 1,009-number buffer), and
50 five-dimensional sequences from a generator and from the seed constructor, their last weight and dimension.
Read back by tests/NanoQL.Tests/Vectors/Numerics/RandomNumbers/KnuthUniformRngVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

RNG = "KnuthUniformRng"
RSG = "KnuthUniformRsg"
SEEDS = [1, 1234, 2147483647]
DRAWS = 2100
DIM = 5
SEQUENCES = 50


def cases():
    out = []
    for seed in SEEDS:
        st = v.settings(ql.Date(15, ql.October, 2026))
        rng = getattr(ql, RNG)(seed)
        out.append({"name": f"{RNG}, seed {seed}",
                    "inputs": {"settings": st, "generator": RNG, "seed": seed, "draws": DRAWS},
                    "expected": {"next": [v.enc(rng.next().value()) for _ in range(DRAWS)]}})
        for how in ("rng", "seed"):
            st = v.settings(ql.Date(15, ql.October, 2026))
            make = getattr(ql, RSG)
            rsg = make(DIM, getattr(ql, RNG)(seed)) if how == "rng" else make(DIM, seed)
            # nextSequence returns a reference to the generator's buffer, QuantLib's const sample_type&, which the
            # next draw overwrites: each draw is read as it comes.
            drawn, weight = [], None
            for _ in range(SEQUENCES):
                sample = rsg.nextSequence()
                drawn.append(list(sample.value()))
                weight = sample.weight()
            out.append({"name": f"{RSG} from the {how}, seed {seed}",
                        "inputs": {"settings": st, "generator": RSG, "from": how, "seed": seed, "dimension": DIM,
                                   "sequences": SEQUENCES},
                        "expected": {"nextSequence": [v.enc(s) for s in drawn],
                                     "lastWeight": v.enc(weight), "dimension": rsg.dimension()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/knuthuniformrng.hpp", cases)
