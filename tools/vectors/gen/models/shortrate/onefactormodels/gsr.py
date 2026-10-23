# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Gsr (ql/models/shortrate/onefactormodels/gsr.hpp, .cpp), on the evaluation date 15 January
2025: its constructions, its refusals and its calibrations.

The constructions: a constant volatility and reversion; piecewise volatilities on three step dates with a constant
reversion; piecewise reversions on the same dates, at another numeraire time. Each one's outputs are reversion(),
volatility(), params() and the zero bond at three points. The refusals: a step date on the curve's reference date
("volsteptimes must be positive"), step dates not increasing, volatilities and reversions of the wrong counts.

The calibrations, to four coterminal swaption helpers (models/shortrate/calibrationhelpers/swaptionhelper.py's specs,
Euribor 6M on its flat 3.1% curve, discounted on the flat 2.7%, 1Y into 4Y to 4Y into 1Y) each priced by
Gaussian1dSwaptionEngine on the model, Levenberg-Marquardt with models/model.py's test settings: calibrate with the
reversions fixed, on a model whose volatility steps are the helpers' exercise dates but the last; calibrate with
everything free, on a constant model; calibrateVolatilitiesIterative, unweighted, on the stepped model, whose record
is its last step's; and calibrateReversionsIterative, unweighted, on a model with piecewise reversions on the same
steps, which the wheel doesn't bind: its inline body (gsr.hpp:126-137), one calibration per helper with
MoveReversion(i)'s flags, runs on the wheel's calibrate. Each calibration's outputs (the end criterion, the function evaluations, the parameters,
the problem values and the helpers' calibration errors) pass the master plan's S7R6 filter, models/model.py's: each
case is refit with every nonzero real input of its helpers and its model one ulp up, then down, and an output is written
only where both refits leave it within 1e-12 x max(|a|, |b|, 1), an integer only where they leave it equal; the counts
print per case and per file.

