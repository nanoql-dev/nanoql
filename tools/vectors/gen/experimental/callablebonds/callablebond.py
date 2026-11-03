# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's callable bonds (ql/experimental/callablebonds/callablebond.hpp, .cpp): CallableFixedRateBond
and CallableZeroCouponBond priced on TreeCallableFixedRateBondEngine with a number of time steps and on
BlackCallableFixedRateBondEngine, and CallableBond's option-adjusted spread family and implied volatility.

The bonds, under these fixtures, most of them QuantLib's test-suite/callablebonds.cpp's:
- 3 June 2004, flat 3.2% (testCached's bond, 10,000 face, 5% semiannual on TARGET): calls at 110 clean on the even
  years, puts at 100 on the odd ones, both, the calls at a dirty price, par calls every year on a log-linear discount
  curve, no callability, an occurred call and a call on the settlement date ahead of live calls (and the same for
  puts: occurred callabilities of the live ones' type only, since QuantLib 1.44 applies a live callability of the other
  type by the occurred one's, which nanoql fixes as QuantLib's master does), testUnexercisableCallability's
  unreachable dirty call and put three days before a coupon date with a clean one on it, a call two days before a
  coupon date (moved to it), a reversion of 1e-12, a Vasicek model with the curve given to the engine, and a call after
  maturity, which QuantLib refuses ("Bond cannot mature before last call/put date");
- 19 August 2004, the same bond settled on its 24 August coupon date, with a call or a put on that date ahead of live
  ones, under includeReferenceDateEvents true and false: QuantLib's arguments read the coupon and the callability on
  the settlement date as occurred by their explicit false, so the two flags give one NPV;
- 16 September 2015, QuantLib's test-runner date, flat 3% (testInterplay's zero-coupon bonds: an out-of-the-money call
  ahead of an in-the-money put, then a later call; an in-the-money put ahead of a call, then a later put), and
  testObservability's replacement: the zero-coupon bond with calls and puts on a 3% curve and on a 4% curve, each with
  its own Hull-White model;
- 15 January 2024, flat 4% on the evaluation date (the wheel survey's bond: 14 par calls on its coupon payment dates
  from 2027, 200 steps), whose spread at its own clean price isn't 0 (QuantLib's NPV basis);
- 31 January 2024 (testOasContinuityThroughExCouponWindow's quarterly bond with a 14-day ex-coupon period): a call
  before the ex-coupon date, one inside the ex-coupon window and one after the payment date; and the same bond on 22
  April 2024, settled inside the window of its 30 April coupon, which its arguments drop, with a call inside the window,
  on the payment date and after it;
- 18 May 2021 (testSnappingExerciseDate2ClosestCouponDate: calls within a week before a coupon date), 10 January 2020
  (testCallableBondOasWithDifferentNotinals' 100 and 25 face amounts, testCallableFixedRateBondWithArbitrarySchedule's
  schedule of dates, which has no tenor) and 30 November 2023 (testEffectiveDurationAndConvexity's bond on a grid of
  the days to maturity over 30 steps);
- for the Black engine, single call and put dates: testImpliedVol's 1% bond, testBlackEngine's zero-coupon bond and
  testBlackEngineDeepInTheMoney's; the 1% bond's implied volatility at an accuracy of 1e-3, and refused after two
  evaluations;
- 16 June 2025, flat 4% on the evaluation date (a 5% semiannual bond to 2030 called at par from its fourth coupon date,
  Hull-White with a reversion of 0.06, 100 steps): the spread for a clean price of 98 converted on the engine's curve
  and on a flat 8%, which serves the conversion only (0.011085320535265009 and 0.011309258864703242), from a guess of
  0.02 at an accuracy of 1e-6, and refused after three evaluations;
- 16 September 2030: a bond called at 100 in September 2019 and maturing on 9 June 2025, expired, on the tree and the
  Black engines, priced on a flat 3% curve dated 2 January 2020: the expired results (an NPV and a settlement value of
  0), and the implied volatility's refusal ("instrument expired"). Its spread family isn't here: QuantLib's prices the
  expired bond's arguments, where nanoql refuses it as the implied volatility is refused
  (tests/NanoQL.Tests/Divergences/ExpiredCallableBondSpreadTests.cs).

A case is one bond, one engine and one evaluation context. Its outputs: the put/call schedule (dates, types, price
amounts and types), every cash flow's date and amount, the settlement and maturity dates, the accrued amount at
settlement and whether the bond has expired; the engine's NPV, settlement value, error estimate and valuation date
(null where QuantLib throws "not provided"), and the clean and dirty prices read from them; on the step engines, the
spread for a clean price (with and without a settlement date), the spread for the engine's own clean price, the clean
price for a spread, and the effective duration and convexity at a spread; on single-date bonds, the implied
volatility for clean and dirty target prices. A refused construction is the case's one output, "construct". Amounts
are compared at the face amount.

The specs here are every callable-bond generator's (the tree and Black engines' files and their harness counterparts,
which import this file):
- a callability is {"price": {"amount", "type"}, "type", "date"};
- a bond is {"type": "CallableFixedRateBond", "settlementDays", "faceAmount", "schedule", "coupons",
  "accrualDayCounter", "paymentConvention", "redemption", "issueDate", "putCallSchedule", "exCouponPeriod"} or
  {"type": "CallableZeroCouponBond", "settlementDays", "faceAmount", "calendar", "maturityDate", "dayCounter",
  "paymentConvention", "redemption", "issueDate", "putCallSchedule"}, with tools/vectors/gen/instruments/bond.py's
  schedule and ex-coupon specs;
- a curve, a model and a time grid are the specs of tools/cxx/src/models/modelspec.hpp and timegrid.hpp: a FlatForward
  or a DiscountCurve with a fixed reference date, a Hull-White or a Vasicek model;
- an engine is {"type": "TreeSteps", "model", "timeSteps", "termStructure"}, {"type": "TreeGrid" | "TreeLattice",
  "model", "timeGrid", "termStructure"} (QuantLib's time-grid engine, which nanoql's lattice overload reproduces from
  the model's tree on the grid; "TreeZeroLattice" is the zero-coupon engine's lattice overload, whose QuantLib
  subclass adds only constructors), a null term structure being QuantLib's empty handle, or {"type": "Black",
  "volatility", "discountCurve"}, the engine's quote constructor.

