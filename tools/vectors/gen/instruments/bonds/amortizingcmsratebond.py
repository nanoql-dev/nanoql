# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AmortizingCmsRateBond (ql/instruments/bonds/amortizingcmsratebond.hpp): 5 bonds on
instruments/bonds/cmsratebond.py's Euribor swap indexes, each under its own evaluation date with flags drawn with the
seed: notionals stepping down every coupon and in fewer steps than the coupons (the last repeating), a gearing with a
spread, a cap and a floor, an issue date, and a seasoned bond whose past coupons read stored fixings. The bond redeems
each notional step at par: the wheel's constructor takes no redemptions.

Each bond's coupons are priced by cmsratebond.py's pricer, set once the bond is built; each case stores a fixing for
every coupon fixing before the evaluation date, as there. A case's outputs are tools/vectors/gen/instruments/bond.py's,
with each cash flow's fixing date and fixing days. Read back by
tests/NanoQL.Tests/Vectors/Instruments/AmortizingCmsRateBondVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location("wheel_cmsratebond", os.path.join(v.GEN_ROOT, "instruments", "bonds",
                                                                               "cmsratebond.py"))
cms = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cms)
bd = cms.bd

D = ql.Date
M, Y = ql.Months, ql.Years
TARGET, THIRTY360_BB, A360 = cms.TARGET, cms.THIRTY360_BB, cms.A360
EV1, EV2 = cms.EV1, cms.EV2


def bonds():
    """(name, bond spec, scale, evaluation date) for the 5 bonds."""
    s1y = bd.schedule_spec(D(20, 1, 2025), D(20, 1, 2030), (1, Y), TARGET, ql.Unadjusted)
    s6m = bd.schedule_spec(D(20, 1, 2025), D(20, 1, 2028), (6, M), TARGET, ql.ModifiedFollowing)
    seasoned = bd.schedule_spec(D(15, 9, 2023), D(15, 9, 2028), (1, Y), TARGET, ql.ModifiedFollowing)
    p1, p2 = bd.cms_pricer_spec(EV1), bd.cms_pricer_spec(EV2)
    c10y1, c10y2, c2y1 = cms.swap_index((10, Y), EV1), cms.swap_index((10, Y), EV2), cms.swap_index((2, Y), EV1,
                                                                                                    "Euribor3M")
    return [
        ("stepping down every coupon", bd.amortizing_cms_bond_spec(2, [100.0, 80.0, 60.0, 40.0, 20.0], s1y, c10y1,
                                                                   THIRTY360_BB, p1, fixing_days=2), 100.0, EV1),
        ("in fewer steps, gearing and spread", bd.amortizing_cms_bond_spec(
            2, [1e6, 5e5], s6m, c2y1, A360, p1, fixing_days=2, gearings=[0.9], spreads=[0.0015]), 1e6, EV1),
        ("capped and floored", bd.amortizing_cms_bond_spec(2, [100.0, 75.0, 50.0, 25.0, 10.0], s1y, c10y2,
                                                           THIRTY360_BB, p2, fixing_days=2, caps=[0.05],
                                                           floors=[0.02]), 100.0, EV2),
        ("with an issue date", bd.amortizing_cms_bond_spec(3, [100.0, 50.0], s1y, c10y1, THIRTY360_BB, p1,
                                                           fixing_days=2, issue=D(20, 1, 2025)), 100.0, EV1),
        ("seasoned", bd.amortizing_cms_bond_spec(2, [100.0, 90.0, 80.0, 70.0, 60.0], seasoned,
                                                 cms.swap_index((10, Y), EV2, forward=0.027), THIRTY360_BB, p2,
                                                 fixing_days=2), 100.0, EV2),
    ]


def cases():
    rnd = random.Random(v.SEED)
    out = []
    for k, (name, spec, scale, ev) in enumerate(bonds()):
        context = (ev, rnd.choice([False, True]), rnd.choice([None, False, True]))
        rate, price = bd.RATES[k % len(bd.RATES)], bd.PRICES[k % len(bd.PRICES)]
        out.append(bd.run_case(name, spec, scale, context, rate, price, bd.CURVE_RATE, rnd, 12, cms.extra,
                               cms.past_fixings(spec, ev)))
    assert any("fixings" in c["inputs"]["settings"] for c in out)
    assert not any("construct" in c["expected"] for c in out)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/bonds/amortizingcmsratebond.hpp", cases)
