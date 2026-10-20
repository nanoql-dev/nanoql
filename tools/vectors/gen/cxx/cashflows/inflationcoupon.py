# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's inflation coupons' price and setPricer (ql/cashflows/inflationcoupon.hpp), each output
computed by the C++ harness: the wheel binds neither.

- "price" cases: CPI and year-on-year coupons, forecast and fixed, each with a pricer of its type, on two discount
  curves: price, the coupon's amount discounted to its payment date.
- "setPricer" cases: each coupon given a pricer of its own type, of the other coupon's type, and none: accepted, or
  refused.

The case's settings store UK RPI's fixings (tools/vectors/gen/cashflows/indexedcashflow.py's) by name, on the first of
each month, as QuantLib stores an inflation index's fixing. A case's inputs hold its settings, the index spec, the
coupon and pricer specs, as tools/cxx/src/cashflows/inflationspec.hpp reads them, and the discount curve. The
functions here are shared by the harness's other inflation generators (tools/vectors/gen/cxx/cashflows/
cpicouponpricer.py, inflationcouponpricer.py, tools/vectors/gen/cxx/instruments/inflationswapinspectors.py,
makeyoyinflationcapfloor.py and tools/vectors/gen/cxx/pricingengines/inflation/inflationcapfloorengines.py), which
import this file. Read back by tests/NanoQL.Tests/Vectors/Cxx/Coupons/InflationCouponCxxVectorTests.cs."""
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


def _load(name, *path):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *path))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


icf = _load("wheel_indexedcashflow", "cashflows", "indexedcashflow.py")
yoyvol = _load("wheel_yoyvol", "termstructures", "volatility", "inflation",
               "yoyinflationoptionletvolatilitystructure.py")
cf = icf.cf
irx = icf.irx

TODAY = icf.TODAY
UK = cf.UK_SETTLEMENT
A365 = cf.A365
THIRTY360_BB = cf.THIRTY360_BB
M = int(ql.Months)
UKRPI = icf.UKRPI
RATIO = icf.YYUKRPI_RATIO
NOMINAL = icf.NOMINAL
OTHER_CURVE = irx.curve_spec(TODAY, 0.05)
CPI_VOL = {"type": "ConstantCPIVolatility", "volatility": 0.01, "settlementDays": 0, "calendar": UK,
           "convention": int(ql.ModifiedFollowing), "dayCounter": A365, "observationLag": [3, M],
           "frequency": int(ql.Monthly), "indexIsInterpolated": False, "enableExtrapolation": False}
YOY_VOL = yoyvol.surface_spec(0.01, 0, UK, ql.ModifiedFollowing, A365, [2, M], ql.Monthly, False)
BLACK_VOL = yoyvol.surface_spec(0.15, 0, UK, ql.ModifiedFollowing, A365, [2, M], ql.Monthly, False)


def harness_fixings():
    """UK RPI's fixings by name, each on the first of its month, where QuantLib's inflation index stores it."""
    out = []
    for serial, value in icf.UK_FIXINGS:
        d = ql.Date(serial)
        out.append([v.enc(ql.Date(1, d.month(), d.year())), value])
    return {"UK RPI": out}


def yoy_coupon(payment, nominal, start, end, days, lag, interp, dc, gearing, spread):
    return {"paymentDate": v.enc(payment), "nominal": nominal, "startDate": v.enc(start), "endDate": v.enc(end),
            "fixingDays": days, "observationLag": [lag, M], "interpolation": int(interp), "dayCounter": dc,
            "gearing": gearing, "spread": spread}


def cpi_coupon(base_cpi, base_date, payment, nominal, start, end, lag, interp, dc, rate):
    return {"baseCPI": base_cpi, "baseDate": v.enc(base_date), "paymentDate": v.enc(payment), "nominal": nominal,
            "startDate": v.enc(start), "endDate": v.enc(end), "observationLag": [lag, M],
            "interpolation": int(interp), "dayCounter": dc, "fixedRate": rate}


def pricer(kind, volatility=None, nominal=None):
    return {"type": kind, "volatility": volatility, "nominalCurve": nominal}


CPI_COUPONS = [
    ("CPI, base CPI, flat, forecast",
     cpi_coupon(305.2, None, ql.Date(17, ql.June, 2025), 1e6, TODAY, ql.Date(15, ql.June, 2025), 3, ql.CPI.Flat,
                A365, 0.02)),
    ("CPI, base date, linear, forecast",
     cpi_coupon(None, ql.Date(1, ql.March, 2024), ql.Date(1, ql.February, 2027), 2.5e6, ql.Date(31, ql.July, 2024),
                ql.Date(31, ql.January, 2027), 3, ql.CPI.Linear, THIRTY360_BB, 0.0125)),
    ("CPI, both bases, linear, fixed",
     cpi_coupon(301.0, ql.Date(1, ql.July, 2023), ql.Date(4, ql.April, 2024), 1e6, ql.Date(1, ql.October, 2023),
                ql.Date(1, ql.April, 2024), 2, ql.CPI.Linear, A365, 0.03)),
]
YOY_COUPONS = [
    ("year-on-year, flat, forecast",
     yoy_coupon(ql.Date(17, ql.June, 2025), 1e6, TODAY, ql.Date(15, ql.June, 2025), 0, 2, ql.CPI.Flat, A365, 1.0,
                0.0)),
    ("year-on-year, linear, forecast, geared",
     yoy_coupon(ql.Date(16, ql.March, 2026), 1e6, ql.Date(15, ql.March, 2025), ql.Date(15, ql.March, 2026), 0, 2,
                ql.CPI.Linear, A365, 1.4, 0.003)),
    ("year-on-year, flat, fixed, fixing days",
     yoy_coupon(ql.Date(3, ql.May, 2024), 1e6, ql.Date(1, ql.May, 2023), ql.Date(1, ql.May, 2024), 2, 2, ql.CPI.Flat,
                THIRTY360_BB, 0.8, -0.001)),
]
CPI_PRICERS = [pricer("CPICouponPricer"), pricer("CPICouponPricer", CPI_VOL, NOMINAL)]
YOY_PRICERS = [pricer("YoYInflationCouponPricer"), pricer("BlackYoYInflationCouponPricer", BLACK_VOL, NOMINAL)]


def price_cases(H):
    out = []
    k = 0
    for coupons, index, kind, pricers in ((CPI_COUPONS, UKRPI, "cpi", CPI_PRICERS),
                                          (YOY_COUPONS, RATIO, "yoy", YOY_PRICERS)):
        method = "CPICoupon::price" if kind == "cpi" else "YoYInflationCoupon::price"
        for label, coupon in coupons:
            for p in pricers:
                for curve in (NOMINAL, OTHER_CURVE):
                    with H.case_settings(v.enc(TODAY), fixings=harness_fixings()) as st:
                        out.append({"name": f"price {label}, {p['type']} / {k}",
                                    "inputs": {"settings": st, "kind": kind + "Price", "index": index,
                                               "coupon": coupon, "pricer": p, "discountCurve": curve},
                                    "expected": {"price": v.attempt(lambda: H.call(method, index, coupon, p, curve))}})
                    k += 1
    return out


def set_pricer_cases(H):
    out = []
    k = 0
    for coupons, index, kind, own, other in ((CPI_COUPONS, UKRPI, "cpi", CPI_PRICERS[1], YOY_PRICERS[1]),
                                             (YOY_COUPONS, RATIO, "yoy", YOY_PRICERS[1], CPI_PRICERS[1])):
        method = "CPICoupon::setPricer" if kind == "cpi" else "YoYInflationCoupon::setPricer"
        label, coupon = coupons[0]
        for which, p in (("its own type", own), ("the other type", other), ("none", None)):
            with H.case_settings(v.enc(TODAY), fixings=harness_fixings()) as st:
                out.append({"name": f"setPricer {label}, {which} / {k}",
                            "inputs": {"settings": st, "kind": kind + "SetPricer", "index": index, "coupon": coupon,
                                       "pricer": p},
                            "expected": {"setPricer": v.attempt(lambda: H.call(method, index, coupon, p))}})
            k += 1
    return out


def cases():
    with cxx.connect() as H:
        return price_cases(H) + set_pricer_cases(H)


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/inflationcoupon.hpp", cases)
