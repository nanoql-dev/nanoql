# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Gaussian1dSwaptionEngine's exercise probabilities
(ql/pricingengines/swaption/gaussian1dswaptionengine.hpp, .cpp), an additional result the wheel doesn't read, each
output computed by the C++ harness: European and Bermudan swaptions of gen/pricingengines/swaption/
gaussian1dswaptionengine.py's kind, payers and receivers, on its three GSR models, with the Naive and the Digital
probabilities, on the model's curve and with a discount curve on the same clock (a flat 2.5%). Each case's outputs are
the NPV and the probabilities, one per exercise alive and the last the probability of no exercise. A curve on another
clock is left out, as in the wheel's file (the S-g1d plan's G1R6 (xvi)).
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/Gaussian1dSwaptionEngineCxxVectorTests.cs reads it back."""
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

_spec = importlib.util.spec_from_file_location(
    "wheel_gaussian1dswaptionengine",
    os.path.join(v.GEN_ROOT, "pricingengines", "swaption", "gaussian1dswaptionengine.py"))
g1d = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(g1d)
swp = g1d.swp

NAIVE, DIGITAL = int(ql.Gaussian1dSwaptionEngine.Naive), int(ql.Gaussian1dSwaptionEngine.Digital)


def swaptions():
    out = []
    for first_tenor, length, count in (((1, ql.Years), (5, ql.Years), 4), ((2, ql.Years), (3, ql.Years), 3)):
        exercise, start, maturity = swp.dates_of(ql.Period(*first_tenor), ql.Period(*length))
        for swap_type in (swp.PAYER, swp.RECEIVER):
            out.append((f"Bermudan from {first_tenor[0]}Y on {length[0]}Y / {swap_type}",
                        swp.swaption_spec(swp.vanilla(swap_type, 1e6, 0.03, start, maturity),
                                          swp.bermudan(g1d.annual(exercise, count)))))
    exercise, start, maturity = swp.dates_of(ql.Period(2, ql.Years), ql.Period(5, ql.Years))
    for swap_type in (swp.PAYER, swp.RECEIVER):
        out.append((f"European 2Y on 5Y / {swap_type}",
                    swp.swaption_spec(swp.vanilla(swap_type, 1e6, 0.028, start, maturity), swp.european(exercise))))
    return out


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 1, 2025))
        out = []
        for name, spec in swaptions():
            for k, model in enumerate(g1d.MODELS):
                for prob, label in ((NAIVE, "Naive"), (DIGITAL, "Digital")):
                    for curve in (None, g1d.OTHER_DISCOUNT):
                        result = H.call("Gaussian1dSwaptionEngine::probabilities", spec, model, 64, 7.0, True, curve,
                                        prob)
                        out.append({"name": f"{name} / model {k} / {label}" + (" / discount curve" if curve else ""),
                                    "inputs": {"settings": st, "swaption": spec, "model": model,
                                               "probabilities": prob, "discountCurve": curve},
                                    "expected": {"NPV": {"value": result[0], "scale": 1e6},
                                                 "probabilities": result[1:]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/swaption/gaussian1dswaptionengine.hpp", cases)
