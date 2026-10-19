# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Callability (ql/instruments/callabilityschedule.hpp) where the wheel can't reach, each
output computed by the C++ harness: hasOccurred, which Callability inherits from Event and the wheel's Callability,
which doesn't derive from Event, lacks.

Six callabilities (calls and puts, clean and dirty, tools/vectors/gen/experimental/callablebonds/callablebond.py's
spec) dated around 16 September 2015, each under four contexts: evaluation dates on, before and after the callability's
date, with includeReferenceDateEvents false and true. The outputs: hasOccurred at seven reference dates (a null one,
the evaluation date, the callability's date and the days either side, and dates a year either side) with
includeRefDate null, false and true, 21 per case: an event on the reference date has occurred unless the flag (or, for a
null flag, the context's includeReferenceDateEvents) says to include it, and a null reference date is the evaluation
date. Callability's hasOccurred is Event's, which, unlike a cash flow's, never reads includeTodaysCashFlows. Read back
by tests/NanoQL.Tests/Vectors/Cxx/Instruments/CallabilityScheduleCxxVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location(
    "wheel_callablebond", os.path.join(v.GEN_ROOT, "experimental", "callablebonds", "callablebond.py"))
cb = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cb)

D = ql.Date
CALLABILITIES = [cb.callability_spec(100.0, cb.CLEAN, cb.CALL, D(16, 9, 2015)),
                 cb.callability_spec(110.0, cb.DIRTY, cb.CALL, D(15, 3, 2018)),
                 cb.callability_spec(90.0, cb.CLEAN, cb.PUT, D(16, 9, 2015)),
                 cb.callability_spec(101.5, cb.DIRTY, cb.PUT, D(29, 2, 2016)),
                 cb.callability_spec(0.0, cb.CLEAN, cb.PUT, D(1, 1, 2010)),
                 cb.callability_spec(1000.0, cb.CLEAN, cb.CALL, D(31, 12, 2030))]
FLAGS = [None, False, True]


def cases():
    out = []
    with cxx.connect() as H:
        for c in CALLABILITIES:
            date = D(c["date"])
            for ev in (date, date - 1, date + 1):
                for ref_events in ((False, True) if ev == date else (False,)):
                    with H.case_settings(ev.serialNumber(), include_reference_date_events=ref_events) as st:
                        refs = [None, ev, date - 1, date, date + 1, date - 365, date + 365]
                        refs = [v.enc(d) for d in refs]
                        flags = [(r, f) for r in refs for f in FLAGS]
                        occurred = [v.attempt(lambda r=r, f=f: H.call("Callability::hasOccurred", c, r, f))
                                    for r, f in flags]
                        kind = "call" if c["type"] == cb.CALL else "put"
                        out.append({"name": f"{kind} on {date.ISO()}, evaluated {ev.ISO()} {ref_events}",
                                    "inputs": {"settings": st, "callability": c, "refDates": refs, "flags": FLAGS},
                                    "expected": {"hasOccurred": occurred}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/callabilityschedule.hpp", cases)
