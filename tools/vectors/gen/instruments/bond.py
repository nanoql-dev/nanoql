# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Bond (ql/instruments/bond.hpp) built by its two public constructors: 11 bonds from their
coupons (fixed-rate legs bullet and amortizing, a draw-down, an ex-coupon leg, a payment lag, coupons given one by one
out of date order, so that the maturity is the last coupon given and not the last paid, a leg of coupons alone on a
null calendar settling on the evaluation date, and three the constructor refuses: an issue date on the first payment,
one after it, and a leg of a redemption with no coupon) and 6 from a face amount, a maturity and cash flows ending in
the redemption (the maturity given or the cash flows', a redemption at 101%, a redemption paid before the last
coupon, which stays last, a redemption alone, and an issue date the constructor refuses).

A case is one bond under one evaluation context, drawn with the seed from 4 evaluation dates (one after every bond's
maturity) x includeReferenceDateEvents x includeTodaysCashFlows null, false or true; the first six bonds have two
cases, under two contexts. Its outputs are:
- the inspectors with no date: the settlement days, the notionals, every cash flow's date and amount, the
  redemptions' dates and amounts, the single redemption's date and amount (refused for none or several), the start,
  maturity and issue dates, and whether the bond has expired;
- at 20 dates (a null one, the bond's settlement date, and 19 drawn from around its payment, accrual and issue dates,
  the evaluation date and dates before its start and after its maturity): the settlement date, the notional,
  tradability, the accrued amount, the next and previous coupon rates and cash-flow dates, the clean and dirty prices
  for the case's yield, and the yield for the case's price (a clean or a dirty price, accuracy 1e-8, 100
  evaluations, guess 0.05, C++'s defaults);
- priced by DiscountingBondEngine on a flat curve whose reference date is the evaluation date: the NPV, the error
  estimate and valuation date (null where QuantLib throws "not provided"), the settlement value, the clean and dirty
  prices read from it, the theoretical yield (bondYield(dc, comp, freq, 1e-8, 100), C++'s defaults, its guess 0.05
  and a clean price, which the wheel can't change), and the settlement value for a clean price of 99.
A refused value is {"throws": true}; a refused construction is the case's one output, "construct". Amounts, prices'
settlement values and notionals are compared at the bond's face amount.

The specs here are every bond generator's (instruments/bonds/*, the bond engine's, the bond functions' and the bond
helpers', and their harness counterparts, which import this file):
- a bond is {"type": "Bond", "settlementDays", "calendar", "issueDate", "coupons"}, QuantLib's first constructor;
  {"type": "BondFaceAmount", "settlementDays", "calendar", "faceAmount", "maturityDate", "issueDate", "cashflows"},
  its second; {"type": "FixedRateBond", "settlementDays", "faceAmount", "schedule", "coupons", "accrualDayCounter",
  "paymentConvention", "redemption", "issueDate", "paymentCalendar", "exCouponPeriod", "firstPeriodDayCounter"};
  {"type": "FloatingRateBond", "settlementDays", "faceAmount", "schedule", "index", "accrualDayCounter",
  "paymentConvention", "fixingDays", "gearings", "spreads", "caps", "floors", "inArrears", "redemption", "issueDate",
  "exCouponPeriod", "fixingConvention"}; {"type": "ZeroCouponBond", "settlementDays", "calendar", "faceAmount",
  "maturityDate", "paymentConvention", "redemption", "issueDate"}; or {"type": "AmortizingFixedRateBond",
  "settlementDays", "notionals", "schedule", "coupons", "accrualDayCounter", "paymentConvention", "issueDate",
  "exCouponPeriod", "redemptions", "paymentLag"}; {"type": "AmortizingFloatingRateBond", "settlementDays",
  "notionals", "schedule", "index", "accrualDayCounter", "paymentConvention", "fixingDays", "gearings", "spreads",
  "caps", "floors", "inArrears", "issueDate", "exCouponPeriod", "redemptions", "paymentLag"}; {"type": "CmsRateBond",
  "settlementDays", "faceAmount", "schedule", "index", "paymentDayCounter", "paymentConvention", "fixingDays",
  "gearings", "spreads", "caps", "floors", "inArrears", "redemption", "issueDate", "pricer"}; or {"type":
  "AmortizingCmsRateBond", "settlementDays", "notionals", "schedule", "index", "paymentDayCounter",
  "paymentConvention", "fixingDays", "gearings", "spreads", "caps", "floors", "inArrears", "issueDate", "pricer"}, a
  CMS bond's index a spec of tools/vectors/gen/indexes/swapindex.py and its pricer the CMS coupons' (cmscoupon.py's:
  Hagan's analytic pricer on the standard model with no mean reversion, on a constant volatility {"referenceDate",
  "volatility"} on TARGET, Modified Following, Actual/365 (Fixed)), set on its coupons once built;
- a leg is tools/vectors/gen/cashflows/cashflows.py's {"fixedRateLeg", "extraFlows"}, with an optional "coupons" of
  fixed-rate coupons given one by one ({"paymentDate", "nominal", "rate", "dayCounter", "accrualStartDate",
  "accrualEndDate", "refPeriodStart", "refPeriodEnd", "exCouponDate"}, a simple annual rate) between the two;
- a schedule is cashflows.py's rule-based spec, or {"dates", "calendar", "convention"} for one built from dates,
  with no tenor, its calendar never null (nanoql's schedule reads a null one as QuantLib's default, NullCalendar);
- an ex-coupon period is null or {"period", "calendar", "convention", "endOfMonth"}; an index is a spec of
  tools/vectors/gen/indexes/interestrateindex.py; a rate is {"rate", "dayCounter", "compounding", "frequency"}; a
  curve is a flat forward curve spec, as there; a calendar or day counter null is QuantLib's empty one, which only
  the harness's files hold; a null fixing days, issue date or maturity date is QuantLib's default.

The bonds are built with ql.Bond, ql.FixedRateBond, ql.FloatingRateBond, ql.ZeroCouponBond and
ql.AmortizingFixedRateBond, the wheel's proxies of the C++ constructors, every argument passed (by name where the
wheel takes names), and their legs with ql.FixedRateLeg, as in cashflows.py. Read back by
tests/NanoQL.Tests/Vectors/Instruments/BondVectorTests.cs, with tests/NanoQL.Tests/Vectors/BondInputs.cs."""
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


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


cf = _load("wheel_cashflows", "cashflows/cashflows.py")
irx = _load("wheel_interestrateindex", "indexes/interestrateindex.py")
swi = _load("wheel_swapindex", "indexes/swapindex.py")

D = ql.Date
TARGET, NULL_CALENDAR, US_SETTLEMENT, UK_SETTLEMENT = cf.TARGET, cf.NULL_CALENDAR, cf.US_SETTLEMENT, cf.UK_SETTLEMENT
US_GOVERNMENT = {"type": "UnitedStates", "market": int(ql.UnitedStates.GovernmentBond)}
A360, A365, THIRTY360_BB, ACT_ISDA = cf.A360, cf.A365, cf.THIRTY360_BB, cf.ACT_ISDA
ACT_ISMA = {"type": "ActualActual", "convention": int(ql.ActualActual.ISMA)}
THIRTY360_US = {"type": "Thirty360", "convention": int(ql.Thirty360.USA)}


def calendar(spec):
    if spec is not None and spec["type"] == "UnitedStates":
        return ql.UnitedStates(spec["market"])
    return cf.calendar(spec)


def day_counter(spec):
    return cf.day_counter(spec)


def date_or_null(serial):
    return ql.Date(serial) if serial is not None else ql.Date()


def real(x):
    """A real as the vectors encode it: a number, or "NaN", "Infinity" or "-Infinity"."""
    return float(x) if isinstance(x, str) else x


# ---- specs ----

def schedule_spec(eff, mat, tenor, cal=TARGET, convention=ql.Unadjusted, rule=ql.DateGeneration.Backward, eom=False,
                  first=None, next_to_last=None, termination_convention=None):
    return {"effectiveDate": v.enc(eff), "terminationDate": v.enc(mat), "tenor": [tenor[0], int(tenor[1])],
            "calendar": cal, "convention": int(convention),
            "terminationDateConvention": int(convention if termination_convention is None else termination_convention),
            "rule": int(rule), "endOfMonth": eom, "firstDate": v.enc(first), "nextToLastDate": v.enc(next_to_last)}


def dates_schedule_spec(dates, cal=NULL_CALENDAR, convention=ql.Unadjusted):
    return {"dates": [v.enc(d) for d in dates], "calendar": cal, "convention": int(convention)}


def ex_coupon_spec(n, units, cal=TARGET, convention=ql.Unadjusted, eom=False):
    return {"period": [n, int(units)], "calendar": cal, "convention": int(convention), "endOfMonth": eom}


def coupon_spec(pay, nominal, rate, dc, start, end):
    return {"paymentDate": v.enc(pay), "nominal": nominal, "rate": rate, "dayCounter": dc,
            "accrualStartDate": v.enc(start), "accrualEndDate": v.enc(end), "refPeriodStart": None,
            "refPeriodEnd": None, "exCouponDate": None}


def leg_spec(fixed=None, coupons=None, extra=None):
    out = {"fixedRateLeg": fixed, "extraFlows": extra or []}
    if coupons is not None:
        out["coupons"] = coupons
    return out


def rate_spec(r, dc, comp, freq):
    return {"rate": r, "dayCounter": dc, "compounding": int(comp), "frequency": int(freq)}


def bond_spec(days, cal, issue, coupons):
    return {"type": "Bond", "settlementDays": days, "calendar": cal, "issueDate": v.enc(issue), "coupons": coupons}


def face_bond_spec(days, cal, face, maturity, issue, cashflows):
    return {"type": "BondFaceAmount", "settlementDays": days, "calendar": cal, "faceAmount": face,
            "maturityDate": v.enc(maturity), "issueDate": v.enc(issue), "cashflows": cashflows}


def fixed_bond_spec(days, face, schedule, coupons, dc, convention=ql.Following, redemption=100.0, issue=None,
                    payment_calendar=None, ex_coupon=None, first_period_dc=None):
    return {"type": "FixedRateBond", "settlementDays": days, "faceAmount": v.enc(face), "schedule": schedule,
            "coupons": coupons, "accrualDayCounter": dc, "paymentConvention": int(convention),
            "redemption": redemption, "issueDate": v.enc(issue), "paymentCalendar": payment_calendar,
            "exCouponPeriod": ex_coupon, "firstPeriodDayCounter": first_period_dc}


def floating_bond_spec(days, face, schedule, index, dc, convention=ql.Following, fixing_days=None, gearings=(1.0,),
                       spreads=(0.0,), caps=(), floors=(), in_arrears=False, redemption=100.0, issue=None,
                       ex_coupon=None, fixing_convention=ql.Preceding):
    return {"type": "FloatingRateBond", "settlementDays": days, "faceAmount": face, "schedule": schedule,
            "index": index, "accrualDayCounter": dc, "paymentConvention": int(convention), "fixingDays": fixing_days,
            "gearings": list(gearings), "spreads": list(spreads), "caps": list(caps), "floors": list(floors),
            "inArrears": in_arrears, "redemption": redemption, "issueDate": v.enc(issue), "exCouponPeriod": ex_coupon,
            "fixingConvention": int(fixing_convention)}


def zero_bond_spec(days, cal, face, maturity, convention=ql.Following, redemption=100.0, issue=None):
    return {"type": "ZeroCouponBond", "settlementDays": days, "calendar": cal, "faceAmount": face,
            "maturityDate": v.enc(maturity), "paymentConvention": int(convention), "redemption": redemption,
            "issueDate": v.enc(issue)}


def amortizing_bond_spec(days, notionals, schedule, coupons, dc, convention=ql.Following, issue=None, ex_coupon=None,
                         redemptions=(100.0,), payment_lag=0):
    return {"type": "AmortizingFixedRateBond", "settlementDays": days, "notionals": list(notionals),
            "schedule": schedule, "coupons": list(coupons), "accrualDayCounter": dc,
            "paymentConvention": int(convention), "issueDate": v.enc(issue), "exCouponPeriod": ex_coupon,
            "redemptions": list(redemptions), "paymentLag": payment_lag}


def amortizing_floating_bond_spec(days, notionals, schedule, index, dc, convention=ql.Following, fixing_days=None,
                                  gearings=(1.0,), spreads=(0.0,), caps=(), floors=(), in_arrears=False, issue=None,
                                  ex_coupon=None, redemptions=(100.0,), payment_lag=0):
    return {"type": "AmortizingFloatingRateBond", "settlementDays": days, "notionals": list(notionals),
            "schedule": schedule, "index": index, "accrualDayCounter": dc, "paymentConvention": int(convention),
            "fixingDays": fixing_days, "gearings": list(gearings), "spreads": list(spreads), "caps": list(caps),
            "floors": list(floors), "inArrears": in_arrears, "issueDate": v.enc(issue), "exCouponPeriod": ex_coupon,
            "redemptions": list(redemptions), "paymentLag": payment_lag}


def cms_pricer_spec(reference, volatility=0.20):
    return {"referenceDate": v.enc(reference), "volatility": volatility}


def cms_bond_spec(days, face, schedule, index, dc, pricer, convention=ql.Following, fixing_days=None,
                  gearings=(1.0,), spreads=(0.0,), caps=(), floors=(), in_arrears=False, redemption=100.0,
                  issue=None):
    return {"type": "CmsRateBond", "settlementDays": days, "faceAmount": face, "schedule": schedule, "index": index,
            "paymentDayCounter": dc, "paymentConvention": int(convention), "fixingDays": fixing_days,
            "gearings": list(gearings), "spreads": list(spreads), "caps": list(caps), "floors": list(floors),
            "inArrears": in_arrears, "redemption": redemption, "issueDate": v.enc(issue), "pricer": pricer}


def amortizing_cms_bond_spec(days, notionals, schedule, index, dc, pricer, convention=ql.Following, fixing_days=None,
                             gearings=(1.0,), spreads=(0.0,), caps=(), floors=(), in_arrears=False, issue=None):
    return {"type": "AmortizingCmsRateBond", "settlementDays": days, "notionals": list(notionals),
            "schedule": schedule, "index": index, "paymentDayCounter": dc, "paymentConvention": int(convention),
            "fixingDays": fixing_days, "gearings": list(gearings), "spreads": list(spreads), "caps": list(caps),
            "floors": list(floors), "inArrears": in_arrears, "issueDate": v.enc(issue), "pricer": pricer}


# ---- the wheel's objects ----

def make_schedule(spec):
    if "dates" in spec:
        return ql.Schedule([ql.Date(d) for d in spec["dates"]], calendar(spec["calendar"]), spec["convention"])
    return ql.Schedule(ql.Date(spec["effectiveDate"]), ql.Date(spec["terminationDate"]),
                       ql.Period(spec["tenor"][0], spec["tenor"][1]), calendar(spec["calendar"]), spec["convention"],
                       spec["terminationDateConvention"], spec["rule"], spec["endOfMonth"],
                       date_or_null(spec["firstDate"]), date_or_null(spec["nextToLastDate"]))


def fixed_rate_leg(spec):
    """cashflows.py's fixed-rate leg, its schedule built here (which takes the government-bond calendar too)."""
    kw = {"dayCount": day_counter(spec["dayCounter"]), "nominals": spec["notionals"],
          "couponRates": spec["couponRates"], "compounding": spec["compounding"],
          "compoundingFrequency": spec["frequency"], "paymentLag": spec["paymentLag"],
          "paymentAdjustment": spec["paymentAdjustment"]}
    if spec["firstPeriodDayCounter"] is not None:
        kw["firstPeriodDayCount"] = day_counter(spec["firstPeriodDayCounter"])
    if spec["paymentCalendar"] is not None:
        kw["paymentCalendar"] = calendar(spec["paymentCalendar"])
    ex = spec["exCouponPeriod"]
    if ex is not None:
        kw.update(exCouponPeriod=ql.Period(ex["period"][0], ex["period"][1]), exCouponCalendar=calendar(ex["calendar"]),
                  exCouponConvention=ex["convention"], exCouponEndOfMonth=ex["endOfMonth"])
    return ql.FixedRateLeg(make_schedule(spec["schedule"]), **kw)


