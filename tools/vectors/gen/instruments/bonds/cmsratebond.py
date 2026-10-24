# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CmsRateBond (ql/instruments/bonds/cmsratebond.hpp): 8 bonds on Euribor swap indexes
(tools/vectors/gen/indexes/swapindex.py's, on Euribor 6M and 3M forecasting on a flat curve whose reference date is the
evaluation date), each under its own evaluation date with flags drawn with the seed: a 10-year and a 2-year index,
annual and semiannual coupons, a gearing below one with a spread, a cap and a floor, a collar, in-arrears fixing, fixing
days other than the index's with a Preceding payment convention, a redemption above par with an issue date, and a
seasoned bond whose past coupons read stored fixings.

Each bond's coupons are priced by the CMS coupons' pricer (cashflows/cmscoupon.py's: Hagan's analytic pricer on the
standard model with no mean reversion, on a constant 20% volatility fixed on the evaluation date), set once the bond
is built, as QuantLib's CmsRateBond builds its coupons with none. Each case stores a fixing for every coupon fixing
before the evaluation date, 2.9% plus a basis point per coupon, through the swap index. A case's outputs are
tools/vectors/gen/instruments/bond.py's: the inspectors with no date, the inspectors and the prices and yield at 12
dates, and the discounting engine's results, on a flat curve whose reference date is the evaluation date, with the
members that read them; with each cash flow's fixing date and fixing days (null for one that isn't a floating-rate
coupon). Read back by tests/NanoQL.Tests/Vectors/Instruments/CmsRateBondVectorTests.cs."""
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
M, Y = ql.Months, ql.Years
TARGET = bd.TARGET
A360, A365, THIRTY360_BB = bd.A360, bd.A365, bd.cf.THIRTY360_BB
EV1, EV2 = D(16, 6, 2025), D(14, 3, 2026)


def swap_index(tenor, ev, ibor="Euribor6M", forward=0.029):
    return bd.swi.swap_index("EuriborSwapIsdaFixA", tenor, 2, "EUR", TARGET, (1, Y), ql.ModifiedFollowing,
                             THIRTY360_BB, {"type": ibor, "curve": bd.irx.curve_spec(ev, forward)})


def bonds():
    """(name, bond spec, face amount, evaluation date) for the 8 bonds."""
    s1y = bd.schedule_spec(D(20, 1, 2025), D(20, 1, 2030), (1, Y), TARGET, ql.Unadjusted)
    s6m = bd.schedule_spec(D(20, 1, 2025), D(20, 1, 2029), (6, M), TARGET, ql.ModifiedFollowing)
    seasoned = bd.schedule_spec(D(15, 9, 2023), D(15, 9, 2028), (1, Y), TARGET, ql.ModifiedFollowing)
    p1, p2 = bd.cms_pricer_spec(EV1), bd.cms_pricer_spec(EV2)
    c10y1, c10y2, c2y1 = swap_index((10, Y), EV1), swap_index((10, Y), EV2), swap_index((2, Y), EV1, "Euribor3M")
    return [
        ("10Y index, annual", bd.cms_bond_spec(2, 100.0, s1y, c10y1, THIRTY360_BB, p1, fixing_days=2), 100.0, EV1),
        ("2Y index, semiannual", bd.cms_bond_spec(2, 100.0, s6m, c2y1, A360, p1, fixing_days=2), 100.0, EV1),
        ("gearing and spread", bd.cms_bond_spec(2, 1e6, s1y, c10y2, THIRTY360_BB, p2, fixing_days=2, gearings=[0.84],
                                                spreads=[0.001]), 1e6, EV2),
        ("capped and floored", bd.cms_bond_spec(2, 100.0, s1y, c10y1, THIRTY360_BB, p1, fixing_days=2, caps=[0.055],
                                                floors=[0.025]), 100.0, EV1),
        ("in arrears", bd.cms_bond_spec(2, 100.0, s1y, c10y2, THIRTY360_BB, p2, fixing_days=2, in_arrears=True),
         100.0, EV2),
        ("3 fixing days, Preceding", bd.cms_bond_spec(2, 100.0, s6m, c10y1, A365, p1, ql.Preceding, 3), 100.0, EV1),
        ("redemption 101 with an issue date", bd.cms_bond_spec(3, 100.0, s1y, c10y1, THIRTY360_BB, p1, fixing_days=2,
                                                               redemption=101.0, issue=D(20, 1, 2025)), 100.0, EV1),
        ("seasoned", bd.cms_bond_spec(2, 100.0, seasoned, swap_index((10, Y), EV2, forward=0.027), THIRTY360_BB, p2,
                                      fixing_days=2), 100.0, EV2),
    ]


def past_fixings(spec, ev):
    """A function of the bond, built without fixings, returning the fixings its coupons read before the evaluation
    date: 2.9% plus a basis point per coupon, stored through the swap index."""
    def fixings(bond):
        pairs = []
        for i, c in enumerate(bond.cashflows()):
            frc = ql.as_floating_rate_coupon(c)
            if frc is not None and frc.fixingDate() < ev:
                pairs.append((frc.fixingDate(), 0.029 + 0.0001 * i))
        return [(bd.swi.make_swap_index(spec["index"]), pairs)] if pairs else None
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
    for k, (name, spec, scale, ev) in enumerate(bonds()):
        context = (ev, rnd.choice([False, True]), rnd.choice([None, False, True]))
        rate, price = bd.RATES[k % len(bd.RATES)], bd.PRICES[k % len(bd.PRICES)]
        out.append(bd.run_case(name, spec, scale, context, rate, price, bd.CURVE_RATE, rnd, 12, extra,
                               past_fixings(spec, ev)))
    assert any("fixings" in c["inputs"]["settings"] for c in out)
    assert not any("construct" in c["expected"] for c in out)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/bonds/cmsratebond.hpp", cases)
