# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's OvernightIndexFuture (ql/instruments/overnightindexfuture.hpp): 21 futures on the
evaluation date 15 January 2025 and 4 refusals, on SOFR-style (the US SOFR calendar) and ESTR-style (TARGET) overnight
indexes, Actual/360, forecasting on flat curves whose reference date is the evaluation date.

The futures are compounded and averaged; monthly and quarterly; starting after the evaluation date, on it (today's
fixing published or not) and before it (every past fixing stored, today's published or not); with a convexity
adjustment; maturing on a holiday (Juneteenth); and one expired, its maturity before the evaluation date, worth 0. A
stored fixing is 4.3% plus a basis point per business day from the value date (3.1% on ESTR), today's 7%. The refusals:
a past fixing missing ("missing rate on"), an index with no forwarding curve, and a forwarding curve that starts after
the evaluation date, compounded and averaged (QuantLib's master words the last two its #2897's way, nanoql's texts).

A future's outputs are its NPV, 100 times one less its rate plus the convexity adjustment, the adjustment, and whether
it has expired. Generated and left out: the compounded futures whose value date is the evaluation date with today's
fixing published, whose NPV QuantLib computes without that fixing, where the averaged future uses it
(tests/NanoQL.Tests/Divergences/OvernightFutureTodayFixingTests.cs). The index, value date and maturity, which the wheel
hides, are in tools/vectors/gen/cxx/instruments/overnightindexfuture.py.

A case's inputs hold its settings with the stored fixings, the index (an overnight spec of
tools/vectors/gen/indexes/interestrateindex.py), the value and maturity dates, the averaging method and the convexity
adjustment. The wheel's OvernightIndexFuture is the proxy of the C++ constructor, the adjustment a quote handle. Read
back by tests/NanoQL.Tests/Vectors/Instruments/OvernightIndexFutureVectorTests.cs."""
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


irx = _load("wheel_interestrateindex", "indexes/interestrateindex.py")

D = ql.Date
TODAY = irx.TODAY
COMPOUND, SIMPLE = int(ql.RateAveraging.Compound), int(ql.RateAveraging.Simple)
SOFR = irx.overnight_spec("SOFR", 0, irx.US_SOFR, irx.A360, irx.curve_spec(TODAY, 0.043, irx.A360))
ESTR = irx.overnight_spec("ESTR", 0, irx.TARGET, irx.A360, irx.curve_spec(TODAY, 0.031))
NO_CURVE = dict(SOFR, curve=None)
LATE_CURVE = dict(SOFR, curve=irx.curve_spec(D(17, 1, 2025), 0.043, irx.A360))
TODAYS_FIXING = 0.07


def past(index_spec, start, base, with_today):
    """The fixings from start to the day before the evaluation date, base plus a basis point a business day, and
    today's when with_today."""
    index = irx.make_index(index_spec)
    cal = index.fixingCalendar()
    pairs, d, k = [], cal.adjust(start, ql.Preceding), 0
    while d < TODAY:
        pairs.append((d, base + 0.0001 * k))
        d, k = cal.advance(d, 1, ql.Days), k + 1
    if with_today:
        pairs.append((TODAY, TODAYS_FIXING))
    return [(index, pairs)] if pairs else None


def futures():
    """(name, index spec, value date, maturity, averaging, convexity, fixings) for the futures and refusals."""
    us = ql.UnitedStates(ql.UnitedStates.SOFR)
    out = []
    for avg, tag in ((COMPOUND, "compounded"), (SIMPLE, "averaged")):
        out += [
            (f"quarterly, forward / {tag}", SOFR, D(19, 3, 2025), D(18, 6, 2025), avg, 0.0, None),
            (f"monthly, forward / {tag}", SOFR, D(3, 2, 2025), D(3, 3, 2025), avg, 0.0, None),
            (f"seasoned / {tag}", SOFR, D(2, 1, 2025), D(3, 2, 2025), avg, 0.0, past(SOFR, D(2, 1, 2025), 0.043, False)),
            (f"seasoned, today's fixing / {tag}", SOFR, D(2, 1, 2025), D(3, 2, 2025), avg, 0.0,
             past(SOFR, D(2, 1, 2025), 0.043, True)),
            (f"from today / {tag}", SOFR, TODAY, us.advance(TODAY, ql.Period(1, ql.Months)), avg, 0.0, None),
            (f"from today, today's fixing / {tag}", SOFR, TODAY, us.advance(TODAY, ql.Period(1, ql.Months)), avg, 0.0,
             past(SOFR, TODAY, 0.043, True)),
            (f"convexity adjustment / {tag}", SOFR, D(19, 3, 2025), D(18, 6, 2025), avg, 0.0015, None),
            (f"ESTR quarterly / {tag}", ESTR, D(19, 3, 2025), D(18, 6, 2025), avg, 0.0, None),
            (f"maturing on Juneteenth / {tag}", SOFR, D(19, 3, 2025), D(19, 6, 2025), avg, 0.0, None),
        ]
    out += [
        ("seasoned ESTR / compounded", ESTR, D(15, 11, 2024), D(17, 2, 2025), COMPOUND, 0.0,
         past(ESTR, D(15, 11, 2024), 0.031, False)),
        ("seasoned ESTR / averaged", ESTR, D(15, 11, 2024), D(17, 2, 2025), SIMPLE, 0.0,
         past(ESTR, D(15, 11, 2024), 0.031, False)),
        ("expired", SOFR, D(18, 9, 2024), D(18, 12, 2024), COMPOUND, 0.0, None),
        ("refused / a past fixing missing", SOFR, D(2, 1, 2025), D(3, 2, 2025), COMPOUND, 0.0, None),
        ("refused / no forwarding curve", NO_CURVE, D(19, 3, 2025), D(18, 6, 2025), COMPOUND, 0.0, None),
        ("refused / a curve starting after today / compounded", LATE_CURVE, TODAY, D(18, 2, 2025), COMPOUND, 0.0, None),
        ("refused / a curve starting after today / averaged", LATE_CURVE, TODAY, D(18, 2, 2025), SIMPLE, 0.0, None),
    ]
    return out


def cases():
    out = []
    for name, spec, value, maturity, avg, convexity, fixings in futures():
        with v.case_settings(TODAY, fixings=fixings) as st:
            index = irx.make_index(spec)
            f = ql.OvernightIndexFuture(index, value, maturity, ql.QuoteHandle(ql.SimpleQuote(convexity)), avg)
            expected = {"NPV": v.attempt(f.NPV), "convexityAdjustment": f.convexityAdjustment(),
                        "isExpired": f.isExpired()}
            # QuantLib's compounded future on its value date reads no fixing for today, where nanoql's does: the NPV is
            # left out
            todays = fixings is not None and any(d == TODAY for _, pairs in fixings for d, _ in pairs)
            if avg == COMPOUND and value == TODAY and todays:
                del expected["NPV"]
            out.append({"name": name,
                        "inputs": {"settings": st, "index": spec, "valueDate": v.enc(value),
                                   "maturityDate": v.enc(maturity), "averaging": avg, "convexityAdjustment": convexity},
                        "expected": expected})
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    assert "NPV" not in by_name["from today, today's fixing / compounded"]
    for c in out:
        assert c["name"].startswith("refused") == (c["expected"].get("NPV") == v.THROWS), c["name"]
    assert by_name["expired"]["isExpired"] and by_name["expired"]["NPV"] == 0.0


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/overnightindexfuture.hpp", cases)
