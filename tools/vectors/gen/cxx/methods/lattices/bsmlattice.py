# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BlackScholesLattice (ql/methods/lattices/bsmlattice.hpp), which the wheel doesn't expose,
each output computed by the C++ harness (docs/porting-playbook.md §9.4).

Lattices over the Cox-Ross-Rubinstein, Jarrow-Rudd and Leisen-Reimer trees of tools/vectors/gen/cxx/methods/lattices/
binomialtree.py's spec, at two risk-free rates and step counts: riskFreeRate(), dt(), and at every step size(i),
discount(i, j) and underlying(i, j) at every node, descendant(i, j, b) and probability(i, j, b) where a step follows,
stepback(i, values) from seeded values at the next step, and TreeLattice1D's grid(t) at each step's time.

A lattice argument is the harness's spec (tools/cxx/src/methods/lattices/bsmlattice.cpp): {"tree", "riskFreeRate",
"end", "steps"}, the tree binomialtree.hpp's spec. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/Lattices/BlackScholesLatticeCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

REFERENCE = 46310  # 15 October 2026


def tree(kind, steps, strike=100.0):
    return {"type": kind, "referenceDate": REFERENCE, "spot": 100.0, "r": 0.05, "q": 0.02, "volatility": 0.25,
            "end": 1.0, "steps": steps, "strike": strike}


LATTICES = [("CoxRossRubinstein", 4, 0.05), ("CoxRossRubinstein", 6, 0.0), ("JarrowRudd", 5, 0.05),
            ("LeisenReimer", 5, 0.03)]


def cases():
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []
        for kind, steps, rate in LATTICES:
            s = {"tree": tree(kind, steps), "riskFreeRate": rate, "end": 1.0, "steps": steps}
            sizes = [H.call("BlackScholesLattice::size", s, i) for i in range(steps + 1)]
            values = [[round(rnd.uniform(0.0, 30.0), 6) for _ in range(sizes[i + 1])] for i in range(steps)]
            times = [i * 1.0 / steps for i in range(steps + 1)]
            out.append({
                "name": f"{kind}, {steps} steps, rate {rate}",
                "inputs": {"settings": st, "lattice": s, "values": values, "times": times},
                "expected": {
                    "riskFreeRate": H.call("BlackScholesLattice::riskFreeRate", s),
                    "dt": H.call("BlackScholesLattice::dt", s),
                    "size": sizes,
                    "discount": [[H.call("BlackScholesLattice::discount", s, i, j) for j in range(sizes[i])]
                                 for i in range(steps + 1)],
                    "underlying": [[H.call("BlackScholesLattice::underlying", s, i, j) for j in range(sizes[i])]
                                   for i in range(steps + 1)],
                    "descendant": [[[H.call("BlackScholesLattice::descendant", s, i, j, b) for b in range(2)]
                                    for j in range(sizes[i])] for i in range(steps)],
                    "probability": [[[H.call("BlackScholesLattice::probability", s, i, j, b) for b in range(2)]
                                     for j in range(sizes[i])] for i in range(steps)],
                    "stepback": [H.call("BlackScholesLattice::stepback", s, i, values[i]) for i in range(steps)],
                    "grid": [H.call("BlackScholesLattice::grid", s, t) for t in times]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/lattices/bsmlattice.hpp", cases)
