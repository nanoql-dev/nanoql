# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Quantity (ql/experimental/commodities/quantity.hpp, .cpp), which the wheel doesn't
expose, each output computed by the C++ harness (tools/cxx/src/experimental/commodities/quantity.cpp): its sums,
differences, ratios, comparisons and closeness under each of Quantity's conversion types, within one unit and across
units, and its scalings.

QuantLib rounds a converted quantity to its unit's whole units, which the S-xrt plan's XRR10 removes, so every
conversion here yields whole units, where the rounding leaves it as it is; QuantLib's == throws on a unit mismatch
without conversion, where nanoql's returns false (the vector test maps it). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Commodities/QuantityCxxVectorTests.cs."""
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


NO, BASE, AUTOMATED = 0, 1, 2


def cases():
    out = []
    q = cs.quantity
    same = [[q(10.0), q(4.5)], [q(4.5), q(10.0)], [q(-3.25, "GAL"), q(-3.25, "GAL")], [q(0.0, "MT"), q(7.0, "MT")]]
    mixed = [[q(2.0, "MB"), q(1000.0)], [q(3000.0), q(2.0, "MB")], [q(84.0, "GAL"), q(3.0)], [q(5.0), q(84.0, "GAL")],
             [q(1.0, "kl"), q(6.28981)], [q(2.0, "MB"), q(2000.0)]]
    with cxx.connect() as H:
        st = H.settings(TODAY)
        for name, settings, pairs in [
            ("no conversion, one unit and mismatched units", {"conversionType": NO, "baseUnit": None},
             same + mixed[:2]),
            ("automated conversion", {"conversionType": AUTOMATED, "baseUnit": None}, same[:2] + mixed),
            ("conversion to barrels", {"conversionType": BASE, "baseUnit": "BBL"}, same[:2] + mixed),
            ("conversion to thousand-barrels", {"conversionType": BASE, "baseUnit": "MB"},
             [[q(2.0, "MB"), q(1000.0)], [q(3000.0), q(1.0, "MB")]]),
            ("conversion to a base unit, none set", {"conversionType": BASE, "baseUnit": None}, mixed[:1]),
        ]:
            expected = H.call("Quantity::arithmetic", settings, pairs)
            out.append({"name": name, "inputs": {"settings": st, "quantitySettings": settings, "pairs": pairs},
                        "expected": expected})
    return out

if __name__ == "__main__":
    v.main(__file__, "ql/experimental/commodities/quantity.hpp", cases)
