# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BlackSwaptionEngine and BachelierSwaptionEngine
(ql/pricingengines/swaption/blackswaptionengine.hpp) on a Volatility, the constructors the wheel doesn't bind, each
output computed by the C++ harness: 14 swaptions of tools/vectors/gen/instruments/swaption.py, whose functions build
them, each priced by the Black engine (with its displacement) and the Bachelier engine on a volatility, each with
both cash annuity models.

The Bachelier engine's constructors on a volatility pass their model to the base's constructor in the displacement's
place, so the model the engine keeps is always DiscountCurve: a cash swaption settled at the par yield curve prices
alike under both models, where the Black engine's two models differ. The swaptions cover each consistent settlement
pair, both kinds of underlying, a spread, a zero volatility, a Bermudan exercise and an expired swaption.

Each case's outputs are, per engine and model ([engine][output], the engines Black and Bachelier with the SwapRate
model, then with DiscountCurve), the NPV and the eleven additional results (spreadCorrection, strike, atmForward,
annuity, swapLength, stdDev, vega, delta, timeToExpiry, impliedVolatility, forwardPrice); a refused pricing or a result
the engine didn't set is {"throws": true}. Each case's inputs hold the swaption, the discount curve, the engines
({"type", "vol", "dayCounter", "displacement", "model"}, no displacement for the Bachelier engine) and the outputs' tags.
Read back by tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/BlackSwaptionEngineCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_swaption",
                                               os.path.join(v.GEN_ROOT, "instruments", "swaption.py"))
swp = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(swp)

TAGS = ["NPV", "spreadCorrection", "strike", "atmForward", "annuity", "swapLength", "stdDev", "vega", "delta",
        "timeToExpiry", "impliedVolatility", "forwardPrice"]
NAMES = [
    "1Y on 5Y payer 0.02 / 0-0 / 0", "1Y on 5Y receiver 0.02 / 1-3 / 3", "2Y on 2Y payer 0.02 / 1-2 / 6",
    "5Y on 10Y payer 0.031 / 0-1 / 13", "spread 0.001 / 0-0", "spread -0.0025 / 1-3",
    "cash par yield on 3M Euribor, model 0", "zero volatility", "displaced lognormal, negative strike",
    "Bermudan exercise", "expired", "overnight-indexed ESTR / 1-3 / spread 0.0", "overnight-indexed SOFR",
    "semiannual fixed leg, cash par yield",
]


def engines_of(black, bachelier):
    out = []
    for model in (swp.SWAP_RATE, swp.DISCOUNT_CURVE):
        out.append({"type": "BlackSwaptionEngine", "vol": black["vol"], "dayCounter": black["dayCounter"],
                    "displacement": black["displacement"], "model": model})
        out.append({"type": "BachelierSwaptionEngine", "vol": bachelier["vol"], "dayCounter": bachelier["dayCounter"],
                    "model": model})
    return out


def priced(H, spec, engine, tag):
    if engine["type"] == "BlackSwaptionEngine":
        return H.call("BlackSwaptionEngine::calculate(Volatility)", spec, swp.DISCOUNT, engine["vol"],
                      engine["dayCounter"], engine["displacement"], engine["model"], tag)
    return H.call("BachelierSwaptionEngine::calculate(Volatility)", spec, swp.DISCOUNT, engine["vol"],
                  engine["dayCounter"], engine["model"], tag)


def case(H, name, spec, black, bachelier, settings):
    harness_settings = {"include_reference_date_events": settings.get("include_reference_date_events", False)}
    if "use_at_par_coupons" in settings:
        harness_settings["use_at_par_coupons"] = settings["use_at_par_coupons"]
    with H.case_settings(v.enc(swp.TODAY), **harness_settings) as st:
        engines = engines_of(black, bachelier)
        scale = swp.fvf.scale_of(spec["swap"])
        values = []
        for e in engines:
            row = []
            for t in TAGS:
                x = v.attempt(lambda e=e, t=t: priced(H, spec, e, t))
                amount = t in ("NPV", "vega", "delta", "annuity", "forwardPrice") and not isinstance(x, dict)
                row.append(v.amount(x, scale) if amount else x)
            values.append(row)
        return {"name": name, "inputs": {"settings": st, "swaption": spec, "discountCurve": swp.DISCOUNT,
                                         "engines": engines, "tags": TAGS},
                "expected": {"values": values}}


def cases():
    rows = {row[0]: row for row in swp.swaptions()}
    with cxx.connect() as H:
        out = [case(H, *rows[name]) for name in NAMES]
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"]["values"] for c in out}
    # F1: the Bachelier engine's two models price a par-yield cash swaption alike; the Black engine's differ.
    cash = by_name["cash par yield on 3M Euribor, model 0"]
    assert cash[1] == cash[3], (cash[1], cash[3])
    assert cash[0][0] != cash[2][0]
    assert by_name["expired"][0][1] == v.THROWS and by_name["expired"][0][0]["value"] == 0.0


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/swaption/blackswaptionengine.hpp", cases)
