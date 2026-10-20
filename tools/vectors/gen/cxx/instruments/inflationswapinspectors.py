# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the inflation swaps' and CPIBond's inspectors (ql/instruments/zerocouponinflationswap.hpp,
yearonyearinflationswap.hpp, cpiswap.hpp and bonds/cpibond.hpp), each output computed by the C++ harness: the wheel
binds none of them.

- "zeroCouponInflationSwap" cases: swaps with the inflation leg's calendar and convention given, the calendar alone,
  and neither, which QuantLib replaces by the fixed leg's: every inspector. A convention given as Following, which
  QuantLib replaces as well, is left out (tests/NanoQL.Tests/Divergences/ZeroCouponInflationSwapConventionTests.cs).
- "yearOnYearInflationSwap" cases: payer and receiver swaps on the ratio index: every inspector.
- "cpiSwap" cases: swaps with and without a base CPI and an inflation nominal: every inspector.
- "cpiBond" cases: bonds with and without a base CPI and an issue date: every inspector.

Each inspector is an output of its own name. A calendar or day counter is its name, an index its name, a lag [length, units], a schedule its dates. A case's inputs
hold its settings, the index spec (tools/vectors/gen/cashflows/indexedcashflow.py's) and the instrument's inputs, with
the wheel generators' names (tools/vectors/gen/instruments/zerocouponinflationswap.py, yearonyearinflationswap.py and
cpiswap.py, at the top level; bonds/cpibond.py's bond spec as "bond"). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Instruments/InflationSwapInspectorsCxxVectorTests.cs."""
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


def _load(name, *path):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *path))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ic = _load("cxx_inflationcoupon", "cxx", "cashflows", "inflationcoupon.py")
cpiswap = _load("wheel_cpiswap", "instruments", "cpiswap.py")
cpibond = _load("wheel_cpibond", "instruments", "bonds", "cpibond.py")
cf = ic.cf

TODAY = ic.TODAY
UK = ic.UK
A365 = ic.A365
THIRTY360_BB = ic.THIRTY360_BB
M = ic.M
PAYER, RECEIVER = int(ql.Swap.Payer), int(ql.Swap.Receiver)


def zero_swap(t, nominal, start, maturity, fix_cal, fix_conv, dc, rate, lag, interp, inf_cal, inf_conv):
    return {"type": t, "nominal": nominal, "startDate": v.enc(start), "maturity": v.enc(maturity),
            "fixCalendar": fix_cal, "fixConvention": int(fix_conv), "dayCounter": dc, "fixedRate": rate,
            "observationLag": [lag, M], "interpolation": int(interp), "infCalendar": inf_cal,
            "infConvention": None if inf_conv is None else int(inf_conv)}


def annual(start, end):
    return cf.schedule_spec(start, end, ql.Period(1, ql.Years), UK, ql.Unadjusted, ql.DateGeneration.Backward, False)


def semiannual(start, end, convention=ql.ModifiedFollowing):
    return cf.schedule_spec(start, end, ql.Period(6, ql.Months), UK, convention, ql.DateGeneration.Backward, False)


def yoy_swap(t, nominal, fixed_schedule, rate, fixed_dc, yoy_schedule, lag, interp, spread, yoy_dc, convention):
    return {"type": t, "nominal": nominal, "fixedSchedule": fixed_schedule, "fixedRate": rate,
            "fixedDayCount": fixed_dc, "yoySchedule": yoy_schedule, "observationLag": [lag, M],
            "interpolation": int(interp), "spread": spread, "yoyDayCount": yoy_dc, "paymentCalendar": UK,
            "paymentConvention": int(convention)}


def cpi_swap(t, nominal, subtract, spread, float_dc, float_schedule, float_roll, rate, base, fixed_dc, fixed_schedule,
             fixed_roll, lag, interp, inflation_nominal):
    return {"type": t, "nominal": nominal, "subtractInflationNominal": subtract, "spread": spread,
            "floatDayCount": float_dc, "floatSchedule": float_schedule, "floatRoll": int(float_roll), "fixingDays": 0,
            "floatIndex": cpiswap.GBP_IBOR, "fixedRate": rate, "baseCPI": base, "fixedDayCount": fixed_dc,
            "fixedSchedule": fixed_schedule, "fixedRoll": int(fixed_roll), "observationLag": [lag, M],
            "interpolation": int(interp), "inflationNominal": inflation_nominal}


