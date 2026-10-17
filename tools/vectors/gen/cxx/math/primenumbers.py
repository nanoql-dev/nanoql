# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for PrimeNumbers::get (ql/math/primenumbers.hpp), which the wheel doesn't expose
(docs/porting-playbook.md §9.4), each output computed by the C++ harness: 500 indices up to 21,200, the number of
dimensions QuantLib's Sobol and Halton tables reach, among them every index of Jäckel's table of first primes (0 to
14), the first computed ones after it, and an even spread with a fixed random sample above. The harness's QuantLib
keeps the primes it has computed between calls, which nanoql recomputes per call; the indices go up and down, so that
QuantLib answers some from its list and extends it for others.
tests/NanoQL.Tests/Vectors/Cxx/Numerics/PrimeNumbersCxxVectorTests.cs reads it back."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

MAX_INDEX = 21200


def indices():
    first = list(range(40))
    spread = [round(40 + (MAX_INDEX - 40) * i / 259) for i in range(260)]
    rng = random.Random(v.SEED)
    sample = [rng.randrange(MAX_INDEX + 1) for _ in range(200)]
    out = first + spread + sample
    assert len(out) == 500 and max(out) == MAX_INDEX
    return out


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        ix = indices()
        return [{"name": "indices", "inputs": {"settings": st, "absoluteIndex": ix},
                 "expected": {"get": [v.attempt(lambda i=i: H.call("PrimeNumbers::get", i)) for i in ix]}}]


if __name__ == "__main__":
    v.main(__file__, "ql/math/primenumbers.hpp", cases)
