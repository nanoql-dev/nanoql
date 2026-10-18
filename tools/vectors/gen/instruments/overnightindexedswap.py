# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's OvernightIndexedSwap (ql/instruments/overnightindexedswap.hpp), on the evaluation date 15
January 2025: QuantLib's four constructors (one nominal or a list of nominals, with one schedule for both legs or one
for each) × 20 variants of the conventions the swap passes to its overnight leg: the payment lag, adjustment and
calendar, compounded and averaged rates, lookbacks with and without the observation shift, lockouts, telescopic value
dates (refused with a lookback and no shift), the rounding of the compounded rate (17 places refused), spreads,
receivers and a seasoned swap reading stored fixings; on overnight indexes with SOFR's, ESTR's and SONIA's
conventions, each on a flat curve. Also a swap whose overnight schedule has one date, which builds an empty overnight
leg (D13); a swap whose payments are all past, with its expired results; and one swap of QuantLib's
testNotifications (a 2-year annual swap on ESTR, discounted on a flat 2% curve), priced on a flat 2% forecasting curve
and rebuilt on a flat 3% one, the rebuild that replaces QuantLib's relink.

Each swap's outputs are whether it builds, QuantLib's refusals recorded as its one throw; its inspectors (the payment
frequency, the overnight index's name, the averaging method, the lookback days, null for QuantLib's Null<Natural>(),
the lockout days, the observation shift, the payment lag and convention, nominal and nominals, which QuantLib refuses
for nominals that aren't constant or the same on both legs); each leg's payment dates and amounts; and the results of
DiscountingSwapEngine on a flat discount curve, with and without the settlement date's flows (NPV, the per-leg NPV and
BPS, the fair rate and spread, the fixed-leg accessors and the overnight-leg ones). NPVs are compared at the largest
nominal and BPS at it times 1e-4.

Telescopic swaps follow the plan's telescopic rule (instruments/fixedvsfloatingswap.py's make_ois): QuantLib builds
the swap with the flag first, so a refusal is its own; the expected values are then the same swap's built without
it, with the averaging approximation set on an averaged swap's overnight leg. The swaps are built with
ql.OvernightIndexedSwap, the wheel's proxy of three of QuantLib's four constructors; QuantLib's third (one nominal and
two schedules) delegates to its fourth with one-element nominal lists, which the wheel builds. Each case's inputs hold
its settings (with the stored fixings), its "swap" spec (instruments/fixedvsfloatingswap.py's), the discount curve and
the engine's includeSettlementDateFlows. Read back by
tests/NanoQL.Tests/Vectors/Instruments/OvernightIndexedSwapVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_fixedvsfloatingswap",
                                               os.path.join(v.GEN_ROOT, "instruments", "fixedvsfloatingswap.py"))
fvf = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(fvf)

TODAY = fvf.TODAY
SPOT = fvf.d(17, 1, 2025)
D = fvf.d
SIMPLE = ql.RateAveraging.Simple

# (index spec, calendar spec) by variant, round robin.
INDEXES = [(fvf.SOFR, fvf.US_SOFR), (fvf.ESTR, fvf.TARGET), (fvf.SONIA, fvf.UK_EXCHANGE)]
FORMS = ["nominal", "nominals", "nominal, two schedules", "nominals, two schedules"]


