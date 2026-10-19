# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BlackCallableFixedRateBondEngine and BlackCallableZeroCouponBondEngine
(ql/experimental/callablebonds/blackcallablebondengine.hpp, .cpp) where the wheel can't reach, each output computed by
the C++ harness: the engines on a callable-bond volatility structure (a constant volatility, by reference date or by
settlement days), which the wheel's engine can't take, and the zero-coupon engine, which it doesn't name.

tools/vectors/gen/experimental/callablebonds/blackcallablebondengine.py's three bonds (a 5% fixed-rate bond and a
zero-coupon bond on QuantLib's test-runner date, a 3% annual bond whose curve starts on its evaluation date), each with
one call and, apart, one put at 100 clean on three of that file's dates, on six engines: the fixed-rate engine on
structures at the evaluation date (Actual/365, as the quote constructor's, which it reproduces), at a date a year
before it (Actual/360) and two days after it (Thirty/360), and moving with two TARGET settlement days; and the
zero-coupon engine by its quote and on the first structure. The structure's day counter and reference date give the
exercise time the option is priced to, and the bond's own conventions its forward yield. A case's outputs are the NPV
and the settlement value. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/CallableBonds/BlackCallableBondEngineCxxVectorTests.cs, with
tests/NanoQL.Tests/Vectors/CallableBondInputs.cs."""
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

_spec = importlib.util.spec_from_file_location(
    "wheel_blackcallablebondengine",
    os.path.join(v.GEN_ROOT, "experimental", "callablebonds", "blackcallablebondengine.py"))
bk = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bk)
cb = bk.cb


def structure(vol, reference=None, dc=cb.A365, settlement_days=None):
    if settlement_days is None:
        return {"referenceDate": v.enc(reference), "volatility": vol, "dayCounter": dc}
    return {"settlementDays": settlement_days, "calendar": cb.TARGET, "volatility": vol, "dayCounter": dc}


def engines(today, curve):
    """(label, engine spec) for the six engines."""
    return [
        ("a structure at the evaluation date",
         {"type": "BlackStructure", "volatility": structure(0.2, today), "discountCurve": curve}),
        ("a structure a year back, Actual/360",
         {"type": "BlackStructure", "volatility": structure(0.2, today - 365, cb.A360), "discountCurve": curve}),
        ("a structure two days on, Thirty/360",
         {"type": "BlackStructure", "volatility": structure(0.2, today + 2, cb.THIRTY360_BB), "discountCurve": curve}),
        ("a moving structure, two settlement days",
         {"type": "BlackStructure", "volatility": structure(0.2, None, cb.A365, 2), "discountCurve": curve}),
        ("the zero-coupon engine by its quote", {"type": "BlackZero", "volatility": 0.2, "discountCurve": curve}),
        ("the zero-coupon engine on a structure",
         {"type": "BlackZeroStructure", "volatility": structure(0.2, today), "discountCurve": curve}),
    ]


def cases():
    out = []
    with cxx.connect() as H:
        for name, st, build, curve, scale, exercise in bk.bonds():
            for label, date in exercise[1:4]:
                for kind, kind_name in ((cb.CALL, "call"), (cb.PUT, "put")):
                    spec = build([cb.callability_spec(100.0, cb.CLEAN, kind, date)])
                    for engine_label, engine in engines(st[0], curve):
                        with H.case_settings(st[0].serialNumber(), include_reference_date_events=st[1],
                                             include_todays_cash_flows=st[2]) as s:
                            out.append({"name": f"{name} / a {kind_name} on {label} / {engine_label}",
                                        "inputs": {"settings": s, "bond": spec, "engine": engine},
                                        "expected": {
                                            "NPV": v.amount(H.call("BlackCallableFixedRateBondEngine::NPV", spec,
                                                                   engine), scale),
                                            "settlementValue": v.amount(H.call(
                                                "BlackCallableFixedRateBondEngine::settlementValue", spec, engine),
                                                scale)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/callablebonds/blackcallablebondengine.hpp", cases)
