# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CalibratedModel::calibrate and value (ql/models/model.hpp, .cpp): 50 calibrations of
HullWhite and Vasicek to swaption helpers priced by JamshidianSwaptionEngine, and 11 refusals and edge cases.

The helper sets are QuantLib's test-suite/shortratemodels.cpp's (five coterminal swaptions on the evaluation date 15
February 2002, on Euribor 6M and on its twin with no fixing days) and models/shortrate/calibrationhelpers/
swaptionhelper.py's second market's (six swaptions from 1Y x 5Y to 10Y x 5Y on Euribor 6M, with Black volatilities and
the relative price error, normal volatilities and the implied-volatility error, or Black volatilities and the price
error). The calibrations cover Levenberg-Marquardt (QuantLib's test-suite settings, its defaults, and with the cost
function's Jacobian, whose central differences take the calibration function's step 1e-6) and Simplex; the test-suite's
and tighter and shorter end criteria (one ending at its maximum iterations); weights; Hull-White's FixedReversion() and
other fixed parameters (Vasicek's among them); and constraints added to the model's own (NoConstraint, which changes
nothing, PositiveConstraint, and a BoundaryConstraint, from starts inside and outside it). Each calibration starts from
a fresh model, so no case sees another's parameters (the plan's caveat 4).

Each calibration's outputs are the calibrated parameters ("params"), the end criterion ("endCriteria", QuantLib's
EndCriteria::Type), the problem values and the number of function evaluations, which counts the evaluation of the
problem values after the minimization, as QuantLib's does ("problemValues", "functionEvaluation"; caveat 5: read only
after a calibration); each helper's calibration error under the calibrated model ("calibrationErrors"); and value() at
the calibrated parameters and at two others ("value", "values"). The refusals and edge cases: no helpers; weights or
fixed-parameter specs of the wrong size, and both at once; every parameter fixed; a helper with no engine; value() at
no parameters, too few and too many; and value() with no helpers, which is 0.

