# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ZeroCouponBond (ql/instruments/bonds/zerocouponbond.hpp): 12 bonds, each under one
evaluation context drawn with the seed from tools/vectors/gen/instruments/bond.py's 24, on TARGET, the US government
bond, UK and null calendars, with 0 to 3 settlement days, the payment conventions Following, ModifiedFollowing,
Preceding and Unadjusted (on maturities that fall on a weekend or a holiday, so that the redemption is paid on
another day than the unadjusted maturity date the bond keeps), redemptions at, below and above par, issue dates (one
after the evaluation date's settlement, which floors the settlement date) and none, and face amounts of 100 and of
millions.

A case's outputs are bond.py's: the inspectors with no date, the inspectors and the prices and yield at 14 dates (a
null one, the settlement date and 12 drawn), and the discounting engine's results with the members that read them. A
zero-coupon bond has no coupon, so QuantLib refuses its next coupon rate before the redemption ("no coupon paid at
cashflow date ..."). Read back by tests/NanoQL.Tests/Vectors/Instruments/ZeroCouponBondVectorTests.cs."""
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


def bonds():
    """(name, bond spec, face amount) for the 12 bonds."""
    return [
        ("TARGET Following", bd.zero_bond_spec(2, TARGET, 100.0, D(15, 3, 2030)), 100.0),
        ("Saturday maturity, Following", bd.zero_bond_spec(2, TARGET, 100.0, D(15, 6, 2030)), 100.0),
        ("Saturday maturity, Preceding", bd.zero_bond_spec(1, TARGET, 100.0, D(15, 6, 2030), ql.Preceding), 100.0),
        ("month-end Saturday, ModifiedFollowing", bd.zero_bond_spec(3, US, 1e6, D(31, 5, 2031), ql.ModifiedFollowing,
                                                                    100.0, D(30, 11, 2024)), 1e6),
        ("US holiday maturity", bd.zero_bond_spec(1, US, 100.0, D(4, 7, 2028), ql.Following, 100.0,
                                                  D(30, 11, 2024)), 100.0),
        ("UK unadjusted", bd.zero_bond_spec(0, UK, 100.0, D(25, 12, 2027), ql.Unadjusted), 100.0),
        ("null calendar", bd.zero_bond_spec(0, NULL_CALENDAR, 100.0, D(20, 9, 2026), ql.Following), 100.0),
        ("redemption 105", bd.zero_bond_spec(2, TARGET, 5e5, D(16, 6, 2028), ql.Following, 105.0), 5e5),
        ("redemption 95", bd.zero_bond_spec(2, TARGET, 100.0, D(16, 6, 2026), ql.Following, 95.0, D(16, 6, 2024)),
         100.0),
        ("issue after the settlement", bd.zero_bond_spec(2, TARGET, 100.0, D(15, 12, 2029), ql.Following, 100.0,
                                                         D(1, 10, 2026)), 100.0),
        ("short", bd.zero_bond_spec(2, TARGET, 100.0, D(30, 9, 2025)), 100.0),
        ("long", bd.zero_bond_spec(3, US, 100.0, D(15, 11, 2055), ql.ModifiedFollowing), 100.0),
    ]


def cases():
    rnd = random.Random(v.SEED)
    out = []
    for k, (name, spec, scale) in enumerate(bonds()):
        context = rnd.choice(bd.contexts())
        rate, price = bd.RATES[k % len(bd.RATES)], bd.PRICES[k % len(bd.PRICES)]
        out.append(bd.run_case(name, spec, scale, context, rate, price, bd.CURVE_RATE, rnd, 14))
    assert any(c["expected"]["maturityDate"] != c["expected"]["redemptionDate"] for c in out)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/bonds/zerocouponbond.hpp", cases)
