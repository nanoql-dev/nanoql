# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's EnergyFuture (ql/experimental/commodities/energyfuture.hpp, .cpp), which the wheel doesn't
expose, each output computed by the C++ harness (tools/cxx/src/experimental/commodities/energyfuture.cpp): bought and
sold futures quoted off today's fixing and off the forward curve, with unit and fixed secondary costs; their NPV,
pricing errors and cost amounts.

On the base unit and currency at one lot alone: QuantLib's unit factors invert on any other unit, it squares the lot and
charges its costs on the raw quantity, all of which agree at one lot of barrels; a last fixing yesterday, which QuantLib
refuses, is left out. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Commodities/EnergyFutureCxxVectorTests.cs."""
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


def future(buy_sell, amount, trade_price, costs=None):
    return {"buySell": buy_sell, "quantity": cs.quantity(amount), "tradePrice": cs.unit_cost(trade_price),
            "index": cs.index("WTI", cs.WTI_CURVE), "commodityType": cs.WTI_TYPE, "secondaryCosts": costs}


def cases():
    out = []
    to_today = {"WTI": cs.history(TODAY - 45, TODAY - 1) + [[TODAY, 74.35]]}
    old = {"WTI": cs.history(TODAY - 45, TODAY - 5)}
    with cxx.connect() as H:
        for name, spec, fixings in [
            ("a bought future off today's fixing", future(1, 1000.0, 72.5), to_today),
            ("a sold future off today's fixing, with costs", future(-1, 1000.0, 72.5, vs.COSTS), to_today),
            ("a bought future off the forward curve", future(1, 250.0, 75.1), old),
            ("a sold future off the forward curve, with costs", future(-1, 250.0, 75.1, vs.COSTS), old),
        ]:
            with H.case_settings(TODAY, fixings=fixings) as st:
                expected = H.call("EnergyFuture::calculate", spec)
            out.append({"name": name, "inputs": {"settings": st, "future": spec}, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/commodities/energyfuture.hpp", cases)
