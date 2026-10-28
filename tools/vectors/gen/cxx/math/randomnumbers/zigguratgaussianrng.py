# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for ZigguratGaussianRng over Xoshiro256StarStarUniformRng (ql/math/randomnumbers/zigguratgaussianrng.hpp) at
the 64-bit seeds the wheel's binding can't pass, each output computed by the C++ harness on a fresh generator: the
first 15,000 draws of next(), enough to reach the tail's draw by hand and the wedges' acceptance, for QuantLib's test's
seed 42 and two seeds beyond 2^63. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/RandomNumbers/ZigguratGaussianRngCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

SEEDS = ["42", "10108360646465513120", "18446744073709551615"]
DRAWS = 15000


def cases():
    out = []
    with cxx.connect() as H:
        for seed in SEEDS:
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            out.append({"name": f"seed {seed}", "inputs": {"settings": st, "seed": seed, "draws": DRAWS},
                        "expected": {"next": H.call("ZigguratGaussianRng<Xoshiro256StarStarUniformRng>::next", seed,
                                                    DRAWS)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/zigguratgaussianrng.hpp", cases)
