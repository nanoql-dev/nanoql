# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's basis swap rate helpers (ql/experimental/termstructures/basisswapratehelpers.hpp): the
IBOR-IBOR, overnight-IBOR and overnight-overnight helpers, each case a set of them, 1 to 10 years, bootstrapping a
piecewise curve (linear zero yields, or log-linear discounts) on the evaluation date 15 January 2025, the other index's
curve known and flat: Euribor 3M bootstrapped against a known 6M and the reverse, with a payment lag, at-par coupons and
the forward rule; USD LIBOR 6M bootstrapped against a known SOFR with the basis on the LIBOR leg, and SOFR bootstrapped
against a known USD LIBOR 3M, averaged, paying quarterly; SOFR bootstrapped against a known Fed Funds, paying quarterly,
and Fed Funds against a known SOFR, averaged, with a payment lag; each with an external discount curve or discounting on
the curve being built, which the IBOR-IBOR helper refuses ("discounting term structure handle is empty", its swap's
engine on the empty handle, basisswapratehelpers.cpp:129). Telescopic value dates are left out: nanoql builds them in
full (tests/NanoQL.Tests/Divergences/TelescopicValueDatesTests.cs).

Each case's outputs are whether the curve builds, QuantLib's refusal recorded as its throw; then the curve's dates and
maximum date; each helper's earliest, maturity, latest relevant, pillar and latest dates; and the solved outputs: the
curve's data, its discounts at 12 dates and each helper's implied quote on it. The stability filter of the S7 plan's
S7R6 runs on each case: the curve is rebuilt with its helpers' quotes moved one ulp up, then down, together and each
alone, and a solved output is written only where every move leaves it within 1e-12 x max(|a|, |b|, 1); a dropped one is
written null and its index listed in the case's inputs "dropped". The generator prints "dropped n of m outputs" per case
and for the file.

