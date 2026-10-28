# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Ranlux3UniformRng and Ranlux4UniformRng (ql/math/randomnumbers/ranluxuniformrng.hpp), which the wheel
doesn't bind, each output computed by the C++ harness on a fresh generator: the first 1,500 draws of next() for seeds
across [1, 4294967295], among them QuantLib's test's two, the standard engines' default 19780503, and the seeds at and
around the seed sequence's modulus 2147483563 (a multiple of which draws seed 1's sequence). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/RandomNumbers/RanluxUniformRngCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

SEEDS = [1, 2938723, 4390109, 19780503, 2147483562, 2147483563, 2147483564, 4294967295]
DRAWS = 1500


def cases():
    out = []
    with cxx.connect() as H:
        for generator in ("Ranlux3UniformRng", "Ranlux4UniformRng"):
            for seed in SEEDS:
                st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
                out.append({"name": f"{generator}, seed {seed}",
                            "inputs": {"settings": st, "generator": generator, "seed": seed, "draws": DRAWS},
                            "expected": {"next": H.call(f"{generator}::next", seed, DRAWS)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/ranluxuniformrng.hpp", cases)
