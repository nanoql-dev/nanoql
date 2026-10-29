# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's UnitOfMeasureConversion
(ql/experimental/commodities/unitofmeasureconversion.hpp, .cpp), which the wheel doesn't expose, each output computed
by the C++ harness (tools/cxx/src/experimental/commodities/unitofmeasureconversion.cpp): a direct conversion's convert
of quantities in its source, its target and another unit, its code, and the chains of two conversions in each of
chain()'s four orientations, converting through them, and the pair it refuses to chain. Nothing is fitted. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Commodities/UnitOfMeasureConversionCxxVectorTests.cs."""
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


def conversion(source, target, factor, commodity_type=None):
    return {"commodityType": commodity_type or cs.NULL, "source": source, "target": target, "factor": factor}


def cases():
    out = []
    q = cs.quantity
    with cxx.connect() as H:
        st = H.settings(TODAY)
        for name, c, qs in [
            ("barrels to gallons", conversion("BBL", "GAL", 42), [q(10.0), q(84.0, "GAL"), q(1.0, "MT")]),
            ("thousand-barrels to barrels", conversion("MB", "BBL", 1000), [q(2.5, "MB"), q(1500.0), q(3.0, "l")]),
            ("a heating oil factor", conversion("MT", "BBL", 7.45, {"name": "Heating Oil", "code": "HO"}),
             [q(3.0, "MT"), q(14.9), q(-2.0, "MT")]),
        ]:
            expected = H.call("UnitOfMeasureConversion::convert", c, qs)
            out.append({"name": name, "inputs": {"settings": st, "conversion": c, "quantities": qs},
                        "expected": expected})
        gal_bbl = conversion("BBL", "GAL", 42)
        mb_bbl = conversion("MB", "BBL", 1000)
        bbl_l = conversion("BBL", "l", 158.987)
        kl_bbl = conversion("kl", "BBL", 6.28981)
        for name, c1, c2, qs in [
            ("sharing their sources", gal_bbl, bbl_l, [q(1.0, "GAL"), q(1.0, "l"), q(5.0)]),
            ("the first's source the second's target", gal_bbl, mb_bbl, [q(42.0, "GAL"), q(0.001, "MB")]),
            ("the first's target the second's source", mb_bbl, bbl_l, [q(2.0, "MB"), q(317974.0, "l")]),
            ("sharing their targets", mb_bbl, kl_bbl, [q(1.0, "MB"), q(6.28981, "kl")]),
            ("sharing no unit", conversion("MB", "GAL", 42000), bbl_l, [q(1.0)]),
        ]:
            try:
                expected = H.call("UnitOfMeasureConversion::chain", c1, c2, qs)
            except RuntimeError:
                expected = {"chain": {"throws": True}}
            out.append({"name": "a chain " + name, "inputs": {"settings": st, "first": c1, "second": c2,
                                                                "quantities": qs}, "expected": expected})
    return out

if __name__ == "__main__":
    v.main(__file__, "ql/experimental/commodities/unitofmeasureconversion.hpp", cases)
