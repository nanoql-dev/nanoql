# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's OptionletStripper (ql/termstructures/volatility/optionlet/optionletstripper.hpp), each output
computed by the C++ harness on an OptionletStripper1: the members the wheel doesn't bind (the fixing tenors, payment
dates and accrual periods, the optionlet frequency and the payment lag), and the strip of the strippers the wheel can't
build, Ibor ones with a payment lag and no optionlet frequency (the wheel's proxy refuses None for its std::optional).

The strippers: QuantLib's own test fixtures (test-suite/optionletstripper.cpp, 28 October 2013: the flat 10 x 10 and the
16 x 13 market surfaces on a flat 4% curve, Euribor 6M, payment lags 0 and 2), the cap survey's 4 x 4 surface (16 June
2025, Euribor 6M and 3M), and SOFR on the stripper tests' overnight surfaces (15 April 2025, a 3M optionlet frequency,
lags 0 and 2, the 15 April fixing stored). Each case's outputs are the tenors, payment dates, accrual periods,
frequency, lag, fixing dates and times, at-the-money rates, every date's strikes and volatilities and the date past the
last ({"throws": true}), and the switch strike.

A stripper is {"surface" (a surface of termstructures/volatility/capfloor/capfloortermvolsurface.py), "index" (an Ibor
or overnight index spec of indexes/interestrateindex.py, its curve inside), "discount" (null or a flat curve of it),
"switchStrike" (null), "accuracy", "maxIter", "type", "displacement", "dontThrow", "optionletFrequency" (null or [length,
units]), "paymentLag"}, as tools/cxx/src/termstructures/volatility/optionlet/stripperspec.hpp reads it. The functions
here build the strippers the other harness stripper generators share. Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/OptionletStripperCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


os1 = _load("wheel_optionletstripper1", "termstructures/volatility/optionlet/optionletstripper1.py")
irx = os1.irx
cfs = os1.cfs
ql = os1.ql
M, Y = os1.M, os1.Y
TARGET = {"type": "TARGET"}
US_SOFR = {"type": "UnitedStates", "market": int(ql.UnitedStates.SOFR)}
A360 = {"type": "Actual360"}

OCT13 = ql.Date(28, 10, 2013)
JUN25 = ql.Date(16, 6, 2025)
APR25 = ql.Date(15, 4, 2025)
SOFR_FIXING = [[v.enc(ql.Date(15, 4, 2025)), 3.04 / 100.0]]


def euribor(months, curve):
    """Euribor's conventions as an IborIndex spec (ql/indexes/ibor/euribor.cpp): 2 settlement days, TARGET, modified
    following, end of month, Actual/360."""
    return irx.ibor_spec("Euribor", (months, int(ql.Months)), 2, TARGET, ql.ModifiedFollowing, True, A360, curve)


def sofr(curve):
    """SOFR's conventions as an OvernightIndex spec (ql/indexes/ibor/sofr.cpp): no settlement days, the SOFR calendar,
    Actual/360."""
    return irx.overnight_spec("SOFR", 0, US_SOFR, A360, curve)


def stripper(surface, index, discount=None, vol_type=ql.ShiftedLognormal, displacement=0.0, dont_throw=False,
             frequency=None, lag=0, switch=None, accuracy=1e-6, max_iter=100):
    return {"surface": surface, "index": index, "discount": discount, "switchStrike": switch, "accuracy": accuracy,
            "maxIter": max_iter, "type": int(vol_type), "displacement": displacement, "dontThrow": dont_throw,
            "optionletFrequency": frequency, "paymentLag": lag}


def strippers():
    """(name, evaluation date, fixings or None, spec)."""
    flat4_13 = irx.curve_spec(OCT13, 0.04)
    flat4_25 = irx.curve_spec(JUN25, 0.04)
    sofr_curve = irx.curve_spec(APR25, 0.04)
    flat = cfs.surface_spec([(i, Y) for i in range(1, 11)], [(j + 1) / 100.0 for j in range(10)],
                            [[0.18] * 10 for _ in range(10)], 0)
    market = cfs.surface_spec(os1.MARKET_TENORS, os1.MARKET_STRIKES, os1.MARKET_VOLS, 0)
    survey = cfs.surface_spec(os1.SURVEY_TENORS, os1.SURVEY_STRIKES, os1.SURVEY_VOLS, 0)
    sofr_flat = cfs.surface_spec(os1.SOFR_TENORS, os1.SOFR_STRIKES, [[30.0 / 10000.0] * 3 for _ in os1.SOFR_TENORS], 2,
                                 cal=US_SOFR, convention=ql.ModifiedFollowing, dc=A360)
    fixings = {"SOFRON Actual/360": SOFR_FIXING}
    return [
        ("flat surface, payment lag 0", OCT13, None, stripper(flat, euribor(6, flat4_13))),
        ("flat surface, payment lag 2", OCT13, None, stripper(flat, euribor(6, flat4_13), lag=2)),
        ("market surface, payment lag 2", OCT13, None, stripper(market, euribor(6, flat4_13), lag=2)),
        ("survey surface", JUN25, None, stripper(survey, euribor(6, flat4_25))),
        ("survey surface, Euribor 3M, lag 1", JUN25, None, stripper(survey, euribor(3, flat4_25), lag=1)),
        ("SOFR, flat, lag 0", APR25, fixings,
         stripper(sofr_flat, sofr(sofr_curve), discount=sofr_curve, vol_type=ql.Normal, frequency=[3, M])),
        ("SOFR, flat, lag 2", APR25, fixings,
         stripper(sofr_flat, sofr(sofr_curve), discount=sofr_curve, vol_type=ql.Normal, frequency=[3, M], lag=2)),
    ]


def case(H, name, today, fixings, spec):
    kw = {} if fixings is None else {"fixings": fixings}
    with H.case_settings(v.enc(today), **kw) as st:
        def call(method, *args):
            return v.attempt(lambda: H.call(method, spec, *args))

        tenors = H.call("OptionletStripper::optionletFixingTenors", spec)
        n = len(tenors)
        expected = {"optionletFixingTenors": tenors}
        for k in ("optionletPaymentDates", "optionletAccrualPeriods", "optionletFrequency", "paymentLag",
                  "optionletFixingDates", "optionletFixingTimes", "atmOptionletRates"):
            expected[k] = call(f"OptionletStripper::{k}")
        expected["optionletStrikes"] = [call("OptionletStripper::optionletStrikes", i) for i in range(n + 1)]
        expected["optionletVolatilities"] = [call("OptionletStripper::optionletVolatilities", i) for i in range(n + 1)]
        expected["switchStrike"] = call("OptionletStripper1::switchStrike")
        return {"name": name, "inputs": {"settings": st, "stripper": spec}, "expected": expected}


def cases():
    with cxx.connect() as H:
        return [case(H, *row) for row in strippers()]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/optionlet/optionletstripper.hpp", cases)
