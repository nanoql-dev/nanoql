# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SwapSpreadIndex (ql/experimental/coupons/swapspreadindex.hpp, .cpp), a swap-rate spread
index: on four pairs of tools/vectors/gen/indexes/swapindex.py's swap indexes with gearings (1, -1), (2, -0.5),
(1, 1) and (0.5, 0.25), the name (its gearings to four decimals), family name, tenor, fixing days, calendar, currency
and day counter, the gearings, the forecast and the fixing at three future dates, and with the two indexes' fixings
stored on one past date and the first's alone on another, the past fixings (null where one is missing) and the
fixing on the first; maturityDate's refusal; and the constructor's six refusals, an index pair differing in fixing
days, calendar, currency, day counter, fixed leg tenor or fixed leg convention, each as {"throws": true}.

Each case's inputs "index1" and "index2" are swapindex.py's specs, "familyName", "gearing1" and "gearing2". The wheel's
SwapSpreadIndex(familyName, swapIndex1, swapIndex2, gearing1, gearing2) is the C++ constructor. Read back by
tests/NanoQL.Tests/Vectors/Experimental/Coupons/SwapSpreadIndexVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


si = _load("swapindex", "indexes/swapindex.py")
TODAY = si.TODAY
CAL = ql.TARGET()
FUTURE = [CAL.advance(TODAY, 1, ql.Months), CAL.advance(TODAY, 1, ql.Years), CAL.advance(TODAY, 5, ql.Years)]
PAST_BOTH = CAL.advance(TODAY, -10, ql.Days)
PAST_FIRST = CAL.advance(TODAY, -20, ql.Days)
SPECS = dict(si.indexes())
E6M = SPECS["2Y on Euribor 6M"]["iborIndex"]


def custom(currency="EUR", convention=ql.ModifiedFollowing):
    return si.swap_index("EurSwap", (5, ql.Years), 2, currency, si.TARGET, (1, ql.Years), convention, si.THIRTY360_BB, E6M)


PAIRS = [
    ("2Y against 10Y", SPECS["10Y, simple curve"], SPECS["2Y on Euribor 6M"], 1.0, -1.0),
    ("30Y against 5Y, geared", SPECS["30Y, negative curve"], SPECS["5Y on Euribor 6M"], 2.0, -0.5),
    ("a sum", SPECS["5Y on Euribor 6M"], SPECS["24M"], 1.0, 1.0),
    ("an exogenous pair", SPECS["5Y exogenous"], SPECS["10Y exogenous, simple"], 0.5, 0.25),
]
REFUSALS = [
    ("refused: fixing days", SPECS["5Y on Euribor 6M"], SPECS["7Y, no settlement days"]),
    ("refused: fixing calendar", SPECS["5Y on Euribor 6M"], SPECS["15Y on the US SOFR calendar, Euribor 6M"]),
    ("refused: currency", SPECS["5Y on Euribor 6M"], custom(currency="USD")),
    ("refused: day counter", SPECS["5Y on Euribor 6M"], SPECS["6Y, Preceding, Actual/360"]),
    ("refused: fixed leg tenor", SPECS["5Y on Euribor 6M"], SPECS["18M, semiannual fixed leg"]),
    ("refused: fixed leg convention", SPECS["5Y on Euribor 6M"], custom(convention=ql.Following)),
]


def fixings(i1, i2):
    return [(i1, [(PAST_BOTH, 0.0312), (PAST_FIRST, 0.0305)]), (i2, [(PAST_BOTH, 0.0221)])]


def case(name, s1, s2, g1, g2):
    i1, i2 = si.make_swap_index(s1), si.make_swap_index(s2)
    with v.case_settings(TODAY, fixings=fixings(i1, i2)) as st:
        inputs = {"settings": st, "familyName": "CmsSpread", "index1": s1, "index2": s2, "gearing1": g1,
                  "gearing2": g2, "future": v.enc(FUTURE), "pastBoth": v.enc(PAST_BOTH), "pastFirst": v.enc(PAST_FIRST)}
        idx = ql.SwapSpreadIndex("CmsSpread", i1, i2, g1, g2)
        past_first = idx.pastFixing(PAST_FIRST)
        expected = {
            "name": idx.name(), "familyName": idx.familyName(), "tenor": [idx.tenor().length(), int(idx.tenor().units())],
            "fixingDays": idx.fixingDays(), "fixingCalendar": idx.fixingCalendar().name(),
            "currency": idx.currency().code(), "dayCounter": idx.dayCounter().name(),
            "gearing1": idx.gearing1(), "gearing2": idx.gearing2(),
            "forecastFixing": [v.enc(idx.forecastFixing(d)) for d in FUTURE],
            "fixing": [v.enc(idx.fixing(d)) for d in FUTURE],
            "pastFixingBoth": v.enc(idx.pastFixing(PAST_BOTH)),
            "pastFixingFirst": None if past_first is None else v.enc(past_first),
            "fixingPast": v.enc(idx.fixing(PAST_BOTH)),
            "maturityDate": v.attempt(lambda: idx.maturityDate(FUTURE[0])),
        }
        return {"name": name, "inputs": inputs, "expected": expected}


def refusal(name, s1, s2):
    i1, i2 = si.make_swap_index(s1), si.make_swap_index(s2)
    with v.case_settings(TODAY) as st:
        try:
            ql.SwapSpreadIndex("CmsSpread", i1, i2)
        except RuntimeError:
            return {"name": name, "inputs": {"settings": st, "familyName": "CmsSpread", "index1": s1, "index2": s2,
                                             "gearing1": 1.0, "gearing2": -1.0},
                    "expected": {"construct": dict(v.THROWS)}}
        raise AssertionError(name)


def cases():
    return [case(*p) for p in PAIRS] + [refusal(*r) for r in REFUSALS]


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/coupons/swapspreadindex.hpp", cases)
