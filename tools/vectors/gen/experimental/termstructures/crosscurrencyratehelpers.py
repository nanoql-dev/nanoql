# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's cross-currency rate helpers (ql/experimental/termstructures/crosscurrencyratehelpers.hpp):
the constant-notional and mark-to-market basis swap helpers and the constant-notional fixed-vs-floating swap helper,
each case a set of them, 1 to 10 years, bootstrapping a piecewise curve (log-linear discounts, or linear zero yields)
on the evaluation date 15 January 2025, the discount curve of the currency the collateral isn't in, the other
currency's flat curve known: EUR (Euribor 3M) against USD (USD LIBOR 3M), the collateral in either currency and the
basis on either leg; semiannual payments, a payment lag on the coupons and on the notional exchanges, and NoFrequency
read as unset; either MtM leg resettable, FX resets two days before on TARGET; ESTR against SOFR paying quarterly;
fixed EUR against USD LIBOR, the collateral on either leg, and fixed EUR against SOFR paying semiannually; and the
helpers' refusals: a tenor shorter than the index's ("tenor (1M) is shorter than the coupon frequency"), and an
overnight index with no payment frequency.

Each case's outputs are whether the curve builds, QuantLib's refusal (a helper's or the bootstrap's) recorded as its
throw; then the curve's dates and maximum date; each helper's earliest, maturity, latest relevant, pillar and latest
dates; and the solved outputs: the curve's data, its discounts at 12 dates and each helper's implied quote on it. The
stability filter runs on each case, as in experimental/termstructures/basisswapratehelpers.py: a solved output is
written only where moving every quote one ulp, together and alone, leaves it within 1e-12 x max(|a|, |b|, 1); a
dropped one is written null and its index listed in the inputs' "dropped". The generator prints "dropped n of m
outputs" per case and for the file.