def variants():
    """(label, keyword arguments of fvf.ois, start date, fixings wanted) for the 20 variants. A variant that sets a
    convention past the payment adjustment gives the payment calendar too, which the wheel needs to reach it."""
    cal = "calendar"  # replaced by the variant's index calendar
    return [
        ("plain", {}, SPOT, False),
        ("payment lag 2", {"payment_lag": 2}, SPOT, False),
        ("Preceding payments, lag 1", {"payment_lag": 1, "payment_adjustment": ql.Preceding,
                                       "payment_calendar": cal}, SPOT, False),
        ("payments on TARGET", {"payment_calendar": fvf.TARGET, "payment_adjustment": ql.ModifiedFollowing}, SPOT,
         False),
        ("averaged", {"averaging": SIMPLE, "payment_calendar": cal}, SPOT, False),
        ("lookback 2", {"lookback": 2, "payment_calendar": cal}, SPOT, False),
        ("lookback 2 with shift", {"lookback": 2, "shift": True, "payment_calendar": cal}, SPOT, False),
        ("lookback 0", {"lookback": 0, "payment_calendar": cal}, SPOT, False),
        ("lockout 2", {"lockout": 2, "payment_calendar": cal}, SPOT, False),
        ("lockout 3, lookback 1 with shift", {"lockout": 3, "lookback": 1, "shift": True, "payment_calendar": cal},
         SPOT, False),
        ("telescopic", {"telescopic": True, "payment_calendar": cal}, SPOT, False),
        ("telescopic, averaged", {"telescopic": True, "averaging": SIMPLE, "payment_calendar": cal}, SPOT, False),
        ("telescopic, lookback 2 without shift", {"telescopic": True, "lookback": 2, "payment_calendar": cal}, SPOT,
         False),
        ("telescopic, lookback 2 with shift", {"telescopic": True, "lookback": 2, "shift": True,
                                               "payment_calendar": cal}, SPOT, False),
        ("rounding 6", {"rounding": 6, "payment_calendar": cal}, SPOT, False),
        ("rounding 0", {"rounding": 0, "payment_calendar": cal}, SPOT, False),
        ("rounding 17", {"rounding": 17, "payment_calendar": cal}, SPOT, False),
        ("averaged with spread", {"averaging": SIMPLE, "spread": 0.001, "payment_calendar": cal}, SPOT, False),
        ("negative spread", {"spread": -0.0015}, SPOT, False),
        ("seasoned", {"spread": 0.0005}, D(15, 10, 2024), True),
    ]


