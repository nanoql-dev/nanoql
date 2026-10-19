# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BMASwap (ql/instruments/bmaswap.hpp) and, through its BMA leg, its average-BMA coupons
(ql/cashflows/averagebmacoupon.hpp): 30 swaps on the evaluation date 15 January 2025, payer and receiver of BMA, of 1
to 15 years, with Libor fractions from 0 to 1 and Libor spreads; Libor legs on Libor-style indexes of the US dollar
(3M and 1M, on the UK exchange calendar) and on Euribor 3M; BMA legs weekly, monthly or quarterly on the US
government-bond calendar, rule-based or from dates, with irregular periods and explicit day counters; forward-starting
and seasoned swaps, which read stored Libor and BMA fixings; a swap that has expired; and a type outside Swap::Type,
which QuantLib refuses after building the legs ("Unknown BMA-swap type").

Each case's outputs are whether the swap builds, QuantLib's refusal recorded as its throw; then its inspectors (the
Libor fraction and spread, the nominal and the type); the results of DiscountingSwapEngine on a flat discount curve,
with no settlement-date flag (the NPV, the per-leg NPV and BPS, the four leg accessors, the fair Libor fraction, null
where QuantLib refuses it, and the fair Libor spread); the Libor leg's payment dates, rates and amounts; and the BMA
leg's payment and accrual dates, rates and amounts, and its first coupon's nominal, fixing days, gearing and spread,
and its refusals of a single fixing date, index fixing and convexity adjustment. NPVs and amounts are compared at the
nominal, and BPS at it times 1e-4.

