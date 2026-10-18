# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's VanillaSwap (ql/instruments/vanillaswap.hpp), on the evaluation date 15 January 2025: 60
swaps, payer and receiver, of 1, 2, 5, 10 and 20 years, with floating spreads of -0.1%, 0 and 0.2%, at-par and
indexed coupons (the swap's explicit choice, or the at-par switch under both settings), nominals of 100, 1,000,000
and 25,000,000, and fixed rates around the curve's: 30 with annual fixed and semiannual floating legs on a Euribor
6M-style index (TARGET, Actual/360, on a flat 3.1% curve), as QuantLib's swap tests build them, and 30 with
semiannual fixed and quarterly floating legs, every other one on a Euribor 3M-style index on a steep curve (4.5%
simple) and the others on the 6M index, whose tenor is longer than the coupons', some with an explicit Following
payment convention;
10 of them, on the 6M index with quarterly coupons, again with a BlackIborCouponPricer set on the built swap's
floating leg (QuantLib's setCouponPricer(swap->floatingLeg(), pricer); nanoql's
CouponPricerFunctions.SetCouponPricer(swap, 1, pricer)): the bivariate-lognormal timing adjustment, which moves the
rates when a coupon pays before its index matures, at several volatilities and correlations, and pricers whose
explicit at-par choice may differ from the leg's; and one swap of QuantLib's testNotifications (a 2-year semiannual
swap on Euribor 6M,
discounted on a flat 2% curve), priced on a flat 2% forecasting curve and rebuilt on a flat 3% one, the rebuild that
replaces QuantLib's relink.

Each swap's outputs are the results of DiscountingSwapEngine on a flat 2.7% discount curve (NPV, the per-leg NPV and
BPS, the fair rate and spread, and the four leg accessors) and each coupon's amount, leg by leg; NPVs are
compared at the nominal and BPS at it times 1e-4.

The swaps are built with ql.VanillaSwap, the wheel's proxy of C++ QuantLib::VanillaSwap's constructor, whose floating
leg is an IborLeg read with the at-par switch the case sets; the pricers with ql.BlackIborCouponPricer and the
volatility with ql.ConstantOptionletVolatility, as tools/vectors/gen/cashflows/iborcoupon.py builds them. Each case's
inputs hold its settings, its "swap" (a vanilla swap spec of instruments/fixedvsfloatingswap.py), its "pricer" (null
for none) and the discount curve. Read back by tests/NanoQL.Tests/Vectors/Instruments/VanillaSwapVectorTests.cs."""
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
ibc = fvf.ibc

TODAY = fvf.TODAY
SPOT = fvf.d(17, 1, 2025)
LENGTHS = [1, 2, 5, 10, 20]
SPREADS = [-0.001, 0.0, 0.002]
NOMINALS = [100.0, 1e6, 2.5e7]
# (explicit choice, at-par switch): the swap's own choice, or the switch's under both settings.
COUPONS = [(None, True), (None, False), (True, True), (False, False)]
PRICED_AGAIN = 10


def swaps():
    """(name, spec, at-par switch) for the 60 swaps."""
    calendar = ql.TARGET()
    out = []
    k = 0
    for swap_type in (fvf.PAYER, fvf.RECEIVER):
        for length in LENGTHS:
            maturity = calendar.advance(SPOT, length, ql.Years, ql.ModifiedFollowing)
            fixed = fvf.sched(SPOT, maturity, fvf.ONE_Y, convention=ql.Unadjusted, rule=ql.DateGeneration.Forward)
            floating = fvf.sched(SPOT, maturity, fvf.SIX_M, rule=ql.DateGeneration.Forward)
            for spread in SPREADS:
                indexed, at_par = COUPONS[k % len(COUPONS)]
                nominal = NOMINALS[k % len(NOMINALS)]
                rate = round(0.031 + 0.0005 * ((k % 7) - 3), 6)
                spec = fvf.vanilla(swap_type, nominal, fixed, rate, fvf.THIRTY360_BB, floating, fvf.E6M, spread,
                                   fvf.A360, use_indexed=indexed)
                kind = "payer" if swap_type == fvf.PAYER else "receiver"
                choice = {None: "switch", True: "indexed", False: "at par"}[indexed]
                out.append((f"{kind} {length}Y spread {spread} {choice} {'at par' if at_par else 'indexed'} "
                            f"nominal {nominal:g}", spec, at_par))
                k += 1
    for swap_type in (fvf.PAYER, fvf.RECEIVER):
        for length in LENGTHS:
            maturity = calendar.advance(SPOT, length, ql.Years, ql.ModifiedFollowing)
            fixed = fvf.sched(SPOT, maturity, fvf.SIX_M, convention=ql.ModifiedFollowing)
            floating = fvf.sched(SPOT, maturity, fvf.THREE_M)
            for spread in SPREADS:
                indexed, at_par = COUPONS[k % len(COUPONS)]
                nominal = NOMINALS[k % len(NOMINALS)]
                rate = round(0.045 + 0.0005 * ((k % 5) - 2), 6)
                # every other swap fixes on the 6M index, whose tenor is longer than its quarterly coupons
                index, label = (fvf.E3M, "3M") if k % 2 == 0 else (fvf.E6M, "6M index, 3M")
                spec = fvf.vanilla(swap_type, nominal, fixed, rate, fvf.A360, floating, index, spread, fvf.A360,
                                   payment_convention=ql.Following if k % 3 == 0 else None, use_indexed=indexed)
                kind = "payer" if swap_type == fvf.PAYER else "receiver"
                choice = {None: "switch", True: "indexed", False: "at par"}[indexed]
                out.append((f"{label} {kind} {length}Y spread {spread} {choice} {'at par' if at_par else 'indexed'} "
                            f"nominal {nominal:g}", spec, at_par))
                k += 1
    return out


def pricers():
    """The pricers set on the floating legs of ten swaps, alternately at-par and indexed legs: each explicit choice
    is the other one."""
    bivariate = ql.BlackIborCouponPricer.BivariateLognormal
    return [ibc.pricer_spec(ibc.VOL_LN, bivariate, 1.0), ibc.pricer_spec(ibc.VOL_LN, bivariate, 0.7),
            ibc.pricer_spec(ibc.VOL_SLN, bivariate, 0.9), ibc.pricer_spec(None, use_indexed=False),
            ibc.pricer_spec(None, use_indexed=True), ibc.pricer_spec(ibc.VOL_N, bivariate, 0.5),
            ibc.pricer_spec(ibc.VOL_LN, bivariate, 0.95, use_indexed=True),
            ibc.pricer_spec(ibc.VOL_LN, bivariate, 0.85, use_indexed=False), ibc.pricer_spec(ibc.VOL_LN),
            ibc.pricer_spec(ibc.VOL_SLN, bivariate, 0.6)]


def notifications():
    """QuantLib's testNotifications swap, on a 2% forecasting curve and rebuilt on a 3% one."""
    calendar = ql.TARGET()
    spot = calendar.advance(TODAY, 2, ql.Days)
    schedule = fvf.sched(spot, calendar.advance(spot, 2, ql.Years), fvf.SIX_M, convention=ql.Following)
    out = []
    for forward in (0.02, 0.03):
        index = fvf.preset("Euribor6M", fvf.curve(forward))
        spec = fvf.vanilla(fvf.PAYER, 100000.0, schedule, 0.03, fvf.A365, schedule, index, 0.0, fvf.A365)
        out.append((f"notifications: forecasting curve {forward}", spec, None))
    return out