def swaps():
    """(name, spec, fixings, includeSettlementDateFlows) for the 80 swaps of the grid."""
    out = []
    k = 0
    for form in FORMS:
        for label, kw, start, wants_fixings in variants():
            index, cal = INDEXES[k % len(INDEXES)]
            kw = dict(kw)
            if kw.get("payment_calendar") == "calendar":
                kw["payment_calendar"] = cal
            end = fvf.d(start.dayOfMonth(), start.month(), start.year() + 2)
            fixed = fvf.sched(start, end, fvf.ONE_Y, cal)
            overnight = fvf.sched(start, end, fvf.SIX_M if k % 2 == 0 else fvf.THREE_M, cal)
            swap_type = fvf.RECEIVER if k % 5 == 3 or label == "negative spread" else fvf.PAYER
            fixed_dc = fvf.A365 if index is fvf.SONIA else fvf.A360
            nominal = [1e6, 2.5e7, 1e8][k % 3]
            rate = round(index["curve"]["forward"] + 0.0004 * ((k % 5) - 2), 6)
            if form == "nominal":
                spec = fvf.ois(form, swap_type, [nominal], fixed, rate, fixed_dc, index, **kw)
            elif form == "nominals":
                spec = fvf.ois(form, swap_type, [nominal, nominal / 2], fixed, rate, fixed_dc, index, **kw)
            elif form == "nominal, two schedules":
                spec = fvf.ois(form, swap_type, [nominal], fixed, rate, fixed_dc, index, [nominal], overnight, **kw)
            else:
                n_overnight = 4 if k % 2 == 0 else 8
                spec = fvf.ois(form, swap_type, [nominal, nominal / 2], fixed, rate, fixed_dc, index,
                               [nominal] * (n_overnight // 2) + [nominal / 2] * (n_overnight // 2), overnight, **kw)
            fx = [fvf.fixings(index, start - 10, TODAY - 1, v.SEED + k)] if wants_fixings else None
            include = [None, False, True][k % 3]
            out.append((f"{form} / {label}", spec, fx, include))
            k += 1
    return out


def one_date():
    """A swap whose overnight schedule has one date: an empty overnight leg (D13)."""
    fixed = fvf.sched(SPOT, D(17, 1, 2027), fvf.ONE_Y, fvf.TARGET)
    overnight = fvf.dates_schedule([SPOT], fvf.TARGET)
    return ("one-date overnight schedule", fvf.ois("nominals, two schedules", fvf.PAYER, [1e6], fixed, 0.03,
                                                   fvf.A360, fvf.ESTR, [1e6], overnight), None, None)


def expired():
    """A swap whose payments are all past: its expired results, its coupons read from stored fixings."""
    start, end = D(14, 1, 2022), D(16, 1, 2024)
    fixed = fvf.sched(start, end, fvf.ONE_Y, fvf.TARGET)
    spec = fvf.ois("nominal", fvf.PAYER, [1e6], fixed, 0.01, fvf.A360, fvf.ESTR)
    return ("expired", spec, [fvf.fixings(fvf.ESTR, start - 10, end, v.SEED + 99)], None)


def notifications():
    """QuantLib's testNotifications swap, on a 2% forecasting curve and rebuilt on a 3% one."""
    calendar = ql.TARGET()
    spot = calendar.advance(TODAY, 2, ql.Days)
    schedule = fvf.sched(spot, calendar.advance(spot, 2, ql.Years), fvf.ONE_Y, fvf.TARGET, ql.Following)
    out = []
    for forward in (0.02, 0.03):
        index = fvf.preset("Estr", fvf.curve(forward, fvf.A360))
        spec = fvf.ois("nominal", fvf.PAYER, [100000.0], schedule, 0.03, fvf.A360, index)
        out.append((f"notifications: forecasting curve {forward}", spec, None, None))
    return out


DISCOUNT = fvf.DISCOUNT
NOTIFICATIONS_DISCOUNT = fvf.curve(0.02, fvf.A360)


def outputs(sw, scale):
    expected = {"paymentFrequency": v.attempt(lambda: int(sw.paymentFrequency())),
                "overnightIndex": sw.overnightIndex().name(),
                "averagingMethod": int(sw.averagingMethod()),
                "lookbackDays": v.enc(sw.lookbackDays()),
                "lockoutDays": sw.lockoutDays(),
                "applyObservationShift": sw.applyObservationShift(),
                "paymentLag": sw.paymentLag(),
                "paymentConvention": int(sw.paymentConvention()),
                "nominal": v.attempt(sw.nominal),
                "nominals": v.attempt(lambda: list(sw.nominals()))}
    expected.update(fvf.leg_outputs("fixedLeg", sw.fixedLeg(), scale))
    expected.update(fvf.leg_outputs("overnightLeg", sw.overnightLeg(), scale))
    return expected


def case(name, spec, fx, include, discount):
    scale = fvf.scale_of(spec)
    with v.case_settings(TODAY, fixings=fx) as st:
        inputs = {"settings": st, "swap": spec, "discountCurve": discount, "includeSettlementDateFlows": include}
        try:
            sw = fvf.make_ois(spec)
        except RuntimeError:
            return {"name": name, "inputs": inputs, "expected": {"built": dict(v.THROWS)}}
        expected = {"built": True}
        expected.update(outputs(sw, scale))
        sw.setPricingEngine(fvf.engine(discount, include))
        expected.update(fvf.results(sw, scale))
        if "calculate" not in expected:
            r = v.result
            expected["overnightLegBPS"] = v.amount(r(sw.overnightLegBPS), scale * 1e-4)
            expected["overnightLegNPV"] = v.amount(r(sw.overnightLegNPV), scale)
        return {"name": name, "inputs": inputs, "expected": expected}


def cases():
    out = [case(name, spec, fx, include, DISCOUNT) for name, spec, fx, include in swaps() + [one_date(), expired()]]
    out += [case(name, spec, fx, include, NOTIFICATIONS_DISCOUNT) for name, spec, fx, include in notifications()]
    check_rows(out)
    return out


def check_rows(out):
    """The rows the file must hold, whatever the grid."""
    def find(suffix):
        return [c for c in out if c["name"].endswith(suffix)]
    assert all(c["expected"] == {"built": v.THROWS} for c in find("/ rounding 17"))
    assert all(c["expected"] == {"built": v.THROWS} for c in find("/ telescopic, lookback 2 without shift"))
    assert all(c["expected"]["built"] is True for c in find("/ telescopic, averaged"))
    gone = find("expired")[0]["expected"]
    assert gone["NPV"]["value"] == 0.0 and gone["fairRate"] is None
    one = find("one-date overnight schedule")[0]["expected"]
    assert one["overnightLegDates"] == [] and one["fairSpread"] is None
    assert any(c["expected"].get("nominal") == v.THROWS for c in out)
    assert any(c["expected"].get("nominals") == v.THROWS for c in out)
    assert any(c["expected"].get("lookbackDays", 0) is None for c in out)
    rebuilt = find("notifications: forecasting curve 0.02") + find("notifications: forecasting curve 0.03")
    assert rebuilt[0]["expected"]["NPV"] != rebuilt[1]["expected"]["NPV"]


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/overnightindexedswap.hpp", cases)