The fixings a seasoned or expired swap reads are stored with vectors.settings(fixings=...): every valid fixing date of
the Libor and BMA indexes from three weeks before the swap's start to the day before the evaluation date. Each case's
inputs hold its settings, "swap" ({"type", "nominal", "liborSchedule", "liborFraction", "liborSpread", "liborIndex",
"liborDayCount", "bmaSchedule", "bmaCurve", "bmaDayCount"}: schedules and indexes are specs of
instruments/fixedvsfloatingswap.py, the BMA index a BMAIndex on "bmaCurve", a null day counter QuantLib's empty one)
and "discountCurve", a flat forward spec. Read back by tests/NanoQL.Tests/Vectors/Instruments/BMASwapVectorTests.cs."""
import importlib.util
import os
import random
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
irx = fvf.irx

TODAY = fvf.TODAY
D = fvf.d
M, Y, W = ql.Months, ql.Years, ql.Weeks
MF, F, U = ql.ModifiedFollowing, ql.Following, ql.Unadjusted
BACKWARD, FORWARD = ql.DateGeneration.Backward, ql.DateGeneration.Forward
US_GOVT = {"type": "UnitedStates", "market": int(ql.UnitedStates.GovernmentBond)}
UK = irx.UK_EXCHANGE
A360, A365 = irx.A360, irx.A365
ACT_ISDA = {"type": "ActualActual", "convention": int(ql.ActualActual.ISDA)}

LIBOR_CURVE = fvf.curve(0.0342, A360, ql.Simple)
BMA_CURVE = fvf.curve(0.0261)
DISCOUNT = fvf.curve(0.0307)
LIBOR_3M = fvf.with_currency(irx.ibor_spec("UsdLibor", (3, M), 2, UK, MF, False, A360, LIBOR_CURVE), "USD")
LIBOR_1M = fvf.with_currency(irx.ibor_spec("UsdLibor", (1, M), 2, UK, MF, False, A360, LIBOR_CURVE), "USD")
EURIBOR_3M = fvf.preset("Euribor3M", fvf.curve(0.0291, A360, ql.Simple))


def sched(eff, mat, tenor, cal, convention=MF, rule=BACKWARD, eom=False, first=None, next_to_last=None):
    return fvf.sched(eff, mat, tenor, cal, convention, rule, eom, first, next_to_last)


def swap(swap_type, nominal, start, end, fraction, spread=0.0, libor=LIBOR_3M, libor_tenor=ql.Period(3, M),
         libor_cal=UK, libor_dc=None, bma_tenor=ql.Period(1, W), bma_convention=F, bma_rule=BACKWARD,
         bma_dc=None, bma_curve=BMA_CURVE, libor_first=None, bma_next_to_last=None, bma_dates=None):
    bma = (fvf.dates_schedule(bma_dates, irx.NULL_CALENDAR) if bma_dates is not None
           else sched(start, end, bma_tenor, US_GOVT, bma_convention, bma_rule, next_to_last=bma_next_to_last))
    return {"type": int(swap_type), "nominal": nominal,
            "liborSchedule": sched(start, end, libor_tenor, libor_cal, first=libor_first),
            "liborFraction": fraction, "liborSpread": spread, "liborIndex": libor, "liborDayCount": libor_dc,
            "bmaSchedule": bma, "bmaCurve": bma_curve, "bmaDayCount": bma_dc}


def swaps():
    """(name, spec, evaluation date) for the 30 swaps."""
    P, R = ql.Swap.Payer, ql.Swap.Receiver
    spot = D(17, 1, 2025)
    wednesdays = [D(22, 1, 2025) + 7 * k for k in range(14)]
    out = [
        ("2Y payer", swap(P, 1e6, spot, D(17, 1, 2027), 0.70), TODAY),
        ("2Y receiver", swap(R, 1e6, spot, D(17, 1, 2027), 0.70), TODAY),
        ("5Y payer, spread, monthly BMA leg", swap(P, 1e6, spot, D(17, 1, 2030), 0.67, 0.001,
                                                    bma_tenor=ql.Period(1, M)), TODAY),
        ("5Y receiver, quarterly BMA leg", swap(R, 1e6, spot, D(17, 1, 2030), 0.75, -0.0005,
                                                bma_tenor=ql.Period(3, M)), TODAY),
        ("1Y monthly BMA leg", swap(P, 1e6, spot, D(20, 1, 2026), 0.60, bma_tenor=ql.Period(1, M)), TODAY),
        ("10Y, quarterly BMA leg", swap(P, 1e6, spot, D(17, 1, 2035), 0.68, bma_tenor=ql.Period(3, M)), TODAY),
        ("15Y receiver, spread, quarterly BMA leg", swap(R, 1e6, spot, D(17, 1, 2040), 0.72, 0.0015,
                                                          bma_tenor=ql.Period(3, M)), TODAY),
        ("seasoned 3Y", swap(P, 1e6, D(2, 10, 2024), D(2, 10, 2027), 0.69), TODAY),
        ("seasoned 2Y receiver", swap(R, 1e6, D(6, 11, 2024), D(6, 11, 2026), 0.71, 0.0002), TODAY),
        ("seasoned a week", swap(P, 1e6, D(8, 1, 2025), D(8, 1, 2027), 0.70), TODAY),
        ("forward-starting 3Y", swap(P, 1e6, D(15, 7, 2025), D(15, 7, 2028), 0.70, bma_tenor=ql.Period(1, M)), TODAY),
        ("zero fraction, no spread", swap(P, 1e6, spot, D(17, 1, 2027), 0.0, libor_dc=A360), TODAY),
        ("zero fraction, spread", swap(R, 1e6, spot, D(17, 1, 2027), 0.0, 0.003, libor_dc=A360), TODAY),
        ("full fraction, spread", swap(P, 1e6, spot, D(17, 1, 2027), 1.0, 0.002), TODAY),
        ("explicit day counters", swap(P, 1e6, spot, D(17, 1, 2028), 0.70, libor_dc=A365, bma_dc=A360,
                                       bma_tenor=ql.Period(1, M)), TODAY),
        ("Actual/Actual BMA leg", swap(R, 1e6, spot, D(17, 1, 2028), 0.70, bma_dc=ACT_ISDA, bma_tenor=ql.Period(1, M)),
         TODAY),
        ("on Euribor 3M", swap(P, 1e6, spot, D(17, 1, 2029), 0.80, libor=EURIBOR_3M, libor_cal=irx.TARGET,
                               bma_tenor=ql.Period(3, M)), TODAY),
        ("on Libor 1M", swap(P, 1e6, spot, D(17, 1, 2027), 0.66, libor=LIBOR_1M,
                             libor_tenor=ql.Period(1, M)), TODAY),
        ("short front Libor stub", swap(P, 1e6, spot, D(17, 1, 2028), 0.70, libor_first=D(17, 3, 2025),
                                        bma_tenor=ql.Period(1, M)), TODAY),
        ("short back BMA stub, forward", swap(R, 1e6, spot, D(19, 2, 2026), 0.70, bma_tenor=ql.Period(1, M),
                                              bma_rule=FORWARD), TODAY),
        ("BMA leg unadjusted", swap(P, 1e6, spot, D(17, 1, 2027), 0.70, bma_convention=U), TODAY),
        ("BMA next-to-last date", swap(P, 1e6, spot, D(17, 1, 2027), 0.70, bma_tenor=ql.Period(1, M),
                                       bma_next_to_last=D(5, 1, 2027)), TODAY),
        ("BMA leg from dates", swap(P, 1e6, wednesdays[0], wednesdays[-1], 0.70, bma_dates=wednesdays), TODAY),
        ("nominal 25,000,000", swap(R, 2.5e7, spot, D(17, 1, 2027), 0.69), TODAY),
        ("nominal 100", swap(P, 100.0, spot, D(17, 1, 2027), 0.70), TODAY),
        ("negative BMA curve", swap(P, 1e6, spot, D(17, 1, 2030), 0.70, bma_tenor=ql.Period(1, M),
                                    bma_curve=fvf.curve(-0.0042, A365, ql.Compounded, ql.Quarterly)), TODAY),
        ("7Y payer, spread, quarterly BMA leg", swap(P, 1e6, spot, D(17, 1, 2032), 0.65, -0.001,
                                                      bma_tenor=ql.Period(3, M)), TODAY),
        ("short swap of three weeks", swap(R, 1e6, spot, D(7, 2, 2025), 0.70, libor_tenor=ql.Period(1, M)), TODAY),
        ("expired", swap(P, 1e6, D(2, 1, 2024), D(2, 1, 2025), 0.70), TODAY),
        ("unknown type", swap(5, 1e6, spot, D(17, 1, 2027), 0.70), TODAY),
    ]
    assert len(out) == 30
    return out


def day_counter(spec):
    return ql.InterestRate().dayCounter() if spec is None else irx.day_counter(spec)


def make_swap(spec):
    return ql.BMASwap(spec["type"], spec["nominal"], fvf.schedule(spec["liborSchedule"]), spec["liborFraction"],
                      spec["liborSpread"], fvf.make_index(spec["liborIndex"]), day_counter(spec["liborDayCount"]),
                      fvf.schedule(spec["bmaSchedule"]), ql.BMAIndex(irx.handle(spec["bmaCurve"])),
                      day_counter(spec["bmaDayCount"]))


def fixings(spec, rnd):
    """Every valid fixing date of the Libor and BMA indexes from three weeks before the swap's start to the day before
    the evaluation date, each with a fixing."""
    libor, bma = fvf.make_index(spec["liborIndex"]), ql.BMAIndex()
    start = fvf.schedule(spec["liborSchedule"]).startDate() - 21
    out = []
    for index, level in ((libor, 0.034), (bma, 0.026)):
        pairs = [(start + n, round(level + rnd.uniform(-0.002, 0.002), 6)) for n in range(TODAY - start)
                 if index.isValidFixingDate(start + n)]
        if pairs:
            out.append((index, pairs))
    return out or None


def outputs(s, nominal):
    bp = nominal * 1e-4
    s.setPricingEngine(ql.DiscountingSwapEngine(irx.handle(DISCOUNT)))
    libor = list(s.liborLeg())
    bma = [ql.as_floating_rate_coupon(cf) for cf in s.bmaLeg()]
    first = bma[0]
    return {
        "built": True,
        "liborFraction": s.liborFraction(),
        "liborSpread": s.liborSpread(),
        "nominal": s.nominal(),
        "type": int(s.type()),
        "NPV": v.amount(s.NPV(), nominal),
        "legNPV": [v.amount(s.legNPV(j), nominal) for j in range(2)],
        "legBPS": [v.amount(s.legBPS(j), bp) for j in range(2)],
        "liborLegNPV": v.amount(s.liborLegNPV(), nominal),
        "liborLegBPS": v.amount(s.liborLegBPS(), bp),
        "bmaLegNPV": v.amount(s.bmaLegNPV(), nominal),
        "bmaLegBPS": v.amount(s.bmaLegBPS(), bp),
        "fairLiborFraction": v.result(s.fairLiborFraction),
        "fairLiborSpread": v.result(s.fairLiborSpread),
        "liborDate": [v.enc(cf.date()) for cf in libor],
        "liborRate": [v.attempt(lambda cf=cf: ql.as_coupon(cf).rate()) for cf in libor],
        "liborAmount": [v.amount(cf.amount(), nominal) for cf in libor],
        "bmaDate": [v.enc(c.date()) for c in bma],
        "bmaAccrualStartDate": [v.enc(c.accrualStartDate()) for c in bma],
        "bmaAccrualEndDate": [v.enc(c.accrualEndDate()) for c in bma],
        "bmaNominal": first.nominal(),
        "bmaFixingDays": first.fixingDays(),
        "bmaGearing": first.gearing(),
        "bmaSpread": first.spread(),
        "bmaRate": [v.attempt(c.rate) for c in bma],
        "bmaAmount": [v.amount(c.amount(), nominal) for c in bma],
        "bmaFixingDate": v.attempt(first.fixingDate),
        "bmaIndexFixing": v.attempt(first.indexFixing),
        "bmaConvexityAdjustment": v.attempt(first.convexityAdjustment),
    }


def cases():
    rnd = random.Random(v.SEED)
    out = []
    for name, spec, today in swaps():
        with v.case_settings(today, fixings=fixings(spec, rnd)) as st:
            try:
                s = make_swap(spec)
            except RuntimeError:
                expected = {"built": dict(v.THROWS)}
            else:
                expected = outputs(s, spec["nominal"])
            out.append({"name": name, "inputs": {"settings": st, "swap": spec, "discountCurve": DISCOUNT},
                        "expected": expected})
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    assert by_name["unknown type"] == {"built": v.THROWS}
    expired = by_name["expired"]
    assert expired["NPV"]["value"] == 0.0 and expired["fairLiborFraction"] is None, expired
    assert expired["fairLiborSpread"] == "NaN", expired
    assert by_name["zero fraction, no spread"]["fairLiborFraction"] is None
    assert by_name["2Y payer"]["bmaFixingDate"] == v.THROWS


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/bmaswap.hpp", cases)