The wheel's calls: ql.CallableFixedRateBond and ql.CallableZeroCouponBond, every argument passed by name (the wheel
requires the fixed-rate bond's middle four, C++'s defaults); ql.TreeCallableFixedRateBondEngine(model, steps or grid,
handle) and ql.BlackCallableFixedRateBondEngine(QuoteHandle, handle), a fresh engine and model per case (the wheel
survey's caveats 1 and 4); OAS, cleanPriceOAS, effectiveDuration and effectiveConvexity positional, after the NPV
(caveat 2: they call the engine directly), on step engines only (caveat 7); impliedVolatility, which builds its own
Black engine. Read back by tests/NanoQL.Tests/Vectors/Experimental/CallableBonds/CallableBondVectorTests.cs, with
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

_spec = importlib.util.spec_from_file_location("wheel_bond", os.path.join(v.GEN_ROOT, "instruments", "bond.py"))
bd = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bd)

D = ql.Date
TARGET, NULL_CALENDAR = bd.TARGET, bd.NULL_CALENDAR
A365, A360, THIRTY360_BB = bd.A365, bd.A360, bd.THIRTY360_BB
THIRTY360_US = {"type": "Thirty360", "convention": int(ql.Thirty360.USA)}
THIRTY360_ISDA = {"type": "Thirty360", "convention": int(ql.Thirty360.ISDA)}
US_NYSE = {"type": "UnitedStates", "market": int(ql.UnitedStates.NYSE)}
US_FED = {"type": "UnitedStates", "market": int(ql.UnitedStates.FederalReserve)}
US_GOVERNMENT = bd.US_GOVERNMENT
CALL, PUT = int(ql.Callability.Call), int(ql.Callability.Put)
CLEAN, DIRTY = int(ql.BondPrice.Clean), int(ql.BondPrice.Dirty)
CONTINUOUS, COMPOUNDED, SIMPLE = int(ql.Continuous), int(ql.Compounded), int(ql.Simple)
NO_FREQUENCY, ANNUAL, SEMIANNUAL, QUARTERLY = int(ql.NoFrequency), int(ql.Annual), int(ql.Semiannual), \
    int(ql.Quarterly)


# ---- specs ----

def callability_spec(amount, price_type, kind, date):
    return {"price": {"amount": amount, "type": int(price_type)}, "type": int(kind), "date": v.enc(date)}


def fixed_spec(days, face, schedule, coupons, dc, convention=ql.Following, redemption=100.0, issue=None, calls=(),
               ex_coupon=None):
    return {"type": "CallableFixedRateBond", "settlementDays": days, "faceAmount": face, "schedule": schedule,
            "coupons": list(coupons), "accrualDayCounter": dc, "paymentConvention": int(convention),
            "redemption": redemption, "issueDate": v.enc(issue), "putCallSchedule": list(calls),
            "exCouponPeriod": ex_coupon}


def zero_spec(days, face, cal, maturity, dc, convention=ql.Following, redemption=100.0, issue=None, calls=()):
    return {"type": "CallableZeroCouponBond", "settlementDays": days, "faceAmount": face, "calendar": cal,
            "maturityDate": v.enc(maturity), "dayCounter": dc, "paymentConvention": int(convention),
            "redemption": redemption, "issueDate": v.enc(issue), "putCallSchedule": list(calls)}


def flat(reference, forward, dc=A365, compounding=CONTINUOUS, frequency=ANNUAL):
    return {"type": "FlatForward", "referenceDate": v.enc(reference), "forward": forward, "dayCounter": dc,
            "compounding": compounding, "frequency": frequency}


def discount_curve(dates, discounts, dc=A365):
    return {"type": "DiscountCurve", "dates": [v.enc(d) for d in dates], "discounts": list(discounts),
            "dayCounter": dc}


def hull_white(curve, a=0.1, sigma=0.01):
    return {"type": "HullWhite", "curve": curve, "a": a, "sigma": sigma, "params": None}


def vasicek(r0=0.05, a=0.1, b=0.05, sigma=0.01, lam=0.0):
    return {"type": "Vasicek", "r0": r0, "a": a, "b": b, "sigma": sigma, "lambda": lam, "params": None}


def tree_steps(model, steps, ts=None):
    return {"type": "TreeSteps", "model": model, "timeSteps": steps, "termStructure": ts}


def tree_grid(model, times, steps, ts=None, kind="TreeGrid"):
    return {"type": kind, "model": model, "timeGrid": {"times": list(times), "steps": steps}, "termStructure": ts}


def black(vol, curve):
    return {"type": "Black", "volatility": vol, "discountCurve": curve}


# ---- the wheel's objects ----

def make_callability(spec):
    p = spec["price"]
    return ql.Callability(ql.BondPrice(p["amount"], p["type"]), spec["type"], ql.Date(spec["date"]))


def make_schedule_of(calls):
    s = ql.CallabilitySchedule()
    for c in calls:
        s.append(make_callability(c))
    return s


def make_bond(spec):
    calls = make_schedule_of(spec["putCallSchedule"])
    if spec["type"] == "CallableFixedRateBond":
        kw = {"paymentConvention": spec["paymentConvention"], "redemption": spec["redemption"],
              "issueDate": bd.date_or_null(spec["issueDate"]), "putCallSchedule": calls}
        kw.update(bd.ex_coupon_kw(spec["exCouponPeriod"]))
        return ql.CallableFixedRateBond(spec["settlementDays"], spec["faceAmount"], bd.make_schedule(spec["schedule"]),
                                        spec["coupons"], bd.day_counter(spec["accrualDayCounter"]), **kw)
    if spec["type"] == "CallableZeroCouponBond":
        return ql.CallableZeroCouponBond(spec["settlementDays"], spec["faceAmount"], bd.calendar(spec["calendar"]),
                                         ql.Date(spec["maturityDate"]), bd.day_counter(spec["dayCounter"]),
                                         paymentConvention=spec["paymentConvention"],
                                         redemption=spec["redemption"],
                                         issueDate=bd.date_or_null(spec["issueDate"]), putCallSchedule=calls)
    raise ValueError(f"unknown bond {spec['type']}")