def make_leg(spec):
    """The leg a spec describes: its fixed-rate leg's coupons, then its coupons given one by one, then its flows."""
    flows = list(fixed_rate_leg(spec["fixedRateLeg"])) if spec["fixedRateLeg"] is not None else []
    for c in spec.get("coupons", []):
        flows.append(ql.FixedRateCoupon(ql.Date(c["paymentDate"]), c["nominal"], c["rate"],
                                        day_counter(c["dayCounter"]), ql.Date(c["accrualStartDate"]),
                                        ql.Date(c["accrualEndDate"])))
    flows += [cf.EXTRA_FLOWS[f["type"]](f["amount"], ql.Date(f["date"])) for f in spec["extraFlows"]]
    return ql.Leg(flows)


def ex_coupon_kw(ex):
    if ex is None:
        return {}
    return {"exCouponPeriod": ql.Period(ex["period"][0], ex["period"][1]), "exCouponCalendar": calendar(ex["calendar"]),
            "exCouponConvention": ex["convention"], "exCouponEndOfMonth": ex["endOfMonth"]}


def make_bond(spec):
    t = spec["type"]
    if t == "Bond":
        return ql.Bond(spec["settlementDays"], calendar(spec["calendar"]), date_or_null(spec["issueDate"]),
                       make_leg(spec["coupons"]))
    if t == "BondFaceAmount":
        return ql.Bond(spec["settlementDays"], calendar(spec["calendar"]), spec["faceAmount"],
                       date_or_null(spec["maturityDate"]), date_or_null(spec["issueDate"]),
                       make_leg(spec["cashflows"]))
    if t == "FixedRateBond":
        assert spec["firstPeriodDayCounter"] is None, "the wheel's FixedRateBond takes no first-period day counter"
        kw = {"paymentConvention": spec["paymentConvention"], "redemption": spec["redemption"],
              "issueDate": date_or_null(spec["issueDate"])}
        if spec["paymentCalendar"] is not None:
            kw["paymentCalendar"] = calendar(spec["paymentCalendar"])
        kw.update(ex_coupon_kw(spec["exCouponPeriod"]))
        return ql.FixedRateBond(spec["settlementDays"], real(spec["faceAmount"]), make_schedule(spec["schedule"]),
                                spec["coupons"], day_counter(spec["accrualDayCounter"]), **kw)
    if t == "FloatingRateBond":
        kw = {"paymentConvention": spec["paymentConvention"], "gearings": spec["gearings"],
              "spreads": spec["spreads"], "caps": spec["caps"], "floors": spec["floors"],
              "inArrears": spec["inArrears"], "redemption": spec["redemption"],
              "issueDate": date_or_null(spec["issueDate"]), "fixingConvention": spec["fixingConvention"]}
        if spec["fixingDays"] is not None:
            kw["fixingDays"] = spec["fixingDays"]
        kw.update(ex_coupon_kw(spec["exCouponPeriod"]))
        return ql.FloatingRateBond(spec["settlementDays"], spec["faceAmount"], make_schedule(spec["schedule"]),
                                   irx.make_index(spec["index"]), day_counter(spec["accrualDayCounter"]), **kw)
    if t == "ZeroCouponBond":
        return ql.ZeroCouponBond(spec["settlementDays"], calendar(spec["calendar"]), spec["faceAmount"],
                                 ql.Date(spec["maturityDate"]), spec["paymentConvention"], spec["redemption"],
                                 date_or_null(spec["issueDate"]))
    if t == "AmortizingFixedRateBond":
        kw = {"paymentConvention": spec["paymentConvention"], "issueDate": date_or_null(spec["issueDate"]),
              "redemptions": spec["redemptions"], "paymentLag": spec["paymentLag"]}
        kw.update(ex_coupon_kw(spec["exCouponPeriod"]))
        return ql.AmortizingFixedRateBond(spec["settlementDays"], spec["notionals"], make_schedule(spec["schedule"]),
                                          spec["coupons"], day_counter(spec["accrualDayCounter"]), **kw)
    if t == "AmortizingFloatingRateBond":
        kw = {"paymentConvention": spec["paymentConvention"], "gearings": spec["gearings"],
              "spreads": spec["spreads"], "caps": spec["caps"], "floors": spec["floors"],
              "inArrears": spec["inArrears"], "issueDate": date_or_null(spec["issueDate"]),
              "redemptions": spec["redemptions"], "paymentLag": spec["paymentLag"]}
        if spec["fixingDays"] is not None:
            kw["fixingDays"] = spec["fixingDays"]
        kw.update(ex_coupon_kw(spec["exCouponPeriod"]))
        return ql.AmortizingFloatingRateBond(spec["settlementDays"], spec["notionals"], make_schedule(spec["schedule"]),
                                             irx.make_index(spec["index"]), day_counter(spec["accrualDayCounter"]),
                                             **kw)
    if t in ("CmsRateBond", "AmortizingCmsRateBond"):
        # the wheel's CmsRateBond takes every argument by position, its fixing days Null<Natural>() for the index's
        fixing_days = 2147483647 if spec["fixingDays"] is None else spec["fixingDays"]
        args = (make_schedule(spec["schedule"]), swi.make_swap_index(spec["index"]),
                day_counter(spec["paymentDayCounter"]), spec["paymentConvention"], fixing_days, spec["gearings"],
                spec["spreads"], spec["caps"], spec["floors"], spec["inArrears"])
        if t == "CmsRateBond":
            bond = ql.CmsRateBond(spec["settlementDays"], spec["faceAmount"], *args, spec["redemption"],
                                  date_or_null(spec["issueDate"]))
        else:
            bond = ql.AmortizingCmsRateBond(spec["settlementDays"], spec["notionals"], *args,
                                            date_or_null(spec["issueDate"]))
        ql.setCouponPricer(bond.cashflows(), make_cms_pricer(spec["pricer"]))
        return bond
    raise ValueError(f"unknown bond {t}")