Left out: weighted iterative calibrations, which QuantLib 1.44 refuses for every weight vector (the S-g1d plan's
G1R6 (xiv); tests/NanoQL.Tests/Divergences/GsrIterativeWeightsTests.cs); reversions between 0 and 1e-2 (G1R4 (i)).
The wheel's Gsr(handle, dates, quotes, quotes, T), reversion, volatility, params, zerobond, calibrate and
calibrateVolatilitiesIterative forward to the C++ members of those names. Read back by
tests/NanoQL.Tests/Vectors/Models/GsrVectorTests.cs."""
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
g1d = _load("wheel_gaussian1dswaptionengine", "pricingengines/swaption/gaussian1dswaptionengine.py")
swh = mdl.swh
irx = g1d.irx

TODAY = swh.TODAY
CAL = ql.TARGET()
Y = ql.Years
CURVE = swh.DISCOUNT
LM = mdl.LM_TEST
EC = mdl.EC_TEST

HELPERS = [swh.helper_spec("maturity", (o, Y), (5 - o, Y), vol, swh.E6M, CURVE)
           for o, vol in ((1, 0.20), (2, 0.19), (3, 0.18), (4, 0.175))]
EXERCISES = [v.enc(CAL.advance(TODAY, o, Y, ql.ModifiedFollowing)) for o in (1, 2, 3)]
STEPPED = {"curve": CURVE, "volstepdates": EXERCISES, "volatilities": [0.006, 0.006, 0.006, 0.006],
           "reversions": [0.02], "T": 60.0}
REVERSIONS = {"curve": CURVE, "volstepdates": EXERCISES, "volatilities": [0.006, 0.0065, 0.006, 0.0055],
              "reversions": [0.02, 0.02, 0.02, 0.02], "T": 60.0}
CONSTANT = {"curve": CURVE, "volstepdates": [], "volatilities": [0.006], "reversions": [0.02], "T": 60.0}

CONSTRUCTIONS = [
    ("constant", CONSTANT),
    ("piecewise volatilities", g1d.MODELS[1]),
    ("piecewise reversions", g1d.MODELS[2]),
]
ZB_POINTS = [(1.0, 0.0, 0.0), (5.0, 1.0, 0.5), (10.0, 3.0, -1.0)]


def construction_case(name, spec):
    with v.case_settings(TODAY) as st:
        m = g1d.make_gsr(spec)
        expected = {"reversion": list(m.reversion()), "volatility": list(m.volatility()), "params": list(m.params()),
                    "zerobond": [m.zerobond(*p) for p in ZB_POINTS]}
        return {"name": name, "inputs": {"settings": st, "model": spec, "zerobondPoints": ZB_POINTS},
                "expected": v.enc(expected)}


def refusal_cases():
    rows = [
        ("a step date on the reference date", dict(CONSTANT, volstepdates=[v.enc(TODAY)], volatilities=[0.01, 0.01])),
        ("step dates not increasing", dict(CONSTANT, volstepdates=[EXERCISES[1], EXERCISES[0]],
                                           volatilities=[0.01, 0.01, 0.01])),
        ("a step date repeated", dict(CONSTANT, volstepdates=[EXERCISES[0], EXERCISES[0]],
                                      volatilities=[0.01, 0.01, 0.01])),
        ("too few volatilities", dict(STEPPED, volatilities=[0.01, 0.01, 0.01])),
        ("too many volatilities", dict(CONSTANT, volatilities=[0.01, 0.01])),
        ("reversions of the wrong count", dict(STEPPED, reversions=[0.02, 0.03])),
    ]
    out = []
    for name, spec in rows:
        with v.case_settings(TODAY) as st:
            out.append({"name": "refused / " + name, "inputs": {"settings": st, "model": spec},
                        "expected": {"model": v.attempt(lambda: g1d.make_gsr(spec).params())}})
    return out


def priced_helpers(specs, model):
    out = []
    for spec in specs:
        h = swh.make_helper(spec)
        h.setPricingEngine(ql.Gaussian1dSwaptionEngine(model, 64, 7.0))
        out.append(h)
    return out


def calibrated(kind, specs, model_spec):
    with v.case_settings(TODAY, use_at_par_coupons=True) as st:
        model = g1d.make_gsr(model_spec)
        helpers = priced_helpers(specs, model)
        method, criteria = mdl.make_method(LM), mdl.make_criteria(EC)
        if kind == "iterative":
            model.calibrateVolatilitiesIterative(helpers, method, criteria)
        elif kind == "reversions iterative":
            # calibrateReversionsIterative, which the wheel doesn't bind, is inline (gsr.hpp:126-137): one calibration
            # per helper with MoveReversion(i)'s flags, run here on the wheel's calibrate as QuantLib's body runs it.
            n_rev, n_vol = len(model_spec["reversions"]), len(model_spec["volatilities"])
            for i, h in enumerate(helpers):
                fix = [True] * (n_rev + n_vol)
                fix[i] = False
                model.calibrate([h], method, criteria, ql.NoConstraint(), [], fix)
        elif kind == "fixed reversions":
            n_rev = len(model_spec["reversions"])
            fix = [True] * n_rev + [False] * len(model_spec["volatilities"])
            model.calibrate(helpers, method, criteria, ql.NoConstraint(), [], fix)
        else:
            model.calibrate(helpers, method, criteria)
        outputs = {"endCriteria": int(model.endCriteria()), "functionEvaluation": model.functionEvaluation()}
        for key, values in (("params", list(model.params())), ("problemValues", list(model.problemValues())),
                            ("calibrationErrors", [h.calibrationError() for h in helpers])):
            outputs.update({f"{key}[{i}]": x for i, x in enumerate(values)})
        return st, outputs


def calibration_case(dropped, name, kind, model_spec):
    st, outputs = calibrated(kind, HELPERS, model_spec)
    refits = [calibrated(kind, mdl.moved(copy.deepcopy(HELPERS), d), mdl.moved(copy.deepcopy(model_spec), d))[1]
              for d in (math.inf, -math.inf)]
    kept = {k: x for k, x in outputs.items() if all(mdl.stable(x, r[k]) for r in refits)}
    dropped.case(name, len(outputs) - len(kept), len(outputs))
    return {"name": name,
            "inputs": {"settings": st, "helpers": HELPERS, "model": model_spec, "kind": kind, "method": LM,
                       "endCriteria": EC},
            "expected": v.enc(kept)}


def cases():
    out = [construction_case(*row) for row in CONSTRUCTIONS]
    out += refusal_cases()
    dropped = mdl.Dropped()
    out += [calibration_case(dropped, "calibrate, reversions fixed", "fixed reversions", STEPPED),
            calibration_case(dropped, "calibrate, everything free", "free", CONSTANT),
            calibration_case(dropped, "calibrateVolatilitiesIterative", "iterative", STEPPED),
            calibration_case(dropped, "calibrateReversionsIterative", "reversions iterative", REVERSIONS)]
    dropped.file("gsr")
    for c in out:
        if c["name"].startswith("refused / "):
            assert c["expected"]["model"] == v.THROWS, c["name"]
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/shortrate/onefactormodels/gsr.hpp", cases)
