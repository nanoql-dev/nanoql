# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the overnight-index future's members the wheel hides (ql/instruments/overnightindexfuture.hpp), each
output computed by the C++ harness (docs/porting-playbook.md §9.4): the futures of
tools/vectors/gen/instruments/overnightindexfuture.py but its refusals, on its evaluation date.

Each future's outputs are its index's name, value date, maturity date and convexity adjustment. A case's inputs hold
its settings and the method's arguments (the index, an overnight spec of indexes/indexspec.hpp; the value date,
maturity, adjustment and averaging). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Instruments/OvernightIndexFutureCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


oif = _load("wheel_overnightindexfuture", "instruments/overnightindexfuture.py")

OUTPUTS = ["indexName", "valueDate", "maturityDate", "convexityAdjustment"]


def cases():
    out = []
    with cxx.connect() as H:
        for name, spec, value, maturity, avg, convexity, fixings in oif.futures():
            if name.startswith("refused"):
                continue
            with H.case_settings(v.enc(oif.TODAY)) as st:
                args = [spec, v.enc(value), v.enc(maturity), convexity, avg]
                out.append({"name": name, "inputs": {"settings": st, "arguments": args},
                            "expected": dict(zip(OUTPUTS, H.call("OvernightIndexFuture", *args)))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/overnightindexfuture.hpp", cases)
