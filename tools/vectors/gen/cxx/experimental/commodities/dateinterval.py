# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DateInterval (ql/experimental/commodities/dateinterval.hpp, .cpp), which the wheel
doesn't expose, each output computed by the C++ harness (tools/cxx/src/experimental/commodities/dateinterval.cpp): an
interval's intersections with overlapping, contained, touching and disjoint intervals, and isDateBetween under its four
flag pairs at dates inside and outside it.

Left out: the interval's own ends, which QuantLib's isDateBetween excludes whatever its flags say and nanoql includes
as they say (the S-xrt plan's XRR5 (iii), tests/NanoQL.Tests/Divergences/DateIntervalTests.cs). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Commodities/DateIntervalCxxVectorTests.cs."""
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
    with cxx.connect() as H:
        st = H.settings(TODAY)
        for name, start, end, others, dates in [
            ("a month", TODAY, TODAY + 30,
             [[TODAY - 10, TODAY + 5], [TODAY + 5, TODAY + 20], [TODAY + 25, TODAY + 60], [TODAY - 10, TODAY],
              [TODAY + 30, TODAY + 31], [TODAY + 31, TODAY + 40], [TODAY - 20, TODAY - 1], [TODAY - 5, TODAY + 90]],
             [TODAY - 1, TODAY + 1, TODAY + 15, TODAY + 29, TODAY + 31, TODAY + 400]),
            ("a single day", TODAY + 7, TODAY + 7, [[TODAY, TODAY + 7], [TODAY + 8, TODAY + 9], [TODAY, TODAY + 30]],
             [TODAY + 6, TODAY + 8]),
        ]:
            expected = H.call("DateInterval::values", start, end, others, dates)
            out.append({"name": name, "inputs": {"settings": st, "startDate": start, "endDate": end,
                                                  "others": others, "dates": dates}, "expected": expected})
    return out

if __name__ == "__main__":
    v.main(__file__, "ql/experimental/commodities/dateinterval.hpp", cases)
