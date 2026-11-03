# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SwaptionHelper (ql/models/shortrate/calibrationhelpers/swaptionhelper.hpp), with its base
BlackCalibrationHelper (ql/models/calibrationhelper.hpp): 57 helpers, 48 unlagged twins and 8 refusals, on two
markets.

The first market is QuantLib's test-suite's (test-suite/shortratemodels.cpp): the evaluation date 15 February 2002, a
flat 4.875825% curve from the settlement date 19 February 2002 on Actual/365 (Fixed), Euribor 6M on it, and its
coterminal helpers (1Y to 5Y options on swaps ending after 6 years, 11.48% to 10%). The second is the evaluation date
15 January 2025, a flat 2.7% discount curve and indexes forecasting on a flat 3.1% one: Euribor-style 6M and 3M Ibor
indexes, one with no fixing days, and an ESTR-style overnight index, which builds an overnight-indexed swap (on its
1D tenor's daily floating schedule, so its times run to thousands).

The helpers cover QuantLib's three constructors (an option tenor and a swap length, an exercise date and a length, an
exercise date and an end date), strikes (none, at the money, and below and above the forward, which choose a receiver
or a payer), nominals of 1 to 10 million, the three error types, normal volatilities and shifted lognormal ones with a
shift, settlement days (none, which takes the index's value date, and 0, 2 and 5, whose value isn't read), both
averaging methods of the overnight-indexed swap, fixed legs of 1Y and 6M on Thirty/360 and Actual/360, and an
exercise on the evaluation date, which has expired.

