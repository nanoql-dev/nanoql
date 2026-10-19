# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for what the wheel doesn't bind of QuantLib's CapFloor (ql/instruments/capfloor.hpp), each output computed by
the C++ harness, on the evaluation date 15 January 2025: 22 instruments built with CapFloor's four-argument and
three-argument constructors (the wheel's Cap, Floor and Collar call the first, and binds neither), their optionlets
and their last floating-rate coupons.

The instruments are tools/vectors/gen/instruments/capfloor.py's Ibor legs: caps, floors and collars with one rate or
several (the last extended to the leg's length), more rates than coupons, a cap that keeps the floor rates it is given,
unpadded, and an empty leg; and the constructors' refusals in QuantLib's order ("no cap rates given", "no floor rates
given", "no strikes given" before "only Cap/Floor types allowed in this constructor", which the three-argument
constructor gives a collar and a type outside the enum alike). A type outside the enum is left out of the four-argument
constructor, which QuantLib builds unchecked and nanoql refuses (playbook §2, the enum row).

Each case's outputs are the built instrument's type, cap and floor rates and leg size ("built.<name>", or "built"
{"throws": true}); per index from 0 to the leg's size, the optionlet's inspectors and its cash flow's payment date with
its start and maturity dates ("optionlet.<name>", a list over the indexes, the last one past the leg refused, "1st
optionlet does not exist, only 0" for an empty leg); and whether the last cash flow is a floating-rate coupon, with its
inspectors ("last.<name>", tools/vectors/gen/cxx/cashflows/floatingratecoupon.py's), which a leg whose last period has
a zero gearing, built as a fixed-rate coupon, doesn't have. lastFloatingRateCoupon reads the back of the leg, undefined
for an empty one, which the harness refuses and this file leaves out. Each case's inputs hold the instrument spec
("capFloor", {"type", "leg", "legType", "capRates", "floorRates"} or the same with "strikes"). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Instruments/CapFloorCxxVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location("wheel_capfloor", os.path.join(v.GEN_ROOT, "instruments", "capfloor.py"))
cfl = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cfl)

TODAY = cfl.TODAY
CAP, FLOOR, COLLAR = cfl.CAP, cfl.FLOOR, cfl.COLLAR
INSPECTORS = ["date", "nominal", "accrualStartDate", "accrualEndDate", "referencePeriodStart", "referencePeriodEnd",
              "exCouponDate", "accrualPeriod", "fixingDate", "fixingDays", "gearing", "spread", "isInArrears",
              "fixingConvention", "dayCounter", "index", "hasPricer"]
OPTIONLET = ["type", "capRates", "floorRates", "legSize", "date", "startDate", "maturityDate"]


def strikes_spec(type_, leg, strikes):
    return {"type": type_, "leg": leg, "legType": "IborLeg", "strikes": list(strikes)}


def instruments():
    """(name, instrument spec) for the 22 instruments."""
    two = cfl.ibor_leg(cfl.SPOT, ql.Period(2, ql.Years))
    three = cfl.ibor_leg(cfl.SPOT, ql.Period(3, ql.Years), cfl.E3M, ql.Period(3, ql.Months), (2e6,))
    zero_last = cfl.ibor_leg(cfl.SPOT, ql.Period(2, ql.Years), gearings=[1.0, 1.0, 1.0, 0.0], spreads=[0.0, 0.0, 0.0,
                                                                                                         0.03])
    spec = cfl.instrument_spec
    return [
        ("four arguments: a cap", spec(CAP, two, [0.03])),
        ("four arguments: a floor", spec(FLOOR, two, (), [0.025])),
        ("four arguments: a collar", spec(COLLAR, three, [0.04, 0.045], [0.02])),
        ("four arguments: a cap keeping floor rates", spec(CAP, two, [0.03], [0.01, 0.02])),
        ("four arguments: more rates than coupons", spec(FLOOR, two, (), [0.02, 0.021, 0.022, 0.023, 0.024, 0.025])),
        ("four arguments: a cap, zero gearing last", spec(CAP, zero_last, [0.03])),
        ("four arguments: a cap on an empty leg", spec(CAP, None, [0.03], (), "Empty")),
        ("four arguments: a collar on an empty leg", spec(COLLAR, None, [0.03], [0.01], "Empty")),
        ("four arguments: no cap rates", spec(CAP, two, (), [0.03])),
        ("four arguments: no floor rates", spec(FLOOR, two, [0.03])),
        ("four arguments: a collar, no cap rates", spec(COLLAR, two, (), [0.02])),
        ("four arguments: a collar, no floor rates", spec(COLLAR, two, [0.04])),
        ("four arguments: a cap on an empty leg, no rates", spec(CAP, None, (), (), "Empty")),
        ("three arguments: a cap", strikes_spec(CAP, two, [0.03])),
        ("three arguments: a floor, rates extended", strikes_spec(FLOOR, three, [0.02, 0.022, 0.024])),
        ("three arguments: a cap, more strikes than coupons", strikes_spec(CAP, two, [0.03] * 7)),
        ("three arguments: a floor on an empty leg", dict(strikes_spec(FLOOR, None, [0.03]), legType="Empty")),
        ("three arguments: a collar", strikes_spec(COLLAR, two, [0.03])),
        ("three arguments: a type outside the enum", strikes_spec(7, two, [0.03])),
        ("three arguments: no strikes", strikes_spec(CAP, two, [])),
        ("three arguments: a collar, no strikes", strikes_spec(COLLAR, two, [])),
        ("three arguments: zero gearing last", strikes_spec(FLOOR, zero_last, [0.02])),
    ]


def case(H, name, spec):
    with H.case_settings(v.enc(TODAY)) as st:
        expected = {}
        try:
            built = H.call("CapFloor::CapFloor", spec)
        except RuntimeError:
            return {"name": name, "inputs": {"settings": st, "capFloor": spec}, "expected": {"built": dict(v.THROWS)}}
        for k in ("type", "capRates", "floorRates", "legSize"):
            expected[f"built.{k}"] = built[k]
        optionlets = [v.attempt(lambda i=i: H.call("CapFloor::optionlet", spec, i)) for i in range(built["legSize"] + 1)]
        for k in OPTIONLET:
            expected[f"optionlet.{k}"] = [o if "throws" in o else o[k] for o in optionlets]
        if built["legSize"] > 0:
            last = H.call("CapFloor::lastFloatingRateCoupon", spec)
            expected["lastIsFloating"] = last is not None
            if last is not None:
                for k in INSPECTORS:
                    expected[f"last.{k}"] = last[k]
        return {"name": name, "inputs": {"settings": st, "capFloor": spec}, "expected": expected}


def cases():
    with cxx.connect() as H:
        return [case(H, name, spec) for name, spec in instruments()]


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/capfloor.hpp", cases)
