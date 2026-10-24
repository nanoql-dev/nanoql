# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Hagan CMS coupon pricers (ql/cashflows/conundrumpricer.hpp, .cpp): NumericHaganPricer and
AnalyticHaganPricer on CMS coupons, on the evaluation date Monday 16 June 2025, the curve swaptionvolcube.py's flat 5%.

Each case is a pricer on a volatility: the numeric pricer on each g-function model (Standard, ExactYield,
ParallelShifts, NonParallelShifts at a mean reversion of 0 and of 0.03) and the analytic pricer on the Standard and
ExactYield models, each on swaptionvolcube.py's at-the-money matrix (lognormal), the matrix scaled to normal
volatilities, and the SABR cube with beta fixed on the lognormal matrix, extrapolating; and both pricers on a shifted
lognormal matrix, which QuantLib's option pricer refuses ("VanillaOptionPricer: a normal or a zero-shift lognormal
volatility is required"). Each prices the same six coupons on the EUR 10Y and 2Y swap indexes: one year long starting in
1, 5 and 10 years, with a gearing and a spread, in arrears, and one whose fixing is the evaluation date, which QuantLib
takes as determined (a forecast fixing today). The outputs are, per coupon, the coupon's rate, then the pricer's
swapletPrice, and capletRate, capletPrice, floorletRate and floorletPrice at each strike (QuantLib's queries after the
coupon's rate initialized the pricer), and the rate of a collared coupon on the same terms (cap 6%, floor 3.5%), each as
a flat list over the coupons and strikes. The strikes are 3%, 5% and 7%, and on the normal matrix also -0.5%.

Left out, the S-cms plan's CMR5 fixes: the analytic pricer on the shift models, whose convexity term is high by
P(T0)/P(Tp) ((xxii)); a lognormal cap at or below 0 ((xxiii)); an index with an exogenous discounting curve ((xxii)); a
non-whole-year Standard index ((i)); and reversions below 1e-4 but 0 ((v)).

The SABR cube's cases take the stability filter (the S7 plan's S7R6): each is priced again on the cube refitted with
every input volatility moved one ulp up, then down (swaptionvolcube.py's moved()), and an output is written only where
both leave it within 1e-12 x max(|a|, |b|, 1); the dropped indexes per output are in the case's inputs "dropped".

A refused value is {"throws": true}. Each case's inputs hold its settings, the pricer ("pricer": {"type", "model",
"meanReversion"}), the volatility ("volatility": a matrix or cube spec of swaptionvolcube.py's), the coupons ("coupons":
{"payment", "start", "end", "fixingDays", "index", "gearing", "spread", "dayCounter", "inArrears"}), the strikes, the
collar and "dropped". The wheel's pricers and coupons are SWIG wrappers of the C++ classes; its pricer queries read the
state the coupon's rate() left. Read back by tests/NanoQL.Tests/Vectors/Coupons/ConundrumPricerVectorTests.cs."""
import importlib.util
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_swaptionvolcube", os.path.join(v.GEN_ROOT, "termstructures", "volatility", "swaption", "swaptionvolcube.py"))
svc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(svc)
irx, mat = svc.irx, svc.mat

TODAY = svc.TODAY
Y = svc.Y
CURVE = svc.flat_curve(TODAY)
CMS10 = svc.euribor([10, Y], CURVE)
CMS2 = svc.euribor([2, Y], CURVE)
CAP, FLOOR = 0.06, 0.035
STRIKES = [0.03, 0.05, 0.07]
NORMAL_STRIKES = [-0.005, 0.03, 0.05, 0.07]
MODELS = {"Standard": ql.GFunctionFactory.Standard, "ExactYield": ql.GFunctionFactory.ExactYield,
          "ParallelShifts": ql.GFunctionFactory.ParallelShifts,
          "NonParallelShifts": ql.GFunctionFactory.NonParallelShifts}


def vols():
    shifts = [[0.01] * len(mat.ATM_SWAPS) for _ in mat.ATM_OPTIONS]
    return {
        "lognormal matrix": svc.atm_floating(),
        "normal matrix": svc.atm_floating(svc.scaled(mat.ATM_VOLS, 0.05), svc.N),
        "SABR cube": svc.sabr_spec(svc.atm_floating(), CURVE, fixed=svc.BETA_FIXED, extrapolate=True),
        "shifted lognormal matrix": svc.atm_floating(shifts=shifts),
    }


def pricers():
    out = [{"type": "numeric", "model": m, "meanReversion": 0.0} for m in MODELS]
    out.append({"type": "numeric", "model": "NonParallelShifts", "meanReversion": 0.03})
    out += [{"type": "analytic", "model": m, "meanReversion": 0.0} for m in ("Standard", "ExactYield")]
    return out


