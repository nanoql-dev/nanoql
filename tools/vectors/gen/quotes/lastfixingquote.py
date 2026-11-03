# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LastFixingQuote (ql/quotes/lastfixingquote.hpp, .cpp): the last fixing an index has available,
at the earlier of its store's last date and the evaluation date.

On Euribor 6M with a store of TARGET business days' fixings from 2 December 2024: the store ending before the evaluation
date (on its last business day, and two weeks before); ending on it; and holding fixings after it, the evaluation date
itself among them, where QuantLib's reference date is the evaluation date and nanoql's the last stored date on or before
it, the same date; holding only later fixings, where QuantLib's reference date and nanoql's are the evaluation date,
nanoql taking QuantLib's reading where the store holds nothing on or before it, and the value the index's fixing there,
forecast on a flat 3% curve (with no forecasting curve the value is refused, and so is nanoql's quote, which computes
its value at construction); on evaluation dates of Wednesday 15 January 2025 and Friday 31 January 2025; and an empty
store, whose value and reference date QuantLib refuses. A store holding later fixings but none at the evaluation date is
left out: there QuantLib's reference date is the evaluation date and nanoql's the stored date before it
(tests/NanoQL.Tests/Divergences/LastFixingQuoteTests.cs). Each quote's value and reference date. A case's inputs are the
"index" (a spec of indexes/interestrateindex.py, with no curve but on the forecast cases), its fixings in the settings.
Read back by tests/NanoQL.Tests/Vectors/Quotes/IndexQuoteVectorTests.cs."""
import importlib.util
import os
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

EURIBOR6M = irx.ibor_spec("Euribor", (6, ql.Months), 2, irx.TARGET, ql.ModifiedFollowing, True, irx.A360)
START = ql.Date(2, ql.December, 2024)


def fixings(last, first=START):
    """TARGET business days from first to last, each a rate of 2.5% plus a hundredth of a basis point a day after
    START."""
    cal = ql.TARGET()
    out = []
    d = first
    while d <= last:
        if cal.isBusinessDay(d):
            out.append((d, 0.025 + 1e-6 * (d - START)))
        d += 1
    return out


def cases():
    out = []
    for today in (ql.Date(15, ql.January, 2025), ql.Date(31, ql.January, 2025)):
        cal = ql.TARGET()
        rows = [("ending the business day before", cal.advance(today, -1, ql.Days)),
                ("ending two weeks before", today - 14),
                ("ending on the evaluation date", today),
                ("holding later fixings", today + 21)]
        for label, last in rows:
            index = irx.make_index(EURIBOR6M)
            with v.case_settings(today, fixings=[(index, fixings(last))]) as st:
                q = ql.LastFixingQuote(index)
                out.append({"name": f"{today.ISO()}, a store {label}",
                            "inputs": {"settings": st, "index": EURIBOR6M},
                            "expected": {"value": q.value(), "referenceDate": v.enc(q.referenceDate())}})
        forecast = irx.ibor_spec("Euribor", (6, ql.Months), 2, irx.TARGET, ql.ModifiedFollowing, True, irx.A360,
                                 irx.curve_spec(today, 0.03))
        index = irx.make_index(forecast)
        with v.case_settings(today, fixings=[(index, fixings(today + 21, today + 7))]) as st:
            q = ql.LastFixingQuote(index)
            assert q.referenceDate() == today
            out.append({"name": f"{today.ISO()}, a store holding only later fixings, forecast",
                        "inputs": {"settings": st, "index": forecast},
                        "expected": {"value": q.value(), "referenceDate": v.enc(q.referenceDate())}})
        with v.case_settings(today) as st:
            index = irx.make_index(EURIBOR6M)
            q = ql.LastFixingQuote(index)
            expected = {}
            for name, f in (("value", q.value), ("referenceDate", q.referenceDate)):
                try:
                    expected[name] = v.enc(f())
                except RuntimeError:
                    expected[name] = v.THROWS
            assert expected == {"value": v.THROWS, "referenceDate": v.THROWS}, expected
            out.append({"name": f"{today.ISO()}, an empty store", "inputs": {"settings": st, "index": EURIBOR6M},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/quotes/lastfixingquote.hpp", cases)