def make_curve(spec):
    if spec["type"] == "FlatForward":
        return ql.FlatForward(ql.Date(spec["referenceDate"]), spec["forward"], bd.day_counter(spec["dayCounter"]),
                              spec["compounding"], spec["frequency"])
    if spec["type"] == "DiscountCurve":
        return ql.DiscountCurve([ql.Date(d) for d in spec["dates"]], spec["discounts"],
                                bd.day_counter(spec["dayCounter"]))
    raise ValueError(f"unknown curve {spec['type']}")


def handle(spec):
    return ql.YieldTermStructureHandle() if spec is None else ql.YieldTermStructureHandle(make_curve(spec))


def make_model(spec):
    if spec["type"] == "HullWhite":
        return ql.HullWhite(handle(spec["curve"]), spec["a"], spec["sigma"])
    if spec["type"] == "Vasicek":
        return ql.Vasicek(spec["r0"], spec["a"], spec["b"], spec["sigma"], spec["lambda"])
    raise ValueError(f"unknown model {spec['type']}")


def make_engine(spec):
    """A fresh engine, with the objects it keeps alive."""
    t = spec["type"]
    if t == "Black":
        quote = ql.QuoteHandle(ql.SimpleQuote(spec["volatility"]))
        curve = handle(spec["discountCurve"])
        return ql.BlackCallableFixedRateBondEngine(quote, curve), (quote, curve)
    model = make_model(spec["model"])
    ts = handle(spec["termStructure"])
    if t == "TreeSteps":
        return ql.TreeCallableFixedRateBondEngine(model, spec["timeSteps"], ts), (model, ts)
    if t in ("TreeGrid", "TreeLattice", "TreeZeroLattice"):
        g = spec["timeGrid"]
        return ql.TreeCallableFixedRateBondEngine(model, ql.TimeGrid(g["times"], g["steps"]), ts), (model, ts)
    raise ValueError(f"unknown engine {t}")


def trading_ex_coupon(cf, date):
    """CashFlow::tradingExCoupon, which the wheel doesn't bind: on or after a coupon's ex-coupon date."""
    c = ql.as_coupon(cf)
    ecd = c.exCouponDate() if c is not None else ql.Date()
    return ecd != ql.Date() and ecd <= date


def mandatory_times(spec, curve_spec, settings_date):
    """DiscretizedCallableFixedRateBond's mandatory times (the wheel survey's caveat 6, its asset being unreachable):
    the redemption's, the coupons' neither paid nor ex-coupon at settlement, and the live callabilities' (snapped to a
    coupon within the next week), on the curve's day counter from its reference date."""
    bond = make_bond(spec)
    curve = make_curve(curve_spec)
    dc, ref = curve.dayCounter(), curve.referenceDate()
    settlement = bond.settlementDate()
    flows = list(bond.cashflows())
    # the wheel's hasOccurred takes no flag: with includeReferenceDateEvents false it reads QuantLib's false
    assert not ql.Settings.instance().includeReferenceDateEvents
    coupons = [cf for cf in flows[:-1] if not cf.hasOccurred(settlement) and not trading_ex_coupon(cf, settlement)]
    coupon_dates = [cf.date() for cf in coupons]
    coupon_times = [dc.yearFraction(ref, d) for d in coupon_dates]
    times = []
    t = dc.yearFraction(ref, bond.redemption().date())
    if t >= 0.0:
        times.append(t)
    times += [t for t in coupon_times if t >= 0.0]
    for c in bond.callability():
        if c.date() <= settlement:  # Event::hasOccurred(settlement, false)
            continue
        cd = c.date()
        ct = dc.yearFraction(ref, cd)
        for j, tc in enumerate(coupon_times):
            if ct <= tc <= ct + 1.0 / 52 and cd < coupon_dates[j]:
                ct = tc
                break
        if ct >= 0.0:
            times.append(ct)
    return times


def check_live_types(bond):
    """QuantLib 1.44 reads the i-th live callability's type from the i-th entry of the whole schedule, which QuantLib's
    master fixed and nanoql fixes as it does (PR #2875): a vector holds no schedule where the two readings differ,
    so both oracles, which carry 1.44's index, agree with the fix."""
    schedule = list(bond.callability())
    live = [c for c in schedule if c.date() > bond.settlementDate()]  # not Event::hasOccurred(settlement, false)
    for i, c in enumerate(live):
        assert schedule[i].type() == c.type(), "a schedule QuantLib 1.44 prices with another callability's type"


# ---- the outputs ----

def scaled(f, scale):
    return bd.scaled(f, scale)


def bond_outputs(bond, scale):
    calls = list(bond.callability())
    return {"callabilityDates": [v.enc(c.date()) for c in calls],
            "callabilityTypes": [int(c.type()) for c in calls],
            "callabilityPrices": [c.price().amount() for c in calls],
            "callabilityPriceTypes": [int(c.price().type()) for c in calls],
            "cashflowDates": [v.enc(c.date()) for c in bond.cashflows()],
            "cashflowAmounts": [scaled(c.amount, scale) for c in bond.cashflows()],
            "settlementDate": v.enc(bond.settlementDate()),
            "maturityDate": v.enc(bond.maturityDate()),
            "accruedAmount": v.attempt(bond.accruedAmount),
            "isExpired": bond.isExpired()}


def engine_outputs(bond, scale):
    return {"NPV": scaled(bond.NPV, scale),
            "settlementValue": scaled(lambda: v.result(lambda: bond.settlementValue()), scale),
            "errorEstimate": v.attempt(lambda: v.result(bond.errorEstimate)),
            "valuationDate": v.attempt(lambda: v.result(bond.valuationDate)),
            "cleanPrice": v.attempt(bond.cleanPrice),
            "dirtyPrice": v.attempt(bond.dirtyPrice)}


# The effective convexity's recorded tolerance: a second difference of tree prices over bump^2, which amplifies the
# prices' last-bit moves by about 1/bump^2; the Ornstein-Uhlenbeck variance's cancellation-free form, nanoql's, moves it
# by up to 8.2e-9 relative.
CONVEXITY_TOLERANCE = 1e-7