def coupon_spec(start, years, index, gearing=1.0, spread=0.0, in_arrears=False):
    end = ql.TARGET().advance(start, years, ql.Years)
    return {"payment": end.serialNumber(), "start": start.serialNumber(), "end": end.serialNumber(), "fixingDays": 2,
            "index": index, "gearing": gearing, "spread": spread, "dayCounter": irx.A365, "inArrears": in_arrears}


def coupons():
    cal = ql.TARGET()
    spot = cal.advance(TODAY, 2, ql.Days)
    return [
        coupon_spec(cal.advance(spot, 1, ql.Years), 1, CMS10),
        coupon_spec(cal.advance(spot, 5, ql.Years), 1, CMS10, 0.8, 0.002),
        coupon_spec(cal.advance(spot, 10, ql.Years), 1, CMS10),
        coupon_spec(cal.advance(spot, 3, ql.Years), 1, CMS2),
        coupon_spec(cal.advance(spot, 3, ql.Years), 1, CMS10, in_arrears=True),
        coupon_spec(spot, 1, CMS10),
    ]


def make_vol(spec):
    return svc.make_cube(spec) if "atm" in spec else svc.make_atm(spec)


def make_pricer(spec, vol):
    h = ql.SwaptionVolatilityStructureHandle(vol)
    m = ql.QuoteHandle(ql.SimpleQuote(spec["meanReversion"]))
    if spec["type"] == "numeric":
        return ql.NumericHaganPricer(h, MODELS[spec["model"]], m)
    return ql.AnalyticHaganPricer(h, MODELS[spec["model"]], m)


def make_coupon(spec, index, cap=None, floor=None):
    args = (ql.Date(spec["payment"]), 1.0, ql.Date(spec["start"]), ql.Date(spec["end"]), spec["fixingDays"], index,
            spec["gearing"], spec["spread"])
    tail = (ql.Date(spec["start"]), ql.Date(spec["end"]), irx.day_counter(spec["dayCounter"]), spec["inArrears"])
    if cap is None and floor is None:
        return ql.CmsCoupon(*args, *tail)
    return ql.CappedFlooredCmsCoupon(*args, cap, floor, *tail)


def outputs(pricer_spec, vol_spec, specs, strikes, make=None):
    """The case's outputs, each a flat list over the coupons (and strikes), the pricers built by make (make_pricer by
    default)."""
    make = make if make is not None else make_pricer
    out = {k: [] for k in ["rate", "swapletPrice", "capletRate", "capletPrice", "floorletRate", "floorletPrice",
                           "collaredRate"]}
    try:
        vol = make_vol(vol_spec)
    except RuntimeError:
        vol = None
    a = v.attempt
    for spec in specs:
        index = svc.make_index(spec["index"])
        coupon = make_coupon(spec, index)
        try:
            pricer = make(pricer_spec, vol)
            coupon.setPricer(pricer)
            rate = a(coupon.rate)
        except RuntimeError:
            pricer, rate = None, dict(v.THROWS)
        out["rate"].append(rate)
        ok = pricer is not None and rate != v.THROWS
        out["swapletPrice"].append(a(pricer.swapletPrice) if ok else dict(v.THROWS))
        for k in strikes:
            for key, f in (("capletRate", "capletRate"), ("capletPrice", "capletPrice"),
                           ("floorletRate", "floorletRate"), ("floorletPrice", "floorletPrice")):
                out[key].append(a(lambda f=f, k=k: getattr(pricer, f)(k)) if ok else dict(v.THROWS))
        collar = make_coupon(spec, index, CAP, FLOOR)
        try:
            collar.setPricer(make(pricer_spec, vol))
            out["collaredRate"].append(a(collar.rate))
        except RuntimeError:
            out["collaredRate"].append(dict(v.THROWS))
    return out


def cases():
    out = []
    specs = coupons()
    total_lost, total = 0, 0
    for vol_name, vol_spec in vols().items():
        for p in pricers():
            if vol_name == "shifted lognormal matrix" and p["model"] != "Standard":
                continue
            strikes = NORMAL_STRIKES if vol_name == "normal matrix" else STRIKES
            name = f"{p['type']} {p['model']}, reversion {p['meanReversion']!r}, {vol_name}"
            with v.case_settings(TODAY) as st:
                expected = outputs(p, vol_spec, specs, strikes)
                dropped = {}
                if vol_name == "SABR cube":
                    refits = [outputs(p, svc.moved(vol_spec, direction), specs, strikes)
                              for direction in (math.inf, -math.inf)]
                    expected, dropped, lost, n = svc.filtered(name, expected, refits)
                    total_lost, total = total_lost + lost, total + n
                out.append({"name": name,
                            "inputs": {"settings": st, "pricer": p, "volatility": vol_spec, "coupons": specs,
                                       "strikes": strikes, "cap": CAP, "floor": FLOOR, "dropped": dropped},
                            "expected": expected})
    print(f"the file: dropped {total_lost} of {total} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/conundrumpricer.hpp", cases)
