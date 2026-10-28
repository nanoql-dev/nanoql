# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for RandomizedLDS (ql/math/randomnumbers/randomizedlds.hpp), which the wheel doesn't bind, each output
computed by the C++ harness on a fresh generator over QuantLib's default pseudo-random generator,
RandomSequenceGenerator<MersenneTwisterUniformRng>: over SobolRsg in 1, 5 and 30 dimensions, and over LatticeRsg on
rules A and D at 1,024 points in 30 dimensions (QuantLib's test's configuration), each the first 300 points and their
weights, the randomizer renewed every 64 (which restarts the low-discrepancy sequence). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/RandomNumbers/RandomizedLDSCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

DRAWS = 300
EVERY = 64


def cases():
    out = []
    with cxx.connect() as H:
        for dim, lds_seed, prs_seed in ((1, 1, 2), (5, 42, 7), (30, 123456, 4294967295)):
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            r = H.call("RandomizedLDS<SobolRsg>::draws", dim, lds_seed, prs_seed, DRAWS, EVERY)
            out.append({"name": f"Sobol, {dim} dimensions",
                        "inputs": {"settings": st, "sequence": "Sobol", "dimensionality": dim, "ldsSeed": lds_seed,
                                   "prsSeed": prs_seed, "draws": DRAWS, "every": EVERY},
                        "expected": {"nextSequence": [v.enc(s) for s in r["nextSequence"]],
                                     "weight": v.enc(r["weight"])}})
        for rule in ("A", "D"):
            st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
            r = H.call("RandomizedLDS<LatticeRsg>::draws", rule, 1024, 30, 12345678, DRAWS, EVERY)
            out.append({"name": f"lattice rule {rule}",
                        "inputs": {"settings": st, "sequence": "lattice", "rule": rule, "n": 1024, "dimensionality": 30,
                                   "prsSeed": 12345678, "draws": DRAWS, "every": EVERY},
                        "expected": {"nextSequence": [v.enc(s) for s in r["nextSequence"]],
                                     "weight": v.enc(r["weight"])}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/randomizedlds.hpp", cases)
