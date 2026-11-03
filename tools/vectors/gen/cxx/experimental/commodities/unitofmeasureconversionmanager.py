# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's UnitOfMeasureConversionManager
(ql/experimental/commodities/unitofmeasureconversionmanager.hpp, .cpp), nanoql's UnitOfMeasureConversions, which the
wheel doesn't expose, each output computed by the C++ harness
(tools/cxx/src/experimental/commodities/unitofmeasureconversionmanager.cpp): the registry's derived lookups between
every ordered pair of QuantLib's six petroleum units, its direct lookups of the known factors either way round, a
commodity type's own factor, a factor replaced by add(), a chained search through a user's factor, and the lookups it
refuses.

Left out: the direct lookups of the two known factors QuantLib stores backward (gallons and thousand-barrels, litres and
gallons), whose conversions run the other way here, and every chained search QuantLib's recursion can't end, from
barrels or thousand-barrels with no direct factor, held in tests/NanoQL.Tests/Divergences/. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Commodities/UnitOfMeasureConversionManagerCxxVectorTests.cs."""
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


PETROLEUM = ["BBL", "MB", "GAL", "l", "kl", "KL_tk"]
BACKWARD = {("GAL", "MB"), ("MB", "GAL"), ("l", "GAL"), ("GAL", "l")}
KNOWN = [("MB", "BBL"), ("BBL", "GAL"), ("GAL", "MB"), ("l", "GAL"), ("BBL", "l"), ("kl", "BBL"), ("KL_tk", "BBL")]
DIRECT, DERIVED = 0, 1
HO = {"name": "Heating Oil", "code": "HO"}


def conversion(commodity_type, source, target, factor):
    return {"commodityType": commodity_type, "source": source, "target": target, "factor": factor}


def cases():
    out = []
    null = cs.NULL
    derived = [[null, s, t, DERIVED] for s in PETROLEUM for t in PETROLEUM if s != t]
    direct = [[null, a, b, DIRECT] for s, t in KNOWN for a, b in [(s, t), (t, s)] if (a, b) not in BACKWARD]
    refused = [[null, "BBL", "MT", DIRECT], [null, "GAL", "kl", DIRECT], [null, "MT", "BBL", DERIVED],
               [null, "MT", "GAL", DERIVED], [null, "Lot", "BBL", DERIVED], [HO, "GAL", "BBL", DERIVED]]
    own = [[HO, "MT", "BBL", DIRECT], [HO, "BBL", "MT", DIRECT], [HO, "MT", "BBL", DERIVED], [HO, "BBL", "MT", DERIVED],
           [null, "MT", "BBL", DIRECT]]
    with cxx.connect() as H:
        st = H.settings(TODAY)
        setups = [
            ("QuantLib's known factors: every derived pair of the six petroleum units", [], derived),
            ("QuantLib's known factors: the direct lookups either way round, and refusals", [], direct + refused),
            ("a commodity type's own factor beside the null type's", [conversion(HO, "MT", "BBL", 7.45)], own),
            ("a known factor replaced by add, the other way round", [conversion(null, "GAL", "BBL", 1.0 / 42.5)],
             [[null, "BBL", "GAL", DIRECT], [null, "GAL", "BBL", DERIVED], [null, "MB", "GAL", DERIVED],
              [null, "kl", "GAL", DERIVED]]),
            ("a chained search from metric tonnes through a user's factor", [conversion(null, "MT", "GAL", 300.0)],
             [[null, "MT", "BBL", DERIVED], [null, "MT", "GAL", DIRECT], [null, "MT", "GAL", DERIVED],
              [null, "MT", "kl", DERIVED], [null, "MT", "MB", DERIVED]]),
        ]
        for name, adds, queries in setups:
            expected = H.call("UnitOfMeasureConversionManager::lookups", adds, queries)
            out.append({"name": name, "inputs": {"settings": st, "adds": adds, "queries": queries},
                        "expected": expected})
    return out

if __name__ == "__main__":
    v.main(__file__, "ql/experimental/commodities/unitofmeasureconversionmanager.hpp", cases)