CONVEXITY_REASON = ("a second difference of tree prices over bump^2, which the Ornstein-Uhlenbeck variance's "
                    "cancellation-free form moves by up to 8.2e-9 relative")


def convexity(x):
    return ({"value": x, "tolerance": CONVEXITY_TOLERANCE, "reason": CONVEXITY_REASON} if isinstance(x, float)
            else x)


def oas_spec(curve, dc, compounding, frequency, clean_price, settlement=None, spread=0.01, bump=2e-4,
             accuracy=1.0e-10, max_iterations=100, guess=0.0):
    return {"curve": curve, "dayCounter": dc, "compounding": compounding, "frequency": frequency,
            "cleanPrice": clean_price, "settlementDate": v.enc(settlement), "accuracy": accuracy,
            "maxIterations": max_iterations, "guess": guess, "spread": spread, "bump": bump}


def oas_outputs(bond, spec):
    """The spread family, on the bond's attached step engine: each call runs it directly, whatever the bond cached."""
    h = handle(spec["curve"])
    dc = bd.day_counter(spec["dayCounter"])
    comp, freq = spec["compounding"], spec["frequency"]
    settlement = bd.date_or_null(spec["settlementDate"])
    s, bump = spec["spread"], spec["bump"]
    return {"OAS": v.attempt(lambda: bond.OAS(spec["cleanPrice"], h, dc, comp, freq, settlement, spec["accuracy"],
                                              spec["maxIterations"], spec["guess"])),
            "OASAtCleanPrice": v.attempt(lambda: bond.OAS(bond.cleanPrice(), h, dc, comp, freq)),
            "cleanPriceOAS": v.attempt(lambda: bond.cleanPriceOAS(s, h, dc, comp, freq, settlement)),
            "effectiveDuration": v.attempt(lambda: bond.effectiveDuration(s, h, dc, comp, freq, bump)),
            "effectiveConvexity": convexity(v.attempt(lambda: bond.effectiveConvexity(s, h, dc, comp, freq, bump)))}


def implied_spec(curve, targets, accuracy=1e-8, max_evaluations=200, min_vol=1e-4, max_vol=1.0):
    return {"discountCurve": curve, "targets": [{"amount": a, "type": int(t)} for a, t in targets],
            "accuracy": accuracy, "maxEvaluations": max_evaluations, "minVol": min_vol, "maxVol": max_vol}


def implied_outputs(bond, spec):
    h = handle(spec["discountCurve"])
    return {"impliedVolatility": [v.attempt(lambda t=t: bond.impliedVolatility(
        ql.BondPrice(t["amount"], t["type"]), h, spec["accuracy"], spec["maxEvaluations"], spec["minVol"],
        spec["maxVol"])) for t in spec["targets"]]}


def run_case(name, settings, spec, engine, scale, oas=None, implied=None):
    """One case: the bond built and priced under the settings, with the spread family and the implied volatility
    when their specs are given, or the one output "construct" where QuantLib refuses the bond."""
    with v.case_settings(*settings[:1], include_reference_date_events=settings[1],
                         include_todays_cash_flows=settings[2]) as st:
        inputs = {"settings": st, "bond": spec, "engine": engine}
        try:
            bond = make_bond(spec)
        except RuntimeError:
            return {"name": name, "inputs": inputs, "expected": {"construct": dict(v.THROWS)}}
        check_live_types(bond)
        expected = bond_outputs(bond, scale)
        e, keep = make_engine(engine)
        bond.setPricingEngine(e)
        expected.update(engine_outputs(bond, scale))
        if oas is not None:
            inputs["oas"] = oas
            expected.update(oas_outputs(bond, oas))
        if implied is not None:
            inputs["implied"] = implied
            expected.update(implied_outputs(bond, implied))
        del keep
        return {"name": name, "inputs": inputs, "expected": expected}


# ---- the fixtures ----

def semiannual(issue, maturity, cal=TARGET, convention=ql.ModifiedFollowing):
    return bd.schedule_spec(issue, maturity, (6, ql.Months), cal, convention, ql.DateGeneration.Backward)


def cached_fixture():
    """testCached's: 3 June 2004, a flat 3.2% curve from the settlement date three TARGET days later."""
    today = D(3, 6, 2004)
    cal = ql.TARGET()
    settlement = cal.advance(today, 3, ql.Days)
    issue = cal.adjust(today - ql.Period(100, ql.Days))
    maturity = cal.advance(issue, 10, ql.Years)
    return today, cal, settlement, issue, maturity


def runner_fixture(today=D(16, 9, 2015), days=2):
    """callablebonds.cpp's Globals on its test runner's date: the curve from the settlement date two TARGET days
    later, the bond issued 100 days earlier and maturing ten years after."""
    cal = ql.TARGET()
    settlement = cal.advance(today, days, ql.Days)
    issue = cal.adjust(today - ql.Period(100, ql.Days))
    maturity = cal.advance(issue, 10, ql.Years)
    return today, cal, settlement, issue, maturity


def years(cal, issue, rng):
    return [cal.advance(issue, i, ql.Years) for i in rng]


def calls(dates, amount, kind=CALL, price_type=CLEAN):
    return [callability_spec(amount, price_type, kind, d) for d in dates]


def plain(settings_date):
    return (settings_date, False, None)


