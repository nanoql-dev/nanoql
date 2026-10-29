# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CommodityCurve (ql/experimental/commodities/commoditycurve.hpp, .cpp), which the
wheel doesn't expose, each output computed by the C++ harness
(tools/cxx/src/experimental/commodities/commoditycurve.cpp): a curve's node times, last date and prices before, at,
between and after its nodes, forward-flat, on a basis curve and on nearby contracts.

QuantLib reads a basis curve at the outer curve's time (the S-xrt plan's XRR5 (xi)) and converts its price backward
(XRR5 (i)), so every basis curve shares its curve's reference date, day counter and unit, where both readings agree;
the curves are the constructor from dates, which the test also builds through CommodityCurve.Create and SetPrices on
the same dates, from the evaluation date, where QuantLib's setPrices measures the node times from the first date
(XRR5 (x)). Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Commodities/CommodityCurveCxxVectorTests.cs."""
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


def cases():
    out = []
    contracts = [cs.contract("CLH5", TODAY + 35, TODAY + 45, TODAY + 75),
                 cs.contract("CLJ5", TODAY + 66, TODAY + 76, TODAY + 105)]
    dates = [TODAY - 30, TODAY, TODAY + 1, TODAY + 30, TODAY + 31, TODAY + 45, TODAY + 59, TODAY + 89, TODAY + 90,
             TODAY + 200, TODAY + 364, TODAY + 365, TODAY + 366, TODAY + 1000]
    spread = cs.curve("WTI spread", cs.CURVE_DATES, [0.5, 0.6, 0.55, 0.4, 0.35, 0.3, 0.2, 0.1])
    with cxx.connect() as H:
        st = H.settings(TODAY)
        for name, curve, nearby in [
            ("a WTI curve on TARGET and Actual/365", cs.WTI_CURVE, 1),
            ("a WTI curve on its spread's basis", cs.curve("WTI on basis", cs.CURVE_DATES, cs.CURVE_PRICES,
                                                            basis=spread), 2),
            ("a two-node curve in gallons on Actual/360", cs.curve("RBOB", [TODAY, TODAY + 180], [2.10, 2.35],
                                                                     unit="GAL", day_counter=cs.A360), 3),
            ("a curve on a basis on a basis", cs.curve("WTI two bases", cs.CURVE_DATES, cs.CURVE_PRICES,
                                                         basis=cs.curve("spread on spread", cs.CURVE_DATES,
                                                                        [0.2] * 8, basis=spread)), 1),
        ]:
            expected = H.call("CommodityCurve::prices", curve, dates, contracts, nearby)
            out.append({"name": name, "inputs": {"settings": st, "curve": curve, "dates": dates,
                                                  "contracts": contracts, "nearbyOffset": nearby},
                        "expected": expected})
    return out

if __name__ == "__main__":
    v.main(__file__, "ql/experimental/commodities/commoditycurve.hpp", cases)
