# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FloatFloatSwaption (ql/instruments/floatfloatswaption.hpp, .cpp) and its calibration basket
(BasketGeneratingEngine::calibrationBasket, ql/pricingengines/swaption/basketgeneratingengine.hpp, .cpp), on the
evaluation date 15 January 2025: six Bermudan float-float swaptions of
pricingengines/swaption/gaussian1dfloatfloatswaptionengine.py (Euribor 3M against 6M, payer; a gearing of 0.5 with
spreads at the money, receiver; a zero gearing with a 3% spread, a fixed leg in all but name, payer; amortizing with
both capital exchanges; caps and floors on Euribor 3M; Euribor 6M against CMS 10Y), each with a
Gaussian1dFloatFloatSwaptionEngine on gaussian1dswaptionengine.py's first GSR model and the 2.7% discount curve, its
basket of each type (naive and by delta and gamma) on a EuriborSwapIsdaFixA standard swap base forecast on the flat 3.1%
and discounted on the 2.7%, against a constant 20% lognormal swaption volatility. Per helper: its swaption's expiry and
maturity dates, nominal and strike, its volatility and its market value.

**The stability filter** runs on every output: each basket is generated again with the volatility, then the forecast
curve's rate, moved one ulp up and down by math.nextafter; an output is written only where every regeneration leaves it
within 1e-12 x max(|base|, |moved|, 1) (the dates where they agree), a market value, its nominal times a unit price,
only with its nominal, and the generator prints the outputs dropped per case. Left out: the two refusals #2845 adds
(FloatFloatBasketRefusalTests); and a basket's nominals and market values where a helper's nominal is QuantLib's floor
of 1e-6, a match that may be flipped, whose nominal nanoql gives its size (CalibrationBasketNominalTests). The wheel's
FloatFloatSwaption.calibrationBasket(swapIndex, swaptionVolatility, typeStr) is the C++ member's proxy, its helpers read
through as_swaption_helper. Read back by tests/NanoQL.Tests/Vectors/Instruments/FloatFloatSwaptionVectorTests.cs."""
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


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ffe = _load("wheel_gaussian1dfloatfloatswaptionengine", "pricingengines/swaption/gaussian1dfloatfloatswaptionengine.py")
g1d, ffs = ffe.g1d, ffe.ffs
irx, fvf = ffs.irx, ffs.fvf
TODAY = ffe.TODAY
DISCOUNT = ffs.DISCOUNT
RELATIVE = 1e-12
FLOOR = 0.000001  # QuantLib's floor on a matched nominal (basketgeneratingengine.cpp:213-214)
BASKETS = ["Naive", "MaturityStrikeByDeltaGamma"]
FIELDS = ["expiry", "maturity", "nominal", "strike", "volatility", "marketValue"]


def swaptions():
    rows = dict(ffe.swaptions())
    return [(name, rows[name]) for name in (
        "Euribor 3M against 6M, Bermudan, payer", "a gearing of 0.5 and spreads at the money, Bermudan, receiver",
        "a zero gearing and a 3% spread on Euribor 3M, Bermudan, payer",
        "amortizing, capital exchanged, Bermudan", "caps and floors on Euribor 3M, Bermudan",
        "Euribor 6M against CMS 10Y, Bermudan")]


def market(vol=0.20, forecast=0.031):
    return {"forecast": fvf.curve(forecast), "discount": DISCOUNT, "volatility": vol, "swapIndexTenor": [10, int(ql.Years)]}


def basket(spec, m, kind):
    swaption = ffe.make_swaption(spec)
    swaption.setPricingEngine(ffe.engine(g1d.make_gsr(ffe.MODELS[0]), {
        "integrationPoints": 64, "stddevs": 7.0, "extrapolatePayoff": True, "discountCurve": m["discount"]}))
    index = ql.EuriborSwapIsdaFixA(ql.Period(*m["swapIndexTenor"]), irx.handle(m["forecast"]), irx.handle(m["discount"]))
    vol = ql.ConstantSwaptionVolatility(0, ql.TARGET(), ql.ModifiedFollowing, m["volatility"], ql.Actual365Fixed())
    out = {f: [] for f in FIELDS}
    for h in swaption.calibrationBasket(index, vol, kind):
        s = ql.as_swaption_helper(h)
        out["expiry"].append(s.swaptionExpiryDate().serialNumber())
        out["maturity"].append(s.swaptionMaturityDate().serialNumber())
        out["nominal"].append(s.swaptionNominal())
        out["strike"].append(s.swaptionStrike())
        out["volatility"].append(s.volatility().value())
        out["marketValue"].append(s.marketValue())
    return out


def same(a, b):
    return len(a) == len(b) and all(x == y or abs(x - y) <= RELATIVE * max(abs(x), abs(y), 1.0) for x, y in zip(a, b))


def case(name, spec):
    with v.case_settings(TODAY) as st:
        m = market()
        expected = {}
        dropped, flipped = [], []
        for kind in BASKETS:
            base = basket(spec, m, kind)
            moved = [basket(spec, market(vol=math.nextafter(0.20, d)), kind) for d in (math.inf, -math.inf)]
            moved += [basket(spec, market(forecast=math.nextafter(0.031, d)), kind) for d in (math.inf, -math.inf)]
            for f in FIELDS:
                if f in ("nominal", "marketValue") and FLOOR in base["nominal"]:
                    flipped.append(f"{kind}.{f}")
                elif f == "marketValue" and f"{kind}.nominal" not in expected:
                    dropped.append(f"{kind}.{f}")
                elif all(same(base[f], x[f]) for x in moved):
                    expected[f"{kind}.{f}"] = base[f]
                else:
                    dropped.append(f"{kind}.{f}")
        print(f"{name}: dropped {len(dropped)} of {2 * len(FIELDS)} outputs {dropped}, a floored nominal's {flipped}")
        return {"name": name, "inputs": {"settings": st, "swaption": spec, "model": ffe.MODELS[0], "market": m},
                "expected": expected}


def cases():
    return [case(name, spec) for name, spec in swaptions()]


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/floatfloatswaption.hpp", cases)