Each case's inputs hold its settings, "curve" ({"traits": "Discount" or "ZeroYield", "dayCounter"}, built on the
evaluation date), "helpers" (a list of {"kind": "ConstNotionalBasis", "MtMBasis" or "FixedVsFloating", "quote",
"tenor", "fixingDays", "calendar", "convention", "endOfMonth", "collateralCurve", "paymentLag",
"useIndexedCoupons"}, with "baseIndex", "quoteIndex", "isFxBaseCurrencyCollateralCurrency",
"isBasisOnFxBaseCurrencyLeg", "paymentFrequency" and "quoteCurrencyPaymentFrequency" for the basis helpers,
"paymentLagOnNotionalExchanges" for the constant-notional one, "isFxBaseCurrencyLegResettable", "fxResetFixingDays"
and "fxResetFixingCalendar" for the MtM one; "fixedFrequency", "fixedDayCount", "floatIndex", "collateralOnFixedLeg"
and "floatPaymentFrequency" for the fixed-vs-floating one), "dates" and "dropped". Indexes and curves are
instruments/constnotionalcrosscurrencyswap.py's specs; a null frequency, calendar or coupon choice is QuantLib's
default (NoFrequency, the empty calendar, std::nullopt); the wheel's MtM helper takes its arguments after the
resettable flag by name, since it can't build QuantLib's empty calendar. Read back by
tests/NanoQL.Tests/Vectors/Experimental/TermStructures/CrossCurrencyRateHelpersVectorTests.cs."""
import importlib.util
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_constnotionalcrosscurrencyswap",
    os.path.join(v.GEN_ROOT, "instruments", "constnotionalcrosscurrencyswap.py"))
xcs = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(xcs)
irx = xcs.irx

TODAY, M, Y = xcs.TODAY, xcs.M, xcs.Y
STABLE = 1e-12
TARGET = irx.TARGET
F, MF = int(ql.Following), int(ql.ModifiedFollowing)
TENORS = [(1, Y), (2, Y), (3, Y), (5, Y), (7, Y), (10, Y)]
BASES = [-0.0015, -0.0018, -0.0020, -0.0023, -0.0024, -0.0025]
FIXED = [0.0265, 0.0258, 0.0255, 0.0256, 0.0259, 0.0263]
E3M, USD3M, ESTR, SOFR = xcs.E3M, xcs.USD3M, xcs.ESTR, xcs.SOFR
USD_KNOWN, EUR_KNOWN = xcs.USD_DISCOUNT, xcs.EUR_DISCOUNT
NO_FREQUENCY = int(ql.NoFrequency)


def common(kind, quote, tenor, collateral, lag=0, indexed=None, convention=F, eom=False):
    return {"kind": kind, "quote": quote, "tenor": [tenor[0], tenor[1]], "fixingDays": 2, "calendar": TARGET,
            "convention": convention, "endOfMonth": eom, "collateralCurve": collateral, "paymentLag": lag,
            "useIndexedCoupons": indexed}


def const_basis(base_collateral, basis_on_base, collateral, base=E3M, quote=USD3M, frequency=None,
                quote_frequency=None, lag=0, lag_on_exchanges=False, tenors=None, bases=None):
    return [dict(common("ConstNotionalBasis", q, t, collateral, lag), baseIndex=base, quoteIndex=quote,
                 isFxBaseCurrencyCollateralCurrency=base_collateral, isBasisOnFxBaseCurrencyLeg=basis_on_base,
                 paymentFrequency=frequency, quoteCurrencyPaymentFrequency=quote_frequency,
                 paymentLagOnNotionalExchanges=lag_on_exchanges)
            for t, q in zip(tenors or TENORS, bases or BASES)]


def mtm_basis(base_collateral, basis_on_base, base_resettable, collateral, base=E3M, quote=USD3M, frequency=None,
              quote_frequency=None, fx_days=0, fx_calendar=None, lag=0):
    return [dict(common("MtMBasis", q, t, collateral, lag), baseIndex=base, quoteIndex=quote,
                 isFxBaseCurrencyCollateralCurrency=base_collateral, isBasisOnFxBaseCurrencyLeg=basis_on_base,
                 isFxBaseCurrencyLegResettable=base_resettable, paymentFrequency=frequency,
                 quoteCurrencyPaymentFrequency=quote_frequency, fxResetFixingDays=fx_days,
                 fxResetFixingCalendar=fx_calendar)
            for t, q in zip(TENORS, BASES)]


def fixed_float(collateral_on_fixed, collateral, index=USD3M, float_frequency=None, lag=0, indexed=None):
    return [dict(common("FixedVsFloating", q, t, collateral, lag, indexed, MF), fixedFrequency=int(ql.Annual),
                 fixedDayCount=irx.THIRTY360_BB, floatIndex=index, collateralOnFixedLeg=collateral_on_fixed,
                 floatPaymentFrequency=float_frequency)
            for t, q in zip(TENORS, FIXED)]


def frequency(x):
    return ql.NoFrequency if x is None else x


def make_helper(spec, quote):
    q = ql.QuoteHandle(ql.SimpleQuote(quote))
    head = (q, ql.Period(*spec["tenor"]), spec["fixingDays"], irx.calendar(spec["calendar"]), spec["convention"],
            spec["endOfMonth"])
    collateral = irx.handle(spec["collateralCurve"])
    if spec["kind"] == "FixedVsFloating":
        return ql.ConstNotionalCrossCurrencySwapRateHelper(
            *head, spec["fixedFrequency"], irx.day_counter(spec["fixedDayCount"]), xcs.make_index(spec["floatIndex"]),
            collateral, spec["collateralOnFixedLeg"], spec["paymentLag"], spec["useIndexedCoupons"],
            spec["floatPaymentFrequency"])
    args = head + (xcs.make_index(spec["baseIndex"]), xcs.make_index(spec["quoteIndex"]), collateral,
                   spec["isFxBaseCurrencyCollateralCurrency"], spec["isBasisOnFxBaseCurrencyLeg"])
    if spec["kind"] == "ConstNotionalBasis":
        return ql.ConstNotionalCrossCurrencyBasisSwapRateHelper(
            *args, frequency(spec["paymentFrequency"]), spec["paymentLag"],
            frequency(spec["quoteCurrencyPaymentFrequency"]), spec["useIndexedCoupons"],
            spec["paymentLagOnNotionalExchanges"])
    kw = {"paymentFrequency": frequency(spec["paymentFrequency"]), "paymentLag": spec["paymentLag"],
          "quoteCurrencyPaymentFrequency": frequency(spec["quoteCurrencyPaymentFrequency"]),
          "fxResetFixingDays": spec["fxResetFixingDays"], "useIndexedCoupons": spec["useIndexedCoupons"]}
    if spec["fxResetFixingCalendar"] is not None:
        kw["fxResetFixingCalendar"] = irx.calendar(spec["fxResetFixingCalendar"])
    return ql.MtMCrossCurrencyBasisSwapRateHelper(*args, spec["isFxBaseCurrencyLegResettable"], **kw)


def make_curve(spec, helpers):
    dc = irx.day_counter(spec["dayCounter"])
    if spec["traits"] == "ZeroYield":
        return ql.PiecewiseLinearZero(TODAY, helpers, dc)
    return ql.PiecewiseLogLinearDiscount(TODAY, helpers, dc)


DISCOUNT = {"traits": "Discount", "dayCounter": irx.A365}
ZERO = {"traits": "ZeroYield", "dayCounter": irx.A365}

# (label, curve, helpers)
CASES = [
    ("constant notional, USD collateral, the basis on the EUR leg", DISCOUNT, const_basis(False, True, USD_KNOWN)),
    ("constant notional, EUR collateral, the basis on the USD leg", DISCOUNT, const_basis(True, False, EUR_KNOWN)),
    ("constant notional, semiannual, payment lags on the exchanges too, linear zeros", ZERO,
     const_basis(False, True, USD_KNOWN, frequency=int(ql.Semiannual), lag=2, lag_on_exchanges=True)),
    ("constant notional, NoFrequency read as unset", DISCOUNT,
     const_basis(False, True, USD_KNOWN, frequency=NO_FREQUENCY, quote_frequency=NO_FREQUENCY)),
    ("constant notional, ESTR against SOFR, quarterly", DISCOUNT,
     const_basis(False, True, USD_KNOWN, ESTR, SOFR, int(ql.Quarterly))),
    ("constant notional, a tenor shorter than the index's", DISCOUNT,
     const_basis(False, True, USD_KNOWN, tenors=[(1, M)], bases=[-0.001])),
    ("MtM, the USD leg resettable, USD collateral", DISCOUNT, mtm_basis(False, True, False, USD_KNOWN)),
    ("MtM, the EUR leg resettable, EUR collateral, FX resets two days before", DISCOUNT,
     mtm_basis(True, False, True, EUR_KNOWN, fx_days=2, fx_calendar=TARGET)),
    ("MtM, ESTR against SOFR, quarterly, semiannual on the USD leg", ZERO,
     mtm_basis(False, True, False, USD_KNOWN, ESTR, SOFR, int(ql.Quarterly), int(ql.Semiannual))),
    ("MtM, an overnight leg with no payment frequency", DISCOUNT,
     mtm_basis(False, True, False, USD_KNOWN, ESTR, SOFR)),
    ("fixed EUR against USD LIBOR, the collateral on the fixed leg", DISCOUNT, fixed_float(True, EUR_KNOWN)),
    ("fixed EUR against USD LIBOR, the collateral on the floating leg, payment lag 2", DISCOUNT,
     fixed_float(False, USD_KNOWN, lag=2)),
    ("fixed EUR against SOFR, semiannual", ZERO, fixed_float(True, EUR_KNOWN, SOFR, int(ql.Semiannual))),
    ("fixed EUR against SOFR, no payment frequency", DISCOUNT, fixed_float(True, EUR_KNOWN, SOFR)),
]

SAMPLE = [TODAY + n for n in (1, 30, 91, 182, 365, 548, 730, 1096, 1461, 1826, 2922, 3700)]


def solved(curve_spec, helper_specs, quotes):
    """The solved outputs of a build, with the curve and its helpers."""
    helpers = [make_helper(h, q) for h, q in zip(helper_specs, quotes)]
    curve = make_curve(curve_spec, helpers)
    curve.enableExtrapolation()
    out = {"data": list(curve.data()), "discounts": [curve.discount(d) for d in SAMPLE],
           "impliedQuotes": [h.impliedQuote() for h in helpers]}
    return out, curve, helpers


def stable(base, moved):
    return abs(base - moved) <= STABLE * max(abs(base), abs(moved), 1.0)


def moves(quotes):
    out = [[math.nextafter(x, math.inf) for x in quotes], [math.nextafter(x, -math.inf) for x in quotes]]
    for i in range(len(quotes)):
        for direction in (math.inf, -math.inf):
            moved = list(quotes)
            moved[i] = math.nextafter(quotes[i], direction)
            out.append(moved)
    return out


def cases():
    out = []
    total = [0, 0]
    for label, curve_spec, helper_specs in CASES:
        with v.case_settings(TODAY) as st:
            quotes = [h["quote"] for h in helper_specs]
            inputs = {"settings": st, "curve": curve_spec, "helpers": helper_specs, "dates": v.enc(SAMPLE)}
            try:
                base, curve, helpers = solved(curve_spec, helper_specs, quotes)
            except RuntimeError:
                out.append({"name": label, "inputs": dict(inputs, dropped=None),
                            "expected": {"built": dict(v.THROWS)}})
                continue
            moved = [solved(curve_spec, helper_specs, m)[0] for m in moves(quotes)]
            dropped = {}
            for name, values in base.items():
                keep = [all(stable(values[i], m[name][i]) for m in moved) for i in range(len(values))]
                dropped[name] = [i for i, kept in enumerate(keep) if not kept]
                base[name] = [x if kept else None for x, kept in zip(values, keep)]
                total[0] += len(dropped[name])
                total[1] += len(values)
            print(f"{label}: dropped {sum(len(d) for d in dropped.values())} of "
                  f"{sum(len(x) for x in base.values())} outputs")
            expected = {"built": True, "dates": v.enc(list(curve.dates())), "maxDate": v.enc(curve.maxDate()),
                        "earliestDates": v.enc([h.earliestDate() for h in helpers]),
                        "maturityDates": v.enc([h.maturityDate() for h in helpers]),
                        "latestRelevantDates": v.enc([h.latestRelevantDate() for h in helpers]),
                        "pillarDates": v.enc([h.pillarDate() for h in helpers]),
                        "latestDates": v.enc([h.latestDate() for h in helpers])}
            expected.update(base)
            out.append({"name": label, "inputs": dict(inputs, dropped=dropped), "expected": expected})
    print(f"experimental/termstructures/crosscurrencyratehelpers: dropped {total[0]} of {total[1]} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/termstructures/crosscurrencyratehelpers.hpp", cases)
