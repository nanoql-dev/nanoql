# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DepositRateHelper and SwapRateHelper (ql/termstructures/yield/ratehelpers.hpp), each output
computed by the C++ harness: what the wheel can't reach, a helper's implied quote and quote error against a curve of
one's choice, the helper bound to it by setTermStructure, which the wheel doesn't bind; the swap helper's
forwardStart(); and its constructor taking a coupon pricer, which the wheel doesn't wrap.

- 103 cases, the helpers of termstructures/yield/ratehelpers.py on its first evaluation date, 15 January 2024, each
  with the at-par switch it sets there: whether it builds (its pillar date, which QuantLib computes at construction;
  QuantLib's refusals recorded as its throw), then its implied quote and quote error bound to a flat forward curve
  (3.5%, continuous, Actual/365 (Fixed)) and to an interpolated discount curve (log-linear on 10 nodes from the
  evaluation date to 50 years, Actual/365 (Fixed)), both with the evaluation date as reference date, QuantLib's
  refusals recorded as their throws (a deposit fixing before the evaluation date and a seasoned swap read fixings
  that aren't stored), and a swap helper's forward start, [length, units].
- 6 cases of swap helpers given a coupon pricer, set on their swaps' floating legs: the harness's IndexFixingPricer
  (couponspec.hpp; the index fixing shifted by an adjustment), which QuantLib refuses on an Ibor coupon ("pricer not
  compatible with Ibor coupon"); and QuantLib's BlackIborCouponPricer on a 20% lognormal caplet volatility with
  Black76's timing adjustment, on a tenor and a dated helper, and with the bivariate lognormal one at correlations 0.5
  and 0.8, its indexed-coupon choice from the switch, given, and given against the helper's own; with the same
  outputs.

Each case's inputs hold its settings, "helper" (a helper spec of termstructures/yield/ratehelpers.py, with a
"couponPricer" spec of cashflows/couponspec.hpp for the last six) and "curves" (the flat forward spec and {"type":
"DiscountCurve", "dates", "discounts", "dayCounter"}). Read back by tests/NanoQL.Tests/Vectors/Cxx/TermStructures/Yield/
RateHelpersCxxVectorTests.cs."""
import importlib.util
import math
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
    "wheel_ratehelpers", os.path.join(v.GEN_ROOT, "termstructures", "yield", "ratehelpers.py"))
rh = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(rh)
_spec = importlib.util.spec_from_file_location(
    "wheel_iborcoupon", os.path.join(v.GEN_ROOT, "cashflows", "iborcoupon.py"))
ibc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ibc)

TODAY = rh.EVALUATION_DATES[0]
FLAT = rh.fvf.curve(0.035, reference=TODAY)


def discount_curve():
    """Log-linear discounts of a zero curve rising from 3.2% to 4.1%, on 10 nodes to 50 years."""
    offsets = [0, 30, 182, 365, 730, 1826, 3652, 7305, 10958, 18262]
    dates = [TODAY + n for n in offsets]
    dc = ql.Actual365Fixed()
    dfs = [1.0] + [math.exp(-(0.032 + 0.009 * (1.0 - math.exp(-n / 3000.0))) * dc.yearFraction(TODAY, d))
                   for n, d in zip(offsets[1:], dates[1:])]
    return {"type": "DiscountCurve", "dates": v.enc(dates), "discounts": dfs, "dayCounter": rh.A365}


CURVES = [FLAT, discount_curve()]


def pricer_helpers():
    """(name, swap helper spec with its pricer) for the 6 helpers given a coupon pricer."""
    vol = ibc.vol_spec(0.20, reference=TODAY)
    black = ql.BlackIborCouponPricer

    def fixing(adjustment):
        return {"type": "IndexFixingPricer", "adjustment": adjustment}

    def blackp(timing, correlation, use_indexed):
        return ibc.pricer_spec(vol, timing, correlation, use_indexed)

    rows = [
        ("index fixing pricer, +5bp", rh.swap_tenor(0.0301, (5, ql.Years)), fixing(0.0005)),
        ("Black pricer, Black76, dated",
         rh.swap_dates(0.0302, ql.Date(17, 1, 2024), ql.Date(17, 1, 2034)), blackp(black.Black76, 1.0, None)),
        ("Black pricer, Black76", rh.swap_tenor(0.0303, (5, ql.Years)), blackp(black.Black76, 1.0, None)),
        ("Black pricer, bivariate lognormal 0.5", rh.swap_tenor(0.0304, (7, ql.Years)),
         blackp(black.BivariateLognormal, 0.5, None)),
        ("Black pricer, bivariate lognormal 0.8, indexed",
         rh.swap_tenor(0.0305, (10, ql.Years), index=rh.E3M, freq=ql.Quarterly),
         blackp(black.BivariateLognormal, 0.8, True)),
        ("Black pricer at par on an indexed helper", rh.swap_tenor(0.0306, (5, ql.Years), indexed=True),
         blackp(black.BivariateLognormal, 0.5, False)),
    ]
    return [(name, dict(spec, couponPricer=pricer)) for name, spec, pricer in rows]


def outputs(H, spec):
    try:
        pillar = H.call("RateHelper::pillarDate", spec)
    except RuntimeError:
        return {"built": dict(v.THROWS)}
    out = {"built": True, "pillarDate": pillar}
    for name, curve in zip(("Flat", "Curve"), CURVES):
        out["impliedQuote" + name] = v.attempt(lambda c=curve: H.call("RateHelper::impliedQuote", spec, c))
        out["quoteError" + name] = v.attempt(lambda c=curve: H.call("RateHelper::quoteError", spec, c))
    if spec["kind"] == "SwapRateHelper":
        out["forwardStart"] = H.call("SwapRateHelper::forwardStart", spec)
    return out


def cases():
    out = []
    rows = [(n, s, a) for n, s, a in rh.all_helpers()] + [(f"swap {n}", s, None) for n, s in pricer_helpers()]
    with cxx.connect() as H:
        for name, spec, at_par in rows:
            with H.case_settings(v.enc(TODAY), use_at_par_coupons=at_par) as st:
                expected = outputs(H, spec)
            out.append({"name": f"{TODAY.ISO()} {name}", "inputs": {"settings": st, "helper": spec,
                                                                    "curves": CURVES},
                        "expected": expected})
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    first = TODAY.ISO()
    # QuantLib reads a fixing before the evaluation date from the store, which holds none.
    assert by_name[f"{first} deposit fixing on Friday 12 January 2024"]["impliedQuoteFlat"] == v.THROWS
    # A helper bound to a curve prices its quote within the bootstrap's accuracy only on its own curve, never on these.
    assert abs(by_name[f"{first} swap 5Y"]["quoteErrorFlat"]) > 1e-4
    base = by_name[f"{first} swap 5Y"]["impliedQuoteFlat"]
    # An Ibor coupon refuses a pricer that isn't an Ibor coupon pricer.
    assert by_name[f"{first} swap index fixing pricer, +5bp"] == {"built": v.THROWS}
    assert by_name[f"{first} swap Black pricer, bivariate lognormal 0.5"]["impliedQuoteFlat"] != base
    assert by_name[f"{first} swap forward start -1W"]["forwardStart"] == [-1, int(ql.Weeks)]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/ratehelpers.hpp", cases)
