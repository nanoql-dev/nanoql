# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CommodityPricingHelper
(ql/experimental/commodities/commoditypricinghelpers.hpp, .cpp), which the wheel doesn't expose, each output computed
by the C++ harness (tools/cxx/src/experimental/commodities/commoditypricinghelpers.cpp): monthly pricing periods (from
a month's middle, its first and its last day, over a year and over part of a month), the periodicities each schedule
refuses, and unit costs on the base unit; the payment terms and unit costs as QuantLib writes them, and the terms'
equality.

Left out (the S-xrt plan's §2.8): daily periods, one day short in QuantLib (XRR5 (iv)); the other schedules, which
QuantLib leaves without periods and nanoql refuses (XRR5 (v)); unit costs in another unit, which QuantLib converts
backward (XRR5 (i)). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Commodities/CommodityPricingHelperCxxVectorTests.cs."""
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


cs = _load("cxx_commodityindex", "cxx/experimental/commodities/commodityindex.py")
TODAY = cs.TODAY


MONTHLY, DAILY = 5, 3
PER_DAY, PER_MONTH = 2, 4
TRADE, PRICING = 0, 1


def cases():
    out = []
    term = {"name": "Pricing end + 5 days", "eventType": PRICING, "offsetDays": 5, "calendar": cs.TARGET}
    null_term = {"name": "Pricing end", "eventType": PRICING, "offsetDays": 0, "calendar": cs.NULLCAL}
    with cxx.connect() as H:
        st = H.settings(TODAY)
        for name, start, end, quantity, schedule, periodicity, pt in [
            ("monthly from the 15th over a year", TODAY, TODAY + 365, cs.quantity(10000.0), MONTHLY, PER_MONTH, term),
            ("monthly from a month's first day", TODAY + 17, TODAY + 200, cs.quantity(500.0, "GAL"), MONTHLY,
             PER_MONTH, term),
            ("monthly from a month's last day", TODAY + 16, TODAY + 120, cs.quantity(2.0, "MB"), MONTHLY, PER_MONTH,
             null_term),
            ("monthly over part of a month", TODAY, TODAY + 10, cs.quantity(100.0), MONTHLY, PER_MONTH, term),
            ("monthly, its end before its start", TODAY, TODAY - 10, cs.quantity(100.0), MONTHLY, PER_MONTH, term),
            ("monthly refusing a daily quantity", TODAY, TODAY + 60, cs.quantity(10.0), MONTHLY, PER_DAY, term),
            ("daily refusing a monthly quantity", TODAY, TODAY + 60, cs.quantity(10.0), DAILY, PER_MONTH, term),
        ]:
            expected = H.call("CommodityPricingHelper::createPricingPeriods", start, end, quantity, schedule,
                              periodicity, pt)
            out.append({"name": name, "inputs": {"settings": st, "startDate": start, "endDate": end,
                                                  "quantity": quantity, "deliverySchedule": schedule,
                                                  "qtyPeriodicity": periodicity, "paymentTerm": pt},
                        "expected": expected})
        for name, cost, base_currency, base_unit in [
            ("a cost per barrel in dollars", cs.unit_cost(0.15), "USD", "BBL"),
            ("a cost per gallon in a base of gallons", cs.unit_cost(-0.02, "GAL"), "USD", "GAL"),
            ("a zero cost", cs.unit_cost(0.0, "MT", "EUR"), "USD", "BBL"),
            ("a cost in euros with no rate", cs.unit_cost(0.15, "BBL", "EUR"), "USD", "BBL"),
        ]:
            expected = H.call("CommodityPricingHelper::calculateUnitCost", cs.WTI_TYPE, cost, base_currency,
                              base_unit, TODAY)
            out.append({"name": name, "inputs": {"settings": st, "commodityType": cs.WTI_TYPE, "unitCost": cost,
                                                  "baseCurrency": base_currency, "baseUnit": base_unit},
                        "expected": expected})
    return out

if __name__ == "__main__":
    v.main(__file__, "ql/experimental/commodities/commoditypricinghelpers.hpp", cases)
