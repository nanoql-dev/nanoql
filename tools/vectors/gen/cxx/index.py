# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's Index (ql/index.hpp) that the wheel doesn't reach, each output computed by
the C++ harness: allowsNativeFixings() on Ibor and overnight indexes and on the harness's index without native
fixings, whose every writer QuantLib refuses (addFixing, both addFixings and clearFixings); and addFixings(const
TimeSeries<Real>&, bool forceOverwrite), with and without forceOverwrite, in 17 scenarios read back through the store.

The scenarios: a series on an empty store, in date order and reversed, and with a date given twice (the series keeps
its last value); an empty series, which creates an empty history; a series after stored fixings, disjoint, with
equal and near-equal duplicates (kept), with a conflicting duplicate (refused, or replacing with forceOverwrite), and
with an invalid date (refused with and without forceOverwrite); series through names differing by ASCII case, which
share a history, and by a non-ASCII letter, which don't; two indexes' series; overnight series on the SOFR calendar
(one of a quarter's business days, with fixings drawn with the seed), and weekend dates on a calendar with no
holidays. A scenario whose last step is refused records only that throw ("refused"),
since QuantLib stores a refused series' valid fixings before it throws, where nanoql stores nothing; every other
scenario's outputs are IndexManager::histories(), read first, and each index's history ("timeSeries", [serial,
value] pairs in date order).

Each case's inputs hold the indexes ("indexes", specs as in tools/vectors/gen/indexes/interestrateindex.py, whose
functions build them, or {"type": "NoNativeFixingsIndex", "name"}) and the steps ("steps", as in
tools/vectors/gen/index.py, with the op "addFixings(TimeSeries)" for the overload taking a series), or, for the
refusals, the writers each tried on the empty store ("refusals"). Each case runs under the harness's case_settings,
which clears the store after it. tests/NanoQL.Tests/Vectors/Cxx/IndexCxxVectorTests.cs reads it back."""
import importlib.util
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_interestrateindex",
                                               os.path.join(v.GEN_ROOT, "indexes", "interestrateindex.py"))
irx = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(irx)

TODAY = v.enc(irx.TODAY)


def ibor(family, tenor=(6, ql.Months), cal=irx.TARGET):
    return irx.ibor_spec(family, tenor, 2, cal, ql.ModifiedFollowing, True, irx.A360)


E6 = ibor("Euribor")
E6_UPPER = ibor("EURIBOR")
E3 = ibor("Euribor", (3, ql.Months))
RE = ibor("Ré", (3, ql.Months))
RE_UPPER = ibor("RÉ", (3, ql.Months))
SOFR = irx.overnight_spec("Sofr", 0, irx.US_SOFR, irx.A360)
NUL = irx.overnight_spec("Nul", 0, irx.NULL_CALENDAR, irx.A360)
STUB = {"type": "NoNativeFixingsIndex", "name": "NoNative3M Actual/360"}


def day(n, month=1, year=2025):
    return v.enc(ql.Date(n, month, year))


# TARGET business days in January 2025, a Saturday, and New Year's Day, a holiday on every calendar here but one.
JAN = [day(n) for n in (6, 7, 8, 9, 10, 13, 14, 15)]
SAT, NEW_YEAR = day(11), day(1)
VALUES = [0.0291, 0.0288, 0.0302, 0.0297, 0.0310, 0.0305, 0.0299, 0.0312]


def step(op, i, pairs, force=False):
    return {"op": op, "index": i, "dates": [d for d, _ in pairs], "values": [x for _, x in pairs],
            "forceOverwrite": force}


def series(i, pairs, force=False):
    return step("addFixings(TimeSeries)", i, pairs, force)


def batch(i, pairs, force=False):
    return step("addFixings", i, pairs, force)


def quarter():
    """Every SOFR business day of the last quarter of 2024, with fixings drawn with the seed."""
    rnd = random.Random(v.SEED)
    cal = irx.calendar(irx.US_SOFR)
    days = [d for d in (ql.Date(1, 10, 2024) + n for n in range(92)) if cal.isBusinessDay(d)]
    return [(v.enc(d), round(rnd.uniform(0.042, 0.049), 6)) for d in days]


def scenarios():
    jan = list(zip(JAN, VALUES))
    return [
        ("a series on an empty store", [E6], [series(0, jan)]),
        ("a series in reverse date order", [E6], [series(0, jan[::-1])]),
        ("a series with a date given twice", [E6], [series(0, [(JAN[0], 0.01), (JAN[1], 0.02), (JAN[0], 0.03)])]),
        ("an empty series", [E6], [series(0, [])]),
        ("an empty series on a stored history", [E6], [batch(0, jan[:3]), series(0, [])]),
        ("a series after stored fixings", [E6], [batch(0, jan[:4]), series(0, jan[4:])]),
        ("a series with equal and near-equal duplicates", [E6],
         [batch(0, jan[:4]), series(0, [(JAN[1], VALUES[1]), (JAN[2], VALUES[2] * (1 + 5e-15)), (JAN[5], 0.04)])]),
        ("a series with a conflicting duplicate", [E6],
         [batch(0, jan[:4]), series(0, [(JAN[2], 0.05), (JAN[6], 0.04)])]),
        ("a series with a conflicting duplicate and forceOverwrite", [E6],
         [batch(0, jan[:4]), series(0, [(JAN[2], 0.05), (JAN[6], 0.04)], True)]),
        ("a series with an invalid date", [E6], [series(0, [(JAN[3], 0.01), (SAT, 0.02), (JAN[5], 0.03)])]),
        ("a series with an invalid date and forceOverwrite", [E6],
         [batch(0, jan[:2]), series(0, [(NEW_YEAR, 0.02), (JAN[0], 0.03)], True)]),
        ("series through names differing by ASCII case", [E6, E6_UPPER],
         [series(0, jan[:3]), series(1, jan[3:6]), series(1, jan[1:2])]),
        ("series through names differing by a non-ASCII letter", [RE, RE_UPPER],
         [series(0, jan[:3]), series(1, [(JAN[0], 0.07), (JAN[7], 0.08)])]),
        ("two indexes' series", [E6, E3], [series(1, jan[4:]), series(0, jan[:5])]),
        ("a quarter's series", [SOFR], [series(0, quarter())]),
        ("an overnight index's series", [SOFR], [series(0, [(day(13), 0.043), (day(14), 0.0431), (day(17), 0.0432)])]),
        ("weekend dates on a calendar without holidays", [NUL],
         [series(0, [(SAT, 0.021), (day(12), 0.022), (NEW_YEAR, 0.023)])]),
    ]


def run(H, specs, s):
    spec = specs[s["index"]]
    if s["op"] == "addFixing":
        H.call("Index::addFixing", spec, s["date"], s["value"], s["forceOverwrite"])
    elif s["op"] == "addFixings":
        H.call("Index::addFixings(DateIterator,DateIterator,ValueIterator,bool)", spec, s["dates"], s["values"],
               s["forceOverwrite"])
    elif s["op"] == "addFixings(TimeSeries)":
        H.call("Index::addFixings(TimeSeries,bool)", spec, s["dates"], s["values"], s["forceOverwrite"])
    else:
        H.call("Index::clearFixings", spec)


def cases():
    with cxx.connect() as H:
        out = []
        specs = [E6, E3, irx.ibor_spec("Tibor", (3, ql.Months), 2, irx.UK_EXCHANGE, ql.Following, False, irx.A365),
                 SOFR, NUL, STUB]
        with H.case_settings(TODAY) as st:
            out.append({"name": "allowsNativeFixings", "inputs": {"settings": st, "indexes": specs},
                        "expected": {"allowsNativeFixings": [H.call("Index::allowsNativeFixings", s) for s in specs]}})
        refusals = [{"op": "addFixing", "index": 0, "date": JAN[0], "value": 0.01, "forceOverwrite": False},
                    batch(0, [(JAN[0], 0.01)]), series(0, [(JAN[0], 0.01)], True), {"op": "clearFixings", "index": 0}]
        with H.case_settings(TODAY) as st:
            refused = [v.attempt(lambda s=s: run(H, [STUB], s)) for s in refusals]
            out.append({"name": "the writers refuse an index without native fixings",
                        "inputs": {"settings": st, "indexes": [STUB], "refusals": refusals},
                        "expected": {"refused": refused, "histories": H.call("IndexManager::histories")}})
        for name, idx, steps in scenarios():
            with H.case_settings(TODAY) as st:
                inputs = {"settings": st, "indexes": idx, "steps": steps}
                for s in steps[:-1]:
                    run(H, idx, s)
                last = v.attempt(lambda: run(H, idx, steps[-1]))
                if last == v.THROWS:
                    out.append({"name": name, "inputs": inputs, "expected": {"refused": last}})
                    continue
                # histories() first: timeSeries() on a name with no history inserts one.
                names = H.call("IndexManager::histories")
                out.append({"name": name, "inputs": inputs, "expected": {
                    "histories": names, "timeSeries": [H.call("Index::timeSeries", s) for s in idx]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/index.hpp", cases)
