# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the random-number traits (ql/math/randomnumbers/rngtraits.hpp), which the wheel doesn't expose
(docs/porting-playbook.md §9.4), each output computed by the C++ harness. make_sequence_generator of PseudoRandom,
PoissonPseudoRandom and LowDiscrepancy, with icInstance unset, for seeds 1, 42 and 4294967295 at dimension 5: the
first 50 nextSequence draws and the generator's dimension. With icInstance set (average 0.5 and sigma 2 for the
normal ones, lambda 4 for the Poisson one), for the three and for GenericLowDiscrepancy<HaltonRsg,
InverseCumulativeNormal>, seed 42: the first 10 draws, the lastSequence and its weight, and the dimension; the same
Halton traits with icInstance unset, seed 1472677. Each traits' allowsErrorEstimate; and dimension 0, which
PseudoRandom's RandomSequenceGenerator accepts and LowDiscrepancy's SobolRsg refuses. The harness sets icInstance for
one call and resets it after, so every case reads only its own. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Numerics/RandomNumbers/RngTraitsCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

DIMENSION = 5
HALTON = "GenericLowDiscrepancy<HaltonRsg,InverseCumulativeNormal>"
NORMAL = {"average": 0.5, "sigma": 2.0}
TRAITS = [("PseudoRandom", NORMAL), ("PoissonPseudoRandom", {"lambda": 4.0}), ("LowDiscrepancy", NORMAL)]
SEEDS = [1, 42, 4294967295]
DRAWS = 50
IC_SEED = 42
IC_DRAWS = 10
HALTON_SEED = 1472677


def cases():
    out = []
    with cxx.connect() as H:
        def context():
            return H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        for traits, _ in TRAITS:
            for seed in SEEDS:
                st = context()
                r = H.call(f"{traits}::make_sequence_generator", DIMENSION, seed, DRAWS, None)
                out.append({"name": f"{traits}, seed {seed}",
                            "inputs": {"settings": st, "traits": traits, "dimension": DIMENSION, "seed": seed,
                                       "draws": DRAWS, "inverseCumulative": None},
                            "expected": {"nextSequence": v.enc(r["nextSequence"]), "dimension": r["dimension"]}})
        for traits, ic, seed in [(t, ic, IC_SEED) for t, ic in TRAITS] + [(HALTON, NORMAL, IC_SEED),
                                                                          (HALTON, None, HALTON_SEED)]:
            st = context()
            r = H.call(f"{traits}::make_sequence_generator", DIMENSION, seed, IC_DRAWS, ic)
            out.append({"name": f"{traits}, seed {seed}, inverse cumulative {ic}",
                        "inputs": {"settings": st, "traits": traits, "dimension": DIMENSION, "seed": seed,
                                   "draws": IC_DRAWS, "inverseCumulative": ic},
                        "expected": {"nextSequence": v.enc(r["nextSequence"]),
                                     "lastSequence": v.enc(r["lastSequence"]), "lastWeight": v.enc(r["lastWeight"]),
                                     "dimension": r["dimension"]}})
        st = context()
        out.append({"name": "allowsErrorEstimate", "inputs": {"settings": st},
                    "expected": {t: H.call(f"{t}::allowsErrorEstimate") for t in [t for t, _ in TRAITS] + [HALTON]}})
        st = context()
        out.append({"name": "dimension 0", "inputs": {"settings": st, "dimension": 0, "seed": 1},
                    "expected": {t: v.attempt(lambda t=t: H.call(f"{t}::make_sequence_generator", 0, 1, 1,
                                                                 None)["dimension"])
                                 for t in ("PseudoRandom", "LowDiscrepancy")}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/randomnumbers/rngtraits.hpp", cases)