A case's inputs hold the helpers (swaptionhelper.py's specs, each priced by a JamshidianSwaptionEngine on the model,
with the helper's term structure given for a Vasicek), the model ({"type": "HullWhite", "termStructure", "a",
"sigma"} or {"type": "Vasicek", "r0", "a", "b", "sigma", "lambda"}), the method ({"type": "LevenbergMarquardt",
"epsfcn", "xtol", "gtol", "useCostFunctionsJacobian"} or {"type": "Simplex", "lambda"}), the end criteria
(math/optimization/simplex.py's spec), and the optional constraint (math/optimization/constraint.py's spec), weights and
fixed parameters, omitted where QuantLib's default is taken. The wheel can't build QuantLib's empty Constraint(), so a
case that passes weights or fixed parameters passes NoConstraint() before them, which gives the default's parameters,
and its inputs record it. Read back by tests/NanoQL.Tests/Vectors/Models/ModelVectorTests.cs."""
import importlib.util
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


swh = _load("wheel_swaptionhelper", "models/shortrate/calibrationhelpers/swaptionhelper.py")
fvf, irx = swh.fvf, swh.irx
Y = ql.Years

NO_CONSTRAINT = {"type": "NoConstraint"}

HELPERS_2002 = [swh.helper_spec("maturity", (s, Y), (n, Y), vol, swh.EURIBOR6M_2002, swh.CURVE_2002)
                for s, n, vol in swh.COTERMINAL]
INDEX0_2002 = irx.ibor_spec("Euribor", (6, ql.Months), 0, swh.TARGET, ql.ModifiedFollowing, True, swh.A360,
                            swh.CURVE_2002)
HELPERS_2002_0 = [swh.helper_spec("maturity", (s, Y), (n, Y), vol, INDEX0_2002, swh.CURVE_2002)
                  for s, n, vol in swh.COTERMINAL]
GRID_2025 = [((1, Y), (5, Y), 0.22, 0.0090), ((2, Y), (4, Y), 0.21, 0.0088), ((3, Y), (3, Y), 0.20, 0.0086),
             ((5, Y), (2, Y), 0.19, 0.0083), ((7, Y), (3, Y), 0.185, 0.0080), ((10, Y), (5, Y), 0.18, 0.0078)]
HELPERS_2025 = [swh.helper_spec("maturity", o, n, vol, swh.E6M, swh.DISCOUNT) for o, n, vol, _ in GRID_2025]
HELPERS_2025_NORMAL = [swh.helper_spec("maturity", o, n, nvol, swh.E6M, swh.DISCOUNT, error_type=swh.IMPLIED,
                                       vol_type=swh.N) for o, n, _, nvol in GRID_2025]
HELPERS_2025_PRICE = [swh.helper_spec("maturity", o, n, vol, swh.E6M, swh.DISCOUNT, error_type=swh.PRICE,
                                      nominal=1e6) for o, n, vol, _ in GRID_2025]
HELPER_SETS = {"2002": (swh.TODAY_2002, HELPERS_2002), "2002, no fixing days": (swh.TODAY_2002, HELPERS_2002_0),
               "2025": (swh.TODAY, HELPERS_2025), "2025 normal": (swh.TODAY, HELPERS_2025_NORMAL),
               "2025 price": (swh.TODAY, HELPERS_2025_PRICE)}

LM_TEST = {"type": "LevenbergMarquardt", "epsfcn": 1e-8, "xtol": 1e-8, "gtol": 1e-8, "useCostFunctionsJacobian": False}
LM_DEFAULT = dict(LM_TEST)
LM_JACOBIAN = dict(LM_TEST, useCostFunctionsJacobian=True)
SIMPLEX = {"type": "Simplex", "lambda": 0.01}
SIMPLEX_WIDE = {"type": "Simplex", "lambda": 0.1}

EC_TEST = {"maxIterations": 10000, "maxStationaryStateIterations": 100, "rootEpsilon": 1e-6,
           "functionEpsilon": 1e-8, "gradientNormEpsilon": 1e-8}
EC_FIXED = {"maxIterations": 1000, "maxStationaryStateIterations": 500, "rootEpsilon": 1e-8,
            "functionEpsilon": 1e-8, "gradientNormEpsilon": 1e-8}
EC_SHORT = {"maxIterations": 3, "maxStationaryStateIterations": 2, "rootEpsilon": 1e-8, "functionEpsilon": 1e-8,
            "gradientNormEpsilon": 1e-8}
EC_SIMPLEX = {"maxIterations": 400, "maxStationaryStateIterations": 100, "rootEpsilon": 1e-8, "functionEpsilon": 1e-8,
              "gradientNormEpsilon": 1e-8}


def hull_white_spec(ts, a=0.1, sigma=0.01):
    return {"type": "HullWhite", "termStructure": ts, "a": a, "sigma": sigma}


def vasicek_spec(r0=0.03, a=0.1, b=0.05, sigma=0.01, lam=0.0):
    return {"type": "Vasicek", "r0": r0, "a": a, "b": b, "sigma": sigma, "lambda": lam}


def make_model(spec):
    if spec["type"] == "HullWhite":
        return ql.HullWhite(irx.handle(spec["termStructure"]), spec["a"], spec["sigma"])
    return ql.Vasicek(spec["r0"], spec["a"], spec["b"], spec["sigma"], spec["lambda"])


def make_method(spec):
    if spec["type"] == "Simplex":
        return ql.Simplex(spec["lambda"])
    return ql.LevenbergMarquardt(spec["epsfcn"], spec["xtol"], spec["gtol"], spec["useCostFunctionsJacobian"])


def make_constraint(spec):
    t = spec["type"]
    if t == "NoConstraint":
        return ql.NoConstraint()
    if t == "PositiveConstraint":
        return ql.PositiveConstraint()
    if t == "BoundaryConstraint":
        return ql.BoundaryConstraint(spec["low"], spec["high"])
    raise ValueError(t)


def make_criteria(spec):
    return ql.EndCriteria(spec["maxIterations"], spec["maxStationaryStateIterations"], spec["rootEpsilon"],
                          spec["functionEpsilon"], spec["gradientNormEpsilon"])


def priced_helpers(specs, model, model_spec, engine=True):
    """The wheel's helpers, each priced by a Jamshidian engine on the model."""
    out = []
    for spec in specs:
        h = swh.make_helper(spec)
        if engine:
            if model_spec["type"] == "HullWhite":
                h.setPricingEngine(ql.JamshidianSwaptionEngine(model))
            else:
                h.setPricingEngine(ql.JamshidianSwaptionEngine(model, irx.handle(spec["termStructure"])))
        out.append(h)
    return out


