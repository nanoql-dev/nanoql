# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's year-on-year cap and floor price surface
(ql/experimental/inflation/yoycapfloortermpricesurface.hpp, .cpp), through the wheel's
YoYInflationCapFloorTermPriceSurface, QuantLib's InterpolatedYoYCapFloorTermPriceSurface<Bicubic, Cubic>.

- "surface" cases: test-suite/inflationvolatility.cpp's euro market (its nominal curve, cubic in zero rates from 23
  November 2007; its euro year-on-year curve on EU HICP; its cap and floor prices on seven maturities), observed
  linearly with a three-month lag as QuantLib's case, flat, and on the first four maturities: the strikes, the
  maturities' range, the base and maximum dates, the at-the-money swap rates by time and by date, the swap rate and
  the year-on-year rate at dates, the cap, floor and price at tenors and strikes, and the bootstrapped year-on-year
  curve's rate at dates.

Left out: the swap rates by date, a pair of vectors the wheel's binding can't return
(tools/vectors/gen/cxx/experimental/inflation/yoycapfloortermpricesurface.py's harness vectors carry them), and the
swap rates' dates where an intersection fails (the S-xrt plan's XRR5 (xii),
tests/NanoQL.Tests/Divergences/YoYCapFloorPriceSurfaceTests.cs); on these markets every maturity's is found.

A case's inputs hold the settings, the market ({"nominal": {"dates", "rates"}, "yoy": {"referenceDate",
"dates", "rates"}}, the curves on Actual/365 (Fixed), the year-on-year curve monthly) and the surface's {"fixingDays",
"lag", "interpolation", "capStrikes", "floorStrikes", "maturities", "capPrices", "floorPrices"}, on TARGET, Modified
Following and Actual/365 (Fixed). The functions here are shared by the stripper's and the helper's generators, which
import this file. Read back by
tests/NanoQL.Tests/Vectors/Experimental/Inflation/YoYCapFloorTermPriceSurfaceVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

M, Y = int(ql.Months), int(ql.Years)
EVAL = ql.Date(23, ql.November, 2007)
A365 = ql.Actual365Fixed()
CAL = ql.TARGET()

# test-suite/inflationvolatility.cpp's setup()
TIMES_EUR = [0.0109589, 0.0684932, 0.263014, 0.317808, 0.567123, 0.816438, 1.06575, 1.31507, 1.56438, 2.0137,
             3.01918, 4.01644, 5.01644, 6.01644, 7.01644, 8.01644, 9.02192, 10.0192, 12.0192, 15.0247, 20.0301,
             25.0356, 30.0329, 40.0384, 50.0466]
RATES_EUR = [0.0415600, 0.0426840, 0.0470980, 0.0458506, 0.0449550, 0.0439784, 0.0431887, 0.0426604, 0.0422925,
             0.0424591, 0.0421477, 0.0421853, 0.0424016, 0.0426969, 0.0430804, 0.0435011, 0.0439368, 0.0443825,
             0.0452589, 0.0463389, 0.0472636, 0.0473401, 0.0470629, 0.0461092, 0.0450794]
YOY_EU_RATES = [0.0237951, 0.0238749, 0.0240334, 0.0241934, 0.0243567, 0.0245323, 0.0247213, 0.0249348, 0.0251768,
                0.0254337, 0.0257258, 0.0260217, 0.0263006, 0.0265538, 0.0267803, 0.0269378, 0.0270608, 0.0271363,
                0.0272, 0.0272512, 0.0272927, 0.027317, 0.0273615, 0.0273811, 0.0274063, 0.0274307, 0.0274625,
                0.027527, 0.0275952, 0.0276734, 0.027794]
CAP_STRIKES = [0.02, 0.025, 0.03, 0.035, 0.04, 0.05]
FLOOR_STRIKES = [-0.01, 0.00, 0.005, 0.01, 0.015, 0.02]
MATURITIES = [[3, Y], [5, Y], [7, Y], [10, Y], [15, Y], [20, Y], [30, Y]]
CAP_PRICES = [[116.225, 204.945, 296.285, 434.29, 654.47, 844.775, 1132.33],
              [34.305, 71.575, 114.1, 184.33, 307.595, 421.395, 602.35],
              [6.37, 19.085, 35.635, 66.42, 127.69, 189.685, 296.195],
              [1.325, 5.745, 12.585, 26.945, 58.95, 94.08, 158.985],
              [0.501, 2.37, 5.38, 13.065, 31.91, 53.95, 96.97],
              [0.501, 0.695, 1.47, 4.415, 12.86, 23.75, 46.7]]
FLOOR_PRICES = [[0.501, 0.851, 2.44, 6.645, 16.23, 26.85, 46.365],
                [0.501, 2.236, 5.555, 13.075, 28.46, 44.525, 73.08],
                [1.025, 3.935, 9.095, 19.64, 39.93, 60.375, 96.02],
                [2.465, 7.885, 16.155, 31.6, 59.34, 86.21, 132.045],
                [6.9, 17.92, 32.085, 56.08, 95.95, 132.85, 194.18],
                [23.52, 47.625, 74.085, 114.355, 175.72, 229.565, 316.285]]


