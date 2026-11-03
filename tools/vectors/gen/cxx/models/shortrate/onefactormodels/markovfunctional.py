# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's MarkovFunctional (ql/models/shortrate/onefactormodels/markovfunctional.hpp,
.cpp) that the wheel doesn't bind, each output computed by the C++ harness on a model built inside the call: its
numeraire date and time, its arbitrage indices and its model outputs. Six models on a flat 3% curve: QuantLib's
calibration basket 1 (five yearly CMS 10y swaptions) at a flat 20% volatility with Kahale's smile and exponential
extrapolation, as QuantLib's testCalibrationOneInstrumentSet sets it; basket 2 (ten half-yearly 6m caplets) without
a pretreatment; basket 1 on volatility step dates with the digitals' and the yield curve's adjustments; Kahale's
interpolation with the deletion of arbitrage points; no payoff extrapolation; and a zero reversion with SABR's smile.
And the first model with its arbitrage indices forced, each point's left index moved up by one or two by the point's
parity (forceArbitrageIndices): its arbitrage indices, and its numeraire and zero bonds at a few states.
The grids are smaller than the defaults, for speed; QuantLib's own cases run the defaults.

**The stability filter** runs on every output: each model is built again with the volatility structure's volatility
moved one ulp up and down by math.nextafter, then the curve's forward; an output, a whole array or matrix, is written
only where every rebuild leaves each of its numbers within 1e-12 x max(|base|, |moved|, 1) (the dates, indices and
messages where every rebuild gives the same), and the generator prints the outputs dropped.

