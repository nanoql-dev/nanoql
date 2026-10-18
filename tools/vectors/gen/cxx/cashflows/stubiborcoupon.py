# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for StubIborCoupon::WeightedIndex (ql/cashflows/stubiborcoupon.hpp), which the wheel doesn't bind, each
output computed by the C++ harness, on the evaluation date 15 January 2025 and Euribor-style 1W, 1M, 3M and 6M indexes
on flat curves of their own.

16 combinations: two components weighted by exact binary ties at the fifth decimal (1/32, 3/32, 5/32, 7/32, 9/32 and
31/32, which QuantLib's std::fixed with setprecision(4) rounds half to even, as .NET's "F4" does), and by 1/2, 1/3,
0.12345 and 0; one component; three; weights summing to other than 1, a negative one among them; and a first
component shorter than a later one. Each case's outputs are the index's inspectors (name, family name, tenor, fixing
days, day counter, business-day convention, end of month, allowsNativeFixings, and the components' names and
weights), maturityDate at three value dates, forecastFixing at two later fixing dates, pastFixing at three earlier
ones (a fixing stored for every component, for one only, which gives null, QuantLib's Null<Real>(), and for none),
fixing at an earlier date, the evaluation date (a fixing stored for one component only, so the other is forecast) and a
later one, and clone, which QuantLib always refuses. Four more cases are refused by the constructor: no components,
and components whose fixing days, fixing calendars or day counters differ ("index": {"throws": true}).

A refused value is {"throws": true}. Each case's inputs hold the components ("components", [[index, weight], ...],
each index a spec of tools/vectors/gen/indexes/interestrateindex.py) and the dates ("valueDates", "forecastDates",
"pastDates", "fixingDates"). tests/NanoQL.Tests/Vectors/Cxx/Coupons/StubIborCouponCxxVectorTests.cs reads it back."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_iborcoupon", os.path.join(v.GEN_ROOT, "cashflows", "iborcoupon.py"))
ibc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ibc)

irx = ibc.irx
TODAY = ibc.TODAY
d = ibc.d

E1W = ibc.euribor((1, ql.Weeks), irx.curve_spec(TODAY, 0.025))
E1M = ibc.euribor((1, ql.Months), irx.curve_spec(TODAY, 0.028))
E3M = ibc.euribor((3, ql.Months), irx.curve_spec(TODAY, 0.031))
E6M = ibc.euribor((6, ql.Months), irx.curve_spec(TODAY, 0.034))
E3M_1D = dict(E3M, settlementDays=1, familyName="EuriborT1")
E3M_NUL = dict(E3M, fixingCalendar=ibc.NULL_CALENDAR, familyName="EuriborNul")
E3M_365 = dict(E3M, dayCounter=ibc.A365, familyName="Euribor365")
VALUE_DATES = [d(17, 1, 2025), d(31, 1, 2025), d(28, 2, 2025)]
FORECAST_DATES = [d(16, 1, 2025), d(17, 3, 2025)]
PAST_DATES = [d(13, 1, 2025), d(10, 1, 2025), d(9, 1, 2025)]
FIXING_DATES = [d(13, 1, 2025), TODAY, d(20, 1, 2025)]


def combinations():
    out = []
    for w in (1 / 32, 3 / 32, 5 / 32, 7 / 32, 9 / 32, 31 / 32, 0.5, 1 / 3, 0.12345, 0.0):
        out.append([[E1M, 1.0 - w], [E3M, w]])
    out += [[[E6M, 1.0]], [[E1W, 0.2], [E1M, 0.3], [E3M, 0.5]], [[E1M, 1.5], [E3M, -0.5]], [[E3M, 0.75], [E1M, 0.25]],
            [[E1W, 0.6], [E6M, 0.4]], [[E1M, 0.40625], [E6M, 0.59375]]]
    return out


REFUSED = [[], [[E3M, 0.5], [E3M_1D, 0.5]], [[E3M, 0.5], [E3M_NUL, 0.5]], [[E3M, 0.5], [E3M_365, 0.5]]]


def fixings(H):
    """Every component's fixings on 13 January; the first two's on 10 January; none on 9 January; the 1M index's
    today."""
    out = {}
    for spec in (E1W, E1M, E3M, E6M):
        name = H.call("Index::name", spec)
        pairs = [[v.enc(d(13, 1, 2025)), 0.0291]]
        if spec in (E1W, E1M):
            pairs.append([v.enc(d(10, 1, 2025)), 0.0287])
        if spec is E1M:
            pairs.append([v.enc(TODAY), 0.0279])
        out[name] = sorted(pairs)
    return out


def cases():
    with cxx.connect() as H:
        out = []
        dates = {"valueDates": v.enc(VALUE_DATES), "forecastDates": v.enc(FORECAST_DATES),
                 "pastDates": v.enc(PAST_DATES), "fixingDates": v.enc(FIXING_DATES)}
        for k, components in enumerate(combinations()):
            with H.case_settings(v.enc(TODAY), fixings=fixings(H)) as st:
                index = H.call("StubIborCoupon::WeightedIndex", components)
                expected = dict(index)

                def at(method, xs):
                    return [v.attempt(lambda x=x: H.call(f"StubIborCoupon::WeightedIndex::{method}", components, x))
                            for x in xs]

                expected["maturityDate"] = at("maturityDate", dates["valueDates"])
                expected["forecastFixing"] = at("forecastFixing", dates["forecastDates"])
                expected["pastFixing"] = at("pastFixing", dates["pastDates"])
                expected["fixing"] = at("fixing", dates["fixingDates"])
                expected["clone"] = v.attempt(lambda: H.call("StubIborCoupon::WeightedIndex::clone", components))
                out.append({"name": f"{k}: {index['name']}", "inputs": {"settings": st, "components": components,
                                                                        **dates},
                            "expected": expected})
        for k, components in enumerate(REFUSED):
            with H.case_settings(v.enc(TODAY)) as st:
                out.append({"name": f"refused {k}", "inputs": {"settings": st, "components": components, **dates},
                            "expected": {"index": v.attempt(
                                lambda: H.call("StubIborCoupon::WeightedIndex", components))}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/stubiborcoupon.hpp", cases)
