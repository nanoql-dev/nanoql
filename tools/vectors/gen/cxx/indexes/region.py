# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's region equality (ql/indexes/region.hpp), each output computed by the C++ harness: operator==
and operator!= over every ordered pair of nine regions, the six presets and three custom ones (one named like the UK
preset with another code, one with an empty name), as [first][second]. The wheel's proxies don't compare regions.

One case, its inputs the region specs ("regions", as in tools/vectors/gen/indexes/region.py). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Indexes/RegionCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

REGIONS = [{"type": t} for t in ("AustraliaRegion", "EURegion", "FranceRegion", "UKRegion", "USRegion", "ZARegion")] + [
    {"type": "CustomRegion", "name": "UK", "code": "GB"},
    {"type": "CustomRegion", "name": "Testland", "code": "XX"},
    {"type": "CustomRegion", "name": "", "code": ""}]


def cases():
    with cxx.connect() as H:
        with H.case_settings(v.enc(ql.Date(15, ql.June, 2024))) as st:
            return [{"name": "equality / 0",
                     "inputs": {"settings": st, "regions": REGIONS},
                     "expected": {
                         "equal": [[H.call("Region::operator==", a, b) for b in REGIONS] for a in REGIONS],
                         "notEqual": [[H.call("Region::operator!=", a, b) for b in REGIONS] for a in REGIONS]}}]


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/region.hpp", cases)
