# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for DiscretizedSwaption (ql/pricingengines/swaption/discretizedswaption.hpp, .cpp) and the discretized swap
it builds (ql/pricingengines/swap/discretizedswap.hpp, .cpp), which no wheel class exposes, each output computed by the
C++ harness (docs/porting-playbook.md §9.4): 30 swaptions and their swaps on the evaluation date 15 January 2025, and
2 refusals.

The swaptions are those of instruments/swaption.py's kind, on Euribor 6M and ESTR swaps: European and Bermudan
exercises, the Bermudan dates at the fixed coupons' starts, and moved by 3 and 7 days either way (which snap the swap's
schedule dates within a week of them: a date moved forward makes its coupon adjusted after the exercise), and by 8 days
(beyond the week); payers and receivers; and swaps that started before the evaluation date, their first floating coupons' fixings stored with the
case's settings: one whose first floating coupon pays on the evaluation date, with includeTodaysCashFlows unset and
true (which makes a coupon reset in the past and paid on the reference date a known amount, added in the
post-adjustment), and one whose first floating coupon pays later, a known amount either way, and without its fixing
("current floating coupon not given" when the rollback reaches its payment). The times are on Actual/365
(Fixed) from the evaluation date, or on Actual/360 from ten days later, which puts the swaps' first dates before it.

Each swaption's outputs are the discretized swaption's mandatory times ("swaptionTimes") and its swap's
("swapTimes"), in QuantLib's order; the swaption's values and present value when initialized at its last exercise time
on a Hull-White tree (on the flat 2.7% curve, a = 0.05, sigma = 0.01) on TimeGrid(its times, 30) and rolled back to its
first exercise time at or after the reference date ("swaptionValues", "swaptionPresentValue") and to 0
("swaptionValuesAtZero", "swaptionPresentValueAtZero"); the same at 0 on a Vasicek tree ("vasicekPresentValueAtZero");
and the swap's values and present value when initialized at its last time on the Hull-White tree on TimeGrid(its
times, 30) and rolled back to 0 ("swapValuesAtZero", "swapPresentValueAtZero"). Amounts are compared at the nominal.
The swap's two are generated and left out where its first floating coupon pays on the evaluation date: its second
starts that day and fixed two business days before, and nanoql's swap, by its public constructor, knows a floating
coupon by its fixing date (the S-sr plan's SSR6 (xvi); tests/NanoQL.Tests/Divergences/TreeKnownCouponTests.cs), so it
reads that coupon's amount, whose fixing the case doesn't store, where QuantLib's forecasts it on the tree. The
swaption's swap keeps QuantLib's rule.

Each case's inputs hold the swaption (the harness's spec, tools/cxx/src/instruments/swaptionspec.hpp), the reference
date and day counter, the models, the grids' times (the harness's mandatory times) and steps, and the rollbacks'
times. tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/DiscretizedSwaptionCxxVectorTests.cs reads the file back."""
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
HULL_WHITE = {"type": "HullWhite", "curve": swp.DISCOUNT, "a": 0.05, "sigma": 0.01}
VASICEK = {"type": "Vasicek", "r0": 0.03, "a": 0.08, "b": 0.035, "sigma": 0.009, "lambda": 0.0}
STEPS = 30
LATER = TODAY + 10
FRAMES = {"A365": (TODAY, swp.A365), "A360 later": (LATER, swp.A360)}
# Two swaps that started before the evaluation date: the first's first floating coupon pays on it, the second's
# later; each first floating coupon's fixing.
PAYS_TODAY = ql.Date(15, ql.July, 2024)
PAYS_LATER = ql.Date(15, ql.October, 2024)
FIXINGS = {PAYS_TODAY: 0.0365, PAYS_LATER: 0.0335}


def fixed_starts(swap):
    return [ql.as_coupon(cf).accrualStartDate() for cf in fvf.make_swap(swap).fixedLeg()]


def started_swap(swap_type, started):
    """A 5Y swap that started before the evaluation date."""
    return fvf.vanilla(swap_type, 1e6, fvf.sched(started, started + ql.Period(5, Y), ql.Period(1, Y), swp.TARGET,
                                                 ql.ModifiedFollowing),
                       0.03, swp.THIRTY360_BB,
                       fvf.sched(started, started + ql.Period(5, Y), ql.Period(6, M), swp.TARGET, ql.ModifiedFollowing),
                       swp.E6M, 0.0, swp.A360)


def started_exercise(started):
    """A Bermudan exercise on 17 February 2025 and at the swap's later fixed coupon starts."""
    return swp.bermudan([ql.Date(17, ql.February, 2025)]
                        + [TARGET_CAL.adjust(started + ql.Period(i, Y), ql.ModifiedFollowing) for i in range(1, 5)])


