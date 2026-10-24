# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's range accrual floater (ql/cashflows/rangeaccrual.hpp), each output computed by the C++
harness: the coupon's members the wheel doesn't expose, on cashflows/rangeaccrual.py's coupons (the wheel's, on its
evaluation date, index and curve) and two started ones, whose past fixings are stored (cashflows/iborcoupon.py's
history).

Each "coupon" case gives startTime and endTime (from the forwarding curve's reference date, so a started coupon's start
time is negative), lowerTrigger, upperTrigger, observationsNo, observationDates and observationTimes (the schedule's
dates less its start and end), the observation schedule's dates, and priceWithoutOptionality on the discounting curve.
Read back by tests/NanoQL.Tests/Vectors/Cxx/Coupons/RangeAccrualCxxVectorTests.cs."""
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


ra = _load("wheel_rangeaccrual", "cashflows/rangeaccrual.py")
ibc = _load("wheel_iborcoupon", "cashflows/iborcoupon.py")

TODAY = ra.TODAY
D = ql.Date
STARTED = [
    ("started 6M weekly", D(15, ql.May, 2025), 1e6, D(15, ql.November, 2024), D(15, ql.May, 2025), 2, 1.0, 0.0,
     (1, ql.Weeks), 0.02, 0.04, 1.0),
    ("started 3M daily, geared", D(17, ql.March, 2025), 1e6, D(17, ql.December, 2024), D(17, ql.March, 2025), 2, 1.2,
     0.001, (1, ql.Days), 0.025, 0.035, 1.0),
]


def fixings():
    name = ra.irx.make_index(ra.E6M).name()
    return {name: [[d.serialNumber(), x] for d, x in ibc.history(ra.E6M, ibc.FIXINGS_FROM, TODAY - 1, v.SEED)]}


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(TODAY.serialNumber(), fixings=fixings())
        for label, payment, nominal, start, end, days, gearing, spread, tenor, lower, upper, _ in ra.COUPONS + STARTED:
            spec = {"paymentDate": v.enc(payment), "nominal": nominal, "index": ra.E6M, "startDate": v.enc(start),
                    "endDate": v.enc(end), "fixingDays": days, "dayCounter": ra.A360, "gearing": gearing,
                    "spread": spread, "observationSchedule": ra.observations(start, end, tenor),
                    "lowerTrigger": lower, "upperTrigger": upper}
            m = H.call("RangeAccrualFloatersCoupon::members", spec, ra.CURVE)
            out.append({"name": f"coupon {label}",
                        "inputs": {"settings": st, "coupon": spec, "discountCurve": ra.CURVE},
                        "expected": {"startTime": m[0], "endTime": m[1], "lowerTrigger": m[2], "upperTrigger": m[3],
                                     "observationsNo": m[4], "observationDates": m[5], "observationTimes": m[6],
                                     "observationScheduleDates": m[7], "priceWithoutOptionality": m[8]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/rangeaccrual.hpp", cases)
