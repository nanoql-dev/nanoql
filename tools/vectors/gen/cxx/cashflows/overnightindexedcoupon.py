# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for OvernightLeg's setters that the wheel doesn't reach (ql/cashflows/overnightindexedcoupon.hpp), each
output computed by the C++ harness: withLastRecentPeriod, which starts each coupon's rate computation that period
before its end, withLastRecentPeriodCalendar, the calendar it is counted on (the schedule's by default), and
withCouponPricer, which sets the pricer of every coupon and capped coupon and is refused for the other averaging.

16 legs on quarterly and monthly SOFR and ESTR schedules: last recent periods of 1 month, 2 weeks, 10 and 45 days, in
arrears and in advance, on the schedule's calendar and on others; a negative period, which starts the computation after
its end (QuantLib's "degenerate schedule"); a coupon pricer (compounding, averaging with the approximation, the Black
pricers on capped legs) and the refusals of a compounding pricer on an averaged leg and an averaging pricer on a
compounded one. Each leg's outputs are its length (or the conversion's throw) and each cash flow's type, dates,
nominal, rate-computation dates, pricer and underlying pricer (their class names), rate and amount.

Each case's inputs hold its settings (the fixings stored under the index's name) and its "leg" spec, as
tools/vectors/gen/cashflows/overnightindexedcoupon.py writes it, with "lastRecentPeriod" ([length, units] or null),
"lastRecentPeriodCalendar" and "couponPricer". tests/NanoQL.Tests/Vectors/Cxx/Coupons/OvernightIndexedCouponCxxVectorTests.cs
reads it back."""
import importlib.util
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_overnightindexedcoupon",
                                               os.path.join(v.GEN_ROOT, "cashflows", "overnightindexedcoupon.py"))
oic = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(oic)

TODAY = ql.Date(3, 3, 2025)
FIELDS = ["type", "paymentDate", "accrualStartDate", "accrualEndDate", "referencePeriodStart", "referencePeriodEnd",
          "nominal", "rateComputationStartDate", "rateComputationEndDate", "pricer", "underlyingPricer"]


def period(n, unit):
    return [n, int(unit)]


def legs():
    us_gov = {"type": "UnitedStates", "market": int(ql.UnitedStates.GovernmentBond)}
    sofr = oic.index_spec(0, TODAY, 0.0405)
    estr = oic.index_spec(1, TODAY, 0.0291, oic.A360, ql.Simple)
    q = oic.rule_schedule(ql.Date(20, 2, 2025), ql.Date(20, 2, 2026), ql.Period(3, ql.Months), us_gov,
                          ql.ModifiedFollowing)
    m = oic.rule_schedule(ql.Date(31, 1, 2025), ql.Date(31, 7, 2025), ql.Period(1, ql.Months), oic.irx.TARGET,
                          ql.ModifiedFollowing, ql.DateGeneration.Backward, True)
    vol = oic.vol_spec(TODAY, 0.25)
    out = []

    def leg(name, schedule, index, last=None, last_calendar=None, pricer=None, **kw):
        spec = oic.leg_spec(schedule, index, [1e6], oic.A360, **kw)
        spec["lastRecentPeriod"] = last
        spec["lastRecentPeriodCalendar"] = last_calendar
        spec["couponPricer"] = pricer
        out.append((name, spec))

    leg("1M last recent period", q, sofr, period(1, ql.Months))
    leg("2W last recent period", q, sofr, period(2, ql.Weeks))
    leg("10D last recent period, in advance", q, sofr, period(10, ql.Days), in_arrears=False)
    leg("45D last recent period on TARGET", q, sofr, period(45, ql.Days), oic.irx.TARGET)
    leg("10D last recent period on the UK calendar", m, estr, period(10, ql.Days), oic.irx.UK_EXCHANGE)
    leg("no last recent period, a calendar given", m, estr, None, oic.irx.US_SOFR)
    leg("a negative last recent period", q, sofr, period(-5, ql.Days))
    leg("1M last recent period, averaged", m, estr, period(1, ql.Months), averaging=ql.RateAveraging.Simple)
    leg("compounding pricer", q, sofr, pricer=oic.pricer_spec("Compounding"))
    leg("averaging pricer with the approximation", m, estr,
        pricer=oic.pricer_spec("ArithmeticAveraged", by_approx=True), averaging=ql.RateAveraging.Simple)
    leg("Black compounding pricer on a capped leg", q, sofr, pricer=oic.pricer_spec("BlackCompounding", vol),
        caps=[0.045], floors=[0.03])
    leg("Black compounding pricer on daily caps", q, sofr, pricer=oic.pricer_spec("BlackCompounding", vol),
        caps=[0.042], daily=True)
    leg("Black averaging pricer on a floored averaged leg", m, estr, pricer=oic.pricer_spec("BlackAveraging", vol),
        floors=[0.028], averaging=ql.RateAveraging.Simple)
    leg("Black compounding pricer, last recent period", q, sofr, period(1, ql.Months), None,
        oic.pricer_spec("BlackCompounding", vol), caps=[0.044])
    leg("compounding pricer on an averaged leg", m, estr, pricer=oic.pricer_spec("Compounding"),
        averaging=ql.RateAveraging.Simple)
    leg("averaging pricer on a compounded leg", q, sofr, pricer=oic.pricer_spec("ArithmeticAveraged"))
    return out


def cases():
    rng = random.Random(v.SEED + 4)
    with cxx.connect() as H:
        out = []
        for name, spec in legs():
            index = oic.make_index(spec["index"])
            pairs = oic.fixings_for(index, TODAY - 120, TODAY - 1, rng)
            fixings = {index.name(): [[d.serialNumber(), x] for d, x in pairs]}
            with H.case_settings(v.enc(TODAY), fixings=fixings) as st:
                size = v.attempt(lambda: H.call("OvernightLeg::size", spec))
                expected = {"size": size}
                if not isinstance(size, dict):
                    described = [H.call("OvernightLeg::describe", spec, i) for i in range(size)]
                    for f in FIELDS:
                        expected[f + "s"] = [d[f] for d in described]
                    expected["rates"] = [v.attempt(lambda i=i: H.call("OvernightLeg::rate", spec, i))
                                         for i in range(size)]
                    expected["amounts"] = oic.amounts([(lambda i=i: H.call("OvernightLeg::amount", spec, i))
                                                       for i in range(size)], 1e6)
                out.append({"name": name, "inputs": {"settings": st, "leg": spec}, "expected": expected})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/overnightindexedcoupon.hpp", cases)
