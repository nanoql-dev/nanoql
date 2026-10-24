# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CPICapFloor (ql/instruments/cpicapfloor.hpp), which no wheel class exposes, each output
computed by the C++ harness (docs/porting-playbook.md §9.4), on the evaluation date 15 January 2025: 8 caps and
floors on the UK RPI (its availability lag a month, no curve: nothing here prices) and 5 refusals.

The caps and floors vary the type, nominal, start, base CPI and strike, a maturity on a weekend and one before the
evaluation date (expired: QuantLib compares the evaluation date with the unadjusted maturity), the fixing and payment
calendars (TARGET and the UK's settlement calendar) and conventions, a 3-month and a 2-month observation lag, and
flat and linear observation interpolation. Each one's outputs are its type, nominal and strike, its fixing and
payment dates, the index's name, the observation lag, whether it has expired, and the arguments it sets up for an
engine: the type, nominal, start, fixing, payment and maturity dates, base CPI, conventions, strike, lag and
interpolation. The refusals: no fixing calendar, no payment calendar, a flat observation lag shorter than the
index's availability lag, a linear one equal to it, and a linear one shorter.

A case's inputs hold its settings and the method's arguments (type, nominal, start, base CPI, maturity, fixing
calendar and convention, payment calendar and convention, strike, index, lag and interpolation), the index a
cashflows/inflationspec.hpp spec and a calendar an indexes/indexspec.hpp spec, null for QuantLib's empty one. Read
back by tests/NanoQL.Tests/Vectors/Cxx/Instruments/CPICapFloorCxxVectorTests.cs."""
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


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


irx = _load("wheel_interestrateindex", "indexes/interestrateindex.py")

D = ql.Date
TODAY = irx.TODAY
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
FLAT, LINEAR = int(ql.CPI.Flat), int(ql.CPI.Linear)
UKRPI = {"type": "UKRPI", "curve": None}
UK = {"type": "UnitedKingdom", "market": int(ql.UnitedKingdom.Settlement)}
TARGET = irx.TARGET
F, MF, P = int(ql.Following), int(ql.ModifiedFollowing), int(ql.Preceding)
M3, M2, M1, M0 = [3, int(ql.Months)], [2, int(ql.Months)], [1, int(ql.Months)], [0, int(ql.Months)]


def row(type_=CALL, nominal=1e6, start=D(15, 1, 2025), base=290.4, maturity=D(15, 1, 2030), fix=UK, fix_conv=MF,
        pay=UK, pay_conv=MF, strike=0.03, lag=M3, interpolation=FLAT):
    return [type_, nominal, v.enc(start), base, v.enc(maturity), fix, fix_conv, pay, pay_conv, strike, UKRPI, lag,
            interpolation]


CAPS = [
    ("a 5-year cap", row()),
    ("a floor", row(PUT, strike=0.01)),
    ("linear interpolation", row(interpolation=LINEAR)),
    ("a maturity on a Saturday, Following and Preceding", row(maturity=D(15, 6, 2030), fix_conv=P, pay_conv=F)),
    ("TARGET calendars, a 2-month lag", row(fix=TARGET, pay=TARGET, lag=M2, base=301.2, strike=0.025)),
    ("a forward start, another nominal", row(nominal=2.5e7, start=D(15, 7, 2025), maturity=D(15, 7, 2035))),
    ("a month's lag, flat", row(lag=M1)),
    ("expired", row(start=D(15, 1, 2020), maturity=D(15, 1, 2025) - 7)),
    ("refused / no fixing calendar", row(fix=None)),
    ("refused / no payment calendar", row(pay=None)),
    ("refused / a flat lag below the availability lag", row(lag=M0)),
    ("refused / a linear lag equal to the availability lag", row(lag=M1, interpolation=LINEAR)),
    ("refused / a linear lag below it", row(lag=M0, interpolation=LINEAR)),
]

OUTPUTS = ["type", "nominal", "strike", "fixingDate", "payDate", "indexName", "observationLag", "isExpired",
           "argumentsType", "argumentsNominal", "argumentsStartDate", "argumentsFixDate", "argumentsPayDate",
           "argumentsBaseCPI", "argumentsMaturity", "argumentsFixConvention", "argumentsPayConvention",
           "argumentsStrike", "argumentsObservationLag", "argumentsInterpolation"]


def cases():
    out = []
    with cxx.connect() as H:
        for name, args in CAPS:
            with H.case_settings(v.enc(TODAY)) as st:
                r = v.attempt(lambda: H.call("CPICapFloor", *args))
                expected = {"construct": r} if r == v.THROWS else dict(zip(OUTPUTS, r))
                out.append({"name": name, "inputs": {"settings": st, "arguments": args}, "expected": expected})
    for c in out:
        assert c["name"].startswith("refused") == ("construct" in c["expected"]), c["name"]
    assert [c for c in out if c["name"] == "expired"][0]["expected"]["isExpired"]
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/cpicapfloor.hpp", cases)
