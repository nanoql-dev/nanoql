# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's regions (ql/indexes/region.hpp): each preset region's name and code, read through an index
the wheel binds (the presets themselves aren't bound: UK RPI's region is UKRegion, EU HICP's EURegion, US CPI's
USRegion, AU CPI's AustraliaRegion, FR HICP's FranceRegion, ZA CPI's ZARegion), and CustomRegion's.

One case per region, its spec ("region", {"type"} naming the preset, or {"type": "CustomRegion", "name", "code"}) and
its name and code. Equality, which the wheel's proxies don't compare, is the harness's
(tools/vectors/gen/cxx/indexes/region.py). Read back by tests/NanoQL.Tests/Vectors/Indexes/RegionVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

# Each preset region, reached through an index whose region it is.
PRESETS = [("UKRegion", lambda: ql.UKRPI().region()),
           ("EURegion", lambda: ql.EUHICP().region()),
           ("USRegion", lambda: ql.USCPI().region()),
           ("AustraliaRegion", lambda: ql.AUCPI(ql.Quarterly, False).region()),
           ("FranceRegion", lambda: ql.FRHICP().region()),
           ("ZARegion", lambda: ql.ZACPI().region())]
CUSTOM = [("Testland", "XX"), ("UK", "GB"), ("", "")]


def cases():
    out = []
    regions = [({"type": t}, make) for t, make in PRESETS] + [
        ({"type": "CustomRegion", "name": n, "code": c}, lambda n=n, c=c: ql.CustomRegion(n, c)) for n, c in CUSTOM]
    for k, (spec, make) in enumerate(regions):
        with v.case_settings(ql.Date(15, ql.June, 2024)) as st:
            r = make()
            out.append({"name": f"{spec['type']} {r.name()!r} / {k}",
                        "inputs": {"settings": st, "region": spec},
                        "expected": {"name": r.name(), "code": r.code()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/region.hpp", cases)