def cached_cases():
    today, cal, settlement, issue, maturity = cached_fixture()
    sched = semiannual(issue, maturity)
    curve = flat(settlement, 0.032)
    hw = hull_white(curve)
    even, odd = years(cal, issue, range(2, 10, 2)), years(cal, issue, range(1, 10, 2))
    call110 = calls(even, 110.0)
    put100 = calls(odd, 100.0, PUT)
    both = sorted(call110 + put100, key=lambda c: c["date"])

    def bond(cs, face=10000.0):
        return fixed_spec(3, face, sched, [0.05], THIRTY360_BB, ql.ModifiedFollowing, 100.0, issue, cs)

    coupon_dates = list(bd.make_schedule(sched))
    snapped = [callability_spec(101.0, CLEAN, CALL, cal.advance(coupon_dates[6], -2, ql.Days))]
    unexercisable_call = [callability_spec(200.0, DIRTY, CALL, coupon_dates[8] - 3),
                          callability_spec(100.0, CLEAN, CALL, coupon_dates[8])]
    unexercisable_put = [callability_spec(0.0, DIRTY, PUT, coupon_dates[8] - 3),
                         callability_spec(110.0, CLEAN, PUT, coupon_dates[8])]
    occurred_calls = calls([today - 30], 105.0) + call110
    on_settlement_calls = calls([settlement], 100.0) + call110
    on_settlement_puts = calls([settlement], 100.0, PUT) + put100
    discounts = discount_curve([settlement] + [cal.advance(today, n, ql.Years) for n in (1, 2, 3, 5, 7, 10, 15)],
                               [1.0, 0.9712, 0.9401, 0.9083, 0.8452, 0.7841, 0.6987, 0.5703])
    par_calls = calls(years(cal, issue, range(1, 10)), 100.0)
    oas = oas_spec(curve, A365, COMPOUNDED, SEMIANNUAL, 104.0, None, 0.0125)
    st = plain(today)
    return [
        ("cached / calls at 110, 240 steps", st, bond(call110), tree_steps(hw, 240, curve), 10000.0, oas),
        ("cached / puts at 100, 240 steps", st, bond(put100), tree_steps(hw, 240, curve), 10000.0,
         oas_spec(curve, A365, CONTINUOUS, NO_FREQUENCY, 112.0, settlement + 7, 0.004)),
        ("cached / calls and puts, 240 steps", st, bond(both), tree_steps(hw, 240, curve), 10000.0, None),
        ("cached / calls at a dirty 110, 100 steps", st, bond(calls(even, 110.0, CALL, DIRTY)),
         tree_steps(hw, 100, curve), 10000.0, None),
        ("cached / par calls every year, discount curve", st, bond(par_calls),
         tree_steps(hull_white(discounts, 0.03, 0.012), 100, curve), 10000.0,
         oas_spec(discounts, A365, COMPOUNDED, ANNUAL, 99.0, None, -0.002, 1e-3)),
        ("cached / no callability", st, bond([]), tree_steps(hw, 240, curve), 10000.0, None),
        ("cached / an occurred call ahead of live calls", st, bond(occurred_calls), tree_steps(hw, 120, curve),
         10000.0, None),
        ("cached / a call on the settlement date ahead of live calls", st, bond(on_settlement_calls),
         tree_steps(hw, 120, curve), 10000.0, None),
        ("cached / a put on the settlement date ahead of live puts", st, bond(on_settlement_puts),
         tree_steps(hw, 120, curve), 10000.0, None),
        ("cached / an unreachable dirty call before a coupon date", st, bond(unexercisable_call),
         tree_steps(hw, 240, curve), 10000.0, None),
        ("cached / an unreachable dirty put before a coupon date", st, bond(unexercisable_put),
         tree_steps(hw, 240, curve), 10000.0, None),
        ("cached / a call two days before a coupon date", st, bond(snapped), tree_steps(hw, 60, curve), 10000.0,
         oas_spec(curve, A365, COMPOUNDED, SEMIANNUAL, 103.0, None, 0.01)),
        ("cached / a reversion of 1e-12", st, bond(call110), tree_steps(hull_white(curve, 1e-12, 0.003), 60, curve),
         10000.0, None),
        ("cached / Vasicek with the engine's curve", st, bond(call110),
         tree_steps(vasicek(0.03, 0.1, 0.05, 0.01), 100, curve), 10000.0,
         oas_spec(curve, A365, COMPOUNDED, SEMIANNUAL, 104.0, None, 0.0125)),
        ("cached / the same call on a 100 face amount", st, bond(call110, 100.0), tree_steps(hw, 240, curve), 100.0,
         oas),
        ("cached / a call after maturity", st, bond(call110 + calls([maturity + 1], 100.0)),
         tree_steps(hw, 240, curve), 10000.0, None),
    ]


def runner_cases():
    today, cal, settlement, issue, maturity = runner_fixture()
    curve3, curve4 = flat(settlement, 0.03), flat(settlement, 0.04)
    hw3, hw4 = hull_white(curve3), hull_white(curve4)

    def zero(cs, face=100.0):
        return zero_spec(3, face, TARGET, maturity, THIRTY360_BB, ql.ModifiedFollowing, 100.0, issue, cs)

    y4, y6, y8 = (cal.advance(issue, n, ql.Years) for n in (4, 6, 8))
    case1 = [callability_spec(100.0, CLEAN, CALL, y4), callability_spec(1000.0, CLEAN, PUT, y6)]
    case3 = [callability_spec(100.0, CLEAN, PUT, y4), callability_spec(10.0, CLEAN, CALL, y6)]
    even, odd = years(cal, issue, range(2, 10, 2)), years(cal, issue, range(1, 10, 2))
    observability = calls(even, 110.0) + calls(odd, 90.0, PUT)
    sched = semiannual(issue, maturity)
    consistent = fixed_spec(3, 100.0, sched, [0.05], THIRTY360_BB, ql.ModifiedFollowing, 100.0, issue,
                            calls(even, 110.0))
    st = plain(today)
    return [
        ("interplay / an out-of-the-money call ahead of an in-the-money put", st, zero(case1),
         tree_steps(hw3, 240, curve3), 100.0, None),
        ("interplay / the same, and a later call", st, zero(case1 + [callability_spec(100.0, CLEAN, CALL, y8)]),
         tree_steps(hw3, 240, curve3), 100.0, None),
        ("interplay / an in-the-money put ahead of a call", st, zero(case3), tree_steps(hw3, 240, curve3), 100.0,
         None),
        ("interplay / the same, and a later put", st, zero(case3 + [callability_spec(100.0, CLEAN, PUT, y8)]),
         tree_steps(hw3, 240, curve3), 100.0, None),
        ("observability / calls and puts on a 3% curve", st, zero(observability), tree_steps(hw3, 240, curve3),
         100.0, oas_spec(curve3, A365, COMPOUNDED, SEMIANNUAL, 86.0, None, 0.005)),
        ("observability / the same on a rebuilt 4% curve and its model", st, zero(observability),
         tree_steps(hw4, 240, curve4), 100.0, oas_spec(curve4, A365, COMPOUNDED, SEMIANNUAL, 86.0, None, 0.005)),
        ("consistency / calls at 110 on a 5% bond", st, consistent, tree_steps(hull_white(flat(settlement, 0.032)),
                                                                             240, None), 100.0, None),
    ]


