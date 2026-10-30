# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's convertible bonds (ql/instruments/bonds/convertiblebonds.hpp, .cpp): ConvertibleZeroCouponBond,
ConvertibleFixedCouponBond and ConvertibleFloatingRateBond with Callability and SoftCallability schedules, on the
evaluation date 15 October 2026.

Zero-coupon, fixed-coupon (annual and semiannual, with and without an ex-coupon period) and floating-rate convertibles,
issued two TARGET days after the evaluation date or seasoned, under European, American and Bermudan exercises, with
calls and puts at clean and dirty prices, soft calls with triggers, callabilities before and on the settlement date
(which setupArguments leaves out) and an unsorted schedule whose last callability is before maturity (QuantLib checks the
last only). Each case's outputs are the bond's inspectors without a date (tools/vectors/gen/instruments/bond.py's static
outputs, amounts at the notional of 100), its settlement date, and its NPV and settlement value on the
Cox-Ross-Rubinstein convertible engine with 101 steps, a credit spread of 1% and no dividends on
tools/vectors/gen/processes/blackscholesprocess.py's first process (Merton, flat, constant, on Actual/365 (Fixed)
throughout), which reads the arguments setupArguments computes. The engine prices the bond settling on the
evaluation date ("pricedBond", priced_at_evaluation's spec: "settlementDays" 0, an issue date after the evaluation
date moved to it), where QuantLib's master's spot at the settlement date (#2752, taken ahead of the pin as the upstream
sweep's SWR1 (a) and SWI2 rule) is QuantLib 1.44's, divided by a discount of 1; a lag is checked in
tests/NanoQL.Tests/Divergences/ConvertibleUpstreamFixesTests.cs. Then the refusal of a last callability
after maturity, QuantLib's "last callability date (...) later than maturity (...)", at construction.

Generated and left out: an engine price under an American exercise whose time grid's last time rounds below the tree's
maturity, where QuantLib 1.44 skips the conversion at expiry and nanoql takes upstream's fix after v1.44 (#2878, the S-sr
plan's SSR5; tests/NanoQL.Tests/Divergences/AmericanBoundsOnTimeTests.cs), by the grid's arithmetic, (T/N)·N against T.

A bond spec is {"type", "exercise" ({"type", "dates"}: European on its one date, American from its first to its second,
Bermudan on all), "conversionRatio", "callability" (tools/vectors/gen/experimental/callablebonds/callablebond.py's
callability specs, each with "trigger", null but for a soft call), "issueDate", "settlementDays", "dayCounter",
"schedule" (instruments/bond.py's), "redemption", and for the fixed-coupon bond "coupons" and "exCouponPeriod"
(instruments/bond.py's), for the floating-rate bond "index" (an Ibor index spec), "fixingDays", "spreads" and
"exCouponPeriod"}. The wheel's calls, by QuantLib-SWIG 1.44's bonds.i and callablebond.i: each bond's constructor, every
argument by position; Callability and SoftCallability the C++ constructors; BinomialConvertibleEngine(process, "crr",
101, creditSpread, dividends), QuantLib-Python's factory of the named engines. Read back by
tests/NanoQL.Tests/Vectors/Instruments/ConvertibleBondVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
bd = _load("wheel_bond", "instruments/bond.py")
cb = _load("wheel_callablebond", "experimental/callablebonds/callablebond.py")
ibc = _load("wheel_iborcoupon", "cashflows/iborcoupon.py")
irx = ibc.irx

R = bsp.R
A365, A360 = bsp.A365, bsp.A360
TARGET = bd.TARGET
CAL = ql.TARGET()
CALL, PUT = cb.CALL, cb.PUT
CLEAN, DIRTY = cb.CLEAN, cb.DIRTY
ISSUE = CAL.advance(R, 2, ql.Days)
MATURITY = CAL.advance(ISSUE, 5, ql.Years)
FORECAST = irx.curve_spec(R, 0.035)
E6M = ibc.euribor((6, ql.Months), FORECAST)
CREDIT_SPREAD = 0.01


# ---- specs ----

def exercise_spec(kind, dates):
    return {"type": kind, "dates": [v.enc(d) for d in dates]}


def call_spec(amount, price_type, kind, date, trigger=None):
    return dict(cb.callability_spec(amount, price_type, kind, date), trigger=trigger)


def bond_spec(kind, exercise, ratio, calls, issue, days, schedule, redemption=100.0, dc=A365, coupons=None,
              index=None, fixing_days=None, spreads=None, ex_coupon=None):
    spec = {"type": kind, "exercise": exercise, "conversionRatio": ratio, "callability": list(calls),
            "issueDate": v.enc(issue), "settlementDays": days, "dayCounter": dc, "schedule": schedule,
            "redemption": redemption}
    if kind == "ConvertibleFixedCouponBond":
        spec.update(coupons=list(coupons), exCouponPeriod=ex_coupon)
    elif kind == "ConvertibleFloatingRateBond":
        spec.update(index=index, fixingDays=fixing_days, spreads=list(spreads), exCouponPeriod=ex_coupon)
    return spec


# ---- the wheel's objects ----

def make_exercise(spec):
    dates = [ql.Date(d) for d in spec["dates"]]
    if spec["type"] == "European":
        return ql.EuropeanExercise(dates[0])
    if spec["type"] == "American":
        return ql.AmericanExercise(dates[0], dates[1])
    return ql.BermudanExercise(dates)


def make_callabilities(specs):
    s = ql.CallabilitySchedule()
    for c in specs:
        if c["trigger"] is None:
            s.append(cb.make_callability(c))
        else:
            p = c["price"]
            s.append(ql.SoftCallability(ql.BondPrice(p["amount"], p["type"]), ql.Date(c["date"]), c["trigger"]))
    return s


def make_bond(spec):
    ex = make_exercise(spec["exercise"])
    calls = make_callabilities(spec["callability"])
    issue = bd.date_or_null(spec["issueDate"])
    dc = bd.day_counter(spec["dayCounter"])
    schedule = bd.make_schedule(spec["schedule"])
    if spec["type"] == "ConvertibleZeroCouponBond":
        return ql.ConvertibleZeroCouponBond(ex, spec["conversionRatio"], calls, issue, spec["settlementDays"], dc,
                                            schedule, spec["redemption"])
    # the wheel's constructors take their ex-coupon arguments by position
    kw = bd.ex_coupon_kw(spec["exCouponPeriod"])
    ex_coupon = [kw[k] for k in ("exCouponPeriod", "exCouponCalendar", "exCouponConvention", "exCouponEndOfMonth")
                 if k in kw]
    if spec["type"] == "ConvertibleFixedCouponBond":
        return ql.ConvertibleFixedCouponBond(ex, spec["conversionRatio"], calls, issue, spec["settlementDays"],
                                             spec["coupons"], dc, schedule, spec["redemption"], *ex_coupon)
    return ql.ConvertibleFloatingRateBond(ex, spec["conversionRatio"], calls, issue, spec["settlementDays"],
                                          irx.make_index(spec["index"]), spec["fixingDays"], spec["spreads"], dc,
                                          schedule, spec["redemption"], *ex_coupon)


def priced_at_evaluation(spec):
    """The spec the engine prices: settlement lag 0, and an issue date after the evaluation date moved to it, so that
    the bond settles on the evaluation date (a bond settles on the later of the two), where QuantLib's master's spot
    roll divides by a discount of 1 (#2752); and a callability on the lagged bond's settlement date moved to it, so
    that the tree still applies a callability at its time 0, which QuantLib keeps on the settlement date."""
    issue = spec["issueDate"]
    settlement = make_bond(spec).settlementDate().serialNumber()
    calls = [dict(c, date=v.enc(R)) if c["date"] == settlement else c for c in spec["callability"]]
    return dict(spec, settlementDays=0, issueDate=issue if issue is None or issue <= R.serialNumber() else v.enc(R),
                callability=calls)


def american_grid_short(spec, bond, process, steps):
    """Whether an American exercise's tree, from the bond's settlement over the risk-free curve's year fraction to the
    last exercise date, has its grid's last time below that maturity: (T/N)·N < T, where QuantLib 1.44 skips the
    conversion at expiry (#2878)."""
    if spec["exercise"]["type"] != "American":
        return False
    rf = process.riskFreeRate()
    t = rf.dayCounter().yearFraction(bond.settlementDate(), ql.Date(spec["exercise"]["dates"][-1]))
    return (t / steps) * steps < t


# ---- the bonds ----

def schedule(issue, maturity, tenor, convention=ql.Unadjusted, cal=TARGET):
    return bd.schedule_spec(issue, maturity, tenor, cal, convention, ql.DateGeneration.Backward)


EUROPEAN = exercise_spec("European", [MATURITY])
AMERICAN = exercise_spec("American", [ISSUE, MATURITY])
BERMUDAN = exercise_spec("Bermudan", [CAL.advance(ISSUE, k, ql.Years) for k in (1, 2, 3, 4, 5)])
CALLS = [call_spec(105.0, CLEAN, CALL, CAL.advance(ISSUE, 2, ql.Years)),
         call_spec(103.0, DIRTY, CALL, CAL.advance(ISSUE, 3, ql.Years), trigger=1.3),
         call_spec(98.0, CLEAN, PUT, CAL.advance(ISSUE, 4, ql.Years))]
ONCE = schedule(ISSUE, MATURITY, (0, ql.Years))
ANNUAL = schedule(ISSUE, MATURITY, (1, ql.Years))
SEMIANNUAL = schedule(ISSUE, MATURITY, (6, ql.Months), ql.ModifiedFollowing)
SEASONED_ISSUE = ql.Date(15, ql.March, 2024)
SEASONED_MATURITY = ql.Date(15, ql.March, 2030)
SEASONED = schedule(SEASONED_ISSUE, SEASONED_MATURITY, (1, ql.Years), ql.Following)


def bonds():
    """(name, spec) for the convertibles."""
    out = []
    fixed = "ConvertibleFixedCouponBond"
    zero = "ConvertibleZeroCouponBond"
    floating = "ConvertibleFloatingRateBond"
    for ex_name, ex in (("European", EUROPEAN), ("American", AMERICAN), ("Bermudan", BERMUDAN)):
        out.append((f"zero-coupon, {ex_name}, no callability", bond_spec(zero, ex, 0.9, [], ISSUE, 3, ONCE)))
        out.append((f"zero-coupon, {ex_name}, callable", bond_spec(zero, ex, 0.9, CALLS, ISSUE, 3, ONCE, 102.0)))
        out.append((f"fixed annual 4%, {ex_name}, callable",
                    bond_spec(fixed, ex, 1.0, CALLS, ISSUE, 3, ANNUAL, coupons=[0.04])))
        out.append((f"floating on Euribor 6M + 50bp, {ex_name}, callable",
                    bond_spec(floating, ex, 1.0, CALLS, ISSUE, 2, SEMIANNUAL, dc=A360, index=E6M, fixing_days=2,
                              spreads=[0.005])))
    out.append(("fixed semiannual, rates stepping, ex-coupon 10 days, American",
                bond_spec(fixed, AMERICAN, 1.1, CALLS, ISSUE, 3, SEMIANNUAL, coupons=[0.03, 0.035, 0.04],
                          ex_coupon=bd.ex_coupon_spec(10, ql.Days))))
    out.append(("floating, ex-coupon 5 days, two spreads, Bermudan",
                bond_spec(floating, BERMUDAN, 0.95, CALLS[:2], ISSUE, 2, SEMIANNUAL, dc=A360, index=E6M, fixing_days=2,
                          spreads=[0.004, 0.006], ex_coupon=bd.ex_coupon_spec(5, ql.Days))))
    settlement = CAL.advance(R, 3, ql.Days)
    seasoned_calls = [call_spec(104.0, CLEAN, CALL, ql.Date(15, ql.March, 2026)),
                      call_spec(101.0, DIRTY, PUT, settlement),
                      call_spec(103.0, CLEAN, CALL, ql.Date(15, ql.March, 2027)),
                      call_spec(102.0, CLEAN, CALL, ql.Date(15, ql.March, 2028), trigger=1.2)]
    out.append(("seasoned fixed, callabilities before and on settlement, American",
                bond_spec(fixed, exercise_spec("American", [SEASONED_ISSUE, SEASONED_MATURITY]), 1.0, seasoned_calls,
                          SEASONED_ISSUE, 3, SEASONED, coupons=[0.045])))
    unsorted = [CALLS[2], CALLS[0]]
    out.append(("fixed, unsorted callabilities, European",
                bond_spec(fixed, EUROPEAN, 1.0, unsorted, ISSUE, 3, ANNUAL, coupons=[0.04])))
    out.append(("fixed, redemption 110, no issue date, European",
                bond_spec(fixed, EUROPEAN, 1.0, CALLS, None, 3, ANNUAL, 110.0, coupons=[0.04])))
    return out


def refusals():
    late = [call_spec(105.0, CLEAN, CALL, CAL.advance(MATURITY, 1, ql.Days))]
    return [("refused: last callability after maturity",
             bond_spec("ConvertibleFixedCouponBond", EUROPEAN, 2.0, late, ISSUE, 3, ANNUAL, coupons=[0.04]))]


def cases():
    st = v.settings(R)
    pspec = bsp.PROCESSES[0][1]
    process = bsp.process(pspec)
    out = []
    for name, spec in bonds():
        bond = make_bond(spec)
        expected = bd.static_outputs(bond, 100.0)
        expected["settlementDate"] = v.enc(bond.settlementDate())
        inputs = {"settings": st, "bond": spec, "process": pspec, "creditSpread": CREDIT_SPREAD, "timeSteps": 101}
        priced_spec = priced_at_evaluation(spec)
        priced = make_bond(priced_spec)
        if not american_grid_short(priced_spec, priced, process, 101):
            priced.setPricingEngine(ql.BinomialConvertibleEngine(process, "crr", 101, ql.QuoteHandle(
                ql.SimpleQuote(CREDIT_SPREAD))))
            expected["NPV"] = v.result(priced.NPV)
            expected["settlementValue"] = v.result(priced.settlementValue)
            inputs["pricedBond"] = priced_spec
        out.append({"name": name, "inputs": inputs, "expected": expected})
    for name, spec in refusals():
        out.append({"name": name, "inputs": {"settings": st, "bond": spec},
                    "expected": {"construct": v.attempt(lambda: make_bond(spec))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/bonds/convertiblebonds.hpp", cases)
