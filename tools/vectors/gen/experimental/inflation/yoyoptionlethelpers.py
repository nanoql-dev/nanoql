# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's year-on-year optionlet helper (ql/experimental/inflation/yoyoptionlethelpers.hpp, .cpp),
through the wheel's YoYOptionletHelper.

- "helper" cases: helpers on tools/vectors/gen/experimental/inflation/yoycapfloortermpricesurface.py's euro index,
  caps and floors of one to ten years at strikes from 1% to 4%, observed flat with a three-month lag, with no and two
  fixing days, priced by the unit-displaced Black engine on a constant volatility of 0.5%, 1% and 2%: impliedQuote,
  and the earliest, latest, pillar, maturity and latest relevant dates.

Left out: helpers observed linearly, whose optionlets nanoql's engines price on each coupon's own fixing where
QuantLib's read the index's curve at the period's start (S-infl's documented divergence,
src/NanoQL/PricingEngines/Inflation/InflationCapFloorEngines/YoYInflationCapFloorEngine{TFormula}.cs); QuantLib's
stripper observes its helpers flat.

The wheel's engine holds the volatility's handle, so its helper prices on the volatility linked to it; nanoql's helper
prices on the volatility it is bound to (SetTermStructure), the one the handle holds. A case's inputs hold the
settings, the market, the helper's {"type" (QuantLib's YoYInflationCapFloor::Type), "notional", "lag", "fixingDays",
"interpolation", "strike", "n", "price"} and the volatility, a constant on TARGET, Modified Following and Actual/365
(Fixed) with the helper's lag. Read back by
tests/NanoQL.Tests/Vectors/Experimental/Inflation/YoYOptionletHelperVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ys = _load("wheel_yoycapfloortermpricesurface", "experimental/inflation/yoycapfloortermpricesurface.py")
EVAL = ys.EVAL
CAP, FLOOR = int(ql.YoYInflationCapFloor.Cap), int(ql.YoYInflationCapFloor.Floor)


def helper_spec(t, n, strike, interpolation, fixing_days):
    return {"type": t, "notional": 10000.0, "lag": [3, int(ql.Months)], "fixingDays": fixing_days,
            "interpolation": interpolation, "strike": strike, "n": n, "price": 0.0}


HELPERS = [helper_spec(t, n, k, interp, days)
           for t, k in ((CAP, 0.03), (CAP, 0.04), (FLOOR, 0.01), (FLOOR, 0.02))
           for n in (1, 3, 10)
           for interp in (int(ql.CPI.Flat),)
           for days in (0, 2)]


def cases():
    out = []
    market = ys.market_spec()
    for vol in (0.005, 0.01, 0.02):
        with v.case_settings(EVAL) as st:
            nominal, index = ys.make_market(market)
            hvs = ql.YoYOptionletVolatilitySurfaceHandle(ql.ConstantYoYOptionletVolatility(
                vol, 0, ys.CAL, ql.ModifiedFollowing, ys.A365, ql.Period(3, ql.Months), ql.Monthly, False, -1.0, 3.0))
            pricer = ql.YoYInflationUnitDisplacedBlackCapFloorEngine(index, hvs, nominal)
            results = []
            for h in HELPERS:
                helper = ql.YoYOptionletHelper(ql.QuoteHandle(ql.SimpleQuote(h["price"])), h["notional"], h["type"],
                                               ql.Period(*h["lag"]), ys.A365, ys.CAL, h["fixingDays"], index,
                                               h["interpolation"], h["strike"], h["n"], pricer)
                results.append([v.attempt(helper.impliedQuote), v.enc(helper.earliestDate()),
                                v.enc(helper.latestDate()), v.enc(helper.pillarDate()), v.enc(helper.maturityDate()),
                                v.enc(helper.latestRelevantDate())])
            out.append({"name": f"volatility {vol}",
                        "inputs": {"settings": st, "market": market, "helpers": HELPERS, "volatility": vol},
                        "expected": {"helpers": results}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/inflation/yoyoptionlethelpers.hpp", cases)
