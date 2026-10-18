# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the fixing store QuantLib's Index writes and reads (ql/index.hpp, with ql/indexes/indexmanager.hpp's
IndexManager behind it): 40 scenarios, each a sequence of steps through the wheel's index methods addFixing,
addFixings and clearFixings, then the store read back through timeSeries, hasHistoricalFixing, pastFixing and
IndexManager.histories(). Three more store their fixings through vectors.settings(fixings=...), the evaluation
context's key, and take no steps: names differing by ASCII case, names differing by non-ASCII letters, and NaN and
infinite fixings, which the key writes as the vectors encode reals. The first of them also sets the at-par switch
(use_at_par_coupons=False); each of the three records the switch as QuantLib holds it during the case
("usingAtParCoupons"), which the context read back must give.

The scenarios: one add; batches, in date order and reversed, and batches after adds; a year of SOFR business days in
one batch, its fixings and their order drawn with the seed; equal duplicates by addFixing, within a batch and against
a stored fixing; near-equal duplicates, which QuantLib's close() accepts, keeping the stored value (a relative
difference of 1e-15 and of 9e-15, 1e-30 against a stored 0, -0.0 against 0.0), and conflicting ones (a relative
difference of 1e-14, 1e-20 against a stored 0), refused without forceOverwrite and replacing with it, by addFixing,
against a stored fixing in a batch, and within one batch; invalid dates (a Saturday in a batch, a holiday by
addFixing, with and without forceOverwrite, and with a conflicting duplicate in the same batch); weekend fixings on a
calendar with no holidays; a batch with more values than dates, whose surplus values QuantLib doesn't read; empty
batches, which create an empty history; names differing by ASCII case, which share a history kept under the first
spelling, so that a conflicting duplicate and clearFixings reach it through another spelling; names differing by
non-ASCII letters ("é" and "É"), which QuantLib keeps apart; the order of histories(), which compares names with their
ASCII letters upper-cased; clearFixings, of one history among several and with no history; NaN, negative and zero
fixings, and a NaN duplicate, which close() refuses; and the three context scenarios above.

