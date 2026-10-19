# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's TreeSwaptionEngine (ql/pricingengines/swaption/treeswaptionengine.hpp): 64 swaptions on
the evaluation date 15 January 2025, each priced by six tree engines, and 8 refusals.

The swaptions are those of instruments/swaption.py's kind, on Euribor 6M swaps, with European, Bermudan and American
exercises: Bermudan exercises at each fixed coupon's start, the same moved 10 days earlier (QuantLib's
test-suite/bermudanswaption.cpp's second set, beyond the week the discretized swaption snaps within), 3 days earlier
and 3 days later (which snap the swap's dates to them: a coupon whose date moves forward is adjusted after the
exercise), and quarterly between the coupons; payers and receivers in, at and out of the money, with nominals of 1 to
10 million, on 1Y to 10Y options and swaps, overnight-indexed swaps (which the tree treats as vanilla swaps on their
overnight index), and a swap that started three months before the evaluation date, its past coupons' known amounts
added at their payments (its floating coupon's fixing stored with the case's settings).

Each swaption's outputs are its NPV on a HullWhite (a = 0.05, sigma = 0.01, on the discount curve, a flat 2.7%, so
its fitted tree) with 20, 50 and 100 time steps ("hullWhiteSteps"), on its TimeGrid engine ("hullWhiteGrid"), and
again on that engine after it priced another swaption ("hullWhiteLattice": QuantLib's grid engine builds its lattice
once and reuses it, which is nanoql's lattice overload, its oracle); and on a Vasicek (r0 = 0.03, a = 0.08, b = 0.035,
sigma = 0.009, its unfitted tree), which has no curve, with the discount curve given to the engine, with 50 steps and
on the grid ("vasicekSteps", "vasicekGrid"). The grid holds every time the discretized swaption must stop at: its
inputs' "gridTimes" are the year fractions, on the curve's Actual/365 (Fixed) from its reference date, of the
non-negative coupon reset and payment dates and exercise dates, with "gridSteps" 40 (QuantLib's TimeGrid adds the
steps between them); a time the snapping removes stays on the grid, which changes nothing but the grid both sides
build. The refusals: a cash swaption settled at the par yield curve, time steps of 0, a Vasicek with no curve, a grid
that ends before the swap's last payment, and the started swap without its fixing ("current floating coupon not
given"). An expired swaption is worth 0. Amounts are compared at the nominal.

