# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LMMCurveState (ql/models/marketmodels/curvestates/lmmcurvestate.hpp, .cpp) and the
CurveState base's swapRate (ql/models/marketmodels/curvestate.hpp, .cpp): every read of states set on forward rates and
on discount ratios.

Four rate-time grids (ten semi-annual rates from 0.5, seven quarterly from 0.25, six irregular times from 0.3, and one
rate from 1 to 2), each state set on forwards, and on the discount ratios of those forwards, from first valid indexes
0, 2 and the last rate (where the grid has them). For each: discountRatio(i, j) for every i and j from the index,
forwardRate, coterminalSwapRate and coterminalSwapAnnuity(numeraire, i) at every numeraire from the index, cmSwapRate
and cmSwapAnnuity(last bond, i, spanning) at spannings 1, 2, 3 and past the rates, the vectors forwardRates(),
coterminalSwapRates() and cmSwapRates(spanning) from the index, and swapRate(begin, end) for every begin from the
index and end after it. Throws: a read below the index, an unset state, a set of the wrong size or at an index past
the rates.

Left out: a rate read at the number of rates, which QuantLib's index check admits, reading past its vectors (nanoql
refuses it; tests/NanoQL.Tests/Divergences/MarketModelUndefinedReadsTests.cs).

The wheel's calls, by QuantLib-SWIG 1.44: LMMCurveState(rateTimes), setOnForwardRates(rates, first),
setOnDiscountRatios(ratios, first), and CurveState's members. Read back by
tests/NanoQL.Tests/Vectors/Models/MarketModels/LMMCurveStateVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

R = ql.Date(15, ql.October, 2026)
GRIDS = [
    ("ten semi-annual rates", [0.5 * (i + 1) for i in range(11)]),
    ("seven quarterly rates", [0.25 * (i + 1) for i in range(8)]),
    ("six irregular rates", [0.3, 0.8, 1.0, 1.75, 2.5, 4.0, 4.6]),
    ("one rate", [1.0, 2.0]),
]
SPANS = [1, 2, 3, 12]


def forwards(n):
    return [0.031 + 0.0023 * i - 0.0004 * (i % 3) for i in range(n)]


def discount_ratios(times, fwds):
    d = [1.0]
    for i, f in enumerate(fwds):
        d.append(d[-1] / (1.0 + f * (times[i + 1] - times[i])))
    return [x / d[-1] * 0.97 for x in d]


def reads(cs, first):
    n = cs.numberOfRates()
    return {
        "discountRatios": [[cs.discountRatio(i, j) for j in range(first, n + 1)] for i in range(first, n + 1)],
        "forwardRates": [cs.forwardRate(i) for i in range(first, n)],
        "coterminalSwapRates": [cs.coterminalSwapRate(i) for i in range(first, n)],
        "coterminalSwapAnnuities": [[cs.coterminalSwapAnnuity(k, i) for i in range(first, n)]
                                    for k in range(first, n + 1)],
        "cmSwapRates": [[cs.cmSwapRate(i, s) for i in range(first, n)] for s in SPANS],
        "cmSwapAnnuities": [[cs.cmSwapAnnuity(n, i, s) for i in range(first, n)] for s in SPANS],
        "forwardRatesVector": list(cs.forwardRates())[first:],
        "coterminalSwapRatesVector": list(cs.coterminalSwapRates())[first:],
        "cmSwapRatesVectors": [list(cs.cmSwapRates(s))[first:] for s in SPANS],
        "swapRates": [[cs.swapRate(b, e) for e in range(b + 1, n + 1)] for b in range(first, n)],
        "numberOfRates": n,
        "rateTimes": list(cs.rateTimes()),
        "rateTaus": list(cs.rateTaus()),
    }


def cases():
    st = v.settings(R)
    out = []
    for label, times in GRIDS:
        n = len(times) - 1
        fwds = forwards(n)
        ratios = discount_ratios(times, fwds)
        for first in sorted({0, min(2, n - 1), n - 1}):
            for kind, values in (("forwards", fwds), ("discountRatios", ratios)):
                cs = ql.LMMCurveState(times)
                if kind == "forwards":
                    cs.setOnForwardRates(fwds, first)
                else:
                    cs.setOnDiscountRatios(ratios, first)
                out.append({"name": f"{label}, set on {kind} from {first}",
                            "inputs": {"settings": st, "call": "reads", "rateTimes": times, "kind": kind,
                                       "values": values, "first": first, "spans": SPANS},
                            "expected": v.enc(reads(cs, first))})
        cs = ql.LMMCurveState(times)
        if n > 1:
            cs.setOnForwardRates(fwds, 1)
            below = {"forwardRate": v.attempt(lambda: cs.forwardRate(0)),
                     "discountRatio": v.attempt(lambda: cs.discountRatio(0, n)),
                     "coterminalSwapAnnuity": v.attempt(lambda: cs.coterminalSwapAnnuity(0, 1)),
                     "cmSwapRate": v.attempt(lambda: cs.cmSwapRate(0, 1))}
            out.append({"name": f"{label}, set from 1, read below it",
                        "inputs": {"settings": st, "call": "below", "rateTimes": times, "values": fwds},
                        "expected": below})
        unset = ql.LMMCurveState(times)
        out.append({"name": f"{label}, unset",
                    "inputs": {"settings": st, "call": "unset", "rateTimes": times},
                    "expected": {"forwardRate": v.attempt(lambda: unset.forwardRate(0)),
                                 "forwardRates": v.attempt(lambda: unset.forwardRates())}})

        def bad_set(values, first, on_ratios):
            def f():
                cs = ql.LMMCurveState(times)
                if on_ratios:
                    cs.setOnDiscountRatios(values, first)
                else:
                    cs.setOnForwardRates(values, first)
            return v.attempt(f)

        out.append({"name": f"{label}, sets refused",
                    "inputs": {"settings": st, "call": "badSets", "rateTimes": times, "values": fwds,
                               "ratios": ratios},
                    "expected": {"shortForwards": bad_set(fwds[:-1], 0, False),
                                 "forwardsPastIndex": bad_set(fwds, n, False),
                                 "shortRatios": bad_set(ratios[:-1], 0, True),
                                 "ratiosPastIndex": bad_set(ratios, n, True)}})
    for label, times in (("one time", [1.0]), ("a time at zero", [0.0, 1.0]), ("decreasing times", [1.0, 0.5])):
        out.append({"name": f"refused rate times: {label}",
                    "inputs": {"settings": st, "call": "badTimes", "rateTimes": times},
                    "expected": {"construction": v.attempt(lambda: ql.LMMCurveState(times))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/curvestates/lmmcurvestate.hpp", cases)
