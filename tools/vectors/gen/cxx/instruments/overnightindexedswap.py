# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for OvernightIndexedSwap's inspectors that the wheel doesn't bind (ql/instruments/overnightindexedswap.hpp),
each output computed by the C++ harness: telescopicValueDates() and roundingPrecision(), and the fixed day counter
and payment calendar of swaps built with QuantLib's empty ones, which the wheel can't build (an empty fixed day
counter is the index's, and an empty payment calendar stays empty, each leg paying on its schedule's calendar).

40 swaps on the evaluation date 15 January 2025, over QuantLib's four constructors, each called directly: telescopic
value dates with and without a lookback and the observation shift (QuantLib refuses a lookback without the shift),
rounding precisions of none, 0, 6, 16, 17 and -1 (the last two refused), empty and given fixed day counters, and
empty and given payment calendars with the conventions after it set, on overnight indexes with SOFR's, ESTR's and
SONIA's conventions. Each swap's outputs are its four inspectors, a refused construction recording its throw on each.
The harness builds these telescopic swaps but reads only their inspectors, which the telescopic rule leaves alone.

Each case's inputs hold its settings and its "swap", a spec of tools/vectors/gen/instruments/fixedvsfloatingswap.py.
tests/NanoQL.Tests/Vectors/Cxx/Instruments/OvernightIndexedSwapCxxVectorTests.cs reads it back."""
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

_spec = importlib.util.spec_from_file_location("wheel_fixedvsfloatingswap",
                                               os.path.join(v.GEN_ROOT, "instruments", "fixedvsfloatingswap.py"))
fvf = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(fvf)

TODAY = fvf.TODAY
SPOT = fvf.d(17, 1, 2025)
SIMPLE = ql.RateAveraging.Simple
METHODS = ["telescopicValueDates", "roundingPrecision", "fixedDayCount", "paymentCalendar"]


def swaps():
    us = fvf.sched(SPOT, fvf.d(17, 1, 2027), fvf.ONE_Y, fvf.US_SOFR)
    us6 = fvf.sched(SPOT, fvf.d(17, 1, 2027), fvf.SIX_M, fvf.US_SOFR)
    eu = fvf.sched(SPOT, fvf.d(17, 1, 2027), fvf.ONE_Y, fvf.TARGET)
    uk = fvf.sched(SPOT, fvf.d(17, 1, 2027), fvf.ONE_Y, fvf.UK_EXCHANGE)
    variants = [
        ("plain", {}),
        ("telescopic", {"telescopic": True}),
        ("telescopic, averaged", {"telescopic": True, "averaging": SIMPLE}),
        ("telescopic, lookback 2 with shift", {"telescopic": True, "lookback": 2, "shift": True}),
        ("telescopic, lookback 2 without shift", {"telescopic": True, "lookback": 2}),
        ("rounding 0", {"rounding": 0}),
        ("rounding 6", {"rounding": 6}),
        ("rounding 16", {"rounding": 16}),
        ("rounding 17", {"rounding": 17}),
        ("rounding -1", {"rounding": -1}),
    ]
    out = []
    forms = [("nominal", us, None, fvf.SOFR, [1e6], None),
             ("nominals", eu, None, fvf.ESTR, [1e6, 5e5], None),
             ("nominal, two schedules", us, us6, fvf.SOFR, [1e6], [1e6]),
             ("nominals, two schedules", uk, None, fvf.SONIA, [1e7], [1e7])]
    for k, (form, fixed, overnight, index, fixed_nominals, overnight_nominals) in enumerate(forms):
        for j, (label, kw) in enumerate(variants):
            kw = dict(kw)
            # every other swap has QuantLib's empty fixed day counter, and every third its empty payment calendar
            fixed_dc = None if (k + j) % 2 == 0 else fvf.A360
            if (k + j) % 3 != 0:
                kw["payment_calendar"] = [fvf.TARGET, fvf.UK_EXCHANGE, fvf.US_SOFR][(k + j) % 3]
            spec = fvf.ois(form, fvf.RECEIVER if j % 4 == 1 else fvf.PAYER, fixed_nominals, fixed, 0.03, fixed_dc,
                           index, overnight_nominals, overnight, payment_lag=j % 3, **kw)
            out.append((f"{form} / {label}", spec))
    return out


def cases():
    with cxx.connect() as H:
        out = []
        for name, spec in swaps():
            with H.case_settings(v.enc(TODAY)) as st:
                expected = {m: v.attempt(lambda m=m: H.call(f"OvernightIndexedSwap::{m}", spec)) for m in METHODS}
                out.append({"name": name, "inputs": {"settings": st, "swap": spec}, "expected": expected})
        check_rows(out)
        return out


def check_rows(out):
    def find(suffix):
        return [c["expected"] for c in out if c["name"].endswith(suffix)]
    assert all(e["roundingPrecision"] == v.THROWS for e in find("/ rounding 17") + find("/ rounding -1"))
    assert all(e["telescopicValueDates"] == v.THROWS for e in find("/ telescopic, lookback 2 without shift"))
    assert all(e["telescopicValueDates"] is True for e in find("/ telescopic, averaged"))
    assert any(e["paymentCalendar"] is None for e in find("/ telescopic"))
    # an empty fixed day counter is the index's: SONIA's Actual/365 (Fixed)
    assert any(c["inputs"]["swap"]["fixedDC"] is None and c["expected"]["fixedDayCount"] == "Actual/365 (Fixed)"
               for c in out)


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/overnightindexedswap.hpp", cases)