def calibrations():
    """(name, helper set, model, method, end criteria, constraint, weights, fixParameters) for the 50."""
    out = []
    ts2002, ts2025 = swh.CURVE_2002, swh.DISCOUNT
    hw2002 = hull_white_spec(ts2002)
    # QuantLib's three cached cases and their variations.
    out.append(("testCachedHullWhite", "2002", hw2002, LM_TEST, EC_TEST, None, None, None))
    out.append(("testCachedHullWhiteFixedReversion", "2002", hull_white_spec(ts2002, 0.05, 0.01), LM_DEFAULT,
                EC_FIXED, NO_CONSTRAINT, [], [True, False]))
    out.append(("testCachedHullWhite2", "2002, no fixing days", hw2002, LM_TEST, EC_TEST, None, None, None))
    out.append(("2002 / Simplex", "2002", hw2002, SIMPLEX, EC_SIMPLEX, None, None, None))
    out.append(("2002 / Simplex, wide", "2002", hw2002, SIMPLEX_WIDE, EC_SIMPLEX, None, None, None))
    out.append(("2002 / cost function's Jacobian", "2002", hw2002, LM_JACOBIAN, EC_TEST, None, None, None))
    out.append(("2002 / short end criteria", "2002", hw2002, LM_TEST, EC_SHORT, None, None, None))
    out.append(("2002 / NoConstraint", "2002", hw2002, LM_TEST, EC_TEST, NO_CONSTRAINT, None, None))
    out.append(("2002 / weights", "2002", hw2002, LM_TEST, EC_TEST, NO_CONSTRAINT, [1.0, 2.0, 3.0, 4.0, 5.0], None))
    out.append(("2002 / weights and fixed reversion", "2002", hw2002, LM_TEST, EC_TEST, NO_CONSTRAINT,
                [2.0, 1.0, 1.0, 1.0, 0.5], [True, False]))
    out.append(("2002 / fixed volatility", "2002", hw2002, LM_TEST, EC_TEST, NO_CONSTRAINT, [], [False, True]))
    out.append(("2002 / free, given explicitly", "2002", hw2002, LM_TEST, EC_TEST, NO_CONSTRAINT, [],
                [False, False]))
    out.append(("2002 / PositiveConstraint", "2002", hw2002, LM_TEST, EC_TEST, {"type": "PositiveConstraint"}, None,
                None))
    out.append(("2002 / boundary from inside", "2002", hull_white_spec(ts2002, 0.04, 0.006), LM_TEST, EC_TEST,
                {"type": "BoundaryConstraint", "low": 0.0, "high": 0.2}, None, None))
    out.append(("2002 / boundary from outside", "2002", hull_white_spec(ts2002, 0.3, 0.03), LM_TEST, EC_TEST,
                {"type": "BoundaryConstraint", "low": 0.0, "high": 0.02}, None, None))
    out.append(("2002 / Simplex, boundary", "2002", hull_white_spec(ts2002, 0.04, 0.006), SIMPLEX, EC_SIMPLEX,
                {"type": "BoundaryConstraint", "low": 0.0, "high": 0.2}, None, None))
    out.append(("2002 / Simplex, fixed reversion", "2002", hull_white_spec(ts2002, 0.05, 0.01), SIMPLEX, EC_SIMPLEX,
                NO_CONSTRAINT, [], [True, False]))
    for a, sigma in ((0.03, 0.012), (0.2, 0.002), (0.01, 0.02)):
        out.append((f"2002 / start {a}, {sigma}", "2002", hull_white_spec(ts2002, a, sigma), LM_TEST, EC_TEST, None,
                    None, None))
    # 20 so far: the second market.
    hw2025 = hull_white_spec(ts2025)
    for set_name in ("2025", "2025 normal", "2025 price"):
        out.append((f"{set_name} / Levenberg-Marquardt", set_name, hw2025, LM_TEST, EC_TEST, None, None, None))
        out.append((f"{set_name} / defaults", set_name, hw2025, LM_DEFAULT, EC_FIXED, None, None, None))
        out.append((f"{set_name} / Simplex", set_name, hw2025, SIMPLEX, EC_SIMPLEX, None, None, None))
        out.append((f"{set_name} / fixed reversion", set_name, hull_white_spec(ts2025, 0.05, 0.01), LM_TEST, EC_TEST,
                    NO_CONSTRAINT, [], [True, False]))
        out.append((f"{set_name} / weights", set_name, hw2025, LM_TEST, EC_TEST, NO_CONSTRAINT,
                    [1.0, 0.5, 2.0, 1.0, 1.5, 0.25], None))
        out.append((f"{set_name} / short end criteria", set_name, hw2025, LM_TEST, EC_SHORT, None, None, None))
    # 38 so far: Vasicek, its four parameters.
    for set_name in ("2025", "2025 normal", "2002"):
        out.append((f"{set_name} / Vasicek", set_name, vasicek_spec(), LM_TEST, EC_TEST, None, None, None))
        out.append((f"{set_name} / Vasicek, b and lambda fixed", set_name, vasicek_spec(), LM_TEST, EC_TEST,
                    NO_CONSTRAINT, [], [False, True, False, True]))
    out.append(("2025 / Vasicek, Simplex", "2025", vasicek_spec(), SIMPLEX_WIDE, EC_SIMPLEX, None, None, None))
    out.append(("2025 / Vasicek, a fixed", "2025", vasicek_spec(0.02, 0.15, 0.04, 0.012, 0.0), LM_TEST, EC_TEST,
                NO_CONSTRAINT, [], [True, False, False, False]))
    out.append(("2025 / Vasicek, weights", "2025", vasicek_spec(), LM_TEST, EC_TEST, NO_CONSTRAINT,
                [1.0, 1.0, 2.0, 2.0, 0.5, 0.5], None))
    out.append(("2025 / Vasicek, short", "2025", vasicek_spec(), LM_TEST, EC_SHORT, None, None, None))
    out.append(("2025 / Vasicek, boundary", "2025", vasicek_spec(), LM_TEST, EC_TEST,
                {"type": "BoundaryConstraint", "low": -0.5, "high": 0.5}, None, None))
    out.append(("2025 price / Vasicek, b, sigma and lambda fixed", "2025 price", vasicek_spec(), LM_DEFAULT, EC_FIXED,
                NO_CONSTRAINT, [], [False, True, True, True]))
    assert len(out) == 50, len(out)
    return out