def make_cms_pricer(spec):
    vol = ql.ConstantSwaptionVolatility(ql.Date(spec["referenceDate"]), ql.TARGET(), ql.ModifiedFollowing,
                                        spec["volatility"], ql.Actual365Fixed())
    return ql.AnalyticHaganPricer(ql.SwaptionVolatilityStructureHandle(vol), ql.GFunctionFactory.Standard,
                                  ql.QuoteHandle(ql.SimpleQuote(0.0)))


def make_rate(spec):
    return ql.InterestRate(spec["rate"], day_counter(spec["dayCounter"]), spec["compounding"], spec["frequency"])


def curve_spec(reference, forward, dc=A365, compounding=ql.Continuous, frequency=ql.Annual):
    return irx.curve_spec(reference, forward, dc, compounding, frequency)


def make_curve(spec):
    return ql.FlatForward(ql.Date(spec["referenceDate"]), spec["forward"], day_counter(spec["dayCounter"]),
                          spec["compounding"], spec["frequency"])


def price_spec(amount, kind):
    return {"amount": amount, "type": int(kind)}


def make_price(spec):
    return ql.BondPrice(spec["amount"], spec["type"])


# ---- the outputs every bond file shares ----

def scaled(f, scale):
    """f()'s value compared at the scale, or {"throws": true} where QuantLib throws."""
    r = v.attempt(f)
    return r if isinstance(r, dict) else v.amount(r, scale)


