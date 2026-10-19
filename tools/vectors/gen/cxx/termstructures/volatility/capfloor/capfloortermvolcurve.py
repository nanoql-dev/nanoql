# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CapFloorTermVolCurve (ql/termstructures/volatility/capfloor/capfloortermvolcurve.hpp), each
output computed by the C++ harness: the members the wheel doesn't bind, on the 10 curves of
tools/vectors/gen/termstructures/volatility/capfloor/capfloortermvolcurve.py, whose functions build their specs, on the
evaluation date 15 January 2025.

Each curve's outputs are its settlement days (QuantLib's refusal for a fixed reference date, "settlement days not
provided for this instance", written as null, nanoql's answer), its option tenors, dates and times, and the option
dates of 9 tenors (optionDateFromTenor). Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/CapFloorTermVolCurveCxxVectorTests.cs."""
import importlib.util
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_capfloortermvolcurve",
    os.path.join(v.GEN_ROOT, "termstructures", "volatility", "capfloor", "capfloortermvolcurve.py"))
cvc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cvc)

D, M, Y = cvc.D, cvc.M, cvc.Y
TENORS = [[1, D], [1, M], [3, M], [6, M], [1, Y], [18, M], [5, Y], [26, int(cvc.ql.Weeks)], [0, D]]


def case(H, name, spec):
    with H.case_settings(v.enc(cvc.TODAY)) as st:
        def call(method, *args):
            return v.attempt(lambda: H.call(f"CapFloorTermVolCurve::{method}", spec, *args))

        expected = {"settlementDays": v.result(lambda: H.call("CapFloorTermVolCurve::settlementDays", spec))}
        for k in ("optionTenors", "optionDates", "optionTimes"):
            expected[k] = call(k)
        expected["optionDateFromTenor"] = [call("optionDateFromTenor", t) for t in TENORS]
        return {"name": name, "inputs": {"settings": st, "curve": spec, "tenors": TENORS}, "expected": expected}


def cases():
    specs = cvc.curves(random.Random(v.SEED))
    with cxx.connect() as H:
        return [case(H, name, spec) for name, spec in specs]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/capfloor/capfloortermvolcurve.hpp", cases)