def market_spec():
    d = []
    for t in TIMES_EUR:
        ys = int(t)
        ds = int((t - ys) * 365)
        d.append(EVAL + ql.Period(ys, ql.Years) + ql.Period(ds, ql.Days))
    base = ql.inflationPeriod(EVAL - ql.Period(1, ql.Months), ql.EUHICP().frequency())[0]
    cap_start = CAL.advance(EVAL, -2, ql.Months, ql.ModifiedFollowing)
    yd = [base] + [CAL.advance(cap_start, i, ql.Years, ql.ModifiedFollowing) for i in range(1, len(YOY_EU_RATES))]
    return {"nominal": {"dates": [v.enc(x) for x in d], "rates": RATES_EUR},
            "yoy": {"referenceDate": v.enc(EVAL), "dates": [v.enc(x) for x in yd], "rates": YOY_EU_RATES}}


def surface_spec(interpolation=int(ql.CPI.Linear), n=len(MATURITIES)):
    return {"fixingDays": 0, "lag": [3, M], "interpolation": interpolation, "capStrikes": CAP_STRIKES,
            "floorStrikes": FLOOR_STRIKES, "maturities": MATURITIES[:n],
            "capPrices": [row[:n] for row in CAP_PRICES], "floorPrices": [row[:n] for row in FLOOR_PRICES]}


def make_market(spec):
    nominal = ql.CubicZeroCurve([ql.Date(x) for x in spec["nominal"]["dates"]], spec["nominal"]["rates"], A365)
    y = spec["yoy"]
    yoy = ql.YoYInflationCurve(ql.Date(y["referenceDate"]), [ql.Date(x) for x in y["dates"]], y["rates"], ql.Monthly,
                               A365)
    index = ql.YoYInflationIndex(ql.EUHICP(), ql.YoYInflationTermStructureHandle(yoy))
    return ql.YieldTermStructureHandle(nominal), index


def matrix(rows):
    m = ql.Matrix(len(rows), len(rows[0]))
    for i, r in enumerate(rows):
        for j, x in enumerate(r):
            m[i][j] = x
    return m


def make_surface(spec, nominal, index):
    return ql.YoYInflationCapFloorTermPriceSurface(
        spec["fixingDays"], ql.Period(*spec["lag"]), index, spec["interpolation"], nominal, A365, CAL,
        ql.ModifiedFollowing, spec["capStrikes"], spec["floorStrikes"], [ql.Period(*m) for m in spec["maturities"]],
        matrix(spec["capPrices"]), matrix(spec["floorPrices"]))


CASES = [("QuantLib's case: linear, seven maturities", surface_spec()),
         ("flat, seven maturities", surface_spec(int(ql.CPI.Flat))),
         ("linear, four maturities", surface_spec(n=4))]
TENORS = [[1, Y], [3, Y], [4, Y], [5, Y], [7, Y], [10, Y], [12, Y], [20, Y], [30, Y]]
STRIKES = [-0.01, 0.0, 0.01, 0.02, 0.0225, 0.025, 0.03, 0.04, 0.05]


def cases():
    out = []
    market = market_spec()
    for label, s in CASES:
        with v.case_settings(EVAL) as st:
            nominal, index = make_market(market)
            surf = make_surface(s, nominal, index)
            dates = [surf.referenceDate() + ql.Period(k, ql.Years) for k in (1, 2, 3, 5, 8, 12, 25)]
            times, rates = surf.atmYoYSwapTimeRates()
            yoy = surf.YoYTS()
            expected = {
                "strikes": list(surf.strikes()),
                "minMaturity": v.enc(surf.minMaturity()), "maxMaturity": v.enc(surf.maxMaturity()),
                "baseDate": v.enc(surf.baseDate()), "maxDate": v.enc(surf.maxDate()),
                "atmYoYSwapTimeRates": [list(times), list(rates)],
                "atmYoYSwapRate": [v.attempt(lambda d=d: surf.atmYoYSwapRate(d)) for d in dates],
                "atmYoYRate": [v.attempt(lambda d=d: surf.atmYoYRate(d)) for d in dates],
                "capPrice": [[v.attempt(lambda t=t, k=k: surf.capPrice(ql.Period(*t), k)) for k in STRIKES]
                             for t in TENORS],
                "floorPrice": [[v.attempt(lambda t=t, k=k: surf.floorPrice(ql.Period(*t), k)) for k in STRIKES]
                               for t in TENORS],
                "price": [[v.attempt(lambda t=t, k=k: surf.price(ql.Period(*t), k)) for k in STRIKES]
                          for t in TENORS],
                "yoyRate": [v.attempt(lambda d=d: yoy.yoyRate(d)) for d in dates],
            }
            out.append({"name": label, "inputs": {"settings": st, "market": market, "surface": s,
                                                  "dates": [v.enc(d) for d in dates], "tenors": TENORS,
                                                  "strikes": STRIKES},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/inflation/yoycapfloortermpricesurface.hpp", cases)