def bond_dates(bond, ev, rnd, n):
    """n dates of interest drawn with rnd, after a null one and the bond's settlement date: each cash flow's date and
    the days either side, each coupon's accrual start and the day after, the issue date and the days either side,
    the evaluation date, and dates before the start and after the maturity."""
    out = set()
    for c in bond.cashflows():
        d = c.date()
        out |= {d - 1, d, d + 1}
        cp = ql.as_coupon(c)
        if cp is not None:
            out |= {cp.accrualStartDate(), cp.accrualStartDate() + 1}
    if bond.issueDate() != ql.Date():
        out |= {bond.issueDate() - 1, bond.issueDate(), bond.issueDate() + 1}
    out.add(ev)
    if len(bond.cashflows()) > 0:
        out |= {bond.startDate() - 40, bond.maturityDate() + 40}
    candidates = sorted(out - {bond.settlementDate()})
    picked = sorted(rnd.sample(candidates, min(n - 2, len(candidates))))
    return [None, v.enc(bond.settlementDate())] + [v.enc(d) for d in picked]


def static_outputs(bond, scale):
    try:
        single = bond.redemption()
        redemption_date, redemption_amount = v.enc(single.date()), v.amount(single.amount(), scale)
    except RuntimeError:
        redemption_date, redemption_amount = dict(v.THROWS), dict(v.THROWS)
    return {"settlementDays": bond.settlementDays(),
            "notionals": [v.amount(x, scale) for x in bond.notionals()],
            "cashflowDates": [v.enc(c.date()) for c in bond.cashflows()],
            "cashflowAmounts": [scaled(c.amount, scale) for c in bond.cashflows()],
            "redemptionDates": [v.enc(c.date()) for c in bond.redemptions()],
            "redemptionAmounts": [v.amount(c.amount(), scale) for c in bond.redemptions()],
            "redemptionDate": redemption_date,
            "redemptionAmount": redemption_amount,
            "startDate": v.attempt(bond.startDate),
            "maturityDate": v.attempt(bond.maturityDate),
            "issueDate": v.enc(bond.issueDate()),
            "isExpired": bond.isExpired()}