Each helper's outputs are its market value; the wheel's swaption shims (the exercise date, the strike, the nominal,
the swap's maturity) and the underlying's type and both schedules' dates; times(), the discretized swaption's
mandatory times on the term structure's reference date and day counter; the Black price at half, once and twice the
quoted volatility; the model value and the calibration error with a JamshidianSwaptionEngine on a HullWhite and on a
Vasicek (with the term structure, since Vasicek has no curve of its own); the volatility the Hull-White value implies
(accuracy 1e-10, 1,000 evaluations, within the error type's bracket); for an implied-volatility error, the errors under
two more Hull-White models ("clamps", sigma 1e-5 and 1), whose values fall below and above the Black prices at the
bracket's ends, so the implied volatility is clamped to them ("calibrationErrorClamped"); and, for the expired helper, its model value with
no engine, 0, since QuantLib's swaption reads its expiry before its engine. The refusals: an unknown error type (which
QuantLib refuses only in calibrationError), an unknown volatility type (in the market value), no engine (in the model
value and the error), an implied volatility whose guess, the quote, is outside the bracket or whose target the bracket
doesn't hold, and no term structure. Amounts are compared at the nominal.

A helper's times are generated and left out where nanoql's fix moves its discretized swaption (the tree swaption file's
snapping_moves): QuantLib snaps a swap date in the week after the exercise back to it, as the index's fixing days put
the swap's start, so the times hold the exercise date where nanoql's hold the start
(tests/NanoQL.Tests/Divergences/TreeSwaptionSnappingTests.cs). Each such helper whose unlagged twin, the same helper on
its index with no fixing days, nothing moves has that twin added (" / unlagged"), every output kept: 54 of the 57
helpers' times are left out. The overnight-indexed ones, whose daily floating dates in the week after the exercise snap
without a lag, and the exercise-date forms exercised on a Sunday (18 January 2026), whose swap starts the next business
day without fixing days, get none.

The at-par switch is set once for the run (at par, the wheel's default) and recorded in every case's settings, since the
wheel's helper reads it whenever it recalculates; each case builds its own objects.

A helper spec is {"form" ("maturity", "exerciseDate" or "endDate"), "maturity" and "length" ([length, units]),
"exerciseDate", "endDate" (serials), "volatility", "index" (a spec of instruments/fixedvsfloatingswap.py),
"fixedLegTenor", "fixedLegDayCounter", "floatingLegDayCounter", "termStructure" (a flat forward spec, or null for
QuantLib's empty handle), "errorType", "strike" (null for Null<Real>(), at the money), "nominal", "type", "shift",
"settlementDays" (null for Null<Size>()), "averagingMethod"}, the constructor's arguments in order; a Hull-White model
is {"a", "sigma"} on the helper's term structure, a Vasicek {"r0", "a", "b", "sigma", "lambda"}. The wheel's
SwaptionHelper is the proxy of the C++ constructors, which take their arguments by position only, so a null strike is
Null<Real>() and null settlement days Null<Size>() (2147483647), each passed by position. The functions here build
the specs and the helpers that models/model.py shares. Read back by
tests/NanoQL.Tests/Vectors/Models/SwaptionHelperVectorTests.cs."""
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


fvf = _load("wheel_fixedvsfloatingswap", "instruments/fixedvsfloatingswap.py")
tse = _load("wheel_treeswaptionengine", "pricingengines/swaption/treeswaptionengine.py")
irx, ibc = fvf.irx, fvf.ibc

D, W, M, Y = ql.Days, ql.Weeks, ql.Months, ql.Years
TARGET, A360, A365, THIRTY360_BB = irx.TARGET, irx.A360, irx.A365, irx.THIRTY360_BB
REL, PRICE, IMPLIED = (int(ql.BlackCalibrationHelper.RelativePriceError), int(ql.BlackCalibrationHelper.PriceError),
                       int(ql.BlackCalibrationHelper.ImpliedVolError))
SL, N = int(ql.ShiftedLognormal), int(ql.Normal)
COMPOUND, SIMPLE = int(ql.RateAveraging.Compound), int(ql.RateAveraging.Simple)
# QuantLib's Null<Size>(), which the wheel's positional Natural argument keeps.
NULL_SIZE = 2147483647

# QuantLib's test-suite market.
TODAY_2002 = ql.Date(15, ql.February, 2002)
CURVE_2002 = irx.curve_spec(ql.Date(19, ql.February, 2002), 0.04875825, A365)
EURIBOR6M_2002 = fvf.preset("Euribor6M", CURVE_2002)
COTERMINAL = [(1, 5, 0.1148), (2, 4, 0.1108), (3, 3, 0.1070), (4, 2, 0.1021), (5, 1, 0.1000)]

# The second market.
TODAY = irx.TODAY
DISCOUNT = fvf.curve(0.027)
FORECAST = fvf.curve(0.031)
E6M = ibc.euribor((6, M), FORECAST)
E3M = ibc.euribor((3, M), FORECAST)
E6M0 = irx.ibor_spec("Euribor", (6, M), 0, TARGET, ql.ModifiedFollowing, True, A360, FORECAST)
ESTR = irx.overnight_spec("ESTR", 0, TARGET, A360, FORECAST)

HULL_WHITE = {"a": 0.05, "sigma": 0.01}
# Hull-White models whose values fall below and above the Black prices at the implied-volatility bracket's ends.
CLAMPS = [{"a": 0.05, "sigma": 1e-5}, {"a": 0.05, "sigma": 1.0}]
VASICEK = {"r0": 0.03, "a": 0.08, "b": 0.035, "sigma": 0.009, "lambda": 0.0}


def helper_spec(form, first, second, vol, index, ts, fixed_tenor=(1, Y), fixed_dc=THIRTY360_BB, float_dc=A360,
                error_type=REL, strike=None, nominal=1.0, vol_type=SL, shift=0.0, settlement_days=None,
                averaging=COMPOUND):
    """A helper spec; first and second are the form's two leading arguments: ([n, units], [n, units]) for
    "maturity", (date, [n, units]) for "exerciseDate", (date, date) for "endDate"."""
    spec = {"form": form}
    if form == "maturity":
        spec["maturity"], spec["length"] = [first[0], int(first[1])], [second[0], int(second[1])]
    elif form == "exerciseDate":
        spec["exerciseDate"], spec["length"] = v.enc(first), [second[0], int(second[1])]
    elif form == "endDate":
        spec["exerciseDate"], spec["endDate"] = v.enc(first), v.enc(second)
    else:
        raise ValueError(f"unknown form {form}")
    spec.update({"volatility": vol, "index": index, "fixedLegTenor": [fixed_tenor[0], int(fixed_tenor[1])],
                 "fixedLegDayCounter": fixed_dc, "floatingLegDayCounter": float_dc, "termStructure": ts,
                 "errorType": int(error_type), "strike": strike, "nominal": nominal, "type": int(vol_type),
                 "shift": shift, "settlementDays": settlement_days, "averagingMethod": int(averaging)})
    return spec


def make_helper(spec):
    """The wheel's helper for a spec, every argument by position."""
    form = spec["form"]
    if form == "maturity":
        head = [ql.Period(*spec["maturity"]), ql.Period(*spec["length"])]
    elif form == "exerciseDate":
        head = [ql.Date(spec["exerciseDate"]), ql.Period(*spec["length"])]
    else:
        head = [ql.Date(spec["exerciseDate"]), ql.Date(spec["endDate"])]
    return ql.SwaptionHelper(
        *head, ql.QuoteHandle(ql.SimpleQuote(spec["volatility"])), fvf.make_index(spec["index"]),
        ql.Period(*spec["fixedLegTenor"]), irx.day_counter(spec["fixedLegDayCounter"]),
        irx.day_counter(spec["floatingLegDayCounter"]), irx.handle(spec["termStructure"]), spec["errorType"],
        v.NULL_REAL if spec["strike"] is None else spec["strike"], spec["nominal"], spec["type"], spec["shift"],
        NULL_SIZE if spec["settlementDays"] is None else spec["settlementDays"], spec["averagingMethod"])


def hull_white(ts_spec, spec):
    return ql.HullWhite(irx.handle(ts_spec), spec["a"], spec["sigma"])


def vasicek(spec):
    return ql.Vasicek(spec["r0"], spec["a"], spec["b"], spec["sigma"], spec["lambda"])


def implied_bracket(spec):
    """calibrationError's bracket for the helper's volatility type (calibrationhelper.cpp:50-51)."""
    return (0.0010, 10.0) if spec["type"] == SL else (0.00005, 0.50)


def helpers():
    """(name, spec) for the 57 helpers."""
    out = []
    # QuantLib's coterminal helpers, three error types, with the cases' fixed leg.
    for et in (REL, PRICE, IMPLIED):
        for start, length, vol in COTERMINAL:
            out.append((f"2002 {start}Y x {length}Y / error type {et}",
                        helper_spec("maturity", (start, Y), (length, Y), vol, EURIBOR6M_2002, CURVE_2002,
                                    error_type=et)))
    # 15 so far: the three constructors for the same swaptions, on the second market.
    exercise = ql.TARGET().advance(TODAY, 2, Y, ql.ModifiedFollowing)
    end = ql.TARGET().advance(exercise, 7, Y, ql.ModifiedFollowing)
    for name, index in (("E6M", E6M), ("E3M", E3M), ("E6M, no fixing days", E6M0)):
        out.append((f"2025 maturity form / {name}",
                    helper_spec("maturity", (2, Y), (7, Y), 0.21, index, DISCOUNT, fixed_dc=A360)))
        out.append((f"2025 exercise-date form / {name}",
                    helper_spec("exerciseDate", exercise, (7, Y), 0.21, index, DISCOUNT, fixed_dc=A360)))
        out.append((f"2025 end-date form / {name}",
                    helper_spec("endDate", exercise, end, 0.21, index, DISCOUNT, fixed_dc=A360)))
    # 24 so far: strikes and nominals.
    for k, (strike, nominal, et) in enumerate([(0.01, 1.0, REL), (0.025, 1e6, PRICE), (0.031, 1.0, IMPLIED),
                                                (0.04, 1e6, REL), (0.06, 1e7, PRICE), (0.02, 1000.0, IMPLIED)]):
        out.append((f"strike {strike} / nominal {nominal} / error type {et}",
                    helper_spec("maturity", (1, Y), (5, Y), 0.19, E6M, DISCOUNT, error_type=et, strike=strike,
                                nominal=nominal)))
    # 30 so far: normal and shifted volatilities.
    for k, (vol_type, vol, shift, et) in enumerate([(N, 0.0085, 0.0, REL), (N, 0.0085, 0.0, IMPLIED),
                                                     (N, 0.006, 0.0, PRICE), (SL, 0.15, 0.01, REL),
                                                     (SL, 0.15, 0.01, IMPLIED), (SL, 0.12, 0.02, PRICE)]):
        out.append((f"volatility type {vol_type} / {vol} / shift {shift} / error type {et}",
                    helper_spec("maturity", (3, Y), (4, Y), vol, E6M, DISCOUNT, error_type=et, vol_type=vol_type,
                                shift=shift)))
    # 36 so far: settlement days, whose value isn't read.
    for days in (0, 2, 5):
        out.append((f"settlement days {days}",
                    helper_spec("maturity", (1, Y), (3, Y), 0.2, E6M, DISCOUNT, settlement_days=days)))
        out.append((f"settlement days {days}, exercise-date form",
                    helper_spec("exerciseDate", ql.Date(18, ql.January, 2026), (3, Y), 0.2, E6M, DISCOUNT,
                                settlement_days=days)))
    # 42 so far: fixed legs.
    for tenor, dc in (((6, M), A360), ((6, M), THIRTY360_BB), ((3, M), A365)):
        out.append((f"fixed leg {tenor[0]}{'MY'[tenor[1] == Y]} / {dc['type']}",
                    helper_spec("maturity", (2, Y), (3, Y), 0.2, E6M, DISCOUNT, fixed_tenor=tenor, fixed_dc=dc)))
    # 45 so far: the overnight-indexed swap, short, so that its daily floating schedule stays small.
    for averaging in (COMPOUND, SIMPLE):
        out.append((f"overnight index / averaging {averaging}",
                    helper_spec("maturity", (6, M), (1, Y), 0.22, ESTR, DISCOUNT, fixed_dc=A360,
                                averaging=averaging)))
    out.append(("overnight index / strike 0.035 / settlement days 2",
                helper_spec("exerciseDate", ql.Date(15, ql.July, 2025), (6, M), 0.22, ESTR, DISCOUNT,
                            fixed_dc=A360, strike=0.035, settlement_days=2)))
    # 48 so far: the exercise on the evaluation date, expired; long and short swaptions.
    out.append(("exercise on the evaluation date",
                helper_spec("exerciseDate", TODAY, (5, Y), 0.2, E6M, DISCOUNT)))
    out.append(("exercise on the evaluation date, end-date form",
                helper_spec("endDate", TODAY, ql.Date(17, ql.January, 2028), 0.2, E6M, DISCOUNT, strike=0.03)))
    for k, (option, length, vol) in enumerate([((10, Y), (10, Y), 0.16), ((1, Y), (1, Y), 0.3), ((6, M), (2, Y), 0.25),
                                               ((3, M), (10, Y), 0.18), ((7, Y), (3, Y), 0.17),
                                               ((15, Y), (5, Y), 0.15)]):
        out.append((f"{option[0]}{'MY'[option[1] == Y]} x {length[0]}Y",
                    helper_spec("maturity", option, length, vol, E6M, DISCOUNT, nominal=1e6 if k % 2 else 1.0)))
    # 56 so far: a receiver struck at the money's forward exactly.
    out.append(("the 2002 1Y x 5Y helper struck at the forward",
                helper_spec("maturity", (1, Y), (5, Y), 0.1148, EURIBOR6M_2002, CURVE_2002,
                            strike=0.04999591410983986)))
    assert len(out) == 57, len(out)
    return out


def priced_outputs(spec, helper):
    """A built helper's outputs."""
    scale = spec["nominal"]
    ts = spec["termStructure"]
    out = {"marketValue": v.amount(helper.marketValue(), scale),
           "expiry": v.enc(helper.swaptionExpiryDate()), "strike": helper.swaptionStrike(),
           "nominal": helper.swaptionNominal(), "maturity": v.enc(helper.swaptionMaturityDate()),
           "underlyingType": int(helper.underlying().type()),
           "fixedDates": v.enc(list(helper.underlying().fixedSchedule().dates())),
           "floatingDates": v.enc(list(helper.underlying().floatingSchedule().dates())),
           "times": v.enc(list(helper.times())),
           "blackPrice": [v.amount(helper.blackPrice(f * spec["volatility"]), scale) for f in (0.5, 1.0, 2.0)]}
    hw = hull_white(ts, HULL_WHITE)
    helper.setPricingEngine(ql.JamshidianSwaptionEngine(hw))
    hw_value = helper.modelValue()
    out["modelValueHullWhite"] = v.amount(hw_value, scale)
    out["calibrationErrorHullWhite"] = v.amount(helper.calibrationError(), scale if spec["errorType"] == PRICE else 1.0)
    low, high = implied_bracket(spec)
    out["impliedVolatility"] = v.attempt(lambda: helper.impliedVolatility(hw_value, 1e-10, 1000, low, high))
    va = vasicek(VASICEK)
    helper.setPricingEngine(ql.JamshidianSwaptionEngine(va, irx.handle(ts)))
    out["modelValueVasicek"] = v.amount(helper.modelValue(), scale)
    out["calibrationErrorVasicek"] = v.amount(helper.calibrationError(), scale if spec["errorType"] == PRICE else 1.0)
    if spec["errorType"] == IMPLIED:
        out["calibrationErrorClamped"] = []
        for m in CLAMPS:
            helper.setPricingEngine(ql.JamshidianSwaptionEngine(hull_white(ts, m)))
            out["calibrationErrorClamped"].append(helper.calibrationError())
    return out


def today_of(spec):
    return TODAY_2002 if spec["termStructure"] is CURVE_2002 else TODAY


def times_move(spec):
    """Whether nanoql's fix moves a helper's times, its expiry unread: its times build the discretized swaption whatever
    its expiry."""
    with v.case_settings(today_of(spec), use_at_par_coupons=True):
        return tse.snapping_moves(make_helper(spec).swaption())


def unlagged(spec):
    """A helper's twin on its Ibor index with no fixing days, so that its swap starts on its exercise date."""
    index = spec["index"]
    if index["type"] == "Euribor6M":
        index = irx.ibor_spec("Euribor", (6, M), 0, TARGET, ql.ModifiedFollowing, True, A360, index["curve"])
    elif index["type"] == "IborIndex":
        index = dict(index, settlementDays=0)
    else:
        return None
    return dict(spec, index=index)


def helper_case(name, spec):
    today = today_of(spec)
    with v.case_settings(today, use_at_par_coupons=True) as st:
        helper = make_helper(spec)
        expected = priced_outputs(spec, helper)
        if times_move(spec):
            del expected["times"]
        if helper.swaption().isExpired():
            fresh = make_helper(spec)
            expected["modelValueNoEngine"] = v.amount(fresh.modelValue(), spec["nominal"])
        inputs = {"settings": st, "helper": spec, "hullWhite": HULL_WHITE, "vasicek": VASICEK}
        if spec["errorType"] == IMPLIED:
            inputs["clamps"] = CLAMPS
        return {"name": name, "inputs": inputs, "expected": expected}


def refusal_cases():
    out = []

    def add(name, spec, outputs):
        with v.case_settings(TODAY, use_at_par_coupons=True) as st:
            expected = {}
            for output in outputs:
                helper = make_helper(spec)
                if output == "calibrationError":
                    helper.setPricingEngine(ql.JamshidianSwaptionEngine(hull_white(spec["termStructure"],
                                                                                   HULL_WHITE)))
                    expected[output] = v.attempt(helper.calibrationError)
                elif output == "calibrationErrorNoEngine":
                    expected[output] = v.attempt(helper.calibrationError)
                elif output == "modelValueNoEngine":
                    expected[output] = v.attempt(helper.modelValue)
                elif output == "marketValue":
                    expected[output] = v.attempt(helper.marketValue)
                elif output == "impliedGuessOutside":
                    expected[output] = v.attempt(lambda: helper.impliedVolatility(0.01, 1e-10, 100, 0.25, 1.0))
                elif output == "impliedNotBracketed":
                    expected[output] = v.attempt(lambda: helper.impliedVolatility(10.0, 1e-10, 100, 0.01, 1.0))
                else:
                    raise ValueError(output)
            out.append({"name": "refused / " + name,
                        "inputs": {"settings": st, "helper": spec, "hullWhite": HULL_WHITE},
                        "expected": expected})

    base = dict(form="maturity", first=(1, Y), second=(5, Y), vol=0.2, index=E6M, ts=DISCOUNT)

    def spec_of(**kw):
        a = dict(base, **kw)
        return helper_spec(a.pop("form"), a.pop("first"), a.pop("second"), a.pop("vol"), a.pop("index"),
                           a.pop("ts"), **a)

    add("unknown error type", spec_of(error_type=7), ["calibrationError"])
    add("unknown volatility type", spec_of(vol_type=7), ["marketValue"])
    add("no engine", spec_of(), ["modelValueNoEngine", "calibrationErrorNoEngine"])
    add("implied volatility, guess outside the bracket", spec_of(), ["impliedGuessOutside"])
    add("implied volatility, target not bracketed", spec_of(), ["impliedNotBracketed"])
    add("no term structure", spec_of(ts=None), ["marketValue"])
    add("no term structure, dated form", spec_of(form="exerciseDate", first=ql.Date(15, ql.January, 2026), ts=None),
        ["marketValue"])
    add("normal volatility, implied guess outside the bracket", spec_of(vol_type=N, vol=0.008), ["impliedGuessOutside"])
    return out


def cases():
    out = []
    for name, spec in helpers():
        out.append(helper_case(name, spec))
        twin = unlagged(spec) if times_move(spec) else None
        if twin is not None and not times_move(twin):
            out.append(helper_case(name + " / unlagged", twin))
    out += refusal_cases()
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    # A strike above the forward makes a payer, below it a receiver.
    assert by_name["strike 0.06 / nominal 10000000.0 / error type 1"]["underlyingType"] == int(ql.Swap.Payer)
    assert by_name["strike 0.01 / nominal 1.0 / error type 0"]["underlyingType"] == int(ql.Swap.Receiver)
    # settlementDays' value isn't read: 0, 2 and 5 give the same swap.
    days = [by_name[f"settlement days {d}"] for d in (0, 2, 5)]
    assert days[0]["fixedDates"] == days[1]["fixedDates"] == days[2]["fixedDates"]
    # The clamps: below the bracket, the lower end less the quote; above it, the upper end less the quote.
    clamped = by_name["2002 1Y x 5Y / error type 2"]["calibrationErrorClamped"]
    assert clamped == [0.0010 - 0.1148, 10.0 - 0.1148], clamped
    # The expired helper's swaption is worth 0, with or without an engine.
    expired = by_name["exercise on the evaluation date"]
    assert expired["marketValue"]["value"] == 0.0 and expired["modelValueNoEngine"]["value"] == 0.0
    for c in out[-8:]:
        assert all(x == v.THROWS for x in c["expected"].values()), c["name"]


if __name__ == "__main__":
    v.main(__file__, "ql/models/shortrate/calibrationhelpers/swaptionhelper.hpp", cases)