def calibrate(model, helpers, method_spec, criteria_spec, constraint, weights, fix):
    args = [helpers, make_method(method_spec), make_criteria(criteria_spec)]
    if constraint is not None:
        args.append(make_constraint(constraint))
        if weights is not None or fix is not None:
            args.append(weights or [])
            if fix is not None:
                args.append(fix)
    model.calibrate(*args)


def calibration_case(name, set_name, model_spec, method, criteria, constraint, weights, fix):
    today, specs = HELPER_SETS[set_name]
    if model_spec["type"] == "HullWhite":
        model_spec = dict(model_spec, termStructure=specs[0]["termStructure"])
    with v.case_settings(today, use_at_par_coupons=True) as st:
        model = make_model(model_spec)
        helpers = priced_helpers(specs, model, model_spec)
        calibrate(model, helpers, method, criteria, constraint, weights, fix)
        params = list(model.params())
        expected = {"params": params, "endCriteria": int(model.endCriteria()),
                    "problemValues": list(model.problemValues()), "functionEvaluation": model.functionEvaluation(),
                    "calibrationErrors": [h.calibrationError() for h in helpers]}
        others = [[x * 1.1 for x in params], [x * 0.9 for x in params]]
        expected["value"] = model.value(ql.Array(params), helpers)
        expected["values"] = [model.value(ql.Array(p), helpers) for p in others]
        inputs = {"settings": st, "helpers": specs, "model": model_spec, "method": method, "endCriteria": criteria,
                  "valueParams": others}
        if constraint is not None:
            inputs["constraint"] = constraint
        if weights is not None:
            inputs["weights"] = weights
        if fix is not None:
            inputs["fixParameters"] = fix
        return {"name": name, "inputs": inputs, "expected": expected}


