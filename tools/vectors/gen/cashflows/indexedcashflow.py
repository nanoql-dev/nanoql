# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's indexed cash flows (ql/cashflows/indexedcashflow.hpp) and the zero-inflation cash flow
(ql/cashflows/zeroinflationcashflow.hpp), through the wheel's IndexedCashFlow and ZeroInflationCashFlow.

- "indexed" cases: IndexedCashFlow on a zero-inflation index, its base and fixing dates past, at the last fixing,
  missing or forecast, paying the ratio or its growth: notional, baseDate, fixingDate, date, growthOnly, baseFixing,
  indexFixing and amount.
- "zero" cases: ZeroInflationCashFlow from a start to an end date, observed flat or linearly with the lag, paying the
  ratio or its growth: notional, baseDate, fixingDate, date, growthOnly and amount.
- "notifications" cases, QuantLib's testNotifications replaced (test-suite/inflation.cpp, lines 1553-1587): the test
  case's zero-inflation cash flow priced on its index under one curve, then under another, each amount QuantLib's
  relinked index gives.

A refused value is {"throws": true}. A case's inputs hold its settings, the index spec (tools/vectors/gen/indexes/
inflationindex.py's), its fixings, added through the index one by one, and the cash flow's arguments.

The functions and data here are shared by the other inflation cash-flow, swap, bond and cap generators, which import
this file. Read back by tests/NanoQL.Tests/Vectors/Coupons/IndexedCashFlowVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, *path):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *path))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


cf = _load("wheel_cashflows", "cashflows", "cashflows.py")
irx = _load("wheel_interestrateindex", "indexes", "interestrateindex.py")
ii = _load("wheel_inflationindex", "indexes", "inflationindex.py")
its = ii.its

M, D, Y = int(ql.Months), int(ql.Days), int(ql.Years)
TODAY = ql.Date(15, ql.June, 2024)
UK_FIXINGS = ii.UK_FIXINGS
ZERO_UK = ii.ZERO_UK
YOY_UK = ii.YOY_UK
UKRPI = ii.preset("UKRPI", ZERO_UK)
UKRPI_NO_CURVE = ii.preset("UKRPI")
YYUKRPI_RATIO = ii.ratio(ii.preset("UKRPI"), YOY_UK)
NOMINAL = irx.curve_spec(TODAY, 0.04)


def period(p):
    return [p.length(), int(p.units())]


def lag_of(spec):
    return ql.Period(spec[0], spec[1])


def index_with_fixings(spec, fixings):
    """The index a spec describes, its fixings added through its store's owner one by one."""
    index = ii.make_index(spec)
    ii.apply(ii.store_owner(index, spec), fixings)
    return index


def nominal_curve(spec):
    """The nominal curve's handle: a flat forward with a fixed reference date, empty for a null spec."""
    return irx.handle(spec)


# (label, notional, base date, fixing date, payment date, growth only)
INDEXED = [
    ("past base and fixing", 1e6, ql.Date(1, ql.January, 2023), ql.Date(1, ql.March, 2024), ql.Date(15, ql.July, 2024),
     False),
    ("past base and fixing, growth", 1e6, ql.Date(1, ql.January, 2023), ql.Date(1, ql.March, 2024),
     ql.Date(15, ql.July, 2024), True),
    ("forecast fixing", 2.5e6, ql.Date(1, ql.June, 2023), ql.Date(1, ql.June, 2025), ql.Date(15, ql.September, 2025),
     False),
    ("forecast fixing, growth", 1e6, ql.Date(15, ql.April, 2024), ql.Date(20, ql.April, 2030),
     ql.Date(1, ql.August, 2030), True),
    ("missing fixing, forecast", 1e6, ql.Date(1, ql.February, 2024), ql.Date(1, ql.May, 2024),
     ql.Date(15, ql.August, 2024), False),
    ("base before the fixings", 1e6, ql.Date(1, ql.June, 2021), ql.Date(1, ql.March, 2024), ql.Date(15, ql.July, 2024),
     False),
]

# (label, notional, interpolation, start, end, lag, payment, growth only)
ZERO = [
    ("flat", 1e6, ql.CPI.Flat, ql.Date(15, ql.June, 2024), ql.Date(15, ql.June, 2029), ql.Period(3, ql.Months),
     ql.Date(17, ql.June, 2029), True),
    ("flat, ratio", 1e6, ql.CPI.Flat, ql.Date(15, ql.June, 2024), ql.Date(15, ql.June, 2029), ql.Period(3, ql.Months),
     ql.Date(17, ql.June, 2029), False),
    ("linear", 5e6, ql.CPI.Linear, ql.Date(15, ql.June, 2024), ql.Date(20, ql.December, 2034),
     ql.Period(3, ql.Months), ql.Date(20, ql.December, 2034), True),
    ("linear, month end", 1e6, ql.CPI.Linear, ql.Date(31, ql.May, 2024), ql.Date(31, ql.January, 2027),
     ql.Period(3, ql.Months), ql.Date(1, ql.February, 2027), True),
    ("past start, two-month lag", 1e6, ql.CPI.Flat, ql.Date(10, ql.March, 2023), ql.Date(10, ql.March, 2026),
     ql.Period(2, ql.Months), ql.Date(10, ql.March, 2026), True),
    ("fixed end", 1e6, ql.CPI.Linear, ql.Date(1, ql.December, 2022), ql.Date(1, ql.April, 2024),
     ql.Period(2, ql.Months), ql.Date(3, ql.April, 2024), False),
    ("missing base", 1e6, ql.CPI.Flat, ql.Date(1, ql.January, 2020), ql.Date(1, ql.January, 2025),
     ql.Period(3, ql.Months), ql.Date(3, ql.January, 2025), True),
]