END = ql.Date(15, ql.June, 2029)
INSTRUMENTS = [
    ("zeroCouponInflationSwap", "ZeroCouponInflationSwap", "payer, the inflation leg's defaults", ic.UKRPI,
     zero_swap(PAYER, 1e6, TODAY, END, UK, ql.ModifiedFollowing, THIRTY360_BB, 0.032, 3, ql.CPI.Flat, None, None)),
    ("zeroCouponInflationSwap", "ZeroCouponInflationSwap", "receiver, TARGET and Preceding", ic.UKRPI,
     zero_swap(RECEIVER, 5e6, TODAY, ql.Date(31, ql.May, 2025), UK, ql.Following, A365, 0.03, 2, ql.CPI.Linear,
               cf.TARGET, ql.Preceding)),
    ("zeroCouponInflationSwap", "ZeroCouponInflationSwap", "payer, the inflation calendar alone", ic.UKRPI,
     zero_swap(PAYER, 1e6, TODAY, ql.Date(1, ql.January, 2028), UK, ql.ModifiedFollowing, A365, 0.031, 3,
               ql.CPI.Flat, cf.TARGET, None)),
    ("yearOnYearInflationSwap", "YearOnYearInflationSwap", "payer, flat", ic.RATIO,
     yoy_swap(PAYER, 1e6, annual(TODAY, END), 0.03, THIRTY360_BB, annual(TODAY, END), 2, ql.CPI.Flat, 0.0,
              THIRTY360_BB, ql.ModifiedFollowing)),
    ("yearOnYearInflationSwap", "YearOnYearInflationSwap", "receiver, semiannual, spread, Following", ic.RATIO,
     yoy_swap(RECEIVER, 5e6, annual(TODAY, END), 0.0285, A365, semiannual(TODAY, END), 3, ql.CPI.Linear, 0.0015,
              A365, ql.Following)),
    ("cpiSwap", "CPISwap", "payer, base CPI, subtracted", ic.UKRPI,
     cpi_swap(PAYER, 1e6, True, 0.001, A365, semiannual(TODAY, END), ql.ModifiedFollowing, 0.02, 306.1, A365,
              annual(TODAY, END), ql.ModifiedFollowing, 3, ql.CPI.Flat, None)),
    ("cpiSwap", "CPISwap", "receiver, no base, linear, inflation nominal", ic.UKRPI,
     cpi_swap(RECEIVER, 1e6, False, 0.0, cf.A360, semiannual(TODAY, END, ql.Following), ql.Following, 0.0175,
              None, THIRTY360_BB, annual(TODAY, END), ql.Following, 2, ql.CPI.Linear, 1.2e6)),
    ("cpiBond", "CPIBond", "base CPI, flat, annual", ic.UKRPI,
     cpibond.bond_spec(3, 100.0, 300.0, 3, ql.CPI.Flat, annual(TODAY, END), [0.01], THIRTY360_BB, calendar=UK)),
    ("cpiBond", "CPIBond", "no base, linear, semiannual, issue date", ic.UKRPI,
     cpibond.bond_spec(2, 1000.0, None, 2, ql.CPI.Linear, semiannual(TODAY, END), [0.0125], A365,
                       convention=ql.Following, issue=ql.Date(10, ql.June, 2024), calendar=UK)),
]


def cases():
    out = []
    with cxx.connect() as H:
        for k, (kind, cls, label, index, spec) in enumerate(INSTRUMENTS):
            with H.case_settings(v.enc(TODAY), fixings=ic.harness_fixings()) as st:
                inputs = {"settings": st, "kind": kind, "index": index}
                if kind == "cpiBond":
                    inputs["bond"] = spec
                else:
                    inputs.update(spec)
                out.append({"name": f"{kind} {label} / {k}", "inputs": inputs,
                            "expected": H.call(cls + "::inspectors", index, spec)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/zerocouponinflationswap.hpp", cases)
