# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DigitalCmsLeg and DigitalCmsCoupon (ql/cashflows/digitalcmscoupon.hpp, .cpp), which the wheel
doesn't bind, each output computed by the C++ harness, on the evaluation date of
tools/vectors/gen/cashflows/conundrumpricer.py (16 June 2025) and its flat 5% curve: two-year semiannual legs on its
10-year EUR swap index, priced by the analytic Hagan pricer on the standard model and its at-the-money matrix: cash
calls, asset puts per coupon, short collars on the sub-replication in arrears, naked options, and a zero gearing with
its spread given, the leg building a fixed coupon at it.

Each case's outputs are, per coupon, tools/cxx/src/cashflows/digitalspec.hpp's leg outputs, flattened into a list per
name over the coupons: "digital", "date", the digital inspectors and the rates, null for a fixed coupon but its rate, a
refused value {"throws": true}. Each case's inputs hold its settings and the leg ("leg": digitalspec.hpp's leg spec,
always setting the naked option flag, which QuantLib's builder leaves uninitialized otherwise, the S-cms plan's CMR5
(viii)). tests/NanoQL.Tests/Vectors/Cxx/Coupons/DigitalCmsCouponCxxVectorTests.cs reads it back."""
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


cp = _load("wheel_conundrumpricer", "cashflows/conundrumpricer.py")
ibc = _load("wheel_iborcoupon", "cashflows/iborcoupon.py")
xcp = _load("cxx_conundrumpricer", "cxx/cashflows/conundrumpricer.py")
svc = cp.svc

TODAY = cp.TODAY
INDEX = svc.euribor([10, svc.Y], cp.CURVE)
PRICER = xcp.pricer("AnalyticHaganPricer", xcp.LOGNORMAL)
NAMES = ["digital", "date", "callStrike", "putStrike", "callDigitalPayoff", "putDigitalPayoff", "hasCall", "hasPut",
         "hasCollar", "isLongCall", "isLongPut", "accrualStartDate", "accrualEndDate", "fixingDate", "nominal",
         "gearing", "spread", "rate", "callOptionRate", "putOptionRate"]
LONG, SHORT = int(ql.Position.Long), int(ql.Position.Short)
SUB = 0  # QuantLib's Replication::Sub, which the wheel doesn't bind


def leg(index, pricer, gearings=(), spreads=(), in_arrears=False, call_strikes=(), long_call=LONG, call_atm=False,
        call_payoffs=(), put_strikes=(), long_put=LONG, put_atm=False, put_payoffs=(), replication=None, naked=False):
    schedule = ibc.sched(ql.Date(18, ql.June, 2025), ql.Date(18, ql.June, 2027), ql.Period(6, ql.Months))
    return {"schedule": schedule, "index": index, "notionals": [100.0], "paymentDayCounter": ibc.A360,
            "paymentAdjustment": int(ql.ModifiedFollowing), "fixingDays": [], "gearings": list(gearings),
            "spreads": list(spreads), "inArrears": in_arrears, "callStrikes": list(call_strikes),
            "longCallOption": long_call, "callATM": call_atm, "callPayoffs": list(call_payoffs),
            "putStrikes": list(put_strikes), "longPutOption": long_put, "putATM": put_atm,
            "putPayoffs": list(put_payoffs), "replication": replication, "nakedOption": naked, "pricer": pricer}


def flattened(coupons):
    return {name: [c.get(name) for c in coupons] for name in NAMES}


def legs(index, pricer):
    return [
        ("cash calls at 5%", leg(index, pricer, call_strikes=[0.05], call_payoffs=[0.01])),
        ("asset puts per coupon, short", leg(index, pricer, put_strikes=[0.04, 0.045, 0.05, 0.055], long_put=SHORT)),
        ("short collars, sub-replication, in arrears",
         leg(index, pricer, in_arrears=True, call_strikes=[0.06], long_call=SHORT, call_payoffs=[0.01],
             put_strikes=[0.04], put_payoffs=[0.005], replication={"type": SUB, "gap": 1e-4})),
        ("naked cash calls", leg(index, pricer, call_strikes=[0.05], call_payoffs=[0.01], naked=True)),
        ("a zero gearing with its spread", leg(index, pricer, gearings=[0.0, 1.0], spreads=[0.01], call_strikes=[0.05],
                                               call_payoffs=[0.01])),
    ]


def run(method, today, cases_of):
    out = []
    with cxx.connect() as H:
        for name, spec in cases_of:
            with H.case_settings(v.enc(today)) as st:
                result = v.attempt(lambda: H.call(method, spec))
                expected = flattened(result) if isinstance(result, list) else {"leg": result}
                out.append({"name": name, "inputs": {"settings": st, "leg": spec}, "expected": expected})
    return out


def cases():
    return run("DigitalCmsLeg", TODAY, legs(INDEX, PRICER))


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/digitalcmscoupon.hpp", cases)
