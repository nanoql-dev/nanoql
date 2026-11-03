# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CPI cap and floor price surface (ql/experimental/inflation/cpicapfloortermpricesurface.hpp,
.cpp), which the wheel doesn't bind, each output computed by the C++ harness
(tools/cxx/src/experimental/inflation/cpicapfloortermpricesurface.cpp).

- "values" cases: surfaces on UK RPI's interpolated zero-inflation curve and fixings, the price data
  test-suite/inflationcpicapfloor.cpp quotes on its first four maturities, bilinear and bicubic, observed flat and
  linearly: the strikes, the option dates, the range, the base date, the cap, floor and price at tenors inside and past
  the maturities and at strikes on, between and past the quotes, and the at-the-money rate at the reference date and
  after it.
- "refused" cases: the constructor's checks (too few strikes, a non-increasing maturity, a non-positive price, a cap
  price rising with the strike, no zero-inflation curve), each with QuantLib's text in nanoql's convention facts.

Left out: maturities in months, which QuantLib compounds over their month count
(tests/NanoQL.Tests/Divergences/CPICapFloorPriceSurfaceTests.cs). The functions here are shared by
tools/vectors/gen/cxx/experimental/inflation/cpicapfloorengines.py, which imports this file. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Inflation/CPICapFloorTermPriceSurfaceCxxVectorTests.cs."""
import copy
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


ic = _load("cxx_inflationcoupon", "cxx/cashflows/inflationcoupon.py")
icf = ic.icf
TODAY = ic.TODAY
UK = ic.UK
M, Y = int(ql.Months), int(ql.Years)
A365 = ic.A365
FLAT, LINEAR = int(ql.CPI.Flat), int(ql.CPI.Linear)
INDEX = dict(ic.UKRPI, curve=dict(icf.ZERO_UK, enableExtrapolation=True))
NAME = "CPICapFloorTermPriceSurface::"

# test-suite/inflationcpicapfloor.cpp's quotes in basis points, its first four maturities, a row per maturity
MATURITIES = [[3, Y], [5, Y], [7, Y], [10, Y]]
CAP_STRIKES = [0.03, 0.04, 0.05, 0.06]
FLOOR_STRIKES = [-0.01, 0.0, 0.01, 0.02]
CAP_BPS = [[227.6, 100.27, 38.8, 14.94], [345.32, 127.9, 40.59, 14.11], [477.95, 170.19, 50.62, 16.88],
           [757.81, 303.95, 107.62, 43.61]]
FLOOR_BPS = [[15.62, 28.38, 53.61, 104.6], [21.45, 36.73, 66.66, 129.6], [24.45, 42.08, 77.04, 152.24],
             [39.25, 63.52, 109.2, 203.44]]


def by_strike(bps):
    """A row per strike, a column per maturity, in price units."""
    return [[bps[j][i] / 10000.0 for j in range(len(bps))] for i in range(len(bps[0]))]


def surface_spec(interpolator="Bilinear", interpolation=FLAT, **changes):
    s = {"interpolator": interpolator, "nominal": 1.0, "startRate": 0.035, "observationLag": [2, M],
         "calendar": UK, "convention": int(ql.ModifiedFollowing), "dayCounter": A365, "index": INDEX,
         "interpolation": interpolation, "nominalCurve": ic.NOMINAL, "capStrikes": CAP_STRIKES,
         "floorStrikes": FLOOR_STRIKES, "maturities": MATURITIES, "capPrices": by_strike(CAP_BPS),
         "floorPrices": by_strike(FLOOR_BPS)}
    s.update(changes)
    return s


SURFACES = [("bilinear, flat", surface_spec()),
            ("bilinear, linear", surface_spec(interpolation=LINEAR)),
            ("bicubic, flat", surface_spec("Bicubic"))]
TENORS = [[2, Y], [3, Y], [4, Y], [5, Y], [6, Y], [7, Y], [10, Y], [12, Y]]
STRIKES = [-0.02, -0.01, 0.0, 0.005, 0.01, 0.02, 0.025, 0.03, 0.035, 0.04, 0.05, 0.06, 0.07]
DATES = [TODAY, TODAY + 365, TODAY + 3 * 365, TODAY + 5 * 365]


def _refused():
    prices = by_strike(CAP_BPS)
    rising = copy.deepcopy(prices)
    rising[1][0] = rising[0][0] * 2
    negative = by_strike(FLOOR_BPS)
    negative[0][1] = -0.0001
    return [("one floor strike", surface_spec(floorStrikes=[0.0], floorPrices=[by_strike(FLOOR_BPS)[1]])),
            ("a non-increasing maturity", surface_spec(maturities=[[3, Y], [5, Y], [5, Y], [10, Y]])),
            ("a non-positive floor price", surface_spec(floorPrices=negative)),
            ("a cap price rising with the strike", surface_spec(capPrices=rising)),
            ("no zero-inflation curve", surface_spec(index=ic.UKRPI_NO_CURVE if hasattr(ic, "UKRPI_NO_CURVE")
                                                      else icf.UKRPI_NO_CURVE))]


def cases():
    out = []
    with cxx.connect() as H:
        for label, s in SURFACES:
            with H.case_settings(v.enc(TODAY), fixings=ic.harness_fixings()) as st:
                expected = H.call(NAME + "values", s, TENORS, STRIKES, [v.enc(d) for d in DATES])
                out.append({"name": label, "inputs": {"settings": st, "kind": "values", "surface": s,
                                                      "tenors": TENORS, "strikes": STRIKES,
                                                      "dates": [v.enc(d) for d in DATES]},
                            "expected": expected})
        for label, s in _refused():
            with H.case_settings(v.enc(TODAY), fixings=ic.harness_fixings()) as st:
                out.append({"name": "refused: " + label,
                            "inputs": {"settings": st, "kind": "refused", "surface": s},
                            "expected": {"construction": v.attempt(
                                lambda: H.call(NAME + "values", s, [], [], []))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/inflation/cpicapfloortermpricesurface.hpp", cases)
