# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MarkovFunctional (ql/models/shortrate/onefactormodels/markovfunctional.hpp, .cpp), each output
computed by the wheel, under an evaluation date of 15 January 2025. Four models on a flat 2.7% curve: calibrated to
five yearly CMS 10y swaptions at a flat 20% volatility with Kahale's smile; to ten half-yearly 6m caplets without a
pretreatment; to the swaptions on volatility step dates with the digitals' and the yield curve's adjustments; and at a
zero reversion. On each, the volatilities, the parameters, the numeraire and the zero bond at six points of time and
state, a zero bond and a numeraire read on another curve of the model's clock, the forward rate of a 6m Euribor index,
the swap rate and annuity of a 5y swap index, and a zero-bond call and put. Then a calibration to four coterminal
swaption helpers, priced by the Gaussian1d swaption engine on the model, its first volatility fixed by default:
its end criterion, function evaluations, parameters, problem values and calibration errors.

**The stability filter** runs on every output: each model is built again with every nonzero real of its specs (and of
the helpers, for the calibration) moved one ulp up, then down (models/model.py's moved); an output is written only where
both rebuilds leave it within models/model.py's measure, and the generator prints the outputs dropped per case.

Left out, where nanoql's model differs: flat payoff extrapolation; a curve on another clock; a given curve shorter than
the numeraire time; a model without step dates at a volatility other than 1; and reversions between QuantLib's epsilon
and 1e-2. Each case's input "model" is the spec tools/cxx/src/models/shortrate/onefactormodels/markovfunctional.cpp
reads (the harness file's, tools/vectors/gen/cxx/models/shortrate/onefactormodels/markovfunctional.py);
tests/NanoQL.Tests/Vectors/Models/MarkovFunctionalVectorTests.cs reads it back."""
import copy
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


mdl = _load("wheel_model", "models/model.py")
swc = _load("wheel_swaptionconstantvol", "termstructures/volatility/swaption/swaptionconstantvol.py")
ocv = _load("wheel_constantoptionletvol", "termstructures/volatility/optionlet/constantoptionletvol.py")
swh = mdl.swh
irx = swh.irx

TODAY = swh.TODAY
CAL = ql.TARGET()
TARGET, A360, A365 = irx.TARGET, irx.A360, irx.A365
YEARS, MONTHS = int(ql.Years), int(ql.Months)
MF = int(ql.ModifiedFollowing)
KAHALE, SMILE_EXP, ADJUST_DIGITALS, ADJUST_YTS = 1 << 4, 1 << 5, 1 << 0, 1 << 1
MONEY = [0.1, 0.25, 0.50, 0.75, 1.0, 1.25, 1.50, 2.0, 5.0]
CURVE = swh.DISCOUNT
OTHER = irx.curve_spec(TODAY, 0.035)
LM = mdl.LM_TEST
EC = {"maxIterations": 1000, "maxStationaryStateIterations": 500, "rootEpsilon": 1e-2, "functionEpsilon": 1e-2,
      "gradientNormEpsilon": 1e-2}


def advance(n, unit):
    return v.enc(CAL.advance(TODAY, n, unit))


def settings(adjustments):
    return {"yGridPoints": 32, "yStdDevs": 7.0, "gaussHermitePoints": 16, "digitalGap": 1e-5,
            "marketRateAccuracy": 1e-7, "lowerRateBound": 0.0, "upperRateBound": 2.0, "adjustments": adjustments,
            "smileMoneynessCheckpoints": MONEY}


def swaption_model(adjustments, reversion=0.01, steps=(), vols=(1.0,)):
    return {"curve": CURVE, "reversion": reversion, "volstepdates": list(steps), "volatilities": list(vols),
            "kind": "swaption",
            "swaptionVol": swc.fixed_spec(TODAY, TARGET, ql.ModifiedFollowing, 0.20, A365),
            "expiries": [advance(i, ql.Years) for i in range(1, 6)], "tenors": [[10, YEARS]] * 5,
            "swapIndexTenor": [1, YEARS], "settings": settings(adjustments)}


def caplet_model(adjustments):
    return {"curve": CURVE, "reversion": 0.01, "volstepdates": [], "volatilities": [1.0], "kind": "caplet",
            "capletVol": ocv.fixed_spec(TODAY, TARGET, ql.ModifiedFollowing, 0.20, A365, 0, 0.0),
            "expiries": [advance(6 * i, ql.Months) for i in range(1, 11)],
            "iborIndex": irx.ibor_spec("Euribor", [6, MONTHS], 2, TARGET, ql.ModifiedFollowing, True, A360),
            "settings": settings(adjustments)}


STEPS = [advance(i, ql.Years) for i in range(1, 5)]
MODELS = [
    ("swaptions, Kahale's smile", swaption_model(KAHALE | SMILE_EXP)),
    ("caplets, no pretreatment", caplet_model(0)),
    ("swaptions on steps, the digitals' and the curve's adjustments",
     swaption_model(KAHALE | SMILE_EXP | ADJUST_DIGITALS | ADJUST_YTS, steps=STEPS, vols=(0.9, 1.0, 1.1, 1.2, 1.3))),
    ("swaptions, a zero reversion", swaption_model(KAHALE | SMILE_EXP, reversion=0.0)),
]
# (t, y) and (T, t, y)
NUMERAIRE_POINTS = [(0.0, 0.0), (0.5, 0.3), (1.0, 0.0), (2.5, -1.0), (4.0, 1.5), (5.0, -0.5)]
ZEROBOND_POINTS = [(1.0, 0.0, 0.0), (3.0, 1.0, 0.5), (6.0, 2.0, -1.0), (10.0, 4.5, 0.8), (15.0, 3.0, 0.0),
                   (5.0, 5.0, 0.2)]
CALIBRATION_STEPS = [advance(o, ql.Years) for o in (1, 2, 3)]
HELPERS = [swh.helper_spec("maturity", (o, ql.Years), (5 - o, ql.Years), vol, swh.E6M, CURVE)
           for o, vol in ((1, 0.20), (2, 0.19), (3, 0.18), (4, 0.175))]


def make_model(spec):
    s = spec["settings"]
    st = ql.MarkovFunctionalSettings(s["yGridPoints"], s["yStdDevs"], s["gaussHermitePoints"], s["digitalGap"],
                                     s["marketRateAccuracy"], s["lowerRateBound"], s["upperRateBound"],
                                     s["adjustments"], s["smileMoneynessCheckpoints"])
    steps = [ql.Date(d) for d in spec["volstepdates"]]
    expiries = [ql.Date(d) for d in spec["expiries"]]
    if spec["kind"] == "caplet":
        return ql.MarkovFunctional(irx.handle(spec["curve"]), spec["reversion"], steps, spec["volatilities"],
                                   ql.OptionletVolatilityStructureHandle(ocv.make_surface(spec["capletVol"])),
                                   expiries, irx.make_index(spec["iborIndex"]), st)
    tenors = [ql.Period(n, u) for n, u in spec["tenors"]]
    return ql.MarkovFunctional(irx.handle(spec["curve"]), spec["reversion"], steps, spec["volatilities"],
                               ql.SwaptionVolatilityStructureHandle(swc.make_surface(spec["swaptionVol"])),
                               expiries, tenors, ql.EuriborSwapIsdaFixA(ql.Period(*spec["swapIndexTenor"])), st)


def members(spec):
    with v.case_settings(TODAY) as st:
        m = make_model(spec)
        euribor = ql.Euribor6M(irx.handle(CURVE))
        swap_index = ql.EuriborSwapIsdaFixA(ql.Period(5, ql.Years), irx.handle(CURVE))
        other = irx.handle(OTHER)
        fixing = CAL.advance(TODAY, 2, ql.Years)
        reference = CAL.advance(TODAY, 1, ql.Years)
        out = {"volatility": list(m.volatility()), "params": list(m.params())}
        out.update({f"numeraire[{i}]": m.numeraire(t, y) for i, (t, y) in enumerate(NUMERAIRE_POINTS)})
        out.update({f"zerobond[{i}]": m.zerobond(T, t, y) for i, (T, t, y) in enumerate(ZEROBOND_POINTS)})
        out["numeraireOther"] = m.numeraire(2.0, 0.4, other)
        out["zerobondOther"] = m.zerobond(6.0, 2.0, 0.4, other)
        out["forwardRate"] = m.forwardRate(fixing, reference, 0.3, euribor)
        out["swapRate"] = m.swapRate(fixing, ql.Period(5, ql.Years), reference, 0.3, swap_index)
        out["swapAnnuity"] = m.swapAnnuity(fixing, ql.Period(5, ql.Years), reference, 0.3, swap_index)
        expiry, value, maturity = (CAL.advance(TODAY, 2, ql.Years), CAL.advance(TODAY, 2, ql.Years),
                                   CAL.advance(TODAY, 7, ql.Years))
        for name, kind in (("zerobondCall", ql.Option.Call), ("zerobondPut", ql.Option.Put)):
            out[name] = m.zerobondOption(kind, expiry, value, maturity, 0.88, reference, 0.2)
        flat = {}
        for k, x in out.items():
            if isinstance(x, list):
                flat.update({f"{k}[{i}]": e for i, e in enumerate(x)})
            else:
                flat[k] = x
        return st, flat


def calibrated(spec, helpers):
    with v.case_settings(TODAY, use_at_par_coupons=True) as st:
        model = make_model(spec)
        hs = []
        for h in helpers:
            helper = swh.make_helper(h)
            helper.setPricingEngine(ql.Gaussian1dSwaptionEngine(model, 64, 7.0))
            hs.append(helper)
        model.calibrate(hs, mdl.make_method(LM), mdl.make_criteria(EC))
        outputs = {"endCriteria": int(model.endCriteria()), "functionEvaluation": model.functionEvaluation()}
        for key, values in (("params", list(model.params())), ("problemValues", list(model.problemValues())),
                            ("calibrationErrors", [h.calibrationError() for h in hs])):
            outputs.update({f"{key}[{i}]": x for i, x in enumerate(values)})
        return st, outputs


def stable_kept(outputs, refits):
    return {k: x for k, x in outputs.items() if all(mdl.stable(x, r[k]) for r in refits)}


def cases():
    out = []
    for name, spec in MODELS:
        st, outputs = members(spec)
        refits = [members(mdl.moved(copy.deepcopy(spec), d))[1] for d in (math.inf, -math.inf)]
        kept = stable_kept(outputs, refits)
        print(f"{name}: dropped {len(outputs) - len(kept)} of {len(outputs)} outputs")
        out.append({"name": name,
                    "inputs": {"settings": st, "model": spec, "numerairePoints": NUMERAIRE_POINTS,
                               "zerobondPoints": ZEROBOND_POINTS, "otherCurve": OTHER, "forwardCurve": CURVE},
                    "expected": v.enc(kept)})
    spec = swaption_model(KAHALE | SMILE_EXP, steps=CALIBRATION_STEPS, vols=(1.0, 1.0, 1.0, 1.0))
    st, outputs = calibrated(spec, HELPERS)
    refits = [calibrated(mdl.moved(copy.deepcopy(spec), d), mdl.moved(copy.deepcopy(HELPERS), d))[1]
              for d in (math.inf, -math.inf)]
    kept = stable_kept(outputs, refits)
    print(f"calibration: dropped {len(outputs) - len(kept)} of {len(outputs)} outputs")
    out.append({"name": "calibrated to coterminal swaptions",
                "inputs": {"settings": st, "model": spec, "helpers": HELPERS, "method": LM, "endCriteria": EC},
                "expected": v.enc(kept)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/shortrate/onefactormodels/markovfunctional.hpp", cases)