DATED = ["settlementDate", "notional", "isTradable", "accruedAmount", "nextCouponRate", "previousCouponRate",
         "nextCashFlowDate", "previousCashFlowDate", "cleanPrice", "dirtyPrice", "yield"]


def dated_outputs(bond, dates, rate, price, scale):
    r = make_rate(rate)
    p = make_price(price)
    out = {m: [] for m in DATED}
    for serial in dates:
        d = date_or_null(serial)
        out["settlementDate"].append(v.attempt(lambda: bond.settlementDate(d)))
        out["notional"].append(scaled(lambda: bond.notional(d), scale))
        out["isTradable"].append(v.attempt(lambda: bond.isTradable(d)))
        out["accruedAmount"].append(v.attempt(lambda: bond.accruedAmount(d)))
        out["nextCouponRate"].append(v.attempt(lambda: bond.nextCouponRate(d)))
        out["previousCouponRate"].append(v.attempt(lambda: bond.previousCouponRate(d)))
        out["nextCashFlowDate"].append(v.attempt(lambda: bond.nextCashFlowDate(d)))
        out["previousCashFlowDate"].append(v.attempt(lambda: bond.previousCashFlowDate(d)))
        args = (r.rate(), r.dayCounter(), r.compounding(), r.frequency(), d)
        out["cleanPrice"].append(v.attempt(lambda: bond.cleanPrice(*args)))
        out["dirtyPrice"].append(v.attempt(lambda: bond.dirtyPrice(*args)))
        out["yield"].append(v.attempt(lambda: bond.bondYield(p, r.dayCounter(), r.compounding(), r.frequency(), d,
                                                             1.0e-8, 100, 0.05)))
    return out


