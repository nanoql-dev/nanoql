# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Pool (ql/experimental/credit/pool.hpp, .cpp), which the wheel doesn't expose, each output
computed by the C++ harness (tools/cxx/src/experimental/credit/pool.cpp) on
tools/vectors/gen/cxx/experimental/credit/defaultevent.py's specs.

Pools added in name order, the only order where QuantLib's defaultKeys() pairs each name with its own key (it lists
them in name order; the S-xcr plan's XR8 (i), held in Divergences/PoolKeyOrderTests.cs): an empty pool, names on their
own keys, a name added twice (kept once), and names on Pool::add's default key. For each: the size, names and keys;
has() for held and missing names; each name's issuer read through get() on its key at two dates, and a missing name,
refused; each name's key; and times after setTime calls (on held names, and on a missing one, which no read reaches).
Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/PoolCxxVectorTests.cs."""
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


pf = _load("cxx_defaultevent", "cxx/experimental/credit/defaultevent.py")

TODAY = pf.TODAY
KA = pf.na_key("EUR", pf.SEC_DOM, (0, pf.DAYS), 1.0)
KB = pf.na_key("EUR", pf.SNR_FOR, (0, pf.DAYS), 1.0)
DEFAULT = pf.na_key(None, pf.SEC_DOM, (0, pf.DAYS), 1.0)  # Pool::add's default
IA = pf.issuer([(KA, pf.flat(0.01)), (KB, pf.flat(0.04))])
IB = pf.issuer([(KA, pf.flat(0.02)), (KB, pf.flat(0.05))])
IC = pf.issuer([(DEFAULT, pf.flat(0.03))])
POOLS = [
    ("empty", pf.pool([])),
    ("own keys in name order", pf.pool([("A", IA, KA), ("B", IB, KB), ("C", IA, KB)])),
    ("a name added twice", pf.pool([("A", IA, KA), ("A", IB, KB), ("B", IB, KA)])),
    ("the default key", pf.pool([("A", IC, None), ("B", IC, None)])),
]
NAMES = ["A", "B", "C", "Z"]
DATES = [TODAY + 365, TODAY + 1826]
SET_TIMES = [[["A", 1.5], ["B", 2.25]], [["A", 0.5], ["A", 3.0]], [["Z", 9.0]]]


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for name, p in POOLS:
            with H.case_settings(TODAY.serialNumber()) as st:
                keys = [KA, KB, DEFAULT]
                out.append({"name": name,
                            "inputs": {"settings": st, "pool": p, "names": NAMES, "keys": keys,
                                       "dates": [d.serialNumber() for d in DATES], "setTimes": SET_TIMES},
                            "expected": {
                                "inspect": call("Pool::inspect", p),
                                "has": [call("Pool::has", p, n) for n in NAMES],
                                "get": [call("Pool::get", p, n, k, d.serialNumber())
                                        for n in NAMES for k in keys for d in DATES],
                                "defaultKey": [call("Pool::defaultKey", p, n) for n in NAMES],
                                "getTime": [call("Pool::getTime", p, n, s) for s in SET_TIMES for n in NAMES]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/pool.hpp", cases)
