# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FloatingRateBond (ql/instruments/bonds/floatingratebond.hpp): 11 bonds on Euribor-style
indexes (tools/vectors/gen/indexes/interestrateindex.py's IborIndex with Euribor's conventions, forecasting on a flat
curve whose reference date is the evaluation date), each under its own evaluation date with flags drawn with the
seed: 6-month and 3-month coupons, built under the at-par switch and under the indexed one (the bond reads the switch
when it is built, through its coupons' default pricer: caveat 12), gearings and spreads (fewer than the coupons, the
last repeating), a cap, a collar and in-arrears fixing (QuantLib builds those coupons with no pricer, so their
amounts, rates and prices are refused, "pricer not set"), no fixing days with the following fixing convention, an
ex-coupon period, a redemption above par with an issue date, a seasoned bond whose past coupons read stored fixings,
and a forward-starting bond.

Each case stores a fixing for every coupon fixing before the evaluation date, 2.5% plus a basis point per coupon,
through the index (vectors.case_settings), and records the store and the switch in its settings. A case's outputs are
tools/vectors/gen/instruments/bond.py's: the inspectors with no date, the inspectors and the prices and yield at 12
dates (a null one, the settlement date and 10 drawn), and the discounting engine's results, on a flat curve whose
reference date is the evaluation date, with the members that read them; with each cash flow's fixing date and fixing
days (null for one that isn't a floating-rate coupon). Read back by
tests/NanoQL.Tests/Vectors/Instruments/FloatingRateBondVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location("wheel_bond", os.path.join(v.GEN_ROOT, "instruments", "bond.py"))
bd = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bd)

D = ql.Date
TARGET = bd.TARGET
A360, A365 = bd.A360, bd.A365
EV1, EV2 = D(16, 6, 2025), D(14, 3, 2026)


def euribor(tenor, ev, forward=0.028):
    return bd.irx.ibor_spec("Euribor", tenor, 2, TARGET, ql.ModifiedFollowing, True, A360,
                            bd.irx.curve_spec(ev, forward))


def bonds():
    """(name, bond spec, face amount, evaluation date, at-par switch) for the 11 bonds."""
    s6m = bd.schedule_spec(D(20, 1, 2025), D(20, 1, 2030), (6, ql.Months), TARGET, ql.ModifiedFollowing)
    s3m = bd.schedule_spec(D(22, 3, 2024), D(22, 12, 2027), (3, ql.Months), TARGET, ql.Unadjusted,
                           ql.DateGeneration.Forward)
    seasoned = bd.schedule_spec(D(15, 9, 2023), D(15, 9, 2028), (6, ql.Months), TARGET, ql.ModifiedFollowing)
    forward = bd.schedule_spec(D(20, 1, 2027), D(20, 1, 2031), (6, ql.Months), TARGET, ql.ModifiedFollowing)
    e6m1, e6m2, e3m1 = euribor((6, ql.Months), EV1), euribor((6, ql.Months), EV2), euribor((3, ql.Months), EV1, 0.031)
    return [
        ("6M at par", bd.floating_bond_spec(2, 100.0, s6m, e6m1, A360), 100.0, EV1, True),
        ("6M indexed", bd.floating_bond_spec(2, 100.0, s6m, e6m1, A360), 100.0, EV1, False),
        ("gearing and spreads", bd.floating_bond_spec(2, 1e6, s6m, e6m2, A365, ql.Following, None, [1.5],
                                                      [0.002, 0.003]), 1e6, EV2, True),
        ("capped, no pricer", bd.floating_bond_spec(2, 100.0, s6m, e6m1, A360, caps=[0.04]), 100.0, EV1, True),
        ("collared, no pricer", bd.floating_bond_spec(2, 100.0, s6m, e6m2, A360, caps=[0.04], floors=[0.0]), 100.0,
         EV2, True),
        ("in arrears, no pricer", bd.floating_bond_spec(2, 100.0, s6m, e6m1, A360, in_arrears=True), 100.0, EV1, True),
        ("3M no fixing days, following fixing", bd.floating_bond_spec(0, 100.0, s3m, e3m1, A365, ql.Following, 0,
                                                                      fixing_convention=ql.Following), 100.0, EV1,
         True),
        ("ex-coupon", bd.floating_bond_spec(2, 100.0, s6m, e6m2, A360,
                                            ex_coupon=bd.ex_coupon_spec(5, ql.Days, TARGET, ql.Preceding)), 100.0, EV2,
         False),
        ("redemption 101 with an issue date", bd.floating_bond_spec(3, 100.0, s6m, e6m1, A360, ql.ModifiedFollowing,
                                                                    3, redemption=101.0, issue=D(20, 1, 2025)), 100.0,
         EV1, True),
        ("seasoned", bd.floating_bond_spec(2, 100.0, seasoned, euribor((6, ql.Months), EV2, 0.025), A360), 100.0, EV2,
         True),
        ("forward starting", bd.floating_bond_spec(2, 100.0, forward, e6m1, A360, spreads=[0.0015]), 100.0, EV1,
         False),
    ]


def past_fixings(spec, ev):
    """A function of the bond, built without fixings, returning the fixings its coupons read before the evaluation
    date: 2.5% plus a basis point per coupon, stored through the index."""
    def fixings(bond):
        pairs = []
        for i, c in enumerate(bond.cashflows()):
            frc = ql.as_floating_rate_coupon(c)
            if frc is not None and frc.fixingDate() < ev:
                pairs.append((frc.fixingDate(), 0.025 + 0.0001 * i))
        return [(bd.irx.make_index(spec["index"]), pairs)] if pairs else None
    return fixings


def extra(bond):
    dates, days = [], []
    for c in bond.cashflows():
        frc = ql.as_floating_rate_coupon(c)
        dates.append(v.enc(frc.fixingDate()) if frc is not None else None)
        days.append(frc.fixingDays() if frc is not None else None)
    return {"fixingDates": dates, "fixingDays": days}


def cases():
    rnd = random.Random(v.SEED)
    out = []
    for k, (name, spec, scale, ev, at_par) in enumerate(bonds()):
        context = (ev, rnd.choice([False, True]), rnd.choice([None, False, True]))
        rate, price = bd.RATES[k % len(bd.RATES)], bd.PRICES[k % len(bd.PRICES)]
        out.append(bd.run_case(name, spec, scale, context, rate, price, bd.CURVE_RATE, rnd, 12, extra,
                               past_fixings(spec, ev), at_par))
    assert any(v.THROWS in c["expected"]["cashflowAmounts"] for c in out)
    assert any("fixings" in c["inputs"]["settings"] for c in out)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/bonds/floatingratebond.hpp", cases)
