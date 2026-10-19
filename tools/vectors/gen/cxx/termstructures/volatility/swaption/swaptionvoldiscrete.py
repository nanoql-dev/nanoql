# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SwaptionVolatilityDiscrete (ql/termstructures/volatility/swaption/swaptionvoldiscrete.hpp),
each output computed by the C++ harness, on the 8 volatility matrices of
tools/vectors/gen/termstructures/volatility/swaption/swaptionvolmatrix.py, whose functions build them (QuantLib's
discrete base has no concrete class of its own), under each matrix's evaluation date.

Each case's outputs are optionDateFromTime at 19 times, and the grid the base holds: its option tenors, option dates,
option times, swap tenors and swap lengths. The times reach the reference date, the nodes, between them, past the last
(extrapolated linearly), negative times, and times whose interpolated serial leaves Date's range or the integers:
QuantLib truncates the serial with static_cast, which for NaN, the infinities and a value outside the integers is
undefined and gives MSVC's smallest integer, refused by Date's range check ("Date's serial number (-2147483648) outside
allowed range ..."), as is a serial inside the integers but outside the range. The base's constructor on option dates
never fills its date interpolation, so on the matrix built from option dates every time maps to serial 0 and is
refused, and its option tenors are zero periods (QuantLib's own behaviour, which nanoql keeps).

A refused value is {"throws": true}. Each case's inputs hold the matrix spec, as
tools/cxx/src/termstructures/volatility/swaption/swaptionvolmatrixspec.hpp reads it, and the times. Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/SwaptionVolDiscreteCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_swaptionvolmatrix",
    os.path.join(v.GEN_ROOT, "termstructures", "volatility", "swaption", "swaptionvolmatrix.py"))
svm = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(svm)

INF = float("inf")
NAN = float("nan")
TIMES = [0.0, 1.0 / 12, 0.5, 1.0, 2.7, 10.0, 30.0, 31.0, 60.0, -0.5, -200.0, 300.0, 5.0e6, 6.0e6, 1.0e9, -1.0e9,
         NAN, INF, -INF]
METHOD = "SwaptionVolatilityDiscrete::"


def case(H, name, evaluation_date, spec):
    with H.case_settings(v.enc(evaluation_date)) as st:
        expected = {"optionDateFromTime": [v.attempt(lambda t=t: H.call(METHOD + "optionDateFromTime", spec, t))
                                           for t in TIMES]}
        for member in ("optionTenors", "optionDates", "optionTimes", "swapTenors", "swapLengths"):
            expected[member] = v.attempt(lambda m=member: H.call(METHOD + m, spec))
        return {"name": f"{name} / {evaluation_date.ISO()}",
                "inputs": {"settings": st, "matrix": spec, "times": v.enc(TIMES)},
                "expected": expected}


def cases():
    with cxx.connect() as H:
        out = [case(H, *row) for row in svm.matrices()]
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    fixed = by_name[f"at the money, fixed reference date / {svm.TODAY.ISO()}"]
    # The time 0 is the reference date; NaN, the infinities and the times past the integers are refused.
    assert fixed["optionDateFromTime"][0] == svm.TODAY.serialNumber()
    assert all(x == v.THROWS for x in fixed["optionDateFromTime"][12:])
    # The dates form maps every time to serial 0, refused, and its option tenors are zero periods.
    dates = by_name[f"option dates, normal, Actual/360 / {svm.TODAY.ISO()}"]
    assert all(x == v.THROWS for x in dates["optionDateFromTime"])
    assert all(p == [0, 0] for p in dates["optionTenors"])


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/swaption/swaptionvoldiscrete.hpp", cases)
