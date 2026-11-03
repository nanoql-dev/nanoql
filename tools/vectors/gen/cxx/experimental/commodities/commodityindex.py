# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CommodityIndex (ql/experimental/commodities/commodityindex.hpp, .cpp), which the wheel doesn't
expose, each output computed by the C++ harness (tools/cxx/src/experimental/commodities/commodityindex.cpp): an
index's fixings, last quote date and forward prices, on a curve in the index's own unit, with fixings stored and none,
and through nearby contracts.

The forward price converts the curve's price between units, which nanoql fixes, so every curve is in its index's unit (a
factor of 1, QuantLib's bits); an index with no fixings has no last quote date here, where QuantLib throws, so its case
writes none. Nothing is fitted.

This module also holds the specs the commodities' generators share (tools/cxx/src/experimental/commodities/
commodityspec.hpp's), which tests/NanoQL.Tests/Vectors/CommodityInputs.cs reads. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Commodities/CommodityIndexCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TODAY = 45672  # 15 January 2025, a Wednesday
NULL = "<NULL>"
WTI_TYPE = {"name": "Crude Oil WTI", "code": "WTI"}
TARGET = {"type": "TARGET"}
NULLCAL = {"type": "NullCalendar"}
A365 = {"type": "Actual365Fixed"}
A360 = {"type": "Actual360"}
CONTINUOUS, ANNUAL = 2, 1


def curve(name, dates, prices, unit="BBL", currency="USD", calendar=TARGET, day_counter=A365, commodity_type=None,
          basis=None):
    out = {"name": name, "commodityType": commodity_type or WTI_TYPE, "currency": currency, "unit": unit,
           "calendar": calendar, "dates": dates, "prices": prices, "dayCounter": day_counter}
    if basis is not None:
        out["basis"] = basis
    return out


def index(name, forward_curve, unit="BBL", currency="USD", calendar=TARGET, lot=1.0, contracts=None, nearby=0,
          commodity_type=None):
    return {"name": name, "commodityType": commodity_type or WTI_TYPE, "currency": currency, "unit": unit,
            "calendar": calendar, "lotQuantity": lot, "curve": forward_curve, "contracts": contracts,
            "nearbyOffset": nearby}


def quantity(amount, unit="BBL", commodity_type=None):
    return {"commodityType": commodity_type or WTI_TYPE, "unit": unit, "amount": amount}


def unit_cost(amount, unit="BBL", currency="USD"):
    return {"amount": amount, "currency": currency, "unit": unit}


def yield_curve(forward, day_counter=A365, reference=TODAY):
    return {"referenceDate": reference, "forward": forward, "dayCounter": day_counter, "compounding": CONTINUOUS,
            "frequency": ANNUAL}


def contract(code, expiry, start, end):
    return {"code": code, "expirationDate": expiry, "underlyingStartDate": start, "underlyingEndDate": end}


# A monthly WTI curve from today, and a TARGET-business-day fixing history to yesterday.
CURVE_DATES = [TODAY, TODAY + 31, TODAY + 59, TODAY + 90, TODAY + 120, TODAY + 151, TODAY + 181, TODAY + 365]
CURVE_PRICES = [74.10, 74.85, 75.20, 74.60, 73.95, 73.40, 72.80, 71.50]
WTI_CURVE = curve("WTI forward", CURVE_DATES, CURVE_PRICES)


def history(start, end, base=73.0, step=0.07, calendar_days=None):
    """A fixing a TARGET business day (weekdays, no 1 January) from start to end, inclusive."""
    import QuantLib as ql
    cal = ql.TARGET()
    out = []
    d = start
    k = 0
    while d <= end:
        if cal.isBusinessDay(ql.Date(d)):
            out.append([d, round(base + step * k + 0.3 * ((k * 7) % 5 - 2), 4)])
            k += 1
        d += 1
    return out


FIXINGS = {"WTI": history(TODAY - 45, TODAY - 1)}


def cases():
    out = []
    contracts = [contract("CLH5", TODAY + 35, TODAY + 45, TODAY + 75), contract("CLJ5", TODAY + 66, TODAY + 76,
                                                                                  TODAY + 105),
                 contract("CLK5", TODAY + 96, TODAY + 106, TODAY + 136)]
    dates = [TODAY - 40, TODAY - 10, TODAY - 4, TODAY - 1, TODAY, TODAY + 3, TODAY + 31, TODAY + 45, TODAY + 100,
             TODAY + 200, TODAY + 500]
    with cxx.connect() as H:
        setups = [
            ("an index with a fixing history to yesterday", index("WTI", WTI_CURVE), FIXINGS),
            ("an index with no fixings", index("WTI", WTI_CURVE), {}),
            ("an index on a basis curve", index("WTI", curve("WTI basis", CURVE_DATES, CURVE_PRICES, basis=curve(
                "WTI spread", CURVE_DATES, [0.5, 0.6, 0.55, 0.4, 0.35, 0.3, 0.2, 0.1]))), FIXINGS),
            ("an index through its first nearby contract", index("WTI", WTI_CURVE, contracts=contracts, nearby=1),
             FIXINGS),
            ("an index through its second nearby contract, lots of 1,000", index(
                "WTI", WTI_CURVE, lot=1000.0, contracts=contracts, nearby=2), FIXINGS),
            ("an index on a null calendar, its curve on Actual/360", index(
                "WTI", curve("WTI 360", CURVE_DATES, CURVE_PRICES, calendar=NULLCAL, day_counter=A360),
                calendar=NULLCAL), FIXINGS),
        ]
        for name, spec, fixings in setups:
            with H.case_settings(TODAY, fixings=fixings) as st:
                expected = H.call("CommodityIndex::values", spec, dates)
            out.append({"name": name, "inputs": {"settings": st, "index": spec, "dates": dates},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/commodities/commodityindex.hpp", cases)