def refusal_cases():
    out = []
    specs = HELPERS_2002
    model_spec = hull_white_spec(swh.CURVE_2002)

    def add(name, kind, extra=None, helpers_given=True, engine=True):
        with v.case_settings(swh.TODAY_2002, use_at_par_coupons=True) as st:
            model = make_model(model_spec)
            helpers = priced_helpers(specs, model, model_spec, engine) if helpers_given else []
            inputs = {"settings": st, "helpers": specs if helpers_given else [], "model": model_spec,
                      "method": LM_TEST, "endCriteria": EC_TEST, "engine": engine}
            inputs.update(extra or {})
            if kind == "calibrate":
                def run():
                    calibrate(model, helpers, LM_TEST, EC_TEST, inputs.get("constraint"), inputs.get("weights"),
                              inputs.get("fixParameters"))
                    return list(model.params())
                expected = {"calibrate": v.attempt(run)}
            else:
                expected = {"value": v.attempt(lambda: model.value(ql.Array(inputs["valueParams"]), helpers))}
            out.append({"name": name, "inputs": inputs, "expected": expected})

    add("refused / no helpers", "calibrate", helpers_given=False)
    add("refused / weights of the wrong size", "calibrate", {"constraint": NO_CONSTRAINT, "weights": [1.0, 2.0]})
    add("refused / fixed parameters of the wrong size", "calibrate",
        {"constraint": NO_CONSTRAINT, "weights": [], "fixParameters": [True, False, False]})
    add("refused / weights and fixed parameters of the wrong size", "calibrate",
        {"constraint": NO_CONSTRAINT, "weights": [1.0], "fixParameters": [True]})
    add("refused / every parameter fixed", "calibrate",
        {"constraint": NO_CONSTRAINT, "weights": [], "fixParameters": [True, True]})
    add("refused / a helper with no engine", "calibrate", engine=False)
    add("refused / value at no parameters", "value", {"valueParams": []})
    add("refused / value at too few parameters", "value", {"valueParams": [0.05]})
    add("refused / value at too many parameters", "value", {"valueParams": [0.05, 0.01, 0.0]})
    add("value with no helpers", "value", {"valueParams": [0.05, 0.01]}, helpers_given=False)
    add("value at the start", "value", {"valueParams": [0.1, 0.01]})
    return out


def cases():
    out = [calibration_case(*row) for row in calibrations()]
    out += refusal_cases()
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    # QuantLib's counts on its cached cases include the evaluation of the problem values.
    assert by_name["testCachedHullWhite"]["functionEvaluation"] == 24, by_name["testCachedHullWhite"]
    assert by_name["testCachedHullWhiteFixedReversion"]["functionEvaluation"] == 7
    assert by_name["testCachedHullWhiteFixedReversion"]["params"][0] == 0.05
    # NoConstraint added to the model's own changes nothing.
    assert by_name["2002 / NoConstraint"]["params"] == by_name["testCachedHullWhite"]["params"]
    assert by_name["value with no helpers"]["value"] == 0.0
    for name, e in by_name.items():
        if name.startswith("refused / "):
            assert all(x == v.THROWS for x in e.values()), name


if __name__ == "__main__":
    v.main(__file__, "ql/models/model.hpp", cases)