DISCOUNT = fvf.DISCOUNT
NOTIFICATIONS_DISCOUNT = fvf.curve(0.02)


def case(name, spec, at_par, pricer, discount):
    scale = fvf.scale_of(spec)
    with v.case_settings(TODAY, use_at_par_coupons=at_par) as st:
        sw = fvf.make_vanilla(spec)
        p = ibc.pricer(pricer)
        if p is not None:
            ql.setCouponPricer(sw.floatingLeg(), p)
        sw.setPricingEngine(fvf.engine(discount))
        expected = fvf.results(sw, scale)
        expected.update(fvf.leg_outputs("fixedLeg", sw.fixedLeg(), scale, dates=False))
        expected.update(fvf.leg_outputs("floatingLeg", sw.floatingLeg(), scale, dates=False))
        return {"name": name, "inputs": {"settings": st, "swap": spec, "pricer": pricer, "discountCurve": discount},
                "expected": expected}


def cases():
    out = []
    all_swaps = swaps()
    for name, spec, at_par in all_swaps:
        out.append(case(name, spec, at_par, None, DISCOUNT))
    # Each pricer goes on the next swap of the 6M index on quarterly coupons, where it moves the floating leg.
    candidates = iter([x for x in all_swaps if x[0].startswith("6M index, 3M")])
    for pricer in pricers():
        name, spec, at_par = next(candidates)
        out.append(case(name + " / Black pricer on the floating leg", spec, at_par, pricer, DISCOUNT))
    for name, spec, at_par in notifications():
        out.append(case(name, spec, at_par, None, NOTIFICATIONS_DISCOUNT))
    check_rows(out)
    return out


def check_rows(out):
    """The pricers move the floating leg: each repriced swap's floating-leg NPV differs from the same swap's without
    the pricer, so a pricer the copy dropped would show."""
    by_name = {c["name"]: c for c in out}
    priced_again = [c for c in out if c["name"].endswith(" / Black pricer on the floating leg")]
    assert len(priced_again) == PRICED_AGAIN
    for c in priced_again:
        plain = by_name[c["name"].removesuffix(" / Black pricer on the floating leg")]
        assert c["expected"]["floatingLegNPV"] != plain["expected"]["floatingLegNPV"], c["name"]
    rebuilt = [c for c in out if c["name"].startswith("notifications")]
    assert rebuilt[0]["expected"]["NPV"] != rebuilt[1]["expected"]["NPV"]


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/vanillaswap.hpp", cases)
