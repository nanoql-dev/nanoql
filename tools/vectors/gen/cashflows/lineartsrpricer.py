# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's LinearTsrPricer (ql/cashflows/lineartsrpricer.hpp, .cpp) on CMS coupons, on the evaluation
date Monday 16 June 2025, the curve swaptionvolcube.py's flat 5%, the coupons and outputs those of
tools/vectors/gen/cashflows/conundrumpricer.py.

Each case is a pricer on a volatility: the rate-bound strategy on its default bounds and on explicit ones (1e-4 and
100%), and the vega-ratio strategy at 1% on the default bounds and at 5% on explicit ones (1e-4 and 150%), each at mean
reversions of 0, 1% and -1%, and the default settings with a coupon discounting curve of its own (a flat 4%); on
swaptionvolcube.py's at-the-money matrix (lognormal), the matrix with a 1% shift, the matrix scaled to normal
volatilities, and the interpolated cube on the lognormal matrix, extrapolating. The strikes are 3%, 5% and 7%, on the
shifted matrix also -0.5%, and on the normal one -0.5% and -3%.

Left out, nanoql's fixes: the price-threshold strategy, dead in QuantLib; the standard-deviations strategy; reversions
below 1e-4 but 0; a lognormal strike at or below -shift; the SABR and ZABR cubes, whose sections carry an interpolated
forward of their own; and the vega-ratio strategy on a normal volatility, which QuantLib's section refuses. The
interpolated cube's sections carry the coupon's own forward, so its outputs are QuantLib's.

A refused value is {"throws": true}. Each case's inputs hold its settings, the pricer ("pricer": {"meanReversion",
"settings": {"strategy", "vegaRatio", "lowerRateBound", "upperRateBound", "defaultBounds"}, "couponDiscountCurve"}), the
volatility, the coupons, the strikes and the collar. The wheel's pricer and settings are SWIG wrappers of the C++
classes; its pricer queries read the state the coupon's rate() left. Read back by
tests/NanoQL.Tests/Vectors/Coupons/LinearTsrPricerVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, _dir)
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import conundrumpricer as cp  # noqa: E402
import vectors as v  # noqa: E402

svc, irx, mat = cp.svc, cp.irx, cp.mat
TODAY = cp.TODAY
OTHER_CURVE = svc.flat_curve(TODAY, 0.04)


def vols():
    shifts = [[0.01] * len(mat.ATM_SWAPS) for _ in mat.ATM_OPTIONS]
    return {
        "lognormal matrix": (svc.atm_floating(), cp.STRIKES),
        "shifted lognormal matrix": (svc.atm_floating(shifts=shifts), [-0.005] + cp.STRIKES),
        "normal matrix": (svc.atm_floating(svc.scaled(mat.ATM_VOLS, 0.05), svc.N), [-0.03, -0.005] + cp.STRIKES),
        "interpolated cube": (svc.interpolated_spec(svc.atm_floating(), cp.CURVE, extrapolate=True), cp.STRIKES),
    }


def settings_spec(strategy="RateBound", vega_ratio=0.01, lower=1e-4, upper=2.0, default_bounds=True):
    return {"strategy": strategy, "vegaRatio": vega_ratio, "lowerRateBound": lower, "upperRateBound": upper,
            "defaultBounds": default_bounds}


def pricers(normal):
    out = []
    strategies = [settings_spec(), settings_spec(upper=1.0, default_bounds=False)]
    if not normal:
        strategies += [settings_spec("VegaRatio"), settings_spec("VegaRatio", 0.05, 1e-4, 1.5, False)]
    for s in strategies:
        for m in (0.0, 0.01, -0.01):
            out.append({"meanReversion": m, "settings": s, "couponDiscountCurve": None})
    out.append({"meanReversion": 0.0, "settings": settings_spec(), "couponDiscountCurve": OTHER_CURVE})
    return out


def make_settings(spec):
    """The settings a spec describes. The with... members return a reference to the settings they change, which SWIG
    wraps without owning it, so the settings object built here is kept and returned."""
    s = ql.LinearTsrPricerSettings()
    if spec["strategy"] == "RateBound":
        if not spec["defaultBounds"]:
            s.withRateBound(spec["lowerRateBound"], spec["upperRateBound"])
    elif spec["defaultBounds"]:
        s.withVegaRatio(spec["vegaRatio"])
    else:
        s.withVegaRatio(spec["vegaRatio"], spec["lowerRateBound"], spec["upperRateBound"])
    return s


def make_pricer(spec, vol):
    h = ql.SwaptionVolatilityStructureHandle(vol)
    m = ql.QuoteHandle(ql.SimpleQuote(spec["meanReversion"]))
    curve = spec["couponDiscountCurve"]
    discount = irx.handle(curve) if curve is not None else ql.YieldTermStructureHandle()
    return ql.LinearTsrPricer(h, m, discount, make_settings(spec["settings"]))


def cases():
    out = []
    specs = cp.coupons()
    for vol_name, (vol_spec, strikes) in vols().items():
        for p in pricers(vol_name == "normal matrix"):
            s = p["settings"]
            name = (f"{s['strategy']} {'default' if s['defaultBounds'] else 'explicit'} bounds, reversion "
                    f"{p['meanReversion']!r}{', a coupon discounting curve' if p['couponDiscountCurve'] else ''}, "
                    f"{vol_name}")
            with v.case_settings(TODAY) as st:
                expected = cp.outputs(p, vol_spec, specs, strikes, make_pricer)
                out.append({"name": name,
                            "inputs": {"settings": st, "pricer": p, "volatility": vol_spec, "coupons": specs,
                                       "strikes": strikes, "cap": cp.CAP, "floor": cp.FLOOR},
                            "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/lineartsrpricer.hpp", cases)