def survey_cases():
    """The wheel survey's bond: QuantLib's spread at the engine's own clean price is not 0."""
    today = D(15, 1, 2024)
    issue, maturity = D(15, 1, 2022), D(15, 1, 2034)
    sched = bd.schedule_spec(issue, maturity, (6, ql.Months), TARGET, ql.Unadjusted, ql.DateGeneration.Backward)
    plain_bond = ql.FixedRateBond(2, 100.0, bd.make_schedule(sched), [0.05], ql.Thirty360(ql.Thirty360.BondBasis),
                                  ql.Following, 100.0, issue)
    pay = [c.date() for c in plain_bond.cashflows()][:-1]
    call_dates = [d for d in pay if D(15, 1, 2027) <= d < maturity]
    curve = flat(today, 0.04)
    model = hull_white(curve, 0.03, 0.012)
    spec = fixed_spec(2, 100.0, sched, [0.05], THIRTY360_BB, ql.Following, 100.0, issue, calls(call_dates, 100.0))
    st = plain(today)
    return [
        ("survey / 14 par calls, 200 steps, compounded semiannually", st, spec, tree_steps(model, 200, curve), 100.0,
         oas_spec(curve, THIRTY360_BB, COMPOUNDED, SEMIANNUAL, 97.9169571348849, None, 0.0)),
        ("survey / the same, continuous", st, spec, tree_steps(model, 200, curve), 100.0,
         oas_spec(curve, THIRTY360_BB, CONTINUOUS, NO_FREQUENCY, 97.9169571348849, None, 0.0042)),
        ("survey / the same, simple", st, spec, tree_steps(model, 200, curve), 100.0,
         oas_spec(curve, THIRTY360_BB, SIMPLE, ANNUAL, 99.0, None, -0.001)),
    ]


def ex_coupon_cases():
    """testOasContinuityThroughExCouponWindow's bond, a call before, inside and after the ex-coupon window."""
    today = D(31, 1, 2024)
    maturity = D(31, 1, 2029)
    sched = bd.schedule_spec(today, maturity, (3, ql.Months), US_NYSE, ql.Unadjusted, ql.DateGeneration.Backward,
                             True)
    ex = bd.ex_coupon_spec(14, ql.Days, NULL_CALENDAR, ql.Unadjusted)
    first = bd.make_schedule(sched)[1]
    ex_date = first - ql.Period(14, ql.Days)
    curve = flat(today, 0.04, THIRTY360_BB)
    model = hull_white(curve)
    out = []
    st = plain(today)
    for label, call in (("before the ex-coupon date", ex_date - 3), ("inside the ex-coupon window", ex_date + 5),
                        ("on the payment date", first), ("after the payment date", first + 4)):
        spec = fixed_spec(0, 100.0, sched, [0.06], THIRTY360_BB, ql.Unadjusted, 100.0, today,
                          calls([call], 100.0), ex)
        out.append((f"ex-coupon / a call {label}", st, spec, tree_steps(model, 100, curve), 100.0,
                    oas_spec(curve, THIRTY360_BB, COMPOUNDED, QUARTERLY, 100.0, None, 0.015)))
    return out


def snapping_cases():
    """testSnappingExerciseDate2ClosestCouponDate's bond: calls within a week before the 14 February 2022 coupon."""
    today = D(18, 5, 2021)
    issue = D(20, 5, 2021) - ql.Period(2 * 366, ql.Days)
    maturity = D(14, 2, 2026)
    sched = bd.schedule_spec(issue, maturity, (6, ql.Months), US_FED, ql.Unadjusted, ql.DateGeneration.Backward,
                             False)
    n = len(bd.make_schedule(sched))
    curve = flat(today, 0.02)
    model = hull_white(curve, 1e-12, 0.003)
    out = []
    st = plain(today)
    for days in (-7, -3, 0, 2):
        call = D(14, 2, 2022) + days
        spec = fixed_spec(2, 100.0, sched, [0.05] * (n - 1), THIRTY360_US, ql.Following, 100.0, issue,
                          calls([call], 100.0))
        out.append((f"snapping / a call {days} days from the coupon date", st, spec, tree_steps(model, 40), 100.0,
                    oas_spec(curve, THIRTY360_US, CONTINUOUS, SEMIANNUAL, 99.0, None, 0.02)))
    return out


def notionals_cases():
    """testCallableBondOasWithDifferentNotinals' bonds, and testCallableFixedRateBondWithArbitrarySchedule's."""
    today, cal, settlement, issue, maturity = runner_fixture(D(10, 1, 2020))
    curve = flat(settlement, 0.03)
    model = hull_white(curve)
    sched = semiannual(issue, maturity)
    dates = list(bd.make_schedule(sched))
    first_call, last_call = dates[len(dates) - 5], dates[len(dates) - 2]
    call_dates = [d for d in dates if first_call <= d <= last_call]
    out = []
    st = plain(today)
    for face in (100.0, 25.0):
        spec = fixed_spec(2, face, sched, [0.055], A365, ql.ModifiedFollowing, 100.0, issue, calls(call_dates, 100.0))
        out.append((f"notionals / face amount {face:g}", st, spec, tree_steps(model, 240, curve), face,
                    oas_spec(curve, A365, COMPOUNDED, SEMIANNUAL, 96.0, None, 0.03)))
    arbitrary = bd.dates_schedule_spec([D(20, 2, 2020), D(15, 8, 2020), D(25, 9, 2021), D(27, 1, 2022)], TARGET)
    spec = fixed_spec(2, 100.0, arbitrary, [0.06], A365, ql.ModifiedFollowing, 100.0, issue,
                      calls([D(25, 9, 2021)], 100.0))
    out.append(("arbitrary schedule / a schedule of dates, with no tenor", st, spec, tree_steps(model, 240, curve),
                100.0, oas_spec(curve, A365, COMPOUNDED, SEMIANNUAL, 102.0, None, 0.01)))
    return out


