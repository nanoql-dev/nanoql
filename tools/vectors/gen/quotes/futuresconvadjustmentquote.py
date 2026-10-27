# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FuturesConvAdjustmentQuote (ql/quotes/futuresconvadjustmentquote.hpp, .cpp): the Hull-White
convexity adjustment of an Ibor futures contract at the evaluation date.

On Euribor 3M, on Wednesday 15 January 2025, the first four IMM dates of the main cycle after it, each by its date and
by its IMM code, at futures prices of 97.5 and 98.2, volatilities of 0.5%, 1% and 1.5% and mean reversions of 0 and
3%: each quote's value and its inspectors (futuresValue, volatility, meanReversion, immDate). A case's inputs are the
"index" (a spec of indexes/interestrateindex.py, with no curve), the "form" ("date" with "futuresDate", or "immCode"
with "immCode"), the "futuresPrice", the "volatility" and the "meanReversion". The wheel's constructors take each value
as a quote handle. Read back by tests/NanoQL.Tests/Vectors/Quotes/IndexQuoteVectorTests.cs."""
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

TODAY = ql.Date(15, ql.January, 2025)
EURIBOR3M = irx.ibor_spec("Euribor", (3, ql.Months), 2, irx.TARGET, ql.ModifiedFollowing, True, irx.A360)


def h(x):
    return ql.QuoteHandle(ql.SimpleQuote(x))


def cases():
    st = v.settings(TODAY)
    index = irx.make_index(EURIBOR3M)
    dates = []
    d = TODAY
    for _ in range(4):
        d = ql.IMM.nextDate(d)
        dates.append(d)
    out = []
    for date in dates:
        code = ql.IMM.code(date)
        for price in (97.5, 98.2):
            for vol in (0.005, 0.01, 0.015):
                for mr in (0.0, 0.03):
                    for form in ("date", "immCode"):
                        arg = date if form == "date" else code
                        q = ql.FuturesConvAdjustmentQuote(index, arg, h(price), h(vol), h(mr))
                        inputs = {"settings": st, "index": EURIBOR3M, "form": form, "futuresPrice": price,
                                  "volatility": vol, "meanReversion": mr}
                        if form == "date":
                            inputs["futuresDate"] = date.serialNumber()
                        else:
                            inputs["immCode"] = code
                        out.append({"name": f"{date.ISO()} by {form}, price {price}, vol {vol}, mr {mr}",
                                    "inputs": inputs,
                                    "expected": {"value": q.value(), "futuresValue": q.futuresValue(),
                                                 "volatility": q.volatility(), "meanReversion": q.meanReversion(),
                                                 "immDate": v.enc(q.immDate())}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/quotes/futuresconvadjustmentquote.hpp", cases)
