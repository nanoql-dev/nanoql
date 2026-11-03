# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's EvolutionDescription and its numeraire functions
(ql/models/marketmodels/evolutiondescription.hpp, .cpp): the description's members, the three measures and their
tests, and checkCompatibility.

Rate-time grids (ten semi-annual rates, seven quarterly, six irregular, one rate) with the default evolution times,
coarser ones (every other rate time), finer ones (midpoints added) and ones off the rate times, and given relevance
rates: rateTimes, rateTaus, evolutionTimes, firstAliveRate, relevanceRates, numberOfRates and numberOfSteps; then
terminalMeasure, moneyMarketMeasure and moneyMarketPlusMeasure at offsets 0, 1, 2 and the last, and the three tests
and checkCompatibility on each measure and on altered numeraires (a step's numeraire expired, one too few, the last
step's numeraire maturing inside its step). Throws: refused times, a last evolution time past the last fixing time,
relevance rates of the wrong size, an offset past the last bond.

Left out (each undefined in QuantLib and refused by nanoql;
tests/NanoQL.Tests/Divergences/MarketModelUndefinedReadsTests.cs): checkCompatibility with a numeraire past the rate
times before the last step, isInTerminalMeasure on no numeraires, the money-market tests on fewer numeraires than
steps. The refusal's text of a last evolution time past the last fixing time differs, and only the throw is recorded.

The wheel's calls, by QuantLib-SWIG 1.44: EvolutionDescription(rateTimes, evolutionTimes, relevanceRates) and its
members, and the module's functions of the same names. Read back by
tests/NanoQL.Tests/Vectors/Models/MarketModels/EvolutionDescriptionVectorTests.cs."""
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


def evolutions(times):
    fix = times[:-1]
    yield "default evolution times", []
    if len(fix) > 2:
        yield "every other rate time", fix[1::2]
    mid = sorted(set(fix) | {(a + b) / 2 for a, b in zip([0.0] + fix[:-1], fix)})
    yield "midpoints added", mid
    yield "times off the rate times", [t * 0.93 for t in fix]


def members(e):
    return {"rateTimes": list(e.rateTimes()), "rateTaus": list(e.rateTaus()),
            "evolutionTimes": list(e.evolutionTimes()), "firstAliveRate": list(e.firstAliveRate()),
            "relevanceRates": [list(p) for p in e.relevanceRates()], "numberOfRates": e.numberOfRates(),
            "numberOfSteps": e.numberOfSteps()}


def tests(e, numeraires):
    n = e.numberOfRates()
    return {"isInTerminalMeasure": ql.isInTerminalMeasure(e, numeraires),
            "isInMoneyMarketMeasure": ql.isInMoneyMarketMeasure(e, numeraires),
            "isInMoneyMarketPlusMeasure": [ql.isInMoneyMarketPlusMeasure(e, numeraires, o) for o in range(0, n + 1)],
            "checkCompatibility": v.attempt(lambda: ql.checkCompatibility(e, numeraires) or True)}


def cases():
    st = v.settings(R)
    out = []
    for label, times in GRIDS:
        n = len(times) - 1
        for elabel, evol in evolutions(times):
            e = ql.EvolutionDescription(times, evol)
            steps = e.numberOfSteps()
            measures = {"terminal": list(ql.terminalMeasure(e)), "moneyMarket": list(ql.moneyMarketMeasure(e))}
            for o in sorted({0, 1, 2, n}):
                if o <= n:
                    measures[f"moneyMarketPlus{o}"] = list(ql.moneyMarketPlusMeasure(e, o))
            expected = {**members(e), "terminalMeasure": measures["terminal"],
                        "moneyMarketMeasure": measures["moneyMarket"],
                        "moneyMarketPlusMeasures": [measures[k] for k in measures if k.startswith("moneyMarketPlus")],
                        "offsetPastLast": v.attempt(lambda: ql.moneyMarketPlusMeasure(e, n + 1)),
                        "testOffsetPastLast": v.attempt(lambda: ql.isInMoneyMarketPlusMeasure(e, measures["terminal"],
                                                                                           n + 1))}
            numerairesSets = {k: m for k, m in measures.items()}
            mm = measures["moneyMarket"]
            if steps > 1 and mm[0] > 0:
                expired = list(mm)
                expired[0] = mm[0] - 1
                numerairesSets["firstExpired"] = expired
            lastInside = list(measures["terminal"])
            first_alive_last = e.firstAliveRate()[-1]
            lastInside[-1] = first_alive_last
            numerairesSets["lastStepInside"] = lastInside
            numerairesSets["oneTooMany"] = measures["terminal"] + [n]
            sets, results = [], []
            for k, m in numerairesSets.items():
                sets.append({"label": k, "numeraires": m})
                results.append(tests(e, m))
            for key in ("isInTerminalMeasure", "isInMoneyMarketMeasure", "isInMoneyMarketPlusMeasure",
                        "checkCompatibility"):
                expected[key] = [r[key] for r in results]
            expected["shortCompatibility"] = v.attempt(lambda: ql.checkCompatibility(e, measures["terminal"][:-1]))
            out.append({"name": f"{label}, {elabel}",
                        "inputs": {"settings": st, "call": "description", "rateTimes": times, "evolutionTimes": evol,
                                   "numeraires": sets},
                        "expected": v.enc(expected)})
        steps = n
        relevance = [(i, n) for i in range(steps)]
        e = ql.EvolutionDescription(times, [], relevance)
        out.append({"name": f"{label}, given relevance rates",
                    "inputs": {"settings": st, "call": "relevance", "rateTimes": times, "relevanceRates": relevance},
                    "expected": v.enc(members(e))})
        out.append({"name": f"{label}, refused relevance rates",
                    "inputs": {"settings": st, "call": "badRelevance", "rateTimes": times,
                               "relevanceRates": relevance + [(0, n)]},
                    "expected": {"construction": v.attempt(
                        lambda: ql.EvolutionDescription(times, [], relevance + [(0, n)]))}})
        for blabel, evol in (("a last evolution time past the last fixing time", times[:-1] + [times[-1]]),
                             ("decreasing evolution times", [times[0], times[0] / 2]),
                             ("an evolution time at zero", [0.0])):
            out.append({"name": f"{label}, refused: {blabel}",
                        "inputs": {"settings": st, "call": "badEvolution", "rateTimes": times, "evolutionTimes": evol},
                        "expected": {"construction": v.attempt(lambda: ql.EvolutionDescription(times, evol))}})
    for blabel, times in (("one rate time", [1.0]), ("a rate time at zero", [0.0, 1.0])):
        out.append({"name": f"refused: {blabel}",
                    "inputs": {"settings": st, "call": "badEvolution", "rateTimes": times, "evolutionTimes": []},
                    "expected": {"construction": v.attempt(lambda: ql.EvolutionDescription(times))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/evolutiondescription.hpp", cases)
