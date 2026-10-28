# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's cat bond notional risks (ql/experimental/catbonds/riskynotional.hpp), which the wheel
doesn't expose, each output computed by the C++ harness (tools/cxx/src/experimental/catbonds/catrisk.cpp): the
notional path digital and proportional risks write from events in date order, its notional rate on dates before, on
and between the events and its loss. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/CatBonds/NotionalRiskCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def d(day, month, year):
    return ql.Date(day, month, year).serialNumber()


EVENTS = [
    ("none", []),
    ("one small", [[d(1, 3, 2021), 40]]),
    ("three", [[d(1, 3, 2021), 300], [d(15, 6, 2021), 250], [d(1, 12, 2022), 700]]),
    ("one doom", [[d(30, 11, 2021), 1000]]),
    ("same day", [[d(1, 3, 2021), 400], [d(1, 3, 2021), 400], [d(2, 3, 2021), 900]]),
]
RISKS = [("digital 100", ["Digital", 100.0]), ("digital 500", ["Digital", 500.0]),
         ("proportional 500 to 1500", ["Proportional", 500.0, 1500.0]),
         ("proportional 0 to 1000", ["Proportional", 0.0, 1000.0])]
DATES = [d(1, 1, 2021), d(1, 3, 2021), d(2, 3, 2021), d(15, 6, 2021), d(30, 11, 2021), d(1, 12, 2022),
         d(1, 1, 2030)]


def flat(result):
    """The harness's object result, one output per key; a refused call's one output, "values"."""
    return dict(result) if isinstance(result, dict) and "throws" not in result else {"values": result}


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        with H.case_settings(d(1, 1, 2021)) as st:
            for risk_label, risk in RISKS:
                for events_label, events in EVENTS:
                    args = [risk, events, DATES]
                    out.append({"name": f"{risk_label} {events_label}", "inputs": {"settings": st, "args": args},
                                "expected": flat(call("NotionalRisk::path", *args))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/catbonds/riskynotional.hpp", cases)
