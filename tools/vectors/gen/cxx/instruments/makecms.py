# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MakeCms (ql/instruments/makecms.hpp, .cpp), which the wheel doesn't bind, each output computed
by the C++ harness, on the evaluation date Monday 16 June 2025, the curve, swap index, volatility and pricer specs those
of tools/vectors/gen/cashflows/conundrumpricer.py.

Each case builds a swap from a tenor, the EUR 10Y swap index, its own Ibor index or Euribor 3M, and a list of setters
called in order: the defaults; cms.cpp's Ibor spread and 10-day forward start; the CMS leg received on a nominal of 100;
an effective date, the legs' tenors, calendars, conventions, rules, end-of-month flags, first and next-to-last dates and
day counts; the at-the-money spread, on the index's forwarding curve and on a discounting curve of the builder's (a flat
4%); and the at-the-money spread without a pricer, which QuantLib refuses ("no CmsCouponPricer set (yet)"). Its outputs
are each leg's cash flows ([date, accrual start, accrual end, nominal, whether a CMS coupon, spread, fixing date]) and,
with a pricer (an AnalyticHaganPricer on the Standard model and the lognormal matrix, set by withCmsCouponPricer), the
legs' NPVs and the NPV on the engine the builder attaches.

A refused value is {"throws": true}. Each case's inputs hold its settings and the call's arguments: "swapTenor",
"swapIndex", "iborIndex" (null for the swap index's own), "setters" and "pricer" (null for none).
tests/NanoQL.Tests/Vectors/Cxx/Instruments/MakeCmsCxxVectorTests.cs reads it back."""
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
    "wheel_conundrumpricer", os.path.join(v.GEN_ROOT, "cashflows", "conundrumpricer.py"))
cp = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cp)
svc, irx = cp.svc, cp.irx

TODAY = cp.TODAY
Y, M, D = int(ql.Years), int(ql.Months), int(ql.Days)
PRICER = {"type": "AnalyticHaganPricer", "model": int(ql.GFunctionFactory.Standard), "meanReversion": 0.0,
          "volatility": svc.atm_floating()}
EURIBOR3M = irx.ibor_spec("Euribor", (3, ql.Months), 2, irx.TARGET, ql.ModifiedFollowing, False, irx.A360, cp.CURVE)


def cases_specs():
    cal = ql.TARGET()
    effective = cal.advance(TODAY, 1, ql.Months).serialNumber()
    first = cal.advance(ql.Date(effective), 2, ql.Months).serialNumber()
    other = svc.flat_curve(TODAY, 0.04)
    conventions = [["withCmsLegCalendar", irx.TARGET], ["withCmsLegConvention", int(ql.Following)],
                   ["withCmsLegTerminationDateConvention", int(ql.Unadjusted)],
                   ["withCmsLegRule", int(ql.DateGeneration.Forward)], ["withCmsLegEndOfMonth", True],
                   ["withCmsLegDayCount", irx.A365], ["withFloatingLegCalendar", irx.TARGET],
                   ["withFloatingLegConvention", int(ql.ModifiedFollowing)],
                   ["withFloatingLegTerminationDateConvention", int(ql.Following)],
                   ["withFloatingLegRule", int(ql.DateGeneration.Backward)], ["withFloatingLegEndOfMonth", False],
                   ["withFloatingLegDayCount", irx.A360]]
    return [
        ("the defaults", [5, Y], None, [], None),
        ("cms.cpp's spread and forward start, priced", [5, Y], None,
         [["withIborSpread", 0.0], ["withForwardStart", [10, D]]], PRICER),
        ("received, a nominal of 100, Euribor 3M, priced", [3, Y], EURIBOR3M,
         [["receiveCms", True], ["withNominal", 100.0], ["withIborSpread", 0.002]], PRICER),
        ("an effective date, the legs' tenors and conventions", [4, Y], None,
         [["withEffectiveDate", effective], ["withCmsLegTenor", [6, M]], ["withFloatingLegTenor", [3, M]]]
         + conventions, None),
        ("first and next-to-last dates", [2, Y], None,
         [["withEffectiveDate", effective], ["withCmsLegFirstDate", first], ["withFloatingLegFirstDate", first]],
         None),
        ("next-to-last dates", [2, Y], None,
         [["withEffectiveDate", effective],
          ["withCmsLegNextToLastDate", cal.advance(ql.Date(effective), 22, ql.Months).serialNumber()],
          ["withFloatingLegNextToLastDate", cal.advance(ql.Date(effective), 21, ql.Months).serialNumber()]], None),
        ("the at-the-money spread", [5, Y], None, [["withAtmSpread", True]], PRICER),
        ("the at-the-money spread on a discounting curve", [5, Y], None,
         [["withAtmSpread", True], ["withDiscountingTermStructure", other]], PRICER),
        ("the at-the-money spread without a pricer", [5, Y], None, [["withAtmSpread", True]], None),
    ]


def cases():
    out = []
    with cxx.connect() as H:
        for name, tenor, ibor, setters, pricer in cases_specs():
            with H.case_settings(v.enc(TODAY)) as st:
                try:
                    r = H.call("MakeCms::operator ext::shared_ptr<Swap>", tenor, cp.CMS10, ibor, setters, pricer)
                    expected = {"legs": r["legs"], "npv": r["npv"]}
                except RuntimeError:
                    expected = {"legs": dict(v.THROWS), "npv": dict(v.THROWS)}
                out.append({"name": name,
                            "inputs": {"settings": st, "swapTenor": tenor, "swapIndex": cp.CMS10, "iborIndex": ibor,
                                       "setters": setters, "pricer": pricer},
                            "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/makecms.hpp", cases)
