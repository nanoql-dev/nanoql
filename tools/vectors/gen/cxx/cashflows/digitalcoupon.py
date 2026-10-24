# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DigitalCoupon (ql/cashflows/digitalcoupon.hpp, .cpp), which the wheel doesn't bind, each
output computed by the C++ harness, on the evaluation date of tools/vectors/gen/cashflows/iborcoupon.py (15 January
2025) and its curve: digital calls, puts and collars, cash- and asset-or-nothing, long and short, at-the-money included
or not, on the three replications and a naked option, over Ibor coupons priced by the Black pricer on a lognormal and a
normal caplet volatility, one of them fixing on the evaluation date; and the constructor's refusals, a non-positive gap
and a payoff without its strike.

Each case's outputs are the digital coupon's inspectors and its rate, call option rate and put option rate
(tools/cxx/src/cashflows/digitalspec.hpp's), a refused value {"throws": true}. Each case's inputs hold the underlying
coupon spec ("underlying", as iborcoupon.py's functions build it, its pricer set) and the digital terms ("terms").
tests/NanoQL.Tests/Vectors/Cxx/Coupons/DigitalCouponCxxVectorTests.cs reads it back."""
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
LONG, SHORT = int(ql.Position.Long), int(ql.Position.Short)
# QuantLib's Replication::Type, which the wheel doesn't bind (replication.hpp:34)
SUB, CENTRAL, SUPER = 0, 1, 2
INSPECTORS = ["digital", "callStrike", "putStrike", "callDigitalPayoff", "putDigitalPayoff", "hasCall", "hasPut",
              "hasCollar", "isLongCall", "isLongPut", "date", "accrualStartDate", "accrualEndDate", "fixingDate",
              "nominal", "gearing", "spread"]
RATES = ["rate", "callOptionRate", "putOptionRate"]


def terms(call=None, call_position=LONG, call_atm=False, call_payoff=None, put=None, put_position=LONG, put_atm=False,
          put_payoff=None, replication=None, naked=False):
    return {"callStrike": call, "callPosition": call_position, "isCallATMIncluded": call_atm,
            "callDigitalPayoff": call_payoff, "putStrike": put, "putPosition": put_position, "isPutATMIncluded": put_atm,
            "putDigitalPayoff": put_payoff, "replication": replication, "nakedOption": naked}


def cases():
    lognormal = ibc.pricer_spec(ibc.VOL_LN)
    normal = ibc.pricer_spec(ibc.VOL_N)
    c = ibc.ibor_coupon_spec
    year = c(d(19, 1, 2027), 1e6, d(19, 1, 2026), d(19, 1, 2027), 2, E6M, pricer=lognormal)
    geared = c(d(19, 1, 2027), 1e6, d(19, 1, 2026), d(19, 1, 2027), 2, E6M, 2.0, 0.001, pricer=lognormal)
    on_normal = c(d(19, 1, 2027), 1e6, d(19, 1, 2026), d(19, 1, 2027), 2, E6M, pricer=normal)
    today = c(d(19, 7, 2025), 1e6, d(17, 1, 2025), d(17, 7, 2025), 2, E6M, pricer=lognormal)
    rep = lambda t, gap=1e-4: {"type": t, "gap": gap}  # noqa: E731
    specs = [
        ("cash call, long", year, terms(call=0.03, call_payoff=0.01)),
        ("cash call, short, at the money included", year, terms(call=0.03, call_position=SHORT, call_atm=True,
                                                                call_payoff=0.01)),
        ("asset call, long", year, terms(call=0.025)),
        ("cash put, long", year, terms(put=0.03, put_payoff=0.02)),
        ("asset put, short", year, terms(put=0.035, put_position=SHORT)),
        ("cash collar", year, terms(call=0.04, call_payoff=0.01, put=0.02, put_payoff=0.005)),
        ("asset collar, short call", year, terms(call=0.04, call_position=SHORT, put=0.02)),
        ("sub-replication, short call, long put", year, terms(call=0.03, call_position=SHORT, call_payoff=0.01,
                                                              put=0.025, put_payoff=0.01, replication=rep(SUB))),
        ("super-replication, long call, short put", year, terms(call=0.03, call_payoff=0.01, put=0.025,
                                                                put_position=SHORT, put_payoff=0.01,
                                                                replication=rep(SUPER))),
        ("central replication, a gap of 1e-6", year, terms(call=0.03, call_payoff=0.01, replication=rep(CENTRAL, 1e-6))),
        ("naked option", year, terms(call=0.03, call_payoff=0.01, naked=True)),
        ("geared and spread underlying", geared, terms(call=0.05, call_payoff=0.01, put=0.04)),
        ("normal volatility", on_normal, terms(call=0.03, call_payoff=0.01, put=0.02)),
        ("fixing on the evaluation date", today, terms(call=0.02, call_payoff=0.01, put=0.03)),
        ("refused: a zero gap", year, terms(call=0.03, replication=rep(CENTRAL, 0.0))),
        ("refused: a put payoff without its strike", year, terms(put_payoff=0.01)),
        ("refused: a call payoff without its strike", year, terms(call_payoff=0.01)),
    ]
    out = []
    with cxx.connect() as H:
        for name, underlying, t in specs:
            with H.case_settings(v.enc(TODAY)) as st:
                inspectors = v.attempt(lambda: H.call("DigitalCoupon::inspectors", underlying, t))
                if isinstance(inspectors, dict) and "throws" not in inspectors:
                    rates = H.call("DigitalCoupon::rates", underlying, t)
                    expected = {**{k: inspectors[k] for k in INSPECTORS}, **{k: rates[k] for k in RATES}}
                else:
                    expected = {"inspectors": inspectors}
                out.append({"name": name, "inputs": {"settings": st, "underlying": underlying, "terms": t},
                            "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/digitalcoupon.hpp", cases)
