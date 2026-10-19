# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FixedRateBond (ql/instruments/bonds/fixedratebond.hpp): 14 bonds, each under one
evaluation context drawn with the seed from tools/vectors/gen/instruments/bond.py's 24: semiannual, annual and
quarterly schedules on TARGET, the UK and US calendars and the null calendar, one coupon rate or several (fewer than
the coupons, the last repeating), Actual/360, Actual/365, 30/360 (bond basis and US) and Actual/Actual day counters,
payment conventions, a payment calendar other than the schedule's, a redemption above par, an issue date on the
first payment (which QuantLib's derived bonds don't check), an ex-coupon period, a short first period, a long
schedule, a schedule of dates with no tenor (whose frequency is NoFrequency), and three constructions QuantLib refuses:
an empty schedule ("empty Schedule: no end date"), no coupon rates ("no coupon rates given") and a NaN face amount,
whose coupons start a notional step each ("multiple redemptions created").

A case's outputs are bond.py's: the inspectors with no date, the inspectors and the prices and yield at 12 dates
(a null one, the settlement date and 10 drawn), and the discounting engine's results with the members that read them;
with the bond's frequency and its day counter's name. A refused construction is the case's one output, "construct".
The wheel's FixedRateBond takes no first-period day counter, which the harness's cxx/instruments/bond covers.
Read back by tests/NanoQL.Tests/Vectors/Instruments/FixedRateBondVectorTests.cs."""
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
TARGET, NULL_CALENDAR, UK, US = bd.TARGET, bd.NULL_CALENDAR, bd.UK_SETTLEMENT, bd.US_GOVERNMENT
A360, A365, BB, US30, ISDA, ISMA = bd.A360, bd.A365, bd.THIRTY360_BB, bd.THIRTY360_US, bd.ACT_ISDA, bd.ACT_ISMA


def bonds():
    """(name, bond spec, face amount) for the 14 bonds."""
    s6m = bd.schedule_spec(D(15, 3, 2024), D(15, 3, 2028), (6, ql.Months), TARGET, ql.ModifiedFollowing)
    s1y = bd.schedule_spec(D(16, 6, 2025), D(16, 6, 2030), (1, ql.Years), NULL_CALENDAR, ql.Unadjusted,
                           ql.DateGeneration.Forward)
    s3m = bd.schedule_spec(D(30, 11, 2024), D(30, 11, 2027), (3, ql.Months), US, ql.Unadjusted)
    gilt = bd.schedule_spec(D(29, 2, 1996), D(7, 6, 2021), (6, ql.Months), NULL_CALENDAR, ql.Unadjusted,
                            ql.DateGeneration.Forward, True, D(7, 6, 1996))
    us31 = bd.schedule_spec(D(13, 2, 2014), D(13, 8, 2018), (6, ql.Months), US, ql.Unadjusted,
                            ql.DateGeneration.Forward)
    stub = bd.schedule_spec(D(10, 4, 2025), D(15, 10, 2029), (6, ql.Months), UK, ql.Following,
                            ql.DateGeneration.Backward, False, D(15, 10, 2025))
    long_annual = bd.schedule_spec(D(15, 1, 2025), D(15, 1, 2055), (1, ql.Years), TARGET, ql.Following)
    dates = bd.dates_schedule_spec([D(1, 2, 2025), D(7, 2, 2025), D(1, 4, 2025), D(27, 5, 2026)])
    first_payment = list(bd.make_schedule(s6m).dates())[1]
    return [
        ("6M bullet", bd.fixed_bond_spec(2, 100.0, s6m, [0.05], A360, ql.Following, 100.0, D(15, 3, 2024)), 100.0),
        ("6M varying coupons", bd.fixed_bond_spec(1, 1e6, s6m, [0.02875, 0.03, 0.03125, 0.0325], ISMA,
                                                  ql.ModifiedFollowing, 100.0, D(15, 3, 2024)), 1e6),
        ("1Y null calendar", bd.fixed_bond_spec(0, 100.0, s1y, [0.03], BB, ql.Unadjusted), 100.0),
        ("3M payment calendar and redemption 101", bd.fixed_bond_spec(3, 100.0, s3m, [0.042], A365,
                                                                      ql.ModifiedFollowing, 101.0, None, UK), 100.0),
        ("issue on the first payment", bd.fixed_bond_spec(2, 100.0, s6m, [0.045], A360, ql.Following, 100.0,
                                                          first_payment), 100.0),
        ("gilt ex-coupon", bd.fixed_bond_spec(3, 100.0, gilt, [0.08], ISMA, ql.Unadjusted, 100.0, D(29, 2, 1996), UK,
                                              bd.ex_coupon_spec(6, ql.Days, UK)), 100.0),
        ("30/360 US", bd.fixed_bond_spec(1, 100.0, us31, [0.015], US30, ql.Unadjusted, 100.0), 100.0),
        ("short first period", bd.fixed_bond_spec(2, 5e6, stub, [0.0375], ISDA, ql.Following, 100.0, D(10, 4, 2025)),
         5e6),
        ("30Y annual", bd.fixed_bond_spec(2, 100.0, long_annual, [0.02, 0.025], A365, ql.Following), 100.0),
        ("schedule of dates", bd.fixed_bond_spec(3, 100.0, dates, [0.01], A365, ql.Following), 100.0),
        ("ex-coupon on the null calendar", bd.fixed_bond_spec(2, 100.0, s1y, [0.04], A365, ql.Unadjusted, 100.0, None,
                                                              None, bd.ex_coupon_spec(1, ql.Months, NULL_CALENDAR)),
         100.0),
        ("empty schedule", bd.fixed_bond_spec(2, 100.0, bd.dates_schedule_spec([]), [0.05], A360), 100.0),
        ("no coupon rates", bd.fixed_bond_spec(2, 100.0, s6m, [], A360), 100.0),
        ("NaN face amount", bd.fixed_bond_spec(2, float("nan"), s1y, [0.03], A360), 100.0),
    ]


def extra(bond):
    return {"frequency": int(bond.frequency()), "dayCounter": bond.dayCounter().name()}


def cases():
    rnd = random.Random(v.SEED)
    out = []
    for k, (name, spec, scale) in enumerate(bonds()):
        context = rnd.choice(bd.contexts())
        rate, price = bd.RATES[k % len(bd.RATES)], bd.PRICES[k % len(bd.PRICES)]
        out.append(bd.run_case(name, spec, scale, context, rate, price, bd.CURVE_RATE, rnd, 12, extra))
    assert sum("construct" in c["expected"] for c in out) == 3
    assert any(c["expected"].get("frequency") == int(ql.NoFrequency) for c in out)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/bonds/fixedratebond.hpp", cases)