Each case's inputs hold the swaption (instruments/swaption.py's spec), the discount curve and the grid. The wheel's
TreeSwaptionEngine is the proxy of the C++ constructors, on a model and time steps or a TimeGrid. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/TreeSwaptionEngineVectorTests.cs."""
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


swp = _load("wheel_swaption", "instruments/swaption.py")
fvf, irx = swp.fvf, swp.irx

TODAY = swp.TODAY
TARGET_CAL = swp.TARGET_CAL
D, M, Y = ql.Days, ql.Months, ql.Years
DISCOUNT = swp.DISCOUNT
HULL_WHITE = {"a": 0.05, "sigma": 0.01}
VASICEK = {"r0": 0.03, "a": 0.08, "b": 0.035, "sigma": 0.009, "lambda": 0.0}
STEPS = [20, 50, 100]
GRID_STEPS = 40
# The started swap's floating fixing, on its first coupon's fixing date.
STARTED = ql.Date(15, ql.October, 2024)
STARTED_FIXING = 0.0335


def hull_white():
    return ql.HullWhite(irx.handle(DISCOUNT), HULL_WHITE["a"], HULL_WHITE["sigma"])


def vasicek():
    return ql.Vasicek(VASICEK["r0"], VASICEK["a"], VASICEK["b"], VASICEK["sigma"], VASICEK["lambda"])


def american(first, last):
    return {"type": "AmericanExercise", "earliestDate": v.enc(first), "latestDate": v.enc(last),
            "payoffAtExpiry": False}


def make_swaption(spec):
    e = spec["exercise"]
    if e["type"] == "AmericanExercise":
        exercise = ql.AmericanExercise(ql.Date(e["earliestDate"]), ql.Date(e["latestDate"]), e["payoffAtExpiry"])
        return ql.Swaption(fvf.make_swap(spec["swap"]), exercise, spec["settlementType"], spec["settlementMethod"])
    return swp.make_swaption(spec)


def grid_times(swaption):
    """The year fractions, on the curve's day counter from its reference date, of the non-negative coupon reset and
    payment dates and exercise dates."""
    dc = ql.Actual365Fixed()
    reference = ql.Date(DISCOUNT["referenceDate"])
    swap = swaption.underlying()
    dates = []
    for leg in (swap.fixedLeg(), swap.floatingLeg()):
        for cf in leg:
            coupon = ql.as_coupon(cf)
            dates += [coupon.accrualStartDate(), coupon.date()]
    dates += list(swaption.exercise().dates())
    times = [dc.yearFraction(reference, d) for d in dates]
    return [t for t in times if t >= 0.0]


def fixed_starts(spec):
    """The fixed coupons' start dates of a swaption's swap."""
    swap = fvf.make_swap(spec["swap"])
    return [ql.as_coupon(cf).accrualStartDate() for cf in swap.fixedLeg()]


def swaptions():
    """(name, swaption spec, settings) for the 64 swaptions."""
    out = []
    k = 0
    for exercise_tenor, length in (((1, Y), (5, Y)), ((2, Y), (3, Y)), ((6, M), (2, Y)), ((5, Y), (5, Y))):
        exercise, start, maturity = swp.dates_of(ql.Period(*exercise_tenor), ql.Period(*length))
        atm = 0.031
        for swap_type in (swp.PAYER, swp.RECEIVER):
            for strike in (0.8 * atm, atm, 1.2 * atm):
                nominal = [1.0, 1e6, 1e7, 1000.0][k % 4]
                swap = swp.vanilla(swap_type, nominal, strike, start, maturity)
                tag = f"{exercise_tenor[0]}{'MY'[exercise_tenor[1] == Y]} on {length[0]}Y / {swap_type} / {strike:.5f}"
                out.append((f"European / {tag}", swp.swaption_spec(swap, swp.european(exercise)), {}))
                starts = fixed_starts(swp.swaption_spec(swap, swp.european(exercise)))
                out.append((f"Bermudan / {tag}", swp.swaption_spec(swap, swp.bermudan(starts)), {}))
                k += 1
    # 48 so far: the exercise dates moved, which snap the swap's dates when within a week.
    exercise, start, maturity = swp.dates_of(ql.Period(1, Y), ql.Period(5, Y))
    swap = swp.vanilla(swp.PAYER, 1000.0, 0.05, start, maturity)
    starts = fixed_starts(swp.swaption_spec(swap, swp.european(exercise)))
    for shift in (-10, -3, 3, -7, 7):
        moved = [TARGET_CAL.adjust(d + shift) for d in starts]
        out.append((f"Bermudan moved {shift} days", swp.swaption_spec(swap, swp.bermudan(moved)), {}))
    quarterly = [TARGET_CAL.advance(starts[0], 3 * i, M) for i in range(17)]
    out.append(("Bermudan quarterly", swp.swaption_spec(swap, swp.bermudan(quarterly)), {}))
    # 54 so far: American exercises.
    out.append(("American over the first year", swp.swaption_spec(
        swap, american(starts[0], TARGET_CAL.advance(starts[0], 1, Y))), {}))
    out.append(("American over the swap", swp.swaption_spec(
        swp.vanilla(swp.RECEIVER, 1e6, 0.025, start, maturity), american(starts[0], starts[-1])), {}))
    # 56 so far: overnight-indexed swaps, and long and short swaptions.
    for swap_type in (swp.PAYER, swp.RECEIVER):
        ois = swp.overnight(swap_type, 1e6, 0.03, start, start + ql.Period(3, Y))
        out.append((f"overnight-indexed European / {swap_type}", swp.swaption_spec(ois, swp.european(exercise)), {}))
    e10, s10, m10 = swp.dates_of(ql.Period(10, Y), ql.Period(10, Y))
    out.append(("10Y on 10Y Bermudan", swp.swaption_spec(
        swp.vanilla(swp.PAYER, 1e6, 0.03, s10, m10),
        swp.bermudan(fixed_starts(swp.swaption_spec(swp.vanilla(swp.PAYER, 1e6, 0.03, s10, m10),
                                                    swp.european(e10))))), {}))
    e1m, s1m, m1m = swp.dates_of(ql.Period(1, M), ql.Period(1, Y))
    out.append(("1M on 1Y", swp.swaption_spec(swp.vanilla(swp.RECEIVER, 1e6, 0.031, s1m, m1m), swp.european(e1m)),
                {}))
    # 60 so far: the started swap, and the expired swaption.
    started = fvf.vanilla(swp.PAYER, 1e6, fvf.sched(STARTED, STARTED + ql.Period(5, Y), ql.Period(1, Y), swp.TARGET,
                                                    ql.ModifiedFollowing),
                          0.03, swp.THIRTY360_BB,
                          fvf.sched(STARTED, STARTED + ql.Period(5, Y), ql.Period(6, M), swp.TARGET,
                                    ql.ModifiedFollowing), swp.E6M, 0.0, swp.A360)
    later = [ql.Date(17, ql.February, 2025)] + [TARGET_CAL.adjust(STARTED + ql.Period(i, Y), ql.ModifiedFollowing)
                                                for i in range(1, 5)]
    fixing = [(fvf.make_index(swp.E6M), [(TARGET_CAL.advance(STARTED, -2, D), STARTED_FIXING)])]
    out.append(("started swap, Bermudan", swp.swaption_spec(started, swp.bermudan(later)), {"fixings": fixing}))
    out.append(("started swap, European", swp.swaption_spec(started, swp.european(later[0])), {"fixings": fixing}))
    out.append(("started swap, receiver, Bermudan", swp.swaption_spec(
        dict(started, type=int(swp.RECEIVER)), swp.bermudan(later)), {"fixings": fixing}))
    past = TARGET_CAL.advance(TODAY, -1, M)
    out.append(("expired", swp.swaption_spec(
        swp.vanilla(swp.PAYER, 1e6, 0.03, TARGET_CAL.advance(past, 2, D), TARGET_CAL.advance(past, 2, D)
                    + ql.Period(5, Y)), swp.european(past)), {}))
    assert len(out) == 64, len(out)
    return out


def priced(spec, settings):
    with v.case_settings(TODAY, **settings) as st:
        scale = fvf.scale_of(spec["swap"])
        swaption = make_swaption(spec)
        times = grid_times(swaption)
        expected = {"hullWhiteSteps": []}
        for steps in STEPS:
            swaption.setPricingEngine(ql.TreeSwaptionEngine(hull_white(), steps))
            expected["hullWhiteSteps"].append(fvf.ibc.scaled(swaption.NPV, scale))
        grid_engine = ql.TreeSwaptionEngine(hull_white(), ql.TimeGrid(times, GRID_STEPS))
        swaption.setPricingEngine(grid_engine)
        expected["hullWhiteGrid"] = fvf.ibc.scaled(swaption.NPV, scale)
        again = make_swaption(spec)
        again.setPricingEngine(grid_engine)
        expected["hullWhiteLattice"] = fvf.ibc.scaled(again.NPV, scale)
        swaption.setPricingEngine(ql.TreeSwaptionEngine(vasicek(), 50, irx.handle(DISCOUNT)))
        expected["vasicekSteps"] = fvf.ibc.scaled(swaption.NPV, scale)
        swaption.setPricingEngine(ql.TreeSwaptionEngine(vasicek(), ql.TimeGrid(times, GRID_STEPS),
                                                        irx.handle(DISCOUNT)))
        expected["vasicekGrid"] = fvf.ibc.scaled(swaption.NPV, scale)
        return st, times, expected


def swaption_case(name, spec, settings):
    st, times, expected = priced(spec, settings)
    return {"name": name,
            "inputs": {"settings": st, "swaption": spec, "discountCurve": DISCOUNT, "hullWhite": HULL_WHITE,
                       "vasicek": VASICEK, "steps": STEPS, "gridTimes": times, "gridSteps": GRID_STEPS},
            "expected": expected}


def refusal_cases():
    exercise, start, maturity = swp.dates_of(ql.Period(1, Y), ql.Period(5, Y))
    plain = swp.swaption_spec(swp.vanilla(swp.PAYER, 1e6, 0.03, start, maturity), swp.european(exercise))
    par = swp.swaption_spec(swp.vanilla(swp.PAYER, 1e6, 0.03, start, maturity), swp.european(exercise), swp.CASH,
                            swp.PAR_YIELD)
    started = fvf.vanilla(swp.PAYER, 1e6, fvf.sched(STARTED, STARTED + ql.Period(5, Y), ql.Period(1, Y), swp.TARGET,
                                                    ql.ModifiedFollowing),
                          0.03, swp.THIRTY360_BB,
                          fvf.sched(STARTED, STARTED + ql.Period(5, Y), ql.Period(6, M), swp.TARGET,
                                    ql.ModifiedFollowing), swp.E6M, 0.0, swp.A360)
    no_fixing = swp.swaption_spec(started, swp.bermudan([ql.Date(17, ql.February, 2025)]))
    rows = [("par yield curve, steps", par, "HullWhite", 50, None),
            ("par yield curve, grid", par, "HullWhite", "grid", None),
            ("time steps of 0", plain, "HullWhite", 0, None),
            ("Vasicek with no curve, steps", plain, "Vasicek", 50, None),
            ("Vasicek with no curve, grid", plain, "Vasicek", "grid", None),
            ("grid ending before the last payment", plain, "HullWhite", "short grid", None),
            ("started swap without its fixing", no_fixing, "HullWhite", 50, None),
            ("started swap without its fixing, grid", no_fixing, "HullWhite", "grid", None)]
    out = []
    for name, spec, kind, engine, curve in rows:
        with v.case_settings(TODAY) as st:
            swaption = make_swaption(spec)
            times = grid_times(swaption)
            if engine == "short grid":
                times = [t for t in times if t < 3.0]
            model = hull_white() if kind == "HullWhite" else vasicek()

            def npv():
                e = (ql.TreeSwaptionEngine(model, engine) if isinstance(engine, int)
                     else ql.TreeSwaptionEngine(model, ql.TimeGrid(times, GRID_STEPS)))
                swaption.setPricingEngine(e)
                return swaption.NPV()
            out.append({"name": "refused / " + name,
                        "inputs": {"settings": st, "swaption": spec, "discountCurve": DISCOUNT, "model": kind,
                                   "hullWhite": HULL_WHITE, "vasicek": VASICEK,
                                   "engine": engine if isinstance(engine, int) else "grid",
                                   "gridTimes": times, "gridSteps": GRID_STEPS},
                        "expected": {"NPV": v.attempt(npv)}})
    return out


def cases():
    out = [swaption_case(*row) for row in swaptions()]
    out += refusal_cases()
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    for c in out[-8:]:
        assert c["expected"]["NPV"] == v.THROWS, c["name"]
    for c in out[:-8]:
        # QuantLib's grid engine gives the same value when it prices again on its lattice.
        assert c["expected"]["hullWhiteLattice"] == c["expected"]["hullWhiteGrid"], c["name"]
    assert by_name["expired"]["hullWhiteGrid"]["value"] == 0.0


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/swaption/treeswaptionengine.hpp", cases)