Left out, where nanoql's model differs: flat payoff extrapolation; a model without volatility step dates at a
volatility other than 1, whose state variance QuantLib's computes without the volatility's square; and reversions
between QuantLib's epsilon and 1e-2. Each case's input "model" is the spec
tools/cxx/src/models/shortrate/onefactormodels/markovfunctional.cpp reads; tests/NanoQL.Tests/Vectors/Cxx/Models/
MarkovFunctionalCxxVectorTests.cs reads it back."""
import importlib.util
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_interestrateindex",
                                               os.path.join(v.GEN_ROOT, "indexes", "interestrateindex.py"))
irx = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(irx)

RELATIVE = 1e-12
TODAY = ql.Date(14, ql.November, 2012)
TARGET, A360, A365 = irx.TARGET, irx.A360, irx.A365
YEARS, MONTHS = int(ql.Years), int(ql.Months)
MF = int(ql.ModifiedFollowing)
KAHALE, SMILE_EXP, KAHALE_INT, DELETE, SABR = 1 << 4, 1 << 5, 1 << 6, 1 << 7, 1 << 8
ADJUST_DIGITALS, ADJUST_YTS, NO_PAYOFF_EXTRAPOLATION = 1 << 0, 1 << 1, 1 << 3
MONEY = [0.1, 0.25, 0.50, 0.75, 1.0, 1.25, 1.50, 2.0, 5.0]
NUMERAIRE_POINTS = [[1.0, -1.0], [2.5, 0.3], [4.0, 1.5]]
ZEROBOND_POINTS = [[6.0, 2.5, 0.3], [12.0, 4.0, -0.5], [3.0, 1.0, 2.0]]
OUTPUTS = ["expiries", "tenors", "atm", "annuity", "adjustmentFactors", "digitalsAdjustmentFactors", "messages",
           "smileStrikes", "marketRawCallPremium", "marketRawPutPremium", "marketCallPremium", "marketPutPremium",
           "modelCallPremium", "modelPutPremium", "marketVega", "marketZerorate", "modelZerorate"]


def advance(n, unit):
    return v.enc(ql.TARGET().advance(TODAY, n, unit))


def settings(adjustments, grid=32, gh=16):
    return {"yGridPoints": grid, "yStdDevs": 7.0, "gaussHermitePoints": gh, "digitalGap": 1e-5,
            "marketRateAccuracy": 1e-7, "lowerRateBound": 0.0, "upperRateBound": 2.0, "adjustments": adjustments,
            "smileMoneynessCheckpoints": MONEY}


def swaption_model(adjustments, reversion=0.01, steps=(), vols=(1.0,), vol=0.20, forward=0.03):
    return {"curve": irx.curve_spec(TODAY, forward), "reversion": reversion, "volstepdates": list(steps),
            "volatilities": list(vols), "kind": "swaption",
            "swaptionVol": {"type": "ConstantSwaptionVolatility", "referenceDate": v.enc(TODAY), "calendar": TARGET,
                            "convention": MF, "volatility": vol, "dayCounter": A365, "volatilityType": 0,
                            "shift": 0.0, "enableExtrapolation": False},
            "expiries": [advance(i, ql.Years) for i in range(1, 6)], "tenors": [[10, YEARS]] * 5,
            "swapIndexTenor": [1, YEARS], "settings": settings(adjustments)}


def caplet_model(adjustments, vol=0.20, forward=0.03):
    return {"curve": irx.curve_spec(TODAY, forward), "reversion": 0.01, "volstepdates": [], "volatilities": [1.0],
            "kind": "caplet",
            "capletVol": {"type": "ConstantOptionletVolatility", "referenceDate": v.enc(TODAY), "calendar": TARGET,
                          "convention": MF, "volatility": vol, "dayCounter": A365, "volatilityType": 0,
                          "displacement": 0.0, "enableExtrapolation": False},
            "expiries": [advance(6 * i, ql.Months) for i in range(1, 11)],
            "iborIndex": irx.ibor_spec("Euribor", [6, MONTHS], 2, TARGET, ql.ModifiedFollowing, True, A360),
            "settings": settings(adjustments)}


STEPS = [advance(i, ql.Years) for i in range(1, 5)]
MODELS = [
    ("basket 1, Kahale's smile", swaption_model(KAHALE | SMILE_EXP)),
    ("basket 2, no pretreatment", caplet_model(0)),
    ("basket 1 on steps, the digitals' and the curve's adjustments",
     swaption_model(KAHALE | SMILE_EXP | ADJUST_DIGITALS | ADJUST_YTS, steps=STEPS, vols=(0.9, 1.0, 1.1, 1.2, 1.3))),
    ("basket 1, Kahale's interpolation deleting arbitrage points", swaption_model(KAHALE | KAHALE_INT | DELETE)),
    ("basket 1, no payoff extrapolation", swaption_model(KAHALE | SMILE_EXP | NO_PAYOFF_EXTRAPOLATION)),
    ("basket 1, a zero reversion, SABR's smile", swaption_model(SABR | SMILE_EXP, reversion=0.0)),
]


def moved_models(model):
    out = []
    for d in (math.inf, -math.inf):
        m = {**model}
        key = "swaptionVol" if model["kind"] == "swaption" else "capletVol"
        m[key] = {**model[key], "volatility": math.nextafter(model[key]["volatility"], d)}
        out.append(m)
    for d in (math.inf, -math.inf):
        out.append({**model, "curve": {**model["curve"], "forward": math.nextafter(model["curve"]["forward"], d)}})
    return out


def numbers(x):
    if isinstance(x, list):
        for y in x:
            yield from numbers(y)
    else:
        yield x


def same(a, b):
    if isinstance(a, (str, dict)) or a is None or isinstance(a, int):
        return a == b
    if isinstance(a, list):
        return isinstance(b, list) and len(a) == len(b) and all(same(x, y) for x, y in zip(a, b))
    return a == b or abs(a - b) <= RELATIVE * max(abs(a), abs(b), 1.0)


def outputs(H, model):
    out = {"numeraireDate": v.attempt(lambda: H.call("MarkovFunctional::numeraireDate", model)),
           "numeraireTime": v.attempt(lambda: H.call("MarkovFunctional::numeraireTime", model)),
           "arbitrageIndices": v.attempt(lambda: H.call("MarkovFunctional::arbitrageIndices", model))}
    if "forcedArbitrageIndices" in model:
        out["numeraire"] = v.attempt(lambda: H.call("MarkovFunctional::numeraire", model, NUMERAIRE_POINTS))
        out["zerobond"] = v.attempt(lambda: H.call("MarkovFunctional::zerobond", model, ZEROBOND_POINTS))
        return out
    mo = v.attempt(lambda: H.call("MarkovFunctional::modelOutputs", model))
    for name in OUTPUTS:
        out[name] = mo if isinstance(mo, dict) and "throws" in mo else mo[name]
    return out


def cases():
    with cxx.connect() as H:
        out = []
        with H.case_settings(v.enc(TODAY)) as st:
            first = v.enc(MODELS[0][1])
            indices = H.call("MarkovFunctional::arbitrageIndices", first)
            forced = {**first, "forcedArbitrageIndices": [[l + 1 + p % 2, r] for p, (l, r) in enumerate(indices)]}
            for name, model in MODELS + [("basket 1, Kahale's smile, its arbitrage indices forced", forced)]:
                model = v.enc(model)
                base = outputs(H, model)
                keep = set(base)
                for moved in moved_models(model):
                    again = outputs(H, moved)
                    keep = {k for k in keep if same(base[k], again[k])}
                dropped = sorted(set(base) - keep)
                print(f"{name}: dropped {len(dropped)} of {len(base)} outputs {dropped}")
                inputs = {"settings": st, "model": model}
                if "forcedArbitrageIndices" in model:
                    inputs.update(numerairePoints=NUMERAIRE_POINTS, zerobondPoints=ZEROBOND_POINTS)
                out.append({"name": name, "inputs": inputs,
                            "expected": {k: base[k] for k in base if k in keep}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/shortrate/onefactormodels/markovfunctional.hpp", cases)
