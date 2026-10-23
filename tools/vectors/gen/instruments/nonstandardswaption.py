# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's NonstandardSwaption (ql/instruments/nonstandardswaption.hpp, .cpp) and its calibration basket
(BasketGeneratingEngine::calibrationBasket, ql/pricingengines/swaption/basketgeneratingengine.hpp, .cpp), on the
evaluation date 15 January 2025: four Bermudan nonstandard swaptions of
pricingengines/swaption/gaussian1dnonstandardswaptionengine.py (constant terms, payer and receiver; amortizing with
step-up rates, gearings and spreads; a rebate per exercise date), each with a Gaussian1dNonstandardSwaptionEngine on
gaussian1dswaptionengine.py's first GSR model and the 2.7% discount curve, its basket of each type (naive and by delta
and gamma) on a EuriborSwapIsdaFixA standard swap base forecast on the flat 3.1% and discounted on the 2.7%, against a
constant 20% lognormal swaption volatility. Per helper: its swaption's expiry and maturity dates, nominal and strike,
its volatility and its market value.

**The stability filter** (the S7 plan's S7R6) runs on every output: each basket is generated again with the volatility,
then the forecast curve's rate, moved one ulp up and down by math.nextafter; an output is written only where every
regeneration leaves it within 1e-12 x max(|base|, |moved|, 1) (the dates where they agree), and the generator prints the
outputs dropped per case. The wheel's NonstandardSwaption.calibrationBasket(swapIndex, swaptionVolatility, typeStr) is
the C++ member's proxy, its helpers read through as_swaption_helper. Read back by
tests/NanoQL.Tests/Vectors/Instruments/NonstandardSwaptionVectorTests.cs."""
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


nse = _load("wheel_gaussian1dnonstandardswaptionengine", "pricingengines/swaption/gaussian1dnonstandardswaptionengine.py")
g1d, nss = nse.g1d, nse.nss
irx, fvf = nss.irx, nss.fvf
TODAY = nse.TODAY
RELATIVE = 1e-12
BASKETS = ["Naive", "MaturityStrikeByDeltaGamma"]
FIELDS = ["expiry", "maturity", "nominal", "strike", "volatility", "marketValue"]


def swaptions():
    rows = dict(nse.swaptions())
    return [(name, rows[name]) for name in (
        "constant terms, Bermudan, payer", "constant terms, Bermudan, receiver",
        "amortizing, step-up, gearings and spreads, Bermudan", "a rebate per exercise date, Bermudan")]


def market(vol=0.20, forecast=0.031):
    return {"forecast": fvf.curve(forecast), "discount": nss.DISCOUNT, "volatility": vol, "swapIndexTenor": [10, int(ql.Years)]}


def basket(spec, m, kind):
    swaption = nse.make_swaption(spec)
    swaption.setPricingEngine(nse.engine(g1d.make_gsr(nse.MODELS[0]), {
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
        dropped = []
        for kind in BASKETS:
            base = basket(spec, m, kind)
            moved = [basket(spec, market(vol=math.nextafter(0.20, d)), kind) for d in (math.inf, -math.inf)]
            moved += [basket(spec, market(forecast=math.nextafter(0.031, d)), kind) for d in (math.inf, -math.inf)]
            for f in FIELDS:
                if all(same(base[f], x[f]) for x in moved):
                    expected[f"{kind}.{f}"] = base[f]
                else:
                    dropped.append(f"{kind}.{f}")
        print(f"{name}: dropped {len(dropped)} of {2 * len(FIELDS)} outputs {dropped}")
        return {"name": name, "inputs": {"settings": st, "swaption": spec, "model": nse.MODELS[0], "market": m},
                "expected": expected}


def cases():
    return [case(name, spec) for name, spec in swaptions()]


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/nonstandardswaption.hpp", cases)
