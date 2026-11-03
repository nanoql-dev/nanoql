# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CapFloorTermVolatilityStructure (ql/termstructures/volatility/capfloor/
capfloortermvolatilitystructure.hpp) and the members it inherits, each output computed by the C++ harness, on
CapFloorTermVolSurface, the only concrete one ported: 8 surfaces of
tools/vectors/gen/termstructures/volatility/capfloor/capfloortermvolsurface.py, whose functions build their specs, on
the evaluation date 15 January 2025.

Each surface's outputs are its settlement days, which the wheel doesn't bind (QuantLib's refusal for a fixed reference
date, "settlement days not provided for this instance", which nanoql's null answers, written as null); its reference
date, maximum date and time, minimum and maximum strikes and business-day convention; the option date of 13 tenors
(optionDateFromTenor, which the wheel doesn't bind either); and the structure's three volatility overloads at 15 points
each, by tenor, by date and by time, inside the range, at its edges and outside it, with and without the extrapolation
flag (a refused value {"throws": true}). The settlement-days surfaces take their reference date from the evaluation
date, which the case's settings set; nothing moves it. Each case's inputs hold the surface ("surface", the wheel
generator's spec) and the points ("tenors", "byTenor", "byDate", "byTime"). Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/CapFloorTermVolatilityStructureCxxVectorTests.cs."""
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
    "wheel_capfloortermvolsurface",
    os.path.join(v.GEN_ROOT, "termstructures", "volatility", "capfloor", "capfloortermvolsurface.py"))
cfs = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cfs)

TODAY = cfs.TODAY
D, M, Y = cfs.D, cfs.M, cfs.Y
SURFACES = ["eight years by ten strikes, fixed", "eight years by ten strikes, two settlement days", "no settlement days",
            "twelve tenors in months and years", "two by two", "UK calendar, modified following",
            "unadjusted dates, fixed", "extrapolation enabled"]
TENORS = [[1, D], [1, M], [3, M], [6, M], [1, Y], [18, M], [2, Y], [5, Y], [10, Y], [20, Y], [26, int(cfs.ql.Weeks)],
          [7, D], [0, D]]


def points(rnd, H, spec):
    ref = H.call("CapFloorTermVolatilityStructure::referenceDate", spec)
    max_date = H.call("CapFloorTermVolatilityStructure::maxDate", spec)
    max_time = H.call("CapFloorTermVolatilityStructure::maxTime", spec)
    k0, k1 = spec["strikes"][0], spec["strikes"][-1]
    by_tenor = [[t, rnd.uniform(k0, k1), False] for t in spec["optionTenors"][:8]]
    by_tenor += [[[40, Y], k0, False], [[40, Y], k0, True], [[1, M], k1 + 0.02, False], [[1, M], k1 + 0.02, True]]
    by_tenor = by_tenor[:15]
    by_date = [[ref + int(rnd.uniform(0, max_date - ref)), rnd.uniform(k0, k1), False] for _ in range(10)]
    by_date += [[max_date, k1, False], [max_date + 1, k1, False], [max_date + 1, k1, True], [ref - 1, k0, True],
                [ref, k0, False]]
    by_time = [[rnd.uniform(0.0, max_time), rnd.uniform(k0, k1), False] for _ in range(10)]
    by_time += [[max_time, k0, False], [max_time * 1.5, k0, False], [max_time * 1.5, k0, True], [-0.01, k0, True],
                [0.0, k1 + 0.01, False]]
    return by_tenor, by_date, by_time


def case(H, rnd, name, spec):
    with H.case_settings(v.enc(TODAY)) as st:
        by_tenor, by_date, by_time = points(rnd, H, spec)

        def call(method, *args):
            return v.attempt(lambda: H.call(method, spec, *args))

        expected = {"settlementDays": v.result(lambda: H.call("CapFloorTermVolatilityStructure::settlementDays", spec))}
        for k in ("referenceDate", "maxDate", "maxTime", "minStrike", "maxStrike", "businessDayConvention"):
            expected[k] = call(f"CapFloorTermVolatilityStructure::{k}")
        expected["optionDateFromTenor"] = [call("CapFloorTermVolatilityStructure::optionDateFromTenor", t)
                                           for t in TENORS]
        expected["volatilityByTenor"] = [call("CapFloorTermVolatilityStructure::volatility(Period)", *p) for p in by_tenor]
        expected["volatilityByDate"] = [call("CapFloorTermVolatilityStructure::volatility(Date)", *p) for p in by_date]
        expected["volatilityByTime"] = [call("CapFloorTermVolatilityStructure::volatility(Time)", *p) for p in by_time]
        return {"name": name, "inputs": {"settings": st, "surface": spec, "tenors": TENORS, "byTenor": by_tenor,
                                         "byDate": by_date, "byTime": by_time},
                "expected": expected}


def cases():
    specs = dict(cfs.surfaces(random.Random(v.SEED)))
    rnd = random.Random(v.SEED + 1)
    with cxx.connect() as H:
        return [case(H, rnd, name, specs[name]) for name in SURFACES]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/capfloor/capfloortermvolatilitystructure.hpp", cases)
