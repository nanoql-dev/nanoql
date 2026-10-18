# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's IborCoupon and IborLeg (ql/cashflows/iborcoupon.hpp) that the wheel doesn't
reach, each output computed by the C++ harness, on the evaluation date 15 January 2025.

The five quantities an IborCoupon caches from its pricer (fixingValueDate, fixingEndDate, fixingMaturityDate,
spanningTime, spanningTimeIndexMaturity) for 15 coupons in advance and in arrears, regular and broken, with 0 to 3 and
null fixing days, on Euribor-style indexes and an index on a calendar with no holidays, each priced by a
BlackIborCouponPricer: built without an explicit choice under both settings of the at-par switch, or with an explicit
indexed or at-par choice (which the switch doesn't change); and with no pricer, which QuantLib refuses ("IborCoupon:
pricer not set or not derived from IborCouponPricer").

IborLegs built with the setters the wheel's IborLeg doesn't pass: withAtParCoupons (either way, after
withIndexedCoupons or none, under the switch's other setting, the last setter deciding); withFixingConvention
(Following, ModifiedFollowing,
ModifiedPreceding, Unadjusted, with no fixing days on accrual starts that fall on weekends at month end; Unadjusted
leaves fixing dates QuantLib refuses) and withZeroPayments (every coupon
paid on the last payment date, with and without a payment lag; with in-arrears fixing, which QuantLib refuses), each
cash flow's dates, nominal, fixing date, rate and amount ("cashflows").

A refused value is {"throws": true}. Each case's inputs hold the coupon spec ("coupon", as
tools/vectors/gen/cashflows/iborcoupon.py's functions build it) or the leg spec ("leg", with "fixingConvention" and
"zeroPayments" and "atParCoupons"). tests/NanoQL.Tests/Vectors/Cxx/Coupons/IborCouponCxxVectorTests.cs reads it back."""
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

irx = ibc.irx
TODAY = ibc.TODAY
d = ibc.d

E1M = ibc.euribor((1, ql.Months), ibc.CURVE_STEEP)
E3M = ibc.euribor((3, ql.Months), ibc.CURVE)
E6M = ibc.euribor((6, ql.Months), ibc.CURVE)
E1Y = ibc.euribor((1, ql.Years), ibc.CURVE_STEEP)
NUL3M = irx.ibor_spec("NulIbor", (3, ql.Months), 0, ibc.NULL_CALENDAR, ql.Following, False, ibc.A365, ibc.CURVE)
CACHED = ["fixingValueDate", "fixingEndDate", "fixingMaturityDate", "spanningTime", "spanningTimeIndexMaturity"]


def coupons():
    c = ibc.ibor_coupon_spec
    return [
        c(d(19, 1, 2026), 1e6, d(17, 7, 2025), d(19, 1, 2026), 2, E6M),
        c(d(17, 11, 2025), 1e6, d(19, 5, 2025), d(17, 11, 2025), 1, E3M),
        c(d(19, 2, 2026), 1e6, d(17, 7, 2025), d(19, 2, 2026), 2, E6M),
        c(d(17, 5, 2025), 1e6, d(17, 1, 2025), d(17, 5, 2025), 2, E6M),
        c(d(20, 6, 2025), 1e6, d(20, 3, 2025), d(20, 6, 2025), 0, E3M),
        c(d(20, 6, 2025), 1e6, d(20, 3, 2025), d(20, 6, 2025), 3, E3M),
        c(d(20, 6, 2025), 1e6, d(20, 3, 2025), d(20, 6, 2025), None, E3M),
        c(d(20, 6, 2025), 1e6, d(20, 3, 2025), d(20, 6, 2025), 2, E3M, in_arrears=True),
        c(d(21, 4, 2025), 1e6, d(21, 1, 2025), d(21, 4, 2025), 2, E1Y),
        c(d(17, 1, 2030), 1e6, d(17, 1, 2029), d(17, 1, 2030), 2, E1Y, in_arrears=True),
        c(d(5, 9, 2025), 1e6, d(5, 5, 2025), d(5, 9, 2025), 2, E6M),
        c(d(3, 7, 2025), 1e6, d(3, 4, 2025), d(3, 7, 2025), 0, NUL3M),
        c(d(18, 9, 2025), 1e6, d(18, 8, 2025), d(18, 9, 2025), 2, E1M),
        c(d(19, 8, 2025), 1e6, d(18, 8, 2025), d(19, 8, 2025), 2, E1M),
        c(d(2, 1, 2026), 1e6, d(24, 12, 2025), d(2, 1, 2026), 3, E1M, fixing_convention=ql.Following),
    ]


def pricers():
    """(label, pricer spec, at-par setting)."""
    return [("switch at par", ibc.pricer_spec(), True), ("switch indexed", ibc.pricer_spec(), False),
            ("explicitly indexed", ibc.pricer_spec(use_indexed=True), True),
            ("explicitly at par", ibc.pricer_spec(use_indexed=False), False), ("no pricer", None, True)]


def legs():
    # Unadjusted dates on weekends at month end, which the fixing convention moves with no fixing days.
    eur3 = ibc.sched(d(30, 11, 2025), d(30, 11, 2026), ibc.THREE_M, convention=ql.Unadjusted)
    eur6 = ibc.sched(d(17, 7, 2025), d(17, 7, 2028), ibc.SIX_M)
    nul = ibc.sched(d(5, 4, 2025), d(5, 4, 2026), ibc.THREE_M, convention=ql.Unadjusted, cal=ibc.NULL_CALENDAR)
    out = []
    for convention in (ql.Following, ql.ModifiedFollowing, ql.ModifiedPreceding, ql.Unadjusted):
        spec = ibc.leg_spec(eur3, E3M, [1e6], spreads=[0.001], fixing_days=[0])
        spec["fixingConvention"] = int(convention)
        out.append((f"fixing convention {convention} on weekend fixings at month end", spec))
    spec = ibc.leg_spec(nul, E3M, [1e6], fixing_days=[3])
    spec["fixingConvention"] = int(ql.Preceding)
    out.append(("preceding fixings for a schedule on a calendar with no holidays", spec))
    for lag in (0, 2):
        spec = ibc.leg_spec(eur6, E6M, [1e6, 8e5], payment_lag=lag, gearings=[1.0, 0.0, 1.5], spreads=[0.0, 0.02],
                            payment_dc=ibc.A360)
        spec["zeroPayments"] = True
        out.append((f"zero payments, lag {lag}", spec))
    spec = ibc.leg_spec(eur6, E6M, [1e6], in_arrears=True, pricer=ibc.pricer_spec(ibc.VOL_LN))
    spec["zeroPayments"] = True
    out.append(("zero payments in arrears", spec))
    spec = ibc.leg_spec(eur6, E6M, [1e6], caps=[0.035], pricer=ibc.pricer_spec(ibc.VOL_LN))
    spec["zeroPayments"] = False
    spec["fixingConvention"] = int(ql.Following)
    out.append(("capped, no zero payments, following fixings", spec))
    broken = ibc.sched(d(17, 7, 2025), d(19, 4, 2027), ibc.SIX_M, rule=ql.DateGeneration.Forward)
    for at_par in (True, False):
        for indexed in (None, True, False):
            spec = ibc.leg_spec(broken, E6M, [1e6], fixing_days=[1], indexed=indexed)
            spec["atParCoupons"] = at_par
            out.append((f"at-par coupons {at_par} after indexed coupons {indexed}", (spec, not at_par)))
    return out


def cases():
    with cxx.connect() as H:
        out = []
        for k, spec in enumerate(coupons()):
            for label, p, at_par in pricers():
                with H.case_settings(v.enc(TODAY), use_at_par_coupons=at_par) as st:
                    coupon = dict(spec, pricer=p)
                    out.append({"name": f"{k}: {label}", "inputs": {"settings": st, "coupon": coupon},
                                "expected": {m: v.attempt(lambda m=m: H.call(f"IborCoupon::{m}", coupon))
                                             for m in CACHED}})
        for name, spec in legs():
            spec, switch = spec if isinstance(spec, tuple) else (spec, None)
            with H.case_settings(v.enc(TODAY), use_at_par_coupons=switch) as st:
                try:
                    cashflows = H.call("IborLeg::operator Leg", spec)
                except RuntimeError:
                    out.append({"name": f"leg: {name}", "inputs": {"settings": st, "leg": spec},
                                "expected": {"leg": dict(v.THROWS)}})
                    continue
                scale = max(spec["notionals"])
                expected = {key: [cf[key] for cf in cashflows]
                            for key in ("date", "nominal", "accrualStartDate", "accrualEndDate", "referencePeriodStart",
                                        "referencePeriodEnd", "exCouponDate", "fixingDate", "rate")}
                expected["amount"] = [v.amount(cf["amount"], scale) for cf in cashflows]
                out.append({"name": f"leg: {name}", "inputs": {"settings": st, "leg": spec}, "expected": expected})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/iborcoupon.hpp", cases)