def engine_outputs(bond, curve, rate, scale):
    """The bond priced by DiscountingBondEngine on the curve (the wheel's engine takes the curve alone), then the
    members that read the engine's results."""
    bond.setPricingEngine(ql.DiscountingBondEngine(ql.YieldTermStructureHandle(make_curve(curve))))
    r = make_rate(rate)
    return {"NPV": scaled(bond.NPV, scale),
            "errorEstimate": v.attempt(lambda: v.result(bond.errorEstimate)),
            "valuationDate": v.attempt(lambda: v.result(bond.valuationDate)),
            "settlementValue": scaled(lambda: v.result(lambda: bond.settlementValue()), scale),
            "cleanPriceEngine": v.attempt(lambda: bond.cleanPrice()),
            "dirtyPriceEngine": v.attempt(lambda: bond.dirtyPrice()),
            "yieldEngine": v.attempt(lambda: bond.bondYield(r.dayCounter(), r.compounding(), r.frequency(), 1.0e-8,
                                                            100)),
            "settlementValueFromClean": scaled(lambda: bond.settlementValue(99.0), scale)}


def run_case(name, spec, scale, context, rate, price, curve_rate, rnd, n_dates, extra=None, fixings=None,
             use_at_par_coupons=None):
    """One case of a bond file: the bond built under the context (and the at-par switch and the fixings, when given,
    fixings being a function of the bond built without them that returns cashflows.py's (index, pairs) list), its
    static outputs, the extra outputs extra(bond) gives, its outputs at n_dates dates drawn with rnd, and its engine's
    on a flat curve at curve_rate whose reference date is the evaluation date; or the one output "construct" where
    QuantLib refuses the bond."""
    ev, ref_events, todays = context
    label = f"{name} / {ev.ISO()} {ref_events} {todays}"
    stored = None
    if fixings is not None:
        with v.case_settings(ev, include_reference_date_events=ref_events, include_todays_cash_flows=todays,
                             use_at_par_coupons=use_at_par_coupons):
            stored = fixings(make_bond(spec))
    with v.case_settings(ev, include_reference_date_events=ref_events, include_todays_cash_flows=todays,
                         use_at_par_coupons=use_at_par_coupons, fixings=stored) as st:
        try:
            bond = make_bond(spec)
        except RuntimeError:
            return {"name": label, "inputs": {"settings": st, "bond": spec}, "expected": {"construct": dict(v.THROWS)}}
        curve = curve_spec(ev, curve_rate)
        dates = bond_dates(bond, ev, rnd, n_dates)
        expected = static_outputs(bond, scale)
        if extra is not None:
            expected.update(extra(bond))
        expected.update(dated_outputs(bond, dates, rate, price, scale))
        expected.update(engine_outputs(bond, curve, rate, scale))
        return {"name": label,
                "inputs": {"settings": st, "bond": spec, "dates": dates, "rate": rate, "price": price,
                           "curve": curve},
                "expected": expected}


