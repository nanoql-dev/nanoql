# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's year-on-year cap and floor price surface's at-the-money swap rates by date
(ql/experimental/inflation/yoycapfloortermpricesurface.hpp), a pair of vectors the wheel's binding can't return, each
output computed by the C++ harness (tools/cxx/src/experimental/inflation/yoyoptionletvolatility.cpp,
"YoYCapFloorTermPriceSurface::dateRates").

- "dateRates" cases: tools/vectors/gen/experimental/inflation/yoycapfloortermpricesurface.py's surfaces on the euro
  market: atmYoYSwapDateRates, the dates and rates.

Every maturity's intersection is found on these markets, so each date is its own maturity's option date in QuantLib's
as in nanoql's; a market where one fails is in the S-xrt plan's XRR5 (xii) fact,
tests/NanoQL.Tests/Divergences/YoYCapFloorPriceSurfaceTests.cs. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Inflation/YoYCapFloorTermPriceSurfaceCxxVectorTests.cs."""
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


ys = _load("wheel_yoycapfloortermpricesurface", "experimental/inflation/yoycapfloortermpricesurface.py")


def cases():
    out = []
    market = ys.market_spec()
    with cxx.connect() as H:
        st = H.settings(v.enc(ys.EVAL))
        for label, s in ys.CASES:
            out.append({"name": label, "inputs": {"settings": st, "market": market, "surface": s},
                        "expected": H.call("YoYCapFloorTermPriceSurface::dateRates", market, s)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/inflation/yoycapfloortermpricesurface.hpp", cases)