A scenario whose last step is refused records only that throw ("refused": {"throws": true}), since QuantLib stores a
refused batch's valid fixings, and a null entry at an invalid date, before it throws, where nanoql stores nothing.
Every other scenario's outputs are histories()' names, read first; each index's history ("timeSeries", [serial,
value] pairs in date order); hasHistoricalFixing and pastFixing on each index's stored dates; and, last, on the first
index, hasHistoricalFixing and pastFixing at dates with no fixing ("missingHasHistoricalFixing", "missingPastFixing";
pastFixing refuses an invalid date, and returns null, QuantLib's Null<Real>, at a valid one), since the wheel inserts
null entries and empty histories on those reads. Each case runs under vectors.case_settings, which clears the store
after it.

Each case's inputs hold the indexes ("indexes", specs as in indexes/interestrateindex.py, whose functions build them,
with no curve), the steps ("steps": {"op": "addFixing", "index", "date", "value", "forceOverwrite"}, {"op":
"addFixings", "index", "dates", "values", "forceOverwrite"} or {"op": "clearFixings", "index"}, each "index" a
position in "indexes") and the dates with no fixing ("missing"). Read back by
tests/NanoQL.Tests/Vectors/IndexVectorTests.cs."""
import importlib.util
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_interestrateindex",
                                               os.path.join(v.GEN_ROOT, "indexes", "interestrateindex.py"))
irx = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(irx)

TODAY = irx.TODAY


def ibor(family, tenor=(6, ql.Months), cal=irx.TARGET, dc=irx.A360):
    return irx.ibor_spec(family, tenor, 2, cal, ql.ModifiedFollowing, True, dc)


E6 = ibor("Euribor")
E6_UPPER = ibor("EURIBOR")
E6_LOWER = ibor("euribor")
E3 = ibor("Euribor", (3, ql.Months))
SOFR = irx.overnight_spec("Sofr", 0, irx.US_SOFR, irx.A360)
SONIA = irx.overnight_spec("Sonia", 0, irx.UK_EXCHANGE, irx.A365)
NUL = irx.overnight_spec("Nul", 0, irx.NULL_CALENDAR, irx.A360)
# "Ré3M Actual/360", "RÉ3M Actual/360" and "ré3M Actual/360": the first and last differ by an ASCII letter's case.
RE = ibor("Ré", (3, ql.Months))
RE_UPPER = ibor("RÉ", (3, ql.Months))
RE_ASCII = ibor("ré", (3, ql.Months))
ORDER = [ibor(f, (3, ql.Months)) for f in ["b", "A", "a_", "Z", "_x", "[", "`", "É", "é", "Ü"]]


def day(n, month=1, year=2025):
    return ql.Date(n, month, year)


# TARGET business days in January 2025, a Saturday and a Sunday, and two TARGET holidays.
JAN = [day(n) for n in (6, 7, 8, 9, 10, 13, 14, 15)]
SAT, SUN, NEW_YEAR, CHRISTMAS = day(11), day(12), day(1), day(25, 12, 2024)
MISSING = [day(16), SAT, NEW_YEAR]
VALUES = [0.0391, 0.0388, 0.0402, 0.0397, 0.0410, 0.0405, 0.0399, 0.0412]


def add(i, d, x, force=False):
    return {"op": "addFixing", "index": i, "date": d, "value": x, "forceOverwrite": force}


def batch(i, pairs, force=False):
    return {"op": "addFixings", "index": i, "dates": [d for d, _ in pairs], "values": [x for _, x in pairs],
            "forceOverwrite": force}


def clear(i):
    return {"op": "clearFixings", "index": i}


def year_of_fixings():
    """Every SOFR business day of 2024 with a fixing drawn with the seed, in an order drawn with it."""
    rnd = random.Random(v.SEED)
    cal = irx.calendar(irx.US_SOFR)
    days = [d for d in (ql.Date(1, 1, 2024) + n for n in range(366)) if cal.isBusinessDay(d)]
    pairs = [(d, round(rnd.uniform(0.04, 0.056), 6)) for d in days]
    rnd.shuffle(pairs)
    return pairs


def scenarios():
    """(name, indexes, steps, missing dates, context fixings as (index position, pairs) or None)."""
    jan = list(zip(JAN, VALUES))
    return [
        ("one add", [E6], [add(0, day(13), 0.0391)], MISSING, None),
        ("a batch", [E6], [batch(0, jan[:7])], [day(15), SUN, CHRISTMAS], None),
        ("a batch in reverse date order", [E6], [batch(0, jan[::-1])], MISSING, None),
        ("a batch then one add", [E6], [batch(0, jan[:6]), add(0, day(15), 0.0412)], MISSING, None),
        ("two disjoint batches", [E6], [batch(0, jan[:3]), batch(0, jan[5:])], [day(9), day(10), SAT], None),
        ("an equal duplicate by addFixing", [E6], [add(0, day(13), 0.01), add(0, day(13), 0.01)], MISSING, None),
        ("an equal duplicate within a batch", [E6], [batch(0, [(day(13), 0.01), (day(13), 0.01), (day(14), 0.02)])],
         MISSING, None),
        ("an equal duplicate of a stored fixing in a batch", [E6],
         [add(0, day(13), 0.01), batch(0, [(day(13), 0.01), (day(14), 0.02)])], MISSING, None),
        ("a near-equal duplicate at 1e-15 relative", [E6], [add(0, day(13), 0.01), add(0, day(13), 0.01 + 1e-17)],
         MISSING, None),
        ("a near-equal duplicate at 9e-15 relative", [E6],
         [add(0, day(13), 0.01), add(0, day(13), 0.01 * (1 + 9e-15)), add(0, day(14), 0.02)], MISSING, None),
        ("a near-equal duplicate of zero", [E6], [add(0, day(13), 0.0), add(0, day(13), 1e-30)], MISSING, None),
        ("-0.0 after 0.0", [E6], [add(0, day(13), 0.0), batch(0, [(day(13), -0.0), (day(14), -0.0)])], MISSING,
         None),
        ("a conflicting duplicate by addFixing", [E6], [add(0, day(13), 0.01), add(0, day(13), 0.02)], [], None),
        ("a conflicting duplicate at 1e-14 relative", [E6],
         [add(0, day(13), 0.01), add(0, day(13), 0.01 * (1 + 1e-14))], [], None),
        ("a conflicting duplicate of zero", [E6], [add(0, day(13), 0.0), add(0, day(13), 1e-20)], [], None),
        ("a conflicting duplicate by addFixing with forceOverwrite", [E6],
         [add(0, day(13), 0.01), add(0, day(14), 0.03), add(0, day(13), 0.02, True)], MISSING, None),
        ("a conflicting duplicate of a stored fixing in a batch", [E6],
         [add(0, day(13), 0.01), batch(0, [(day(13), 0.02), (day(14), 0.03)])], [], None),
        ("a conflicting duplicate of a stored fixing in a batch with forceOverwrite", [E6],
         [batch(0, jan[:5]), batch(0, [(day(9), 0.05), (day(13), 0.03), (day(7), 0.06)], True)], MISSING, None),
        ("a conflicting duplicate within one batch", [E6], [batch(0, [(day(13), 0.01), (day(13), 0.02)])], [], None),
        ("a conflicting duplicate within one batch with forceOverwrite", [E6],
         [batch(0, [(day(13), 0.01), (day(14), 0.03), (day(13), 0.02)], True)], MISSING, None),
        ("an invalid date in a batch", [E6], [batch(0, [(day(10), 0.01), (SAT, 0.02), (day(13), 0.03)])], [], None),
        ("an invalid date by addFixing", [E6], [add(0, day(13), 0.01), add(0, NEW_YEAR, 0.02)], [], None),
        ("an invalid date in a batch with forceOverwrite", [E6],
         [add(0, day(13), 0.01), batch(0, [(day(14), 0.02), (SUN, 0.03)], True)], [], None),
        ("an invalid date and a conflicting duplicate in one batch", [E6],
         [add(0, day(13), 0.01), batch(0, [(day(13), 0.02), (CHRISTMAS, 0.03)])], [], None),
        ("weekend fixings on a calendar without holidays", [NUL],
         [batch(0, [(SAT, 0.021), (SUN, 0.022), (day(13), 0.023), (NEW_YEAR, 0.024)])], [day(5), day(14), SAT + 7],
         None),
        ("a batch with more values than dates", [E6],
         [{"op": "addFixings", "index": 0, "dates": [day(13), day(14)], "values": [0.01, 0.02, 0.03, 0.04],
           "forceOverwrite": False}], MISSING, None),
        ("an empty batch", [E6], [batch(0, [])], MISSING, None),
        ("an empty batch on a stored history", [E6], [batch(0, jan[:3]), batch(0, [])], MISSING, None),
        ("names differing by ASCII case share a history", [E6, E6_UPPER, E6_LOWER],
         [add(0, day(13), 0.0391), add(1, day(14), 0.0399), batch(2, [(day(15), 0.0412), (day(13), 0.0391)])],
         MISSING, None),
        ("a conflicting duplicate through another ASCII case", [E6, E6_UPPER],
         [add(0, day(13), 0.01), add(1, day(13), 0.02)], [], None),
        ("clearFixings through another ASCII case", [E6, E6_UPPER], [batch(0, jan[:4]), clear(1)], MISSING, None),
        ("names differing by non-ASCII letters are kept apart", [RE, RE_UPPER, RE_ASCII],
         [add(0, day(13), 0.01), add(1, day(13), 0.02), add(2, day(14), 0.03)], MISSING, None),
        ("the order of histories()", ORDER, [add(i, day(13), 0.01 * (i + 1)) for i in range(len(ORDER))], MISSING,
         None),
        ("clearFixings of one history among two", [E6, E3], [batch(0, jan[:4]), batch(1, jan[2:]), clear(0)],
         MISSING, None),
        ("clearFixings with no history", [E6, E3], [batch(1, jan[:2]), clear(0)], MISSING, None),
        ("clearFixings then an add", [E6], [batch(0, jan[:4]), clear(0), add(0, day(14), 0.05)], MISSING, None),
        ("four indexes' histories", [SONIA, E6, SOFR, E3],
         [batch(0, [(day(13), 0.047), (day(14), 0.0471)]), batch(1, jan[:3]),
          batch(2, [(day(10), 0.043), (day(13), 0.0431), (day(14), 0.0432)]), batch(3, jan[4:])],
         [day(20), day(15)], None),
        ("a year of fixings in one batch, in random order", [SOFR], [batch(0, year_of_fixings())],
         [day(28, 11, 2024), day(25, 12, 2024), day(2, 1, 2025)], None),
        ("NaN, negative and zero fixings", [E6],
         [batch(0, [(day(9), float("nan")), (day(10), -0.005), (day(13), 0.0), (day(14), -1e-300)])], MISSING,
         None),
        ("a NaN duplicate", [E6], [add(0, day(13), float("nan")), add(0, day(13), float("nan"))], [], None),
        ("fixings through the context, names differing by ASCII case", [E6, E6_UPPER, E6_LOWER], [], MISSING,
         [(0, jan[:3]), (1, jan[3:6]), (2, jan[6:])]),
        ("fixings through the context, names differing by non-ASCII letters", [RE, RE_UPPER, RE_ASCII], [], MISSING,
         [(0, [(day(13), 0.01)]), (1, [(day(13), 0.02), (day(14), 0.03)])]),
        ("fixings through the context: NaN and infinite values", [E6], [], MISSING,
         [(0, [(day(9), float("nan")), (day(10), float("inf")), (day(13), float("-inf"))])]),
    ]


# The context scenarios that also set the at-par switch, which their cases read back as QuantLib holds it; the others
# leave it at QuantLib-Python's default.
AT_PAR = {"fixings through the context, names differing by ASCII case": False}


def run(idx, step):
    i = idx[step["index"]]
    if step["op"] == "addFixing":
        i.addFixing(step["date"], step["value"], step["forceOverwrite"])
    elif step["op"] == "addFixings":
        i.addFixings(step["dates"], step["values"], step["forceOverwrite"])
    else:
        i.clearFixings()


def encoded(step):
    out = dict(step)
    for k in ("date", "dates", "value", "values"):
        if k in out:
            out[k] = v.enc(out[k])
    return out


def series(i):
    s = i.timeSeries()
    return list(zip(s.dates(), s.values()))


def cases():
    out = []
    for name, specs, steps, missing, context in scenarios():
        idx = [irx.make_index(s) for s in specs]
        fixings = [(idx[k], pairs) for k, pairs in context] if context is not None else None
        with v.case_settings(TODAY, fixings=fixings, use_at_par_coupons=AT_PAR.get(name)) as st:
            inputs = {"settings": st, "indexes": specs, "steps": [encoded(s) for s in steps],
                      "missing": v.enc(missing)}
            for s in steps[:-1]:
                run(idx, s)
            if steps:
                refused = v.attempt(lambda: run(idx, steps[-1]))
                if refused == v.THROWS:
                    out.append({"name": name, "inputs": inputs, "expected": {"refused": refused}})
                    continue
            # histories() first, then each index's history: timeSeries() on a name with no history inserts one.
            names = list(ql.IndexManager.instance().histories())
            stored = [series(i) for i in idx]
            expected = {
                "histories": names,
                "timeSeries": [[[d, x] for d, x in s] for s in v.enc(stored)],
                "hasHistoricalFixing": [[i.hasHistoricalFixing(d) for d, _ in s] for i, s in zip(idx, stored)],
                "pastFixing": [[v.attempt(lambda i=i, d=d: i.pastFixing(d)) for d, _ in s]
                               for i, s in zip(idx, stored)],
                # Last: these reads insert null entries, and an empty history for a name with none.
                "missingHasHistoricalFixing": [idx[0].hasHistoricalFixing(d) for d in missing],
                "missingPastFixing": [v.attempt(lambda d=d: idx[0].pastFixing(d)) for d in missing]}
            if context is not None:
                # The switch as QuantLib holds it during the case, which the context's UseAtParCoupons must equal.
                expected["usingAtParCoupons"] = ql.IborCoupon.usingAtParCoupons()
            out.append({"name": name, "inputs": inputs, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/index.hpp", cases)
