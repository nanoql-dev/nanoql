# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's callable bonds (ql/experimental/callablebonds/callablebond.hpp, .cpp) where the wheel can't
reach, each output computed by the C++ harness: bonds on QuantLib's empty calendar and payment day counter, which the
wheel can't make, and the engines it can't name, the zero-coupon tree and Black engines and the Black engine on a
volatility structure, with the option-adjusted spread family on them.

Under testCached's fixture (3 June 2004, flat 3.2% from the settlement date) and QuantLib's test-runner date (16
September 2015, flat 3%), tools/vectors/gen/experimental/callablebonds/callablebond.py's specs:
- zero-coupon bonds on the empty calendar: unadjusted, which QuantLib builds and whose settlement date, and so every
  price, it can't compute ("no calendar implementation provided"), and following, whose construction it refuses;
- a zero-coupon bond with the empty payment day counter, which the tree prices and the Black engine refuses, its
  forward yield's day counter empty;
- zero-coupon bonds with calls and puts on TreeCallableZeroCouponBondEngine with time steps and on a time grid through
  their mandatory times, and with one call on BlackCallableZeroCouponBondEngine, by its quote and by a constant
  volatility structure;
- fixed-rate bonds with one call or put on BlackCallableFixedRateBondEngine by a structure, fixed at the evaluation
  date or two days later, on Actual/365 and Thirty/360, or moving with the evaluation date on TARGET's settlement days;
- a fixed-rate bond whose redemption is negative, which QuantLib's arguments refuse ("positive redemption required").
A case's outputs: the put/call schedule (dates, types, price amounts), the cash flows' dates and amounts, the
settlement and maturity dates and the accrued amount; the NPV, settlement value, clean and dirty prices on the case's
engine; on the tree engines, the spread for a clean price at an explicit settlement date, accuracy and guess, the
clean price for a spread at that settlement date, and the effective duration and convexity with an explicit bump; on
single-date bonds, the implied volatility. A refused construction is the case's one output, "construct"; a refused
value is {"throws": true}. Amounts are compared at the face amount. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/CallableBonds/CallableBondCxxVectorTests.cs, with
tests/NanoQL.Tests/Vectors/CallableBondInputs.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_callablebond", os.path.join(v.GEN_ROOT, "experimental", "callablebonds", "callablebond.py"))
cb = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cb)

D = ql.Date


# The effective convexity's recorded tolerance: a second difference of tree prices over bump^2, which amplifies the
# prices' last-bit moves by about 1/bump^2; the Ornstein-Uhlenbeck variance's cancellation-free form, nanoql's, moves it
# by up to 8.2e-9 relative.
CONVEXITY_TOLERANCE = 1e-7


CONVEXITY_REASON = ("a second difference of tree prices over bump^2, which the Ornstein-Uhlenbeck variance's "
                    "cancellation-free form moves by up to 8.2e-9 relative")


def convexity(x):
    return ({"value": x, "tolerance": CONVEXITY_TOLERANCE, "reason": CONVEXITY_REASON} if isinstance(x, float)
            else x)


def zero_engine(kind, engine):
    """A callablebond.py engine spec as the zero-coupon engine's."""
    return dict(engine, type={"TreeSteps": "TreeZeroSteps", "TreeGrid": "TreeZeroGrid", "Black": "BlackZero",
                              "BlackStructure": "BlackZeroStructure"}[engine["type"]]) if kind == "zero" else engine


def structure(vol, curve, reference=None, dc=cb.A365, settlement_days=None):
    """A Black engine on a constant volatility structure, fixed at a reference date or moving."""
    if settlement_days is None:
        spec = {"referenceDate": v.enc(reference), "volatility": vol, "dayCounter": dc}
    else:
        spec = {"settlementDays": settlement_days, "calendar": cb.TARGET, "volatility": vol, "dayCounter": dc}
    return {"type": "BlackStructure", "volatility": spec, "discountCurve": curve}


