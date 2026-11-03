# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's TreeCapFloorEngine (ql/pricingengines/capfloor/treecapfloorengine.hpp, .cpp) and the
discretized cap it rolls back (ql/pricingengines/capfloor/discretizedcapfloor.hpp, .cpp): 14 instruments, built by
tools/vectors/gen/instruments/capfloor.py's functions, each priced on four short-rate models' trees, on the evaluation
date 15 January 2025.

The instruments start three months after the spot date, so that every caplet is fixed after the evaluation date, and
their legs have no fixing days, so that each caplet is fixed on its start, where QuantLib's tree and nanoql's value a
caplet alike: caps, floors and collars at, in and out of the money, a collar whose cap is below its floor, gearings and
spreads, amortizing notionals, a quarterly cap on Euribor 3M, cap rates extended over the leg; and an instrument paid
in full, whose NPV is 0. Each case's outputs are its NPV ("NPV", an amount at the leg's largest notional) on each engine: a
HullWhite (a = 0.05, sigma = 0.01, its fitted tree) on the case's discount curve with 30 and 100 time steps and on a
TimeGrid through the discretized cap's mandatory times with 40 steps; a second instrument of the same spec on that
grid engine after it priced the first (QuantLib's grid engine builds its lattice once and reuses it, which is nanoql's
lattice overload, its oracle); a BlackKarasinski (a = 0.1, sigma = 0.1) with 30 steps; a Vasicek (r0 = 0.03, a = 0.08, b = 0.035,
sigma = 0.009) with the discount curve given to the engine, with 50 steps; and a G2 (a = 0.07, sigma = 0.011, b = 0.3,
eta = 0.008, rho = -0.6) with 10 steps, on its two-dimensional tree.

Generated and left out: the instruments with two fixing days, each caplet fixed before its start, which QuantLib's tree
exercises at its start and nanoql's at its fixing (tests/NanoQL.Tests/Divergences/CapletFixingExpiryTests.cs); the
instruments whose first caplet is fixed on or before the evaluation date and starts after it (every instrument from the
spot date), and the seasoned ones, whose earlier starts QuantLib's grid refuses ("negative times not allowed"), since
nanoql's discretized cap knows a caplet by its fixing date and keeps only the non-negative times (widened;
tests/NanoQL.Tests/Divergences/TreeCapFloorSeasonedTests.cs, TreeKnownCouponTests.cs); and every price on the
Cox-Ingersoll-Ross trees, which are QuantLib's before 1.23. The wheel's TreeCapFloorEngine is the proxy of the C++
constructors, on a model and time steps or a TimeGrid, with an optional term structure (QuantLib-SWIG 1.44,
SWIG/capfloor.i). Each case's inputs hold the instrument, the discount curve, the engines ({"model", the model's
parameters, "steps" or "grid"}) and the grid's times. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/TreeCapFloorEngineVectorTests.cs."""
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


cfl = _load("wheel_capfloor", "instruments/capfloor.py")
irx = cfl.irx

TODAY = cfl.TODAY
M, Y = ql.Months, ql.Years
CAP, FLOOR, COLLAR = cfl.CAP, cfl.FLOOR, cfl.COLLAR
FORWARD = cfl.CAL.advance(cfl.SPOT, 3, M)
GRID_STEPS = 40

HULL_WHITE = {"model": "HullWhite", "a": 0.05, "sigma": 0.01}
ENGINES = [
    dict(HULL_WHITE, steps=30), dict(HULL_WHITE, steps=100), dict(HULL_WHITE, grid=True),
    dict(HULL_WHITE, grid=True, reused=True),
    {"model": "BlackKarasinski", "a": 0.1, "sigma": 0.1, "steps": 30},
    {"model": "Vasicek", "r0": 0.03, "a": 0.08, "b": 0.035, "sigma": 0.009, "lambda": 0.0, "steps": 50},
    {"model": "G2", "a": 0.07, "sigma": 0.011, "b": 0.3, "eta": 0.008, "rho": -0.6, "steps": 10},
]
LEFT_OUT_ENGINES = [{"model": "CoxIngersollRoss", "r0": 0.03, "theta": 0.035, "k": 0.3, "sigma": 0.05, "steps": 30}]


def forward_leg(length, index=cfl.E6M, tenor=ql.Period(6, M), notionals=(1e6,), fixing_days=0, **kw):
    """capfloor.cpp's makeLeg from three months after the spot date, its caplets fixed on their starts by default."""
    return dict(cfl.ibor_leg(FORWARD, ql.Period(length, Y), index, tenor, notionals, **kw), fixingDays=[fixing_days])