def indexed_cases():
    out = []
    for k, (label, notional, base, fixing, payment, growth) in enumerate(INDEXED):
        with v.case_settings(TODAY) as st:
            index = index_with_fixings(UKRPI, UK_FIXINGS)
            c = ql.IndexedCashFlow(notional, index, base, fixing, payment, growth)
            out.append({"name": f"indexed {label} / {k}",
                        "inputs": {"settings": st, "kind": "indexed", "index": UKRPI, "fixings": UK_FIXINGS,
                                   "notional": notional, "baseDate": v.enc(base), "fixingDate": v.enc(fixing),
                                   "paymentDate": v.enc(payment), "growthOnly": growth},
                        "expected": {"notional": c.notional(), "baseDate": v.enc(c.baseDate()),
                                     "fixingDate": v.enc(c.fixingDate()), "date": v.enc(c.date()),
                                     "growthOnly": c.growthOnly(), "baseFixing": v.attempt(c.baseFixing),
                                     "indexFixing": v.attempt(c.indexFixing), "amount": v.attempt(c.amount)}})
    return out


def zero_cases():
    out = []
    for k, (label, notional, interp, start, end, lag, payment, growth) in enumerate(ZERO):
        with v.case_settings(TODAY) as st:
            index = index_with_fixings(UKRPI, UK_FIXINGS)
            c = ql.ZeroInflationCashFlow(notional, index, interp, start, end, lag, payment, growth)
            out.append({"name": f"zero {label} / {k}",
                        "inputs": {"settings": st, "kind": "zero", "index": UKRPI, "fixings": UK_FIXINGS,
                                   "notional": notional, "interpolation": int(interp), "startDate": v.enc(start),
                                   "endDate": v.enc(end), "observationLag": period(lag), "paymentDate": v.enc(payment),
                                   "growthOnly": growth},
                        "expected": {"notional": c.notional(), "baseDate": v.enc(c.baseDate()),
                                     "fixingDate": v.enc(c.fixingDate()), "date": v.enc(c.date()),
                                     "growthOnly": c.growthOnly(), "amount": v.attempt(c.amount)}})
    return out


def notification_cases():
    """testNotifications: a curve relinked under the cash flow's index; each curve here, its own index."""
    out = []
    today = ql.Date(20, ql.October, 2025)
    for k, rate in enumerate((0.02, 0.025)):
        with v.case_settings(today) as st:
            curve = its.curve_spec("ZeroInflationCurve", today, [today - ql.Period(3, ql.Months),
                                                                 today + ql.Period(5, ql.Years)],
                                   [rate, rate], ql.Monthly, {"type": "Actual360"})
            spec = ii.preset("UKRPI", curve)
            fixing_period = ql.inflationPeriod(today - ql.Period(3, ql.Months), ql.Monthly)
            fixings = [[v.enc(fixing_period[0]), 100.0]]
            index = index_with_fixings(spec, fixings)
            c = ql.ZeroInflationCashFlow(10000.0, index, ql.CPI.Flat, today, today + ql.Period(1, ql.Years),
                                         ql.Period(3, ql.Months), today + ql.Period(1, ql.Years))
            out.append({"name": f"notifications, curve at {rate} / {k}",
                        "inputs": {"settings": st, "kind": "zero", "index": spec, "fixings": fixings,
                                   "notional": 10000.0, "interpolation": int(ql.CPI.Flat), "startDate": v.enc(today),
                                   "endDate": v.enc(today + ql.Period(1, ql.Years)), "observationLag": [3, M],
                                   "paymentDate": v.enc(today + ql.Period(1, ql.Years)), "growthOnly": False},
                        "expected": {"notional": c.notional(), "baseDate": v.enc(c.baseDate()),
                                     "fixingDate": v.enc(c.fixingDate()), "date": v.enc(c.date()),
                                     "growthOnly": c.growthOnly(), "amount": v.attempt(c.amount)}})
    return out


def make_cases():
    return indexed_cases() + zero_cases() + notification_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/indexedcashflow.hpp", make_cases)
