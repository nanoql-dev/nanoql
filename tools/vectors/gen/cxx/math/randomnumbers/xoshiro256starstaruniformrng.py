# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for Xoshiro256StarStarUniformRng (ql/math/randomnumbers/xoshiro256starstaruniformrng.hpp) at what the wheel's
binding can't pass, each output computed by the C++ harness on a fresh generator: 64-bit seeds (QuantLib's test's two,
both beyond 2^63, and the range's edges) and the four-word state constructor (QuantLib's test's state and two others),
each the first 1,000 draws of nextInt64, as decimal strings, and of nextReal. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/RandomNumbers/Xoshiro256StarStarUniformRngCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

SEEDS = ["1", "42", "2147483648", "4294967296", "9223372036854775807", "9223372036854775808",
         "10108360646465513120", "16880566536755896171", "18446744073709551615"]
STATES = [["18274946675476036270", "6043068446171522962", "96311065249897859", "16504445955133574805"],
          ["1", "0", "0", "0"],
          ["18446744073709551615", "18446744073709551615", "18446744073709551615", "18446744073709551615"]]
DRAWS = 1000


def cases():
    out = []
    with cxx.connect() as H:
        specs = ([(f"seed {s}", {"seed": s}) for s in SEEDS]
                 + [(f"state {i}", {"state": s}) for i, s in enumerate(STATES)])
        for name, spec in specs:
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            out.append({"name": name, "inputs": {"settings": st, "generator": spec, "draws": DRAWS},
                        "expected": {"nextInt64": H.call("Xoshiro256StarStarUniformRng::nextInt64", spec, DRAWS),
                                     "nextReal": H.call("Xoshiro256StarStarUniformRng::nextReal", spec, DRAWS)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/xoshiro256starstaruniformrng.hpp", cases)