def instruments():
    """(name, spec, settings, kept) for the 14 instruments and the 5 generated and left out."""
    rows = {row[0]: row for row in cfl.instruments()}
    out = []
    spec = cfl.instrument_spec
    out.append(("1Y cap at 3%", spec(CAP, forward_leg(1), [0.03]), {}, True))
    out.append(("3Y cap at 3%", spec(CAP, forward_leg(3), [0.03]), {}, True))
    out.append(("3Y floor at 3%", spec(FLOOR, forward_leg(3), (), [0.03]), {}, True))
    out.append(("3Y collar 3.5% / 2.5%", spec(COLLAR, forward_leg(3), [0.035], [0.025]), {}, True))
    out.append(("5Y cap in the money", spec(CAP, forward_leg(5), [0.015]), {}, True))
    out.append(("5Y cap far out of the money", spec(CAP, forward_leg(5), [0.09]), {}, True))
    out.append(("5Y floor far out of the money", spec(FLOOR, forward_leg(5), (), [0.004]), {}, True))
    out.append(("7Y quarterly cap on 3M", spec(CAP, forward_leg(7, cfl.E3M, ql.Period(3, M), (2.5e6,)), [0.032]), {},
                True))
    out.append(("4Y cap, rates extended", spec(CAP, forward_leg(4), [0.025, 0.03]), {}, True))
    out.append(("3Y collar, cap below floor", spec(COLLAR, forward_leg(3), [0.02], [0.03]), {}, True))
    out.append(("3Y cap, gearing 2, spread 1%", spec(CAP, forward_leg(3, gearings=[2.0], spreads=[0.01]), [0.09]), {},
                True))
    out.append(("5Y cap, amortizing", spec(
        CAP, forward_leg(5, cfl.E1Y, ql.Period(1, Y), (5e6, 4e6, 3e6, 2e6, 1e6)), [0.035]), {}, True))
    out.append(("10Y cap at 3%", spec(CAP, forward_leg(10), [0.03]), {}, True))
    _, paid, _, paid_settings = rows["cap paid in full"]
    out.append(("cap paid in full", paid, paid_settings, True))
    out.append(("left out: 3Y cap at 3%, two fixing days", spec(CAP, forward_leg(3, fixing_days=2), [0.03]), {}, False))
    out.append(("left out: 3Y collar 3.5% / 2.5%, two fixing days",
                spec(COLLAR, forward_leg(3, fixing_days=2), [0.035], [0.025]), {}, False))
    for name in ("3Y cap at 3%", "cap fixing today, stored", "seasoned cap, past fixings"):
        _, s, _, settings = rows[name]
        out.append((f"left out: {name}, from the spot date or seasoned", s, settings, False))
    assert sum(1 for row in out if row[3]) == 14
    return out


def mandatory_times(c, curve):
    """The discretized cap's mandatory times on the curve: its non-negative start and payment times."""
    reference, dc = curve.referenceDate(), curve.dayCounter()
    times = []
    for cf in c.floatingLeg():
        coupon = ql.as_floating_rate_coupon(cf)
        times += [dc.yearFraction(reference, coupon.accrualStartDate()), dc.yearFraction(reference, coupon.date())]
    return [t for t in times if t >= 0.0]


def model_of(e, curve):
    if e["model"] == "HullWhite":
        return ql.HullWhite(curve, e["a"], e["sigma"])
    if e["model"] == "BlackKarasinski":
        return ql.BlackKarasinski(curve, e["a"], e["sigma"])
    if e["model"] == "Vasicek":
        return ql.Vasicek(e["r0"], e["a"], e["b"], e["sigma"], e["lambda"])
    if e["model"] == "CoxIngersollRoss":
        return ql.CoxIngersollRoss(e["r0"], e["theta"], e["k"], e["sigma"])
    return ql.G2(curve, e["a"], e["sigma"], e["b"], e["eta"], e["rho"])


def priced(spec, curve_spec, engines):
    curve = irx.handle(curve_spec)
    c = cfl.make_instrument(spec)
    scale = cfl.scale_of(spec)
    times = mandatory_times(c, curve)
    npvs = []
    grid_engine = None
    for e in engines:
        model = model_of(e, curve)
        given = curve if e["model"] in ("Vasicek", "CoxIngersollRoss") else ql.YieldTermStructureHandle()
        if e.get("grid"):
            # an instrument paid in full has no time on the grid, which QuantLib's TimeGrid refuses
            if not e.get("reused"):
                try:
                    grid_engine = ql.TreeCapFloorEngine(model, ql.TimeGrid(times, GRID_STEPS), given)
                except RuntimeError:
                    grid_engine = None
            if grid_engine is None:
                npvs.append(dict(v.THROWS))
                continue
            c = cfl.make_instrument(spec) if e.get("reused") else c
            c.setPricingEngine(grid_engine)
        else:
            c.setPricingEngine(ql.TreeCapFloorEngine(model, e["steps"], given))
        npvs.append(cfl.attempt_scaled(c.NPV, scale))
    return times, npvs


def cases():
    out = []
    for name, spec, settings, kept in instruments():
        with v.case_settings(TODAY, **cfl.settings_of(spec, settings)) as st:
            times, npvs = priced(spec, cfl.DISCOUNT, ENGINES)
            priced(spec, cfl.DISCOUNT, LEFT_OUT_ENGINES)
            if not kept:
                continue
            out.append({"name": name,
                        "inputs": {"settings": st, "capFloor": spec, "discountCurve": cfl.DISCOUNT, "engines": ENGINES,
                                   "gridTimes": times, "gridSteps": GRID_STEPS},
                        "expected": {"NPV": npvs}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/capfloor/treecapfloorengine.hpp", cases)
