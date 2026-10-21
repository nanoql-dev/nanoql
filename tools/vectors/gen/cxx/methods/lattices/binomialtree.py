# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's binomial trees (ql/methods/lattices/binomialtree.hpp, .cpp), which the wheel doesn't expose,
each output computed by the C++ harness (docs/porting-playbook.md §9.4).

The seven trees, Jarrow-Rudd, Cox-Ross-Rubinstein, the additive equal-probabilities tree, Trigeorgis, Tian,
Leisen-Reimer and Joshi's, on Black-Scholes-Merton processes of flat forwards and a constant volatility from 15 October
2026 on Actual/365 (Fixed): a typical set at 4 and 7 steps, a high volatility at 5, and a negative drift at 6. The
Leisen-Reimer and Joshi trees build an even count on the next odd one, which their columns show. For each: the branches
constant, columns(), and at every column size(i) and underlying(i, j) at every node, and, at every column with a step
after it, descendant(i, j, b) and probability(i, j, b) at every node and branch. Then the constructors' refusals, read
through columns(): a drift too large for the Cox-Ross-Rubinstein tree's probabilities ("negative probability") and for
the additive tree ("drift per step too large for the additive EQP tree: more steps are needed"), and a zero strike for
the Leisen-Reimer and Joshi trees ("strike must be positive").

A tree argument is the harness's spec (tools/cxx/src/methods/lattices/binomialtree.hpp): {"type", "referenceDate",
"spot", "r", "q", "volatility", "end", "steps", "strike"}. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/Lattices/BinomialTreeCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

REFERENCE = 46310  # 15 October 2026
TYPES = ["JarrowRudd", "CoxRossRubinstein", "AdditiveEQPBinomialTree", "Trigeorgis", "Tian", "LeisenReimer", "Joshi4"]


def spec(kind, steps, spot=100.0, r=0.05, q=0.02, vol=0.2, end=1.0, strike=100.0):
    return {"type": kind, "referenceDate": REFERENCE, "spot": spot, "r": r, "q": q, "volatility": vol, "end": end,
            "steps": steps, "strike": strike}


SETS = [("typical, 4 steps", {"steps": 4}), ("typical, 7 steps", {"steps": 7}),
        ("high volatility, 5 steps", {"steps": 5, "vol": 0.6, "end": 2.0, "strike": 120.0}),
        ("negative drift, 6 steps", {"steps": 6, "r": 0.0, "q": 0.06, "spot": 50.0, "strike": 45.0, "end": 0.5})]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))
        out = []
        for kind in TYPES:
            for name, kw in SETS:
                s = spec(kind, **kw)
                columns = H.call("BinomialTree::columns", s)
                expected = {"branches": H.call("BinomialTree::branches"), "columns": columns,
                            "size": [H.call("BinomialTree::size", s, i) for i in range(columns)]}
                expected["underlying"] = [[H.call("BinomialTree::underlying", s, i, j) for j in range(expected["size"][i])]
                                          for i in range(columns)]
                expected["descendant"] = [[[H.call("BinomialTree::descendant", s, i, j, b) for b in range(2)]
                                           for j in range(expected["size"][i])] for i in range(columns - 1)]
                expected["probability"] = [[[H.call("BinomialTree::probability", s, i, j, b) for b in range(2)]
                                            for j in range(expected["size"][i])] for i in range(columns - 1)]
                out.append({"name": f"{kind}, {name}", "inputs": {"settings": st, "tree": s}, "expected": expected})
        refusals = [("CoxRossRubinstein, drift too large", spec("CoxRossRubinstein", 3, r=0.3, q=0.0, vol=0.01)),
                    ("AdditiveEQPBinomialTree, drift too large", spec("AdditiveEQPBinomialTree", 3, r=0.3, q=0.0, vol=0.01)),
                    ("LeisenReimer, zero strike", spec("LeisenReimer", 5, strike=0.0)),
                    ("Joshi4, zero strike", spec("Joshi4", 5, strike=0.0))]
        for name, s in refusals:
            out.append({"name": name, "inputs": {"settings": st, "tree": s},
                        "expected": {"columns": v.attempt(lambda: H.call("BinomialTree::columns", s))}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/lattices/binomialtree.hpp", cases)