def duration_cases():
    """testEffectiveDurationAndConvexity's bond: a 30-day grid to maturity, the curve two settlement days on."""
    today = D(30, 11, 2023)
    effective, maturity, first = D(20, 5, 2021), D(1, 6, 2029), D(1, 12, 2021)
    cal = ql.UnitedStates(ql.UnitedStates.GovernmentBond)
    sched = bd.schedule_spec(effective, maturity, (6, ql.Months), US_GOVERNMENT, ql.Unadjusted,
                             ql.DateGeneration.Backward, True, first)
    cs = [callability_spec(102.438, CLEAN, CALL, D(1, 6, 2024)), callability_spec(101.219, CLEAN, CALL, D(1, 6, 2025)),
          callability_spec(100.0, CLEAN, CALL, D(1, 6, 2026)), callability_spec(100.0, CLEAN, CALL, D(1, 6, 2029))]
    spec = fixed_spec(2, 100.0, sched, [0.04875], THIRTY360_ISDA, ql.Unadjusted, 100.0, effective, cs)
    curve = flat(cal.advance(today, 2, ql.Days), 0.05, THIRTY360_ISDA)
    model = hull_white(curve, 0.03, 0.012)
    steps = (maturity - today) // 30
    st = plain(today)
    return [("duration / four calls, the days to maturity over 30 steps", st, spec, tree_steps(model, steps), 100.0,
             oas_spec(curve, THIRTY360_ISDA, COMPOUNDED, SEMIANNUAL, 70.926, today, 0.05, 0.001))]


def black_cases():
    """Single call and put dates, on the Black engine, with their implied volatilities."""
    today, cal, settlement, issue, maturity = runner_fixture()
    curve = flat(settlement, 0.03)
    sched = semiannual(issue, maturity)
    at8 = bd.make_schedule(sched)[8]

    def one(kind, coupon, amount=100.0):
        return fixed_spec(3, 10000.0, sched, [coupon], THIRTY360_BB, ql.ModifiedFollowing, 100.0, issue,
                          [callability_spec(amount, CLEAN, kind, at8)])

    implied = implied_spec(curve, [(78.5, DIRTY), (78.5, CLEAN), (80.0, CLEAN)])
    st = plain(today)
    out = [
        ("Black / a 1% bond called at 100", st, one(CALL, 0.01), black(0.2, curve), 10000.0, None, implied),
        ("Black / a 1% bond put at 100", st, one(PUT, 0.01), black(0.25, curve), 10000.0, None,
         implied_spec(curve, [(95.0, DIRTY), (94.8, CLEAN), (82.0, DIRTY)], 1e-8, 200, 1e-4, 1.5)),
        ("Black / a 5% bond called at 102", st, one(CALL, 0.05, 102.0), black(0.15, curve), 10000.0, None,
         implied_spec(curve, [(104.0, CLEAN)])),
    ]
    t2, cal2, s2, issue2, mat2 = runner_fixture(D(20, 9, 2022), 3)
    curve2, curve5 = flat(s2, 0.03), flat(s2, 0.05)
    zero = zero_spec(3, 10000.0, TARGET, mat2, THIRTY360_BB, ql.ModifiedFollowing, 100.0, issue2,
                     [callability_spec(100.0, CLEAN, CALL, cal2.advance(issue2, 4, ql.Years))])
    sched2 = semiannual(issue2, mat2)
    deep = fixed_spec(3, 10000.0, sched2, [0.0], THIRTY360_BB, ql.ModifiedFollowing, 100.0, issue2,
                      [callability_spec(50.0, CLEAN, CALL, bd.make_schedule(sched2)[6])])
    st2 = plain(t2)
    out += [
        ("Black / testBlackEngine's zero-coupon bond", st2, zero, black(0.3, curve2), 10000.0, None,
         implied_spec(curve2, [(74.0, CLEAN), (74.6, DIRTY)])),
        ("Black / deep in the money", st2, deep, black(1e-10, curve5), 10000.0, None, None),
    ]
    out += [
        ("Black / the 1% bond's implied volatility at an accuracy of 1e-3", st, one(CALL, 0.01), black(0.2, curve),
         10000.0, None, implied_spec(curve, [(78.5, DIRTY), (80.0, CLEAN)], 1e-3)),
        ("Black / the same refused after two evaluations", st, one(CALL, 0.01), black(0.2, curve), 10000.0, None,
         implied_spec(curve, [(78.5, DIRTY), (80.0, CLEAN)], 1e-8, 2)),
    ]
    return out


def reference_date_cases():
    """testCached's bond settled on its 24 August 2004 coupon date, with a call or a put on that date ahead of live ones
    of its type, under includeReferenceDateEvents true and false."""
    _, cal, _, issue, maturity = cached_fixture()
    today = D(19, 8, 2004)
    settlement = cal.advance(today, 3, ql.Days)
    sched = semiannual(issue, maturity)
    assert settlement in list(bd.make_schedule(sched)), settlement
    curve = flat(settlement, 0.032)
    hw = hull_white(curve)
    call110 = calls(years(cal, issue, range(2, 10, 2)), 110.0)
    put100 = calls(years(cal, issue, range(1, 10, 2)), 100.0, PUT)

    def bond(cs):
        return fixed_spec(3, 10000.0, sched, [0.05], THIRTY360_BB, ql.ModifiedFollowing, 100.0, issue, cs)

    out = []
    for flag in (True, False):
        st = (today, flag, None)
        out += [
            (f"reference date / a coupon and a call on the settlement date ahead of live calls, flag {flag}", st,
             bond(calls([settlement], 100.0) + call110), tree_steps(hw, 120, curve), 10000.0, None),
            (f"reference date / a coupon and a put on the settlement date ahead of live puts, flag {flag}", st,
             bond(calls([settlement], 100.0, PUT) + put100), tree_steps(hw, 120, curve), 10000.0, None),
        ]
    return out


