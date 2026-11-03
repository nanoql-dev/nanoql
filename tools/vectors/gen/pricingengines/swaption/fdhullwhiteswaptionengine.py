# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdHullWhiteSwaptionEngine (ql/pricingengines/swaption/fdhullwhiteswaptionengine.hpp, .cpp),
on the evaluation date 15 January 2025.

European and Bermudan swaptions of tools/vectors/gen/instruments/swaption.py's kind: payers and receivers on Euribor 6M
in, at and out of the money, a floating spread, an overnight-indexed swap on ESTR, discounting on a flat 2.7% curve
and forwarding on a flat 3.1% one, and an expired swaption (worth 0); on two Hull-White models
(tools/vectors/gen/methods/finitedifferences/operators/fdmg2op.py's), at QuantLib's default grid (100 time steps, 100
points) and at a coarse one (20 steps, 25 points, two damping steps, an epsilon of 1e-4), under the Douglas default and
the Hundsdorfer, Craig-Sneyd, modified Craig-Sneyd, modified Hundsdorfer, Crank-Nicolson and implicit Euler schemes on
the coarse grid. Then the refusals: an index forecasting on an Actual/360 curve ("day counter of forward and discount
curve must match"), and a Bermudan swaption with an exercise date before the evaluation date ("exercise dates must not
contain past date").

Generated and left out: TR-BDF2 (tools/vectors/gen/methods/finitedifferences/solvers/fdmhullwhitesolver.py's reason).
Not generated: an American exercise, whose step conditions reach the inner value at times off its exercise dates, where
QuantLib reads its map's end (undefined behaviour) and nanoql refuses
(tests/NanoQL.Tests/Conventions/InnerValueAmericanRefusalTests.cs).

Each case's output is the NPV ("NPV", an amount at the nominal) on each engine spec {"model", "tGrid", "xGrid",
"dampingSteps", "invEps", "scheme" (tools/vectors/gen/methods/finitedifferences/solvers/fdmbackwardsolver.py's spec)}.
The wheel's FdHullWhiteSwaptionEngine is the proxy of the C++ constructor (QuantLib-SWIG 1.44, SWIG/swaption.i). Read
back by tests/NanoQL.Tests/Vectors/PricingEngines/FdHullWhiteSwaptionEngineVectorTests.cs."""
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


g2s = _load("fdmg2solver", "methods/finitedifferences/solvers/fdmg2solver.py")
g2op, swp, fbs = g2s.g2op, g2s.swp, g2s.fbs
fvf = swp.fvf
TODAY = swp.TODAY
M, Y = ql.Months, ql.Years


def engine_spec(model, t_grid, x_grid, damping, inv_eps, scheme, y_grid=None):
    spec = {"model": model, "tGrid": t_grid, "xGrid": x_grid, "dampingSteps": damping, "invEps": inv_eps,
            "scheme": g2s.scheme(scheme)}
    if y_grid is not None:
        spec["yGrid"] = y_grid
    return spec


def swaptions(left_out_index):
    """(name, spec, kind): kind "kept", "refused" (kept, its outputs throws) or "left out"."""
    exercise, start, maturity = swp.dates_of(ql.Period(1, Y), ql.Period(5, Y))
    atm = 0.031
    out = []
    for swap_type in (swp.PAYER, swp.RECEIVER):
        for strike in (0.8 * atm, atm, 1.2 * atm):
            swap = swp.vanilla(swap_type, 1e6, strike, start, maturity)
            starts = [ql.as_coupon(c).accrualStartDate() for c in fvf.make_swap(swap).fixedLeg()]
            out.append((f"European {swap_type} at {strike:.5f}", swp.swaption_spec(swap, swp.european(exercise))))
            out.append((f"Bermudan {swap_type} at {strike:.5f}", swp.swaption_spec(swap, swp.bermudan(starts))))
    spread = swp.vanilla(swp.RECEIVER, 1e6, atm, start, maturity, spread=0.005)
    out.append(("European receiver, floating spread 0.5%", swp.swaption_spec(spread, swp.european(exercise))))
    ois = swp.overnight(swp.PAYER, 1e6, 0.03, start, start + ql.Period(3, Y))
    ois_starts = [ql.as_coupon(c).accrualStartDate() for c in fvf.make_swap(ois).fixedLeg()]
    out.append(("Bermudan overnight-indexed payer", swp.swaption_spec(ois, swp.bermudan(ois_starts))))
    past = swp.TARGET_CAL.advance(TODAY, -1, M)
    expired_swap = swp.vanilla(swp.PAYER, 1e6, 0.03, swp.TARGET_CAL.advance(past, 2, ql.Days),
                               swp.TARGET_CAL.advance(past, 2, ql.Days) + ql.Period(5, Y))
    out.append(("expired", swp.swaption_spec(expired_swap, swp.european(past))))
    e3m = swp.vanilla(swp.PAYER, 1e6, 0.04, start, maturity, index=swp.E3M)
    out.append(("refused: an index on an Actual/360 curve", swp.swaption_spec(e3m, swp.european(exercise))))
    starts = [ql.as_coupon(c).accrualStartDate() for c in fvf.make_swap(swp.vanilla(swp.PAYER, 1e6, atm, start,
                                                                                  maturity)).fixedLeg()]
    past_exercise = swp.bermudan([swp.TARGET_CAL.advance(TODAY, -5, ql.Days)] + starts)
    out.append(("refused: an exercise date before the evaluation date",
                swp.swaption_spec(swp.vanilla(swp.PAYER, 1e6, atm, start, maturity), past_exercise)))
    return out


def engine(spec, models_key):
    model = g2op.make_model(spec["model"])
    scheme = fbs.scheme(spec["scheme"])
    if spec["model"]["type"] == "G2":
        return ql.FdG2SwaptionEngine(model, spec["tGrid"], spec["xGrid"], spec["yGrid"], spec["dampingSteps"],
                                     spec["invEps"], scheme)
    return ql.FdHullWhiteSwaptionEngine(model, spec["tGrid"], spec["xGrid"], spec["dampingSteps"], spec["invEps"],
                                        scheme)


def engines(models, default, coarse, schemes, left_out_schemes):
    kept = []
    for model in models:
        kept.append(default(model))
        kept.append(coarse(model, schemes[0]))
    for s in schemes[1:]:
        kept.append(coarse(models[0], s))
    left_out = [coarse(models[0], s) for s in left_out_schemes]
    return kept, left_out


def cases_for(models, default, coarse, schemes, left_out_schemes):
    kept, left_out = engines(models, default, coarse, schemes, left_out_schemes)
    out = []
    for name, spec in swaptions(None):
        with v.case_settings(TODAY) as st:
            scale = fvf.scale_of(spec["swap"])
            npvs = []
            for e in kept + left_out:
                swaption = swp.make_swaption(spec)
                swaption.setPricingEngine(engine(e, None))
                npvs.append(fvf.ibc.scaled(swaption.NPV, scale))
            out.append({"name": name, "inputs": {"settings": st, "swaption": spec, "engines": kept},
                        "expected": {"NPV": npvs[:len(kept)]}})
    return out


def cases():
    return cases_for(g2op.HULL_WHITES,
                     lambda m: engine_spec(m, 100, 100, 0, 1e-5, "Douglas"),
                     lambda m, s: engine_spec(m, 20, 25, 2, 1e-4, s),
                     ["Douglas", "Hundsdorfer", "CraigSneyd", "ModifiedCraigSneyd", "ModifiedHundsdorfer",
                      "CrankNicolson", "ImplicitEuler"],
                     ["TrBDF2"])


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/swaption/fdhullwhiteswaptionengine.hpp", cases)
