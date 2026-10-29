# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's EnergyBasisSwap (ql/experimental/commodities/energybasisswap.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness (tools/cxx/src/experimental/commodities/energybasisswap.cpp):
WTI against Brent on monthly periods, the basis on the pay leg and on the receive leg, priced from fixings and from
the forward curves, the two indexes' last quote dates apart, with secondary costs; their NPV, pricing errors, cost
amounts, daily positions and payment cash flows.

On the base unit and currency alone, the basis in dollars (the S-xrt plan's §2.8): QuantLib's unit factors invert on any
other unit (XRR5 (i)), it converts a basis from the base currency into the basis currency (XRR5 (ix)), and its payments
pair one leg's currency with the other's rate (XRR5 (viii)), all of which agree in one currency and unit. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Commodities/EnergyBasisSwapCxxVectorTests.cs."""
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
vs = _load("cxx_energyvanillaswap", "cxx/experimental/commodities/energyvanillaswap.py")
TODAY = cs.TODAY

BRENT_TYPE = {"name": "Crude Oil Brent", "code": "BRT"}
BRENT_CURVE = cs.curve("Brent forward", cs.CURVE_DATES, [77.40, 78.05, 78.30, 77.85, 77.10, 76.60, 76.05, 74.90],
                       commodity_type=BRENT_TYPE)
BRENT = cs.index("BRENT", BRENT_CURVE, commodity_type=BRENT_TYPE)
WTI = cs.index("WTI", cs.WTI_CURVE)


def swap(period_list, spread_to_pay, basis, costs=None):
    return {"calendar": cs.TARGET, "spreadIndex": None, "payIndex": BRENT, "receiveIndex": WTI,
            "spreadToPayLeg": spread_to_pay, "payCurrency": "USD", "receiveCurrency": "USD", "periods": period_list,
            "basis": cs.unit_cost(basis), "commodityType": cs.WTI_TYPE, "secondaryCosts": costs,
            "payLegCurve": cs.yield_curve(0.03), "receiveLegCurve": cs.yield_curve(0.035),
            "discountCurve": cs.yield_curve(0.025)}


def cases():
    out = []
    seasoned = [TODAY - 45, TODAY - 14, TODAY + 17, TODAY + 45, TODAY + 76]
    forward = [TODAY + 17, TODAY + 45, TODAY + 76]
    both = {"WTI": cs.history(TODAY - 45, TODAY - 1), "BRENT": cs.history(TODAY - 45, TODAY - 6, 76.5, 0.05)}
    with cxx.connect() as H:
        for name, spec, fixings in [
            ("a seasoned swap, the basis on the receive leg", swap(vs.periods(seasoned, 31000.0), False, 3.25), both),
            ("a seasoned swap, the basis on the pay leg, with costs", swap(vs.periods(seasoned, 31000.0), True, -3.4,
                                                                           vs.COSTS), both),
            ("a forward swap with costs", swap(vs.periods(forward, 10000.0), False, 3.3, vs.COSTS), both),
            ("a seasoned swap, both indexes fixed to yesterday", swap(vs.periods(seasoned, 31000.0), False, 3.25),
             {"WTI": cs.history(TODAY - 45, TODAY - 1), "BRENT": cs.history(TODAY - 45, TODAY - 1, 76.5, 0.05)}),
        ]:
            with H.case_settings(TODAY, fixings=fixings) as st:
                expected = H.call("EnergyBasisSwap::calculate", spec)
            out.append({"name": name, "inputs": {"settings": st, "swap": spec}, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/commodities/energybasisswap.hpp", cases)
