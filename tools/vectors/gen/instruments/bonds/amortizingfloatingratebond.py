# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AmortizingFloatingRateBond (ql/instruments/bonds/amortizingfloatingratebond.hpp): 7 bonds on
instruments/bonds/floatingratebond.py's Euribor-style indexes, each under its own evaluation date with flags drawn with
the seed: notionals stepping down every coupon and in fewer steps than the coupons, built under the at-par switch and
under the indexed one, a gearing with spreads, a cap, redemptions other than par, an ex-coupon period, a payment lag,
an issue date, and a seasoned bond whose past coupons read stored fixings.

Each case stores a fixing for every coupon fixing before the evaluation date, as floatingratebond.py does, and
records the store and the switch in its settings; a capped bond's coupons have no pricer, as QuantLib builds them, so
their amounts, rates and prices are refused. A case's outputs are tools/vectors/gen/instruments/bond.py's, with each
cash flow's fixing date and fixing days. Read back by
tests/NanoQL.Tests/Vectors/Instruments/AmortizingFloatingRateBondVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location("wheel_floatingratebond", os.path.join(
    v.GEN_ROOT, "instruments", "bonds", "floatingratebond.py"))
frb = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(frb)
bd = frb.bd

D = ql.Date
TARGET, A360, A365 = frb.TARGET, frb.A360, frb.A365
EV1, EV2 = frb.EV1, frb.EV2


def bonds():
    """(name, bond spec, scale, evaluation date, at-par switch) for the 7 bonds."""
    s6m = bd.schedule_spec(D(20, 1, 2025), D(20, 1, 2028), (6, ql.Months), TARGET, ql.ModifiedFollowing)
    s3m = bd.schedule_spec(D(22, 3, 2024), D(22, 3, 2027), (3, ql.Months), TARGET, ql.Unadjusted,
                           ql.DateGeneration.Forward)
    seasoned = bd.schedule_spec(D(15, 9, 2023), D(15, 9, 2027), (6, ql.Months), TARGET, ql.ModifiedFollowing)
    e6m1, e6m2 = frb.euribor((6, ql.Months), EV1), frb.euribor((6, ql.Months), EV2)
    e3m1 = frb.euribor((3, ql.Months), EV1, 0.031)
    steps = [100.0, 90.0, 80.0, 70.0, 60.0, 50.0]
    return [
        ("stepping down, at par", bd.amortizing_floating_bond_spec(2, steps, s6m, e6m1, A360), 100.0, EV1, True),
        ("stepping down, indexed", bd.amortizing_floating_bond_spec(2, steps, s6m, e6m1, A360), 100.0, EV1, False),
        ("in fewer steps, gearing and spreads", bd.amortizing_floating_bond_spec(
            2, [1e6, 6e5], s6m, e6m2, A365, ql.Following, None, [1.2], [0.002, 0.003]), 1e6, EV2, True),
        ("capped, no pricer", bd.amortizing_floating_bond_spec(2, steps, s6m, e6m2, A360, caps=[0.04]), 100.0, EV2,
         True),
        ("redemptions, ex-coupon", bd.amortizing_floating_bond_spec(
            2, steps, s6m, e6m1, A360, ex_coupon=bd.ex_coupon_spec(5, ql.Days, TARGET, ql.Preceding),
            redemptions=[100.0, 101.0, 102.0]), 100.0, EV1, True),
        ("3M, a payment lag and an issue date", bd.amortizing_floating_bond_spec(
            0, [100.0, 75.0, 50.0, 25.0], s3m, e3m1, A365, ql.Following, 0, issue=D(22, 3, 2024), payment_lag=2), 100.0,
         EV1, True),
        ("seasoned", bd.amortizing_floating_bond_spec(2, [100.0, 80.0, 60.0, 40.0], seasoned,
                                                      frb.euribor((6, ql.Months), EV2, 0.025), A360), 100.0, EV2,
         True),
    ]


def cases():
    rnd = random.Random(v.SEED)
    out = []
    for k, (name, spec, scale, ev, at_par) in enumerate(bonds()):
        context = (ev, rnd.choice([False, True]), rnd.choice([None, False, True]))
        rate, price = bd.RATES[k % len(bd.RATES)], bd.PRICES[k % len(bd.PRICES)]
        out.append(bd.run_case(name, spec, scale, context, rate, price, bd.CURVE_RATE, rnd, 12, frb.extra,
                               frb.past_fixings(spec, ev), at_par))
    assert any(v.THROWS in c["expected"]["cashflowAmounts"] for c in out)
    assert any("fixings" in c["inputs"]["settings"] for c in out)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/bonds/amortizingfloatingratebond.hpp", cases)
