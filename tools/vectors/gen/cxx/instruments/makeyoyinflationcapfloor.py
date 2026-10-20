# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's year-on-year cap and floor builder (ql/instruments/makeyoyinflationcapfloor.hpp) and the
cap and floor type's stream text (ql/instruments/inflationcapfloor.hpp), each output computed by the C++ harness: the
wheel binds neither.

- "build" cases: caps and floors from the evaluation date, an effective date, fixing days and a forward start, as an
  optionlet, struck explicitly and at the money on a nominal curve, with each setter: the instrument's type, capRates,
  floorRates, startDate, maturityDate and its coupons' accrual and payment dates, nominals, fixing dates, fixing days
  and day counters, each a list ("couponAccrualStartDate" and so on); and the refusals: a collar, no strike and no nominal curve, a strike then an at-the-money strike.
- "typeText" cases: each type's text, and a value outside the three (refused).

A case's inputs hold its settings (UK RPI's fixings stored by name), the index spec
(tools/vectors/gen/cashflows/indexedcashflow.py's ratio of UK RPI) and the builder's spec, as
tools/cxx/src/instruments/makeyoyinflationcapfloor.cpp reads it. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Instruments/MakeYoYInflationCapFloorCxxVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location("cxx_inflationcoupon",
                                               os.path.join(v.GEN_ROOT, "cxx", "cashflows", "inflationcoupon.py"))
ic = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ic)

TODAY = ic.TODAY
UK = ic.UK
M = ic.M
CAP, FLOOR, COLLAR = int(ql.YoYInflationCapFloor.Cap), int(ql.YoYInflationCapFloor.Floor), int(
    ql.YoYInflationCapFloor.Collar)


def builder(t, length, lag=2, interp=ql.CPI.Flat, **setters):
    spec = {"type": t, "length": length, "calendar": UK, "observationLag": [lag, M], "interpolation": int(interp),
            "nominal": None, "effectiveDate": None, "paymentDayCounter": None, "paymentAdjustment": None,
            "fixingDays": None, "asOptionlet": None, "strike": None, "atmStrike": None, "forwardStart": None}
    spec.update(setters)
    return spec


BUILDERS = [
    ("cap, the defaults", builder(CAP, 5, strike=0.03)),
    ("floor, effective date, nominal, day counter, adjustment",
     builder(FLOOR, 3, effectiveDate=v.enc(ql.Date(15, ql.March, 2025)), nominal=2e6, paymentDayCounter=ic.A365,
             paymentAdjustment=int(ql.Following), strike=0.025)),
    ("cap, fixing days, forward start, optionlet",
     builder(CAP, 4, fixingDays=2, forwardStart=[6, M], asOptionlet=True, strike=0.03)),
    ("cap, optionlet off, linear", builder(CAP, 2, lag=3, interp=ql.CPI.Linear, asOptionlet=False, strike=0.028)),
    ("cap, at the money", builder(CAP, 4, atmStrike=ic.NOMINAL)),
    ("floor, at the money, optionlet", builder(FLOOR, 6, atmStrike=ic.NOMINAL, asOptionlet=True)),
    ("collar", builder(COLLAR, 3, strike=0.03)),
    ("no strike and no nominal curve", builder(CAP, 3)),
    ("a strike then an at-the-money strike", builder(CAP, 3, strike=0.03, atmStrike=ic.NOMINAL)),
]


COUPON_MEMBERS = ["accrualStartDate", "accrualEndDate", "date", "nominal", "fixingDate", "fixingDays", "dayCounter"]


def build_cases(H):
    out = []
    for k, (label, spec) in enumerate(BUILDERS):
        with H.case_settings(v.enc(TODAY), fixings=ic.harness_fixings()) as st:
            result = v.attempt(lambda: H.call("MakeYoYInflationCapFloor::build", ic.RATIO, spec))
            if "throws" in result:
                expected = {"build": result}
            else:
                coupons = result.pop("coupons")
                expected = dict(result, **{"coupon" + name[0].upper() + name[1:]: [c[name] for c in coupons]
                                           for name in COUPON_MEMBERS})
            out.append({"name": f"build {label} / {k}",
                        "inputs": {"settings": st, "kind": "build", "index": ic.RATIO, "builder": spec},
                        "expected": expected})
    return out


def type_text_cases(H):
    out = []
    for k in range(4):
        with H.case_settings(v.enc(TODAY)) as st:
            out.append({"name": f"typeText {k} / {k}",
                        "inputs": {"settings": st, "kind": "typeText", "type": k},
                        "expected": {"typeText": v.attempt(lambda: H.call("YoYInflationCapFloor::typeText", k))}})
    return out


def cases():
    with cxx.connect() as H:
        return build_cases(H) + type_text_cases(H)


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/makeyoyinflationcapfloor.hpp", cases)
