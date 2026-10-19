# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's TreeCallableFixedRateBondEngine (ql/experimental/callablebonds/treecallablebondengine.hpp,
.cpp): callable bonds priced on a short-rate model's trinomial tree, built from a number of time steps on the bond's
mandatory times, or on a time grid given to the engine, before any option-adjusted spread.

Eight bonds of tools/vectors/gen/experimental/callablebonds/callablebond.py's fixtures (testCached's calls, puts and
both, the par calls on a discount curve, testInterplay's zero-coupon bond, the wheel survey's bond, whose curve starts
on the evaluation date, the ex-coupon bond with a call inside its window, and the bond on a schedule of dates), each
on fourteen engines:
- Hull-White on the bond's curve with 10, 40 and 100 time steps, and with 60 under a mean reversion of 0.03 and of
  0.5; with a term structure given that it ignores (a Hull-White model discounts on its own curve);
- the time-grid engine on the bond's mandatory times with 50 and 150 steps, and a third with 80 that nanoql prices
  through its lattice overload (the zero-coupon engine's, for the zero-coupon bond), from the model's tree on the same
  grid (QuantLib's grid engine builds its tree once, at construction);
- Vasicek with the curve given to the engine, and without one, which QuantLib refuses ("empty Handle cannot be
  dereferenced");
- QuantLib's refusals of 0 time steps (at the engine's construction), of a regular grid that misses the bond's
  mandatory times ("using inadequate time grid"), and of an empty grid (which QuantLib refuses with "vector too long",
  nanoql with "null time steps for trinomial tree": either throws).
A case's outputs are the NPV, the settlement value, the error estimate and valuation date (null where QuantLib throws
"not provided"), and the clean and dirty prices read from them; a refused engine makes each a throw. A fresh model and
engine per case (the wheel survey's caveats 1 and 4), the grid recomputed from the bond's mandatory times (caveat 6).
Read back by tests/NanoQL.Tests/Vectors/Experimental/CallableBonds/TreeCallableBondEngineVectorTests.cs, with
tests/NanoQL.Tests/Vectors/CallableBondInputs.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_callablebond", os.path.join(v.GEN_ROOT, "experimental", "callablebonds", "callablebond.py"))
cb = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cb)


def bonds():
    """(name, settings, bond spec, its curve spec, face amount) for the eight bonds."""
    cached = {name: entry for name, *entry in cb.cached_cases()}
    runner = {name: entry for name, *entry in cb.runner_cases()}
    survey = {name: entry for name, *entry in cb.survey_cases()}
    ex = {name: entry for name, *entry in cb.ex_coupon_cases()}
    other = {name: entry for name, *entry in cb.notionals_cases()}

    def pick(table, name):
        st, spec, engine, scale = table[name][:4]
        curve = engine["model"]["curve"]
        return name.split(" / ")[1], st, spec, curve, scale

    return [pick(cached, "cached / calls at 110, 240 steps"),
            pick(cached, "cached / puts at 100, 240 steps"),
            pick(cached, "cached / calls and puts, 240 steps"),
            pick(cached, "cached / par calls every year, discount curve"),
            pick(runner, "interplay / an out-of-the-money call ahead of an in-the-money put"),
            pick(survey, "survey / 14 par calls, 200 steps, compounded semiannually"),
            pick(ex, "ex-coupon / a call inside the ex-coupon window"),
            pick(other, "arbitrary schedule / a schedule of dates, with no tenor")]


def engines(spec, curve, today):
    """(label, engine spec) for the fourteen engines on a bond and its curve."""
    with v.case_settings(today):
        times = cb.mandatory_times(spec, curve, today)
    hw = cb.hull_white(curve)
    # the zero-coupon engine's lattice overload for the zero-coupon bond, QuantLib's grid engine in the wheel
    lattice = "TreeZeroLattice" if spec["type"] == "CallableZeroCouponBond" else "TreeLattice"
    other = cb.flat(curve["referenceDate"] if curve["type"] == "FlatForward" else curve["dates"][0], 0.06)
    return [
        ("Hull-White, 10 steps", cb.tree_steps(hw, 10, curve)),
        ("Hull-White, 40 steps", cb.tree_steps(hw, 40, curve)),
        ("Hull-White, 100 steps, no term structure", cb.tree_steps(hw, 100)),
        ("Hull-White a = 0.03, 60 steps", cb.tree_steps(cb.hull_white(curve, 0.03, 0.012), 60, curve)),
        ("Hull-White a = 0.5, 60 steps", cb.tree_steps(cb.hull_white(curve, 0.5, 0.008), 60, curve)),
        ("Hull-White, a term structure it ignores", cb.tree_steps(hw, 40, other)),
        ("time grid, 50 steps", cb.tree_grid(hw, times, 50, curve)),
        ("time grid, 150 steps", cb.tree_grid(hw, times, 150, None)),
        ("lattice, 80 steps", cb.tree_grid(hw, times, 80, curve, lattice)),
        ("Vasicek with the curve", cb.tree_steps(cb.vasicek(0.03, 0.1, 0.05, 0.01), 60, curve)),
        ("Vasicek without a curve", cb.tree_steps(cb.vasicek(0.03, 0.1, 0.05, 0.01), 60, None)),
        ("0 time steps", cb.tree_steps(hw, 0, curve)),
        ("a regular grid that misses the mandatory times",
         {"type": "TreeGrid", "model": hw, "timeGrid": {"end": max(times) + 0.5, "steps": 7}, "termStructure": curve}),
        ("an empty grid", {"type": "TreeGrid", "model": hw, "timeGrid": {}, "termStructure": curve}),
    ]


def make_engine(spec):
    """callablebond.py's engines, and the grids its specs don't take: a regular one and an empty one."""
    g = spec.get("timeGrid")
    if spec["type"] == "TreeGrid" and "times" not in g:
        model = cb.make_model(spec["model"])
        ts = cb.handle(spec["termStructure"])
        grid = ql.TimeGrid(g["end"], g["steps"]) if g else ql.TimeGrid()
        return ql.TreeCallableFixedRateBondEngine(model, grid, ts), (model, ts)
    return cb.make_engine(spec)


def cases():
    out = []
    for bond_name, st, spec, curve, scale in bonds():
        for label, engine in engines(spec, curve, st[0]):
            with v.case_settings(st[0], include_reference_date_events=st[1], include_todays_cash_flows=st[2]) as s:
                bond = cb.make_bond(spec)
                cb.check_live_types(bond)
                try:
                    e, keep = make_engine(engine)
                    bond.setPricingEngine(e)
                    expected = cb.engine_outputs(bond, scale)
                    del keep
                except RuntimeError:
                    expected = {k: dict(v.THROWS) for k in ("NPV", "settlementValue", "errorEstimate",
                                                            "valuationDate", "cleanPrice", "dirtyPrice")}
                out.append({"name": f"{bond_name} / {label}", "inputs": {"settings": s, "bond": spec,
                                                                         "engine": engine},
                            "expected": expected})
    check_rows(out)
    return out


def check_rows(out):
    """The rows the file must hold: each refusal refused, and the lattice reproducing the grid engine's tree."""
    for c in out:
        refused = c["expected"]["NPV"] == v.THROWS
        should = any(c["name"].endswith(s) for s in ("Vasicek without a curve", "0 time steps", "an empty grid",
                                                     "a regular grid that misses the mandatory times"))
        assert refused == should, c["name"]


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/callablebonds/treecallablebondengine.hpp", cases)