def fixing_of(started):
    """The started swap's first floating coupon's fixing, as the harness stores it by the index's name."""
    return {"Euribor6M Actual/360": [[v.enc(TARGET_CAL.advance(started, -2, D)), FIXINGS[started]]]}


def swaptions():
    """(name, swaption spec, frame, settings) for the 30 swaptions."""
    out = []
    exercise, start, maturity = swp.dates_of(ql.Period(1, Y), ql.Period(4, Y))
    for swap_type in (swp.PAYER, swp.RECEIVER):
        swap = swp.vanilla(swap_type, 1e6, 0.03, start, maturity)
        starts = fixed_starts(swap)
        for frame in FRAMES:
            out.append((f"European / {swap_type} / {frame}", swp.swaption_spec(swap, swp.european(exercise)), frame, {}))
            out.append((f"Bermudan / {swap_type} / {frame}", swp.swaption_spec(swap, swp.bermudan(starts)), frame, {}))
        for shift in (-3, 3, -7, 7, -8, 8):
            moved = [TARGET_CAL.adjust(d + shift) for d in starts]
            out.append((f"Bermudan moved {shift} days / {swap_type}", swp.swaption_spec(swap, swp.bermudan(moved)),
                        "A365", {}))
    # 20 so far: an exercise close to a floating date only, and the overnight-indexed swaps.
    swap = swp.vanilla(swp.PAYER, 1e6, 0.03, start, maturity)
    mid = [TARGET_CAL.advance(d, 6, M) for d in fixed_starts(swap)[:-1]]
    out.append(("Bermudan near the floating dates only", swp.swaption_spec(
        swap, swp.bermudan([TARGET_CAL.adjust(d - 2) for d in mid])), "A365", {}))
    ois = swp.overnight(swp.RECEIVER, 1e6, 0.03, start, start + ql.Period(3, Y))
    out.append(("overnight-indexed European", swp.swaption_spec(ois, swp.european(exercise)), "A365", {}))
    out.append(("overnight-indexed Bermudan moved 3 days", swp.swaption_spec(
        ois, swp.bermudan([TARGET_CAL.adjust(d - 3) for d in fixed_starts(ois)])), "A365", {}))
    out.append(("overnight-indexed European / A360 later", swp.swaption_spec(ois, swp.european(exercise)),
                "A360 later", {}))
    # 24 so far: the started swaps.
    for swap_type in (swp.PAYER, swp.RECEIVER):
        today = swp.swaption_spec(started_swap(swap_type, PAYS_TODAY), started_exercise(PAYS_TODAY))
        out.append((f"started swap paying today / {swap_type}", today, "A365", {"fixings": fixing_of(PAYS_TODAY)}))
        out.append((f"started swap paying today, today's cash flows / {swap_type}", today, "A365",
                    {"fixings": fixing_of(PAYS_TODAY), "include_todays_cash_flows": True}))
        later = swp.swaption_spec(started_swap(swap_type, PAYS_LATER), started_exercise(PAYS_LATER))
        out.append((f"started swap paying later / {swap_type}", later, "A365", {"fixings": fixing_of(PAYS_LATER)}))
    assert len(out) == 30, len(out)
    return out