# ---- this file's bonds ----

S6M = schedule_spec(D(15, 3, 2024), D(15, 3, 2028), (6, ql.Months), TARGET, ql.ModifiedFollowing)
S1Y = schedule_spec(D(16, 6, 2025), D(16, 6, 2030), (1, ql.Years), NULL_CALENDAR, ql.Unadjusted,
                    ql.DateGeneration.Forward)
S3M = schedule_spec(D(30, 11, 2024), D(30, 11, 2027), (3, ql.Months), US_GOVERNMENT, ql.Unadjusted)
S6M_UK = schedule_spec(D(20, 1, 2025), D(20, 1, 2029), (6, ql.Months), UK_SETTLEMENT, ql.ModifiedFollowing)


def fixed(schedule, notionals, rates, dc=A360, compounding=ql.Simple, frequency=ql.Annual, payment_calendar=None,
          payment_lag=0, payment_adjustment=ql.Following, ex_coupon=None):
    return dict(cf.leg_spec(dc, notionals, rates, compounding, frequency, None, payment_calendar, payment_lag,
                            payment_adjustment, ex_coupon), schedule=schedule)


def flow(kind, amount, date):
    return cf.flow(kind, amount, date)


def bonds():
    """(name, bond spec, face amount) for this file's 17 bonds."""
    s6m = fixed(S6M, [100.0], [0.05])
    s6m_coupons = list(fixed_rate_leg(s6m))
    first6m, last6m = s6m_coupons[0].date(), s6m_coupons[-1].date()
    redeemed6m = leg_spec(s6m, extra=[flow("Redemption", 100.0, last6m)])
    one_year = fixed(S1Y, [100.0], [0.03], THIRTY360_BB)
    amortizing = fixed(S6M, [100.0, 100.0, 80.0, 80.0, 60.0, 40.0], [0.04])
    draw_down = fixed(S3M, [100.0, 100.0, 100.5, 100.5, 101.5, 101.5, 90.0, 80.0], [0.042], A360)
    seven_days = ex_coupon_spec(7, ql.Days, TARGET, ql.Unadjusted)
    ex_coupon = fixed(S6M_UK, [1e6], [0.045], A365, ex_coupon=seven_days)
    lagged = fixed(S6M_UK, [100.0], [0.02], THIRTY360_BB, payment_calendar=UK_SETTLEMENT, payment_lag=2,
                   payment_adjustment=ql.ModifiedFollowing)
    unordered = [coupon_spec(D(16, 6, 2027), 100.0, 0.04, A365, D(16, 6, 2026), D(16, 6, 2027)),
                 coupon_spec(D(16, 12, 2026), 100.0, 0.04, A365, D(16, 6, 2026), D(16, 12, 2026)),
                 coupon_spec(D(16, 6, 2026), 100.0, 0.04, A365, D(16, 6, 2025), D(16, 6, 2026))]
    compounded = fixed(S3M, [5e5], [0.035], A365, ql.Compounded, ql.Quarterly)
    alone = [flow("Redemption", 100.0, D(16, 6, 2028))]
    at101 = leg_spec(fixed(S3M, [1e6], [0.03], ACT_ISDA), extra=[flow("Redemption", 1.01e6, D(30, 11, 2027))])
    early = leg_spec(coupons=[coupon_spec(D(16, 6, 2026), 100.0, 0.04, A365, D(16, 6, 2025), D(16, 6, 2026)),
                              coupon_spec(D(16, 6, 2027), 100.0, 0.04, A365, D(16, 6, 2026), D(16, 6, 2027))],
                     extra=[flow("Redemption", 100.0, D(16, 12, 2026))])
    return [
        ("coupons / 6M bullet", bond_spec(2, TARGET, D(15, 3, 2024), leg_spec(s6m)), 100.0),
        ("coupons / 1Y null calendar, settling today", bond_spec(0, NULL_CALENDAR, None, leg_spec(one_year)), 100.0),
        ("coupons / amortizing", bond_spec(2, TARGET, None, leg_spec(amortizing)), 100.0),
        ("coupons / draw-down", bond_spec(3, US_GOVERNMENT, D(30, 11, 2024), leg_spec(draw_down)), 100.0),
        ("coupons / ex-coupon", bond_spec(2, TARGET, None, leg_spec(ex_coupon)), 1e6),
        ("coupons / payment lag", bond_spec(1, UK_SETTLEMENT, None, leg_spec(lagged)), 100.0),
        ("coupons / out of date order", bond_spec(2, TARGET, None, leg_spec(coupons=unordered)), 100.0),
        ("coupons / compounded 3M", bond_spec(3, US_GOVERNMENT, None, leg_spec(compounded)), 5e5),
        ("coupons / issue on the first payment", bond_spec(2, TARGET, first6m, leg_spec(s6m)), 100.0),
        ("coupons / issue after the first payment", bond_spec(2, TARGET, D(1, 1, 2030), leg_spec(s6m)), 100.0),
        ("coupons / a redemption alone", bond_spec(2, TARGET, None, leg_spec(extra=alone)), 100.0),
        ("face amount / maturity given", face_bond_spec(2, TARGET, 100.0, D(15, 3, 2028), D(15, 3, 2024), redeemed6m),
         100.0),
        ("face amount / maturity null", face_bond_spec(2, TARGET, 100.0, None, None, redeemed6m), 100.0),
        ("face amount / redemption at 101", face_bond_spec(1, US_GOVERNMENT, 1e6, D(30, 11, 2027), None, at101), 1e6),
        ("face amount / redemption before the last coupon", face_bond_spec(2, TARGET, 100.0, D(16, 6, 2027), None,
                                                                           early), 100.0),
        ("face amount / a redemption alone", face_bond_spec(0, NULL_CALENDAR, 100.0, None, None, leg_spec(extra=alone)),
         100.0),
        ("face amount / issue after the first payment", face_bond_spec(2, TARGET, 100.0, None, D(1, 1, 2030),
                                                                       redeemed6m), 100.0),
    ]


