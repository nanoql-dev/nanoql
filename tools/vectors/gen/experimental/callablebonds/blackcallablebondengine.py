# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BlackCallableFixedRateBondEngine (ql/experimental/callablebonds/blackcallablebondengine.hpp,
.cpp), the Black "European bond option" engine for callable bonds with a single call or put date, through its quote
constructor, whose constant volatility is built on the null calendar with no settlement days, its reference date the
evaluation date.

Three bonds: a 5% semiannual fixed-rate bond and a zero-coupon bond on QuantLib's test-runner date (16 September
2015), their curve from the settlement date two TARGET days later, and a 3% fixed-rate bond whose curve starts on the
evaluation date (so its NPV and settlement value differ). Each with one call and, apart, one put, at 100 clean (and at
101 dirty for one date): the business day after settlement, the first, third and eighth coupon dates after it, a date
between two coupons and the coupon date before maturity; each at forward yield volatilities of 5%, 20% and 60%. Then QuantLib's
refusals of two callability dates and of none ("Must have exactly one call/put date to use Black Engine"). A bond whose
one callability has occurred isn't here: QuantLib reads its live callabilities' first date past the end of an empty
vector, which is undefined (nanoql refuses it, Divergences/BlackCallableEngineTests.cs).

A case's outputs are the NPV, the settlement value, the error estimate and valuation date (null where QuantLib throws
"not provided"), and the clean and dirty prices read from them. The wheel's calls:
ql.BlackCallableFixedRateBondEngine(QuoteHandle, YieldTermStructureHandle), a fresh one per case, on the bonds of
tools/vectors/gen/experimental/callablebonds/callablebond.py. Read back by
tests/NanoQL.Tests/Vectors/Experimental/CallableBonds/BlackCallableBondEngineVectorTests.cs, with
tests/NanoQL.Tests/Vectors/CallableBondInputs.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_callablebond", os.path.join(v.GEN_ROOT, "experimental", "callablebonds", "callablebond.py"))
cb = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cb)

D = ql.Date
VOLS = [0.05, 0.2, 0.6]


def bonds():
    """(name, settings, bond builder from a schedule of callabilities, curve, face, exercise dates)."""
    today, cal, settlement, issue, maturity = cb.runner_fixture()
    curve = cb.flat(settlement, 0.03)
    sched = cb.semiannual(issue, maturity)
    # the coupon dates after the bonds' settlement, three TARGET days on (the schedule starts with a one-day stub)
    dates = [d for d in cb.bd.make_schedule(sched) if d > cal.advance(today, 3, ql.Days)]
    exercise = [("the day after settlement", cal.advance(today, 4, ql.Days)),
                ("the first coupon date", dates[0]), ("the third coupon date", dates[2]),
                ("the eighth coupon date", dates[7]), ("a date between coupons", dates[4] + 40),
                ("the coupon date before maturity", dates[-2])]

    def fixed(calls):
        return cb.fixed_spec(3, 10000.0, sched, [0.05], cb.THIRTY360_BB, ql.ModifiedFollowing, 100.0, issue, calls)

    def zero(calls):
        return cb.zero_spec(3, 10000.0, cb.TARGET, maturity, cb.THIRTY360_BB, ql.ModifiedFollowing, 100.0, issue,
                            calls)

    today2 = D(16, 6, 2025)
    issue2, maturity2 = D(16, 6, 2023), D(16, 6, 2033)
    sched2 = cb.bd.schedule_spec(issue2, maturity2, (1, ql.Years), cb.TARGET, ql.Following,
                                 ql.DateGeneration.Backward)
    dates2 = list(cb.bd.make_schedule(sched2))
    curve2 = cb.flat(today2, 0.035, cb.A360)
    exercise2 = [("the day after settlement", ql.TARGET().advance(today2, 3, ql.Days)),
                 ("the first coupon date", dates2[3]), ("the fifth coupon date", dates2[6]),
                 ("a date between coupons", dates2[4] + 100), ("the coupon date before maturity", dates2[-2])]

    def annual(calls):
        return cb.fixed_spec(2, 1e6, sched2, [0.03], cb.A365, ql.Following, 100.0, issue2, calls)

    return [("fixed 5%", cb.plain(today), fixed, curve, 10000.0, exercise),
            ("zero coupon", cb.plain(today), zero, curve, 10000.0, exercise),
            ("annual 3%, curve from today", cb.plain(today2), annual, curve2, 1e6, exercise2)]


def run(name, st, spec, engine, scale):
    """One case: the bond priced on a fresh engine under the settings."""
    with v.case_settings(st[0], include_reference_date_events=st[1], include_todays_cash_flows=st[2]) as s:
        bond = cb.make_bond(spec)
        cb.check_live_types(bond)
        e, keep = cb.make_engine(engine)
        bond.setPricingEngine(e)
        expected = cb.engine_outputs(bond, scale)
        del keep
        return {"name": name, "inputs": {"settings": s, "bond": spec, "engine": engine}, "expected": expected}


def cases():
    out = []
    for name, st, build, curve, scale, exercise in bonds():
        for label, date in exercise:
            for kind, kind_name in ((cb.CALL, "call"), (cb.PUT, "put")):
                prices = [(100.0, cb.CLEAN)] + ([(101.0, cb.DIRTY)] if label == "the third coupon date" else [])
                for amount, price_type in prices:
                    spec = build([cb.callability_spec(amount, price_type, kind, date)])
                    for vol in VOLS:
                        priced = "" if price_type == cb.CLEAN else f" at {amount:g} dirty"
                        out.append(run(f"{name} / a {kind_name} on {label}{priced} / volatility {vol:g}", st,
                                               spec, cb.black(vol, curve), scale))
        dates = [d for _, d in exercise]
        two = build([cb.callability_spec(100.0, cb.CLEAN, cb.CALL, dates[2]),
                     cb.callability_spec(100.0, cb.CLEAN, cb.PUT, dates[3])])
        out.append(run(f"{name} / two callability dates", st, two, cb.black(0.2, curve), scale))
        out.append(run(f"{name} / no callability", st, build([]), cb.black(0.2, curve), scale))
    check_rows(out)
    return out


def check_rows(out):
    """The rows the file must hold: the refusals refused, every other case priced."""
    for c in out:
        refused = c["expected"]["NPV"] == v.THROWS
        assert refused == c["name"].endswith(("two callability dates", "no callability")), c["name"]


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/callablebonds/blackcallablebondengine.hpp", cases)