def case(H, name, spec, frame, settings):
    reference, dc = FRAMES[frame]
    scale = fvf.scale_of(spec["swap"])
    with H.case_settings(v.enc(TODAY), **settings) as st:
        args = [spec, v.enc(reference), dc]
        swaption_times = H.call("DiscretizedSwaption::mandatoryTimes", *args)
        swap_times = H.call("DiscretizedSwap::mandatoryTimes", *args)
        day_counter = irx.day_counter(dc)
        exercise_dates = ([ql.Date(spec["exercise"]["date"])] if spec["exercise"]["type"] == "EuropeanExercise"
                          else [ql.Date(d) for d in spec["exercise"]["dates"]])
        exercise_times = [day_counter.yearFraction(reference, d) for d in exercise_dates]
        first = next(t for t in exercise_times if t >= 0.0)
        last = exercise_times[-1]
        swap_from = max(swap_times)

        def rollback(method, model, times, start, to):
            r = H.call(method, *args, model, times, STEPS, start, to)
            return [v.amount(x, scale) for x in r["values"]], v.amount(r["presentValue"], scale)

        expected = {"swaptionTimes": swaption_times, "swapTimes": swap_times}
        values, pv = rollback("DiscretizedSwaption::rollback", HULL_WHITE, swaption_times, last, first)
        expected["swaptionValues"], expected["swaptionPresentValue"] = values, pv
        values, pv = rollback("DiscretizedSwaption::rollback", HULL_WHITE, swaption_times, last, 0.0)
        expected["swaptionValuesAtZero"], expected["swaptionPresentValueAtZero"] = values, pv
        expected["vasicekPresentValueAtZero"] = rollback(
            "DiscretizedSwaption::rollback", VASICEK, swaption_times, last, 0.0)[1]
        values, pv = rollback("DiscretizedSwap::rollback", HULL_WHITE, swap_times, swap_from, 0.0)
        SWAP_PV[name] = pv
        if not name.startswith("started swap paying today"):
            expected["swapValuesAtZero"], expected["swapPresentValueAtZero"] = values, pv
        return {"name": name,
                "inputs": {"settings": st, "swaption": spec, "referenceDate": v.enc(reference), "dayCounter": dc,
                           "hullWhite": HULL_WHITE, "vasicek": VASICEK, "steps": STEPS,
                           "swaptionFrom": last, "swaptionTo": first, "swapFrom": swap_from},
                "expected": expected}


def refusal(H, name, spec, method, settings):
    with H.case_settings(v.enc(TODAY), **settings) as st:
        args = [spec, v.enc(TODAY), swp.A365]
        times = H.call(method.split("::")[0] + "::mandatoryTimes", *args)
        result = v.attempt(lambda: H.call(method, *args, HULL_WHITE, times, STEPS, max(times), 0.0)["presentValue"])
        return {"name": "refused / " + name,
                "inputs": {"settings": st, "swaption": spec, "referenceDate": v.enc(TODAY), "dayCounter": swp.A365,
                           "method": method, "hullWhite": HULL_WHITE, "steps": STEPS, "gridTimes": times},
                "expected": {"presentValue": result}}


def cases():
    with cxx.connect() as H:
        out = [case(H, *row) for row in swaptions()]
        started = swp.swaption_spec(started_swap(swp.PAYER, PAYS_LATER), started_exercise(PAYS_LATER))
        out.append(refusal(H, "started swap without its fixing", started, "DiscretizedSwap::rollback", {}))
        out.append(refusal(H, "started swaption without its fixing", started, "DiscretizedSwaption::rollback", {}))
        check_rows(out)
        return out


# Each case's QuantLib swap value at 0, written or not, for check_rows.
SWAP_PV = {}


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    for c in out[-2:]:
        assert c["expected"]["presentValue"] == v.THROWS, c["name"]
    # Today's known coupon changes QuantLib's swap's value at 0, not its times (that value is left out of the file).
    a = by_name["started swap paying today / 1"]
    b = by_name["started swap paying today, today's cash flows / 1"]
    assert a["swapTimes"] == b["swapTimes"]
    assert SWAP_PV["started swap paying today / 1"] != SWAP_PV["started swap paying today, today's cash flows / 1"]
    # Snapping within the week changes the swaption's times, not those of the swap built from the swap itself.
    moved, plain = by_name["Bermudan moved 3 days / 1"], by_name["Bermudan / 1 / A365"]
    assert moved["swaptionTimes"] != plain["swaptionTimes"] and moved["swapTimes"] == plain["swapTimes"]


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/swaption/discretizedswaption.hpp", cases)
