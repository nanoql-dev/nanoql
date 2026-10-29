# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's EnergyVanillaSwap (ql/experimental/commodities/energyvanillaswap.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness (tools/cxx/src/experimental/commodities/energyvanillaswap.cpp):
payer and receiver swaps on monthly periods priced from fixings and from the forward curve, with unit and fixed
secondary costs, an old last quote date's warning, a payment within two days of today, and an expired swap; their NPV,
pricing errors, cost amounts, daily positions and payment cash flows.

On the base unit and currency alone (the S-xrt plan's §2.8): QuantLib's unit factors invert a price or a quantity on
any other unit (XRR5 (i)), and its payments pair one leg's currency with the other's rate (XRR5 (viii)), which agree
in one currency; an index with no fixings, which QuantLib refuses, is left out (XRR5 (vi)). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Commodities/EnergyVanillaSwapCxxVectorTests.cs."""
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


def periods(starts, amount, pay_lag=5):
    """Monthly periods [start, next start − 1], each paid pay_lag days after its end."""
    out = []
    for a, b in zip(starts, starts[1:]):
        out.append({"startDate": a, "endDate": b - 1, "paymentDate": b - 1 + pay_lag, "quantity": cs.quantity(amount)})
    return out


def swap(payer, period_list, fixed=74.0, costs=None, index=None, calendar=None):
    return {"payer": payer, "calendar": calendar or cs.TARGET, "fixedPrice": {"amount": fixed, "currency": "USD"},
            "fixedPriceUnit": "BBL", "index": index or cs.index("WTI", cs.WTI_CURVE), "payCurrency": "USD",
            "receiveCurrency": "USD", "periods": period_list, "commodityType": cs.WTI_TYPE, "secondaryCosts": costs,
            "payLegCurve": cs.yield_curve(0.03), "receiveLegCurve": cs.yield_curve(0.035),
            "discountCurve": cs.yield_curve(0.025)}


COSTS = {"brokerage": {"unitCost": cs.unit_cost(0.05)}, "clearing": {"money": {"amount": 250.0, "currency": "USD"}}}


def cases():
    out = []
    # 1 December 2024, 1 January, 1 February, 1 March and 1 April 2025
    seasoned = [TODAY - 45, TODAY - 14, TODAY + 17, TODAY + 45, TODAY + 76]
    forward = [TODAY + 17, TODAY + 45, TODAY + 76, TODAY + 106]
    expired = [TODAY - 106, TODAY - 76, TODAY - 45]
    old_history = {"WTI": cs.history(TODAY - 45, TODAY - 8)}
    with cxx.connect() as H:
        for name, spec, fixings in [
            ("a seasoned payer, fixings to yesterday", swap(True, periods(seasoned, 31000.0)), cs.FIXINGS),
            ("a seasoned receiver with secondary costs", swap(False, periods(seasoned, 31000.0), 73.5, COSTS),
             cs.FIXINGS),
            ("a forward payer with secondary costs", swap(True, periods(forward, 10000.0), 75.0, COSTS), cs.FIXINGS),
            ("a payer whose last fixing is a week old", swap(True, periods(seasoned, 31000.0)), old_history),
            ("a payer paid a day after today, within two days", swap(True, periods(
                [TODAY - 45, TODAY - 14, TODAY + 17], 31000.0, pay_lag=-15)), cs.FIXINGS),
            ("an expired payer", swap(True, periods(expired, 31000.0)), cs.FIXINGS),
            ("a payer on a null calendar priced from the curve", swap(True, periods(forward, 2000.0),
                                                                      calendar=cs.NULLCAL), cs.FIXINGS),
        ]:
            with H.case_settings(TODAY, fixings=fixings) as st:
                expected = H.call("EnergyVanillaSwap::calculate", spec)
            out.append({"name": name, "inputs": {"settings": st, "swap": spec}, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/commodities/energyvanillaswap.hpp", cases)