def bonds():
    """(name, settings, bond spec, engine spec, face amount, OAS spec, implied spec)."""
    out = []
    today, cal, settlement, issue, maturity = cb.cached_fixture()
    curve = cb.flat(settlement, 0.032)
    hw = cb.hull_white(curve)
    st = cb.plain(today)
    even, odd = cb.years(cal, issue, range(2, 10, 2)), cb.years(cal, issue, range(1, 10, 2))
    calls = cb.calls(even, 110.0) + cb.calls(odd, 95.0, cb.PUT)
    calls.sort(key=lambda c: c["date"])

    def zero(cs, cal_spec=cb.TARGET, dc=cb.THIRTY360_BB, convention=ql.ModifiedFollowing):
        return cb.zero_spec(3, 10000.0, cal_spec, maturity, dc, convention, 100.0, issue, cs)

    oas = cb.oas_spec(curve, cb.A365, cb.COMPOUNDED, cb.SEMIANNUAL, 72.0, settlement + 10, 0.004, 5e-4, 1e-12, 200,
                      0.002)
    with v.case_settings(today):
        times = cb.mandatory_times(zero(calls), curve, today)
    out += [
        ("cached / zero coupon on the empty calendar, unadjusted", st,
         zero(cb.calls(even, 110.0), None, convention=ql.Unadjusted), zero_engine("zero", cb.tree_steps(hw, 100, curve)),
         10000.0, None, None),
        ("cached / zero coupon on the empty calendar, following", st, zero(cb.calls(even, 110.0), None),
         zero_engine("zero", cb.tree_steps(hw, 100, curve)), 10000.0, None, None),
        ("cached / zero coupon with the empty day counter, tree", st, zero(calls, dc=None),
         zero_engine("zero", cb.tree_steps(hw, 120, curve)), 10000.0, oas, None),
        ("cached / zero coupon with the empty day counter, Black", st,
         zero([cb.callability_spec(100.0, cb.CLEAN, cb.CALL, even[1])], dc=None),
         zero_engine("zero", cb.black(0.2, curve)), 10000.0, None,
         cb.implied_spec(curve, [(80.0, cb.CLEAN)])),
        ("cached / zero coupon calls and puts, zero-coupon tree", st, zero(calls),
         zero_engine("zero", cb.tree_steps(hw, 240, curve)), 10000.0, oas, None),
        ("cached / zero coupon calls and puts, zero-coupon tree on a grid", st, zero(calls),
         zero_engine("zero", cb.tree_grid(hw, times, 90, curve)), 10000.0, oas, None),
    ]
    rt, rcal, rset, rissue, rmat = cb.runner_fixture()
    rcurve = cb.flat(rset, 0.03)
    rst = cb.plain(rt)
    rsched = cb.semiannual(rissue, rmat)
    at8 = cb.bd.make_schedule(rsched)[8]
    one_call = [cb.callability_spec(100.0, cb.CLEAN, cb.CALL, rcal.advance(rissue, 4, ql.Years))]

    def rzero(cs):
        return cb.zero_spec(3, 10000.0, cb.TARGET, rmat, cb.THIRTY360_BB, ql.ModifiedFollowing, 100.0, rissue, cs)

    def fixed(kind, coupon=0.01, redemption=100.0):
        return cb.fixed_spec(3, 10000.0, rsched, [coupon], cb.THIRTY360_BB, ql.ModifiedFollowing, redemption, rissue,
                             [cb.callability_spec(100.0, cb.CLEAN, kind, at8)])

    implied = cb.implied_spec(rcurve, [(78.5, cb.DIRTY), (78.5, cb.CLEAN)])
    out += [
        ("runner / zero coupon, one call, zero-coupon Black", rst, rzero(one_call),
         zero_engine("zero", cb.black(0.3, rcurve)), 10000.0, None, cb.implied_spec(rcurve, [(70.0, cb.CLEAN)])),
        ("runner / zero coupon, one call, zero-coupon Black on a structure", rst, rzero(one_call),
         zero_engine("zero", structure(0.3, rcurve, rt)), 10000.0, None, None),
        ("runner / a call, Black on a structure at the evaluation date", rst, fixed(cb.CALL),
         structure(0.2, rcurve, rt), 10000.0, None, implied),
        ("runner / a call, Black on a structure two days on, Thirty/360", rst, fixed(cb.CALL),
         structure(0.2, rcurve, rset, cb.THIRTY360_BB), 10000.0, None, None),
        ("runner / a put, Black on a moving structure", rst, fixed(cb.PUT), structure(0.25, rcurve, None, cb.A365, 2),
         10000.0, None, None),
        ("runner / a put, Black on a structure a year back", rst, fixed(cb.PUT, 0.04),
         structure(0.25, rcurve, rt - 365, cb.A360), 10000.0, None, None),
        ("runner / a negative redemption", rst, fixed(cb.CALL, 0.01, -5.0), cb.tree_steps(cb.hull_white(rcurve), 60),
         10000.0, None, None),
    ]
    return out