def ex_coupon_window_cases():
    """testOasContinuityThroughExCouponWindow's bond settled inside the ex-coupon window of its first coupon."""
    issue, today = D(31, 1, 2024), D(22, 4, 2024)
    maturity = D(31, 1, 2029)
    sched = bd.schedule_spec(issue, maturity, (3, ql.Months), US_NYSE, ql.Unadjusted, ql.DateGeneration.Backward,
                             True)
    ex = bd.ex_coupon_spec(14, ql.Days, NULL_CALENDAR, ql.Unadjusted)
    first = bd.make_schedule(sched)[1]
    assert first - ql.Period(14, ql.Days) <= today < first, (first, today)
    curve = flat(today, 0.04, THIRTY360_BB)
    model = hull_white(curve)
    out = []
    st = plain(today)
    for label, call in (("inside the window", today + 3), ("on the payment date", first),
                        ("after the payment date", first + 4)):
        spec = fixed_spec(0, 100.0, sched, [0.06], THIRTY360_BB, ql.Unadjusted, 100.0, issue,
                          calls([call], 100.0), ex)
        out.append((f"ex-coupon / settled inside the window, a call {label}", st, spec, tree_steps(model, 100, curve),
                    100.0, oas_spec(curve, THIRTY360_BB, COMPOUNDED, QUARTERLY, 100.0, None, 0.015)))
    return out


def solver_cases():
    """The solvers' inputs, on a 5% semiannual bond to 2030 called at par from its fourth coupon date."""
    today = D(16, 6, 2025)
    issue, maturity = today, D(16, 6, 2030)
    sched = bd.schedule_spec(issue, maturity, (6, ql.Months), TARGET, ql.Unadjusted, ql.DateGeneration.Backward, False)
    dates = list(bd.make_schedule(sched))
    curve, flat8 = flat(today, 0.04), flat(today, 0.08)
    spec = fixed_spec(3, 100.0, sched, [0.05], THIRTY360_BB, ql.Following, 100.0, issue, calls(dates[4:-1], 100.0))
    engine = tree_steps(hull_white(curve, 0.06, 0.01), 100, curve)
    st = plain(today)
    return [
        ("solver / the spread converted on the engine's curve", st, spec, engine, 100.0,
         oas_spec(curve, A365, COMPOUNDED, SEMIANNUAL, 98.0)),
        ("solver / the spread converted on a flat 8%", st, spec, engine, 100.0,
         oas_spec(flat8, A365, COMPOUNDED, SEMIANNUAL, 98.0)),
        ("solver / the spread from a guess of 0.02 at an accuracy of 1e-6", st, spec, engine, 100.0,
         oas_spec(curve, A365, COMPOUNDED, SEMIANNUAL, 98.0, accuracy=1e-6, guess=0.02)),
        ("solver / the spread refused after three evaluations", st, spec, engine, 100.0,
         oas_spec(curve, A365, COMPOUNDED, SEMIANNUAL, 98.0, max_iterations=3)),
    ]


def expired_cases():
    """An expired bond on each engine: the expired results, and the implied volatility's refusal."""
    runner, today = D(16, 9, 2015), D(16, 9, 2030)
    cal = ql.TARGET()
    issue = cal.adjust(runner - ql.Period(100, ql.Days))
    sched = semiannual(issue, cal.advance(issue, 10, ql.Years))
    spec = fixed_spec(3, 100.0, sched, [0.05], THIRTY360_BB, ql.ModifiedFollowing, 100.0, issue,
                      calls([cal.advance(runner, 4, ql.Years)], 100.0))
    curve = flat(D(2, 1, 2020), 0.03)
    implied = implied_spec(curve, [(100.0, CLEAN), (100.0, DIRTY)])
    st = plain(today)
    return [
        ("expired / a bond called in 2019 and matured in 2025, 40 steps", st, spec, tree_steps(hull_white(curve), 40),
         100.0, None, implied),
        ("expired / the same on the Black engine", st, spec, black(0.2, curve), 100.0, None, implied),
    ]


def cases():
    out = []
    for entry in (cached_cases() + reference_date_cases() + runner_cases() + survey_cases() + ex_coupon_cases()
                  + ex_coupon_window_cases() + snapping_cases() + notionals_cases() + duration_cases()
                  + solver_cases()):
        name, st, spec, engine, scale, oas = entry
        out.append(run_case(name, st, spec, engine, scale, oas))
    for name, st, spec, engine, scale, oas, implied in black_cases() + expired_cases():
        out.append(run_case(name, st, spec, engine, scale, oas, implied))
    check_rows(out)
    return out


def check_rows(out):
    """The rows the file must hold."""
    by_name = {c["name"]: c["expected"] for c in out}
    survey = by_name["survey / 14 par calls, 200 steps, compounded semiannually"]
    assert survey["OASAtCleanPrice"] == -4.760451562901835e-05, survey["OASAtCleanPrice"]
    pair = (by_name["observability / calls and puts on a 3% curve"]["NPV"]["value"],
            by_name["observability / the same on a rebuilt 4% curve and its model"]["NPV"]["value"])
    assert pair[0] != pair[1], pair
    assert [n for n, e in by_name.items() if "construct" in e] == ["cached / a call after maturity"]
    # QuantLib's arguments read the settlement date's coupon and callability by an explicit false, whatever the flag
    for kind in ("a call on the settlement date ahead of live calls", "a put on the settlement date ahead of live puts"):
        npvs = [by_name[f"reference date / a coupon and {kind}, flag {flag}"]["NPV"] for flag in (True, False)]
        assert npvs[0] == npvs[1], npvs
    # the spread converted on its curve argument, which doesn't price
    assert by_name["solver / the spread converted on the engine's curve"]["OAS"] == 0.011085320535265009
    assert by_name["solver / the spread converted on a flat 8%"]["OAS"] == 0.011309258864703242
    assert by_name["solver / the spread refused after three evaluations"]["OAS"] == v.THROWS
    assert by_name["Black / the same refused after two evaluations"]["impliedVolatility"] == [v.THROWS] * 2
    for name in ("expired / a bond called in 2019 and matured in 2025, 40 steps", "expired / the same on the Black engine"):
        e = by_name[name]
        assert e["isExpired"] is True and e["NPV"]["value"] == 0.0 and e["settlementValue"]["value"] == 0.0, e
        assert e["impliedVolatility"] == [v.THROWS] * 2, e["impliedVolatility"]


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/callablebonds/callablebond.hpp", cases)