EVALUATION_DATES = [D(16, 6, 2025), D(14, 3, 2026), D(15, 9, 2027), D(15, 1, 2031)]
RATES = [rate_spec(0.04, A365, ql.Compounded, ql.Annual), rate_spec(0.035, THIRTY360_BB, ql.Compounded, ql.Semiannual),
         rate_spec(0.05, A360, ql.Continuous, ql.Annual), rate_spec(0.03, ACT_ISDA, ql.Simple, ql.Annual),
         rate_spec(0.045, A365, ql.SimpleThenCompounded, ql.Semiannual)]
PRICES = [price_spec(99.5, ql.BondPrice.Clean), price_spec(101.25, ql.BondPrice.Dirty)]
CURVE_RATE = 0.032


def contexts():
    return [(ev, ref_events, todays) for ev in EVALUATION_DATES for ref_events in (False, True)
            for todays in (None, False, True)]


def cases():
    rnd = random.Random(v.SEED)
    out = []
    for k, (name, spec, scale) in enumerate(bonds()):
        # the first six bonds under two contexts, the others under one
        for j, (ev, ref_events, todays) in enumerate(rnd.sample(contexts(), 2 if k < 6 else 1)):
            st = v.settings(ev, ref_events, todays)
            try:
                bond = make_bond(spec)
            except RuntimeError:
                out.append({"name": f"{name} / {ev.ISO()} {ref_events} {todays}",
                            "inputs": {"settings": st, "bond": spec}, "expected": {"construct": dict(v.THROWS)}})
                continue
            rate, price = RATES[(k + j) % len(RATES)], PRICES[(k + j) % len(PRICES)]
            curve = curve_spec(ev, CURVE_RATE)
            dates = bond_dates(bond, ev, rnd, 20)
            expected = static_outputs(bond, scale)
            expected.update(dated_outputs(bond, dates, rate, price, scale))
            expected.update(engine_outputs(bond, curve, rate, scale))
            out.append({"name": f"{name} / {ev.ISO()} {ref_events} {todays}",
                        "inputs": {"settings": st, "bond": spec, "dates": dates, "rate": rate, "price": price,
                                   "curve": curve},
                        "expected": expected})
    check_rows(out)
    return out


def check_rows(out):
    """The rows the file must hold, whatever the draws."""
    def any_case(pred):
        return any(pred(c["name"], c["expected"]) for c in out)
    assert sum("construct" in c["expected"] for c in out) == 4
    assert any_case(lambda n, e: n.startswith("coupons / issue on the first payment") and "construct" in e)
    assert any_case(lambda n, e: e.get("isExpired") is True and e["NPV"] == {"value": 0.0, "scale": 100.0})
    assert any_case(lambda n, e: e.get("redemptionDate") == v.THROWS)
    assert any_case(lambda n, e: n.startswith("coupons / out of date order")
                    and e["maturityDate"] < e["cashflowDates"][-1])
    assert any_case(lambda n, e: v.THROWS in e.get("cleanPrice", []))


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/bond.hpp", cases)
