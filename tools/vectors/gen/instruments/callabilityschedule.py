# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Callability (ql/instruments/callabilityschedule.hpp), through the members the wheel binds:
price(), whose amount and type are the BondPrice given, type() and date().

Twenty-four callabilities: calls and puts, clean and dirty prices (0, 100, a premium of 102.438, 110, 1000 and a
fractional 99.875), on dates from 1901 to 2199 and around QuantLib's test-runner date. The wheel's Callability doesn't
derive from Event, so whether one has occurred is the harness's (tools/vectors/gen/cxx/instruments/callabilityschedule.py).
A case's inputs are its settings and the callability's spec, tools/vectors/gen/experimental/callablebonds/
callablebond.py's {"price": {"amount", "type"}, "type", "date"}; its outputs are the price's amount and type, the
type and the date. Read back by tests/NanoQL.Tests/Vectors/Instruments/CallabilityScheduleVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

D = ql.Date
CALL, PUT = int(ql.Callability.Call), int(ql.Callability.Put)
CLEAN, DIRTY = int(ql.BondPrice.Clean), int(ql.BondPrice.Dirty)
EVALUATION_DATE = D(16, 9, 2015)

DATES = [D(1, 1, 1901), D(16, 9, 2015), D(17, 9, 2015), D(29, 2, 2016), D(15, 3, 2028), D(31, 12, 2199)]
PRICES = [(100.0, CLEAN, CALL), (110.0, CLEAN, CALL), (102.438, DIRTY, CALL), (0.0, CLEAN, PUT), (1000.0, DIRTY, PUT),
          (99.875, CLEAN, PUT)]


def spec(amount, price_type, kind, date):
    return {"price": {"amount": amount, "type": price_type}, "type": kind, "date": v.enc(date)}


def cases():
    out = []
    for i, date in enumerate(DATES):
        for j in range(4):
            amount, price_type, kind = PRICES[(i + j) % len(PRICES)]
            s = spec(amount, price_type, kind, date)
            with v.case_settings(EVALUATION_DATE) as st:
                c = ql.Callability(ql.BondPrice(amount, price_type), kind, date)
                out.append({"name": f"{'call' if kind == CALL else 'put'} at {amount:g} "
                                    f"{'clean' if price_type == CLEAN else 'dirty'} on {date.ISO()}",
                            "inputs": {"settings": st, "callability": s},
                            "expected": {"priceAmount": c.price().amount(), "priceType": int(c.price().type()),
                                         "type": int(c.type()), "date": v.enc(c.date())}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/callabilityschedule.hpp", cases)