Each case's inputs hold its settings, "curve" ({"traits": "ZeroYield" or "Discount", "calendar", "dayCounter"}, the
curve built on 0 settlement days), "helpers" (a list of {"kind": "IborIbor", "OvernightIbor" or "OvernightOvernight",
"basis", "tenor", "settlementDays", "calendar", "convention", "endOfMonth", "baseIndex", "otherIndex", "discount",
"bootstrapBaseCurve", "paymentLag"}, with "useIndexedCoupons" and "rule" for IBOR-IBOR; "overnightPaymentFrequency",
"useIndexedCoupons", "rule", "averaging" and "basisOnIborLeg" for overnight-IBOR; "paymentFrequency", "baseAveraging"
and "otherAveraging" for overnight-overnight), "dates" and "dropped". An index is instruments/fixedvsfloatingswap.py's
preset spec, or {"type": "USDLibor", "tenor", "curve"} or {"type": "FedFunds", "curve"}, the bootstrapped one with no
curve; a discount curve and a null "useIndexedCoupons" or "overnightPaymentFrequency" are as QuantLib's defaults. Read
back by tests/NanoQL.Tests/Vectors/Experimental/TermStructures/BasisSwapRateHelpersVectorTests.cs."""
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
    "wheel_fixedvsfloatingswap", os.path.join(v.GEN_ROOT, "instruments", "fixedvsfloatingswap.py"))
fvf = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(fvf)
irx = fvf.irx

TODAY = fvf.TODAY
STABLE = 1e-12
Y, M = int(ql.Years), int(ql.Months)
TARGET = irx.TARGET
US_GOVT = {"type": "UnitedStates", "market": int(ql.UnitedStates.GovernmentBond)}
MF, F = int(ql.ModifiedFollowing), int(ql.Following)
KNOWN = fvf.curve(0.01)
DISCOUNT = fvf.curve(0.005)
COMPOUND, SIMPLE = int(ql.RateAveraging.Compound), int(ql.RateAveraging.Simple)
BACKWARD, FORWARD = int(ql.DateGeneration.Backward), int(ql.DateGeneration.Forward)
TENORS = [(1, Y), (2, Y), (3, Y), (5, Y), (8, Y), (10, Y)]
BASES = [0.0010, 0.0012, 0.0015, 0.0015, 0.0018, 0.0020]


def usd_libor(tenor, curve=None):
    return {"type": "USDLibor", "tenor": [tenor[0], tenor[1]], "curve": curve}


def fed_funds(curve=None):
    return {"type": "FedFunds", "curve": curve}


def make_index(spec):
    if spec["type"] == "USDLibor":
        return ql.USDLibor(ql.Period(*spec["tenor"]), irx.handle(spec["curve"]))
    if spec["type"] == "FedFunds":
        return ql.FedFunds(irx.handle(spec["curve"]))
    return fvf.make_index(spec)


def common(kind, basis, tenor, calendar, convention, base, other, discount, bootstrap_base, payment_lag, eom=False):
    return {"kind": kind, "basis": basis, "tenor": [tenor[0], tenor[1]], "settlementDays": 2, "calendar": calendar,
            "convention": convention, "endOfMonth": eom, "baseIndex": base, "otherIndex": other, "discount": discount,
            "bootstrapBaseCurve": bootstrap_base, "paymentLag": payment_lag}


def ibor_ibor(base, other, bootstrap_base, discount=DISCOUNT, payment_lag=0, indexed=None, rule=BACKWARD):
    return [dict(common("IborIbor", q, t, TARGET, MF, base, other, discount, bootstrap_base, payment_lag),
                 useIndexedCoupons=indexed, rule=rule) for t, q in zip(TENORS, BASES)]


def overnight_ibor(base, other, bootstrap_base, discount=DISCOUNT, payment_lag=0, frequency=None, indexed=None,
                   averaging=COMPOUND, on_ibor=False):
    return [dict(common("OvernightIbor", q, t, US_GOVT, F, base, other, discount, bootstrap_base, payment_lag),
                 overnightPaymentFrequency=frequency, useIndexedCoupons=indexed, rule=BACKWARD, averaging=averaging,
                 basisOnIborLeg=on_ibor) for t, q in zip(TENORS, BASES)]


def overnight_overnight(base, other, bootstrap_base, discount=DISCOUNT, payment_lag=0, frequency=int(ql.Annual),
                        base_averaging=COMPOUND, other_averaging=COMPOUND):
    return [dict(common("OvernightOvernight", -q, t, US_GOVT, F, base, other, discount, bootstrap_base, payment_lag),
                 paymentFrequency=frequency, baseAveraging=base_averaging, otherAveraging=other_averaging)
            for t, q in zip(TENORS, BASES)]


def make_helper(spec, quote):
    q = ql.QuoteHandle(ql.SimpleQuote(quote))
    args = (q, ql.Period(*spec["tenor"]), spec["settlementDays"], irx.calendar(spec["calendar"]), spec["convention"],
            spec["endOfMonth"], make_index(spec["baseIndex"]), make_index(spec["otherIndex"]),
            irx.handle(spec["discount"]), spec["bootstrapBaseCurve"])
    if spec["kind"] == "IborIbor":
        return ql.IborIborBasisSwapRateHelper(*args, spec["useIndexedCoupons"], spec["rule"], spec["paymentLag"])
    if spec["kind"] == "OvernightIbor":
        return ql.OvernightIborBasisSwapRateHelper(*args, spec["paymentLag"], spec["overnightPaymentFrequency"],
                                                   spec["useIndexedCoupons"], spec["rule"], spec["averaging"], False,
                                                   spec["basisOnIborLeg"])
    return ql.OvernightOvernightBasisSwapRateHelper(*args, spec["paymentLag"], spec["paymentFrequency"],
                                                    spec["baseAveraging"], spec["otherAveraging"], False)


def make_curve(spec, helpers):
    cal, dc = irx.calendar(spec["calendar"]), irx.day_counter(spec["dayCounter"])
    if spec["traits"] == "ZeroYield":
        return ql.PiecewiseLinearZero(0, cal, helpers, dc)
    return ql.PiecewiseLogLinearDiscount(0, cal, helpers, dc)


E3M, E6M = fvf.preset("Euribor3M"), fvf.preset("Euribor6M")
E3M_KNOWN, E6M_KNOWN = fvf.preset("Euribor3M", KNOWN), fvf.preset("Euribor6M", KNOWN)
ESTR_KNOWN = fvf.preset("Estr", KNOWN)
SOFR, SOFR_KNOWN = fvf.preset("Sofr"), fvf.preset("Sofr", KNOWN)
ZERO_TARGET = {"traits": "ZeroYield", "calendar": TARGET, "dayCounter": irx.A365}
ZERO_US = {"traits": "ZeroYield", "calendar": US_GOVT, "dayCounter": irx.A365}
DISCOUNT_US = {"traits": "Discount", "calendar": US_GOVT, "dayCounter": irx.A365}

# (label, curve, helpers)
CASES = [
    ("IBOR-IBOR, Euribor 3M bootstrapped against a known 6M", ZERO_TARGET, ibor_ibor(E3M, E6M_KNOWN, True)),
    ("IBOR-IBOR, Euribor 6M bootstrapped against a known 3M, payment lag 2", ZERO_TARGET,
     ibor_ibor(E3M_KNOWN, E6M, False, payment_lag=2)),
    ("IBOR-IBOR without a discount curve", ZERO_TARGET, ibor_ibor(E3M, E6M_KNOWN, True, None)),
    ("IBOR-IBOR, at-par coupons", ZERO_TARGET, ibor_ibor(E3M, E6M_KNOWN, True, indexed=False)),
    ("IBOR-IBOR, the forward rule, log-linear discounts",
     {"traits": "Discount", "calendar": TARGET, "dayCounter": irx.A365},
     ibor_ibor(E3M_KNOWN, E6M, False, rule=FORWARD)),
    ("overnight-IBOR, USD LIBOR 6M bootstrapped against a known SOFR, the basis on the LIBOR leg", ZERO_US,
     overnight_ibor(SOFR_KNOWN, usd_libor((6, M)), False, on_ibor=True)),
    ("overnight-IBOR, SOFR bootstrapped against a known USD LIBOR 3M, averaged, paying quarterly", ZERO_US,
     overnight_ibor(SOFR, usd_libor((3, M), KNOWN), True, frequency=int(ql.Quarterly), averaging=SIMPLE)),
    ("overnight-IBOR, Euribor 6M bootstrapped against a known ESTR, discounting on the curve", ZERO_TARGET,
     [dict(h, calendar=TARGET, convention=MF)
      for h in overnight_ibor(ESTR_KNOWN, E6M, False, discount=None, indexed=True)]),
    ("overnight-overnight, SOFR bootstrapped against a known Fed Funds, paying quarterly", ZERO_US,
     overnight_overnight(fed_funds(KNOWN), SOFR, False, frequency=int(ql.Quarterly))),
    ("overnight-overnight, Fed Funds bootstrapped against a known SOFR, averaged, payment lag 2", DISCOUNT_US,
     overnight_overnight(fed_funds(), SOFR_KNOWN, True, payment_lag=2, base_averaging=SIMPLE)),
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
            quotes = [h["basis"] for h in helper_specs]
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
    print(f"experimental/termstructures/basisswapratehelpers: dropped {total[0]} of {total[1]} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/termstructures/basisswapratehelpers.hpp", cases)
