# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for CappedFlooredCoupon::underlying() (ql/cashflows/capflooredcoupon.hpp), which the wheel doesn't bind,
each output computed by the C++ harness, on the evaluation date 15 January 2025: 12 capped, floored and collared
coupons (CappedFlooredIborCoupon, and CappedFlooredCoupon over an IborCoupon or over a FloatingRateCoupon built
directly) at gearings either side of zero, with a pricer set on the capped coupon (which setPricer sets on its
underlying too), on the underlying only before it is wrapped, or none.

Each case's outputs are the underlying's inspectors (as tools/vectors/gen/cxx/cashflows/floatingratecoupon.py's),
prefixed "underlying.", whether it holds the capped coupon's own pricer ("underlying.pricerIsCoupons"), its rate and
index fixing ("underlying.rate", "underlying.indexFixing", refused without a pricer), and the capped coupon's own rate,
which a CappedFlooredCoupon over a FloatingRateCoupon prices with the harness's IndexFixingPricer, the caplet and
floorlet at their intrinsic values. A refused value is {"throws": true}. Each case's inputs hold the capped coupon spec
("coupon", as tools/vectors/gen/cashflows/iborcoupon.py's functions build it).
tests/NanoQL.Tests/Vectors/Cxx/Coupons/CapFlooredCouponCxxVectorTests.cs reads it back."""
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

_spec = importlib.util.spec_from_file_location("wheel_iborcoupon", os.path.join(v.GEN_ROOT, "cashflows", "iborcoupon.py"))
ibc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ibc)

TODAY = ibc.TODAY
d = ibc.d

E6M = ibc.euribor((6, ql.Months), ibc.CURVE)
E3M = ibc.euribor((3, ql.Months), ibc.CURVE)
INSPECTORS = ["date", "nominal", "accrualStartDate", "accrualEndDate", "referencePeriodStart", "referencePeriodEnd",
              "exCouponDate", "accrualPeriod", "fixingDate", "fixingDays", "gearing", "spread", "isInArrears",
              "fixingConvention", "dayCounter", "index", "hasPricer", "pricerIsCoupons"]


def coupons():
    black = ibc.pricer_spec(ibc.VOL_LN)
    fixing = {"type": "IndexFixingPricer", "adjustment": 0.0004}
    c = ibc.ibor_coupon_spec
    ibor = c(d(19, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M, 1.25, 0.001, ex_coupon=d(12, 1, 2026))
    negative = c(d(19, 1, 2026), 2e6, d(17, 7, 2025), d(19, 1, 2026), 3, E6M, -1.0, 0.06)
    plain = dict(c(d(20, 6, 2025), 5e5, d(20, 3, 2025), d(20, 6, 2025), None, E3M, in_arrears=True, dc=ibc.A365),
                 type="FloatingRateCoupon")
    return [
        c(d(19, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M, cap=0.032, capped=True, pricer=black),
        c(d(19, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), None, E6M, -0.5, 0.04, cap=0.03, floor=0.02,
          capped=True, pricer=black),
        c(d(19, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M, floor=0.03, capped=True),
        ibc.capped_coupon_spec(ibor, cap=0.04, pricer=black),
        ibc.capped_coupon_spec(ibor, floor=0.035, pricer=ibc.pricer_spec(ibc.VOL_N)),
        ibc.capped_coupon_spec(dict(ibor, pricer=black), cap=0.04, floor=0.03),
        ibc.capped_coupon_spec(ibor, cap=0.04),
        ibc.capped_coupon_spec(negative, cap=0.03, pricer=black),
        ibc.capped_coupon_spec(negative, floor=0.025, pricer=black),
        ibc.capped_coupon_spec(plain, cap=0.031, pricer=fixing),
        ibc.capped_coupon_spec(plain, cap=0.033, floor=0.029, pricer=fixing),
        ibc.capped_coupon_spec(dict(plain, pricer=fixing), floor=0.032),
    ]


def cases():
    with cxx.connect() as H:
        out = []
        for k, spec in enumerate(coupons()):
            with H.case_settings(v.enc(TODAY), use_at_par_coupons=k % 2 == 0) as st:
                underlying = H.call("CappedFlooredCoupon::underlying", spec)
                expected = {f"underlying.{name}": underlying[name] for name in INSPECTORS}
                expected["underlying.rate"] = v.attempt(lambda: H.call("CappedFlooredCoupon::underlying()->rate", spec))
                expected["underlying.indexFixing"] = v.attempt(
                    lambda: H.call("CappedFlooredCoupon::underlying()->indexFixing", spec))
                expected["rate"] = v.attempt(lambda: H.call("FloatingRateCoupon::rate", spec))
                out.append({"name": f"{k}: {spec['type']} over {underlying['index']}",
                            "inputs": {"settings": st, "coupon": spec}, "expected": expected})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/capflooredcoupon.hpp", cases)