def cases():
    out = []
    with cxx.connect() as H:
        for name, st, spec, engine, scale, oas, implied in bonds():
            with H.case_settings(st[0].serialNumber(), include_reference_date_events=st[1],
                                 include_todays_cash_flows=st[2]) as s:
                inputs = {"settings": s, "bond": spec, "engine": engine}
                try:
                    dates = H.call("CallableBond::cashflowDates", spec)
                except RuntimeError:
                    out.append({"name": name, "inputs": inputs, "expected": {"construct": dict(v.THROWS)}})
                    continue

                def on(method, *args):
                    return v.attempt(lambda: H.call(method, spec, *args))

                def amount(method, *args):
                    r = on(method, *args)
                    return r if isinstance(r, dict) else v.amount(r, scale)

                expected = {"callabilityDates": on("CallableBond::callabilityDates"),
                            "callabilityTypes": on("CallableBond::callabilityTypes"),
                            "callabilityPrices": on("CallableBond::callabilityPrices"),
                            "cashflowDates": dates,
                            "cashflowAmounts": [v.amount(x, scale) for x in H.call("CallableBond::cashflowAmounts",
                                                                                     spec)],
                            "settlementDate": on("CallableBond::settlementDate"),
                            "maturityDate": on("CallableBond::maturityDate"),
                            "accruedAmount": on("CallableBond::accruedAmount"),
                            "NPV": amount("CallableBond::NPV", engine),
                            "settlementValue": amount("CallableBond::settlementValue", engine),
                            "cleanPrice": on("CallableBond::cleanPrice", engine),
                            "dirtyPrice": on("CallableBond::dirtyPrice", engine)}
                if oas is not None:
                    inputs["oas"] = oas
                    conv = (oas["curve"], oas["dayCounter"], oas["compounding"], oas["frequency"])
                    expected.update({
                        "OAS": on("CallableBond::OAS", engine, oas["cleanPrice"], *conv, oas["settlementDate"],
                                  oas["accuracy"], oas["maxIterations"], oas["guess"]),
                        "cleanPriceOAS": on("CallableBond::cleanPriceOAS", engine, oas["spread"], *conv,
                                            oas["settlementDate"]),
                        "effectiveDuration": on("CallableBond::effectiveDuration", engine, oas["spread"], *conv,
                                                oas["bump"]),
                        "effectiveConvexity": convexity(on("CallableBond::effectiveConvexity", engine, oas["spread"],
                                                           *conv, oas["bump"]))})
                if implied is not None:
                    inputs["implied"] = implied
                    expected["impliedVolatility"] = [
                        on("CallableBond::impliedVolatility", t, implied["discountCurve"], implied["accuracy"],
                           implied["maxEvaluations"], implied["minVol"], implied["maxVol"])
                        for t in implied["targets"]]
                out.append({"name": name, "inputs": inputs, "expected": expected})
    check_rows(out)
    return out


def check_rows(out):
    """The rows the file must hold."""
    by = {c["name"]: c["expected"] for c in out}
    assert by["cached / zero coupon on the empty calendar, following"] == {"construct": v.THROWS}
    unadjusted = by["cached / zero coupon on the empty calendar, unadjusted"]
    assert unadjusted["settlementDate"] == v.THROWS and unadjusted["NPV"] == v.THROWS
    assert by["cached / zero coupon with the empty day counter, tree"]["NPV"] != v.THROWS
    assert by["cached / zero coupon with the empty day counter, Black"]["NPV"] == v.THROWS
    assert by["runner / a negative redemption"]["NPV"] == v.THROWS


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/callablebonds/callablebond.hpp", cases)
