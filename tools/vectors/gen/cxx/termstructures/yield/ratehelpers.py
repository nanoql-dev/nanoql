# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's rate helpers (ql/termstructures/yield/ratehelpers.hpp), each output computed by the C++
harness: what the wheel can't reach, a helper's implied quote and quote error against a curve of one's choice, the
helper bound to it by setTermStructure, which the wheel doesn't bind; the swap helper's forwardStart(); its
constructor taking a coupon pricer, the FRA helper's constructor from a period and a length, and the futures helper's
mixed quote and value forms, which the wheel doesn't wrap.

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
- Appended after them, 117 cases with the same outputs: the 103 FRA, futures and FX swap helpers of
  termstructures/yield/ratehelpers.py on the same date (an FX swap helper bound to a curve discounts on its own
  collateral curve too; one with no collateral curve can't be bound, QuantLib's refusal); 8 FRA helpers from a period
  to start and a length in months (weeks added to months refused); and 6 futures helpers in QuantLib's mixed forms, the
  price a quote and the convexity adjustment a value or the other way round.

Each case's inputs hold its settings, "helper" (a helper spec of termstructures/yield/ratehelpers.py, with a
"couponPricer" spec of cashflows/couponspec.hpp for the six pricer cases, and "quotes" for the mixed futures forms)
and "curves" (the flat forward spec and {"type":
"DiscountCurve", "dates", "discounts", "dayCounter"}). A case's "skip" input, where it has one, lists the outputs
QuantLib's master moves (#2899, taken ahead of the pin as the upstream sweep's SWD14 and SWI3 rule): a deposit's or an
indexed FRA's implied quote and quote error where QuantLib 1.44 reads a fixing before the evaluation date that the
store doesn't hold and refuses it, and master forecasts the quote from the curve; each is null, and the reader nulls
the same outputs (tests/NanoQL.Tests/Divergences/RateHelperUpstreamFixesTests.cs checks the forecast). Read back by tests/NanoQL.Tests/Vectors/Cxx/TermStructures/Yield/
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


def fra_period_lengths():
    """(name, spec) for the 8 FRA helpers from a period to start and a length in months."""
    M, W, Dd, Y = ql.Months, ql.Weeks, ql.Days, ql.Years
    MF, F = ql.ModifiedFollowing, ql.Following
    out = [
        ("1M then 3 months", rh.fra_period_length(0.0381, (1, M), 3)),
        ("3M then 6 months, Actual/365", rh.fra_period_length(0.0382, (3, M), 6, dc=rh.A365)),
        ("1Y then 6 months, end of month", rh.fra_period_length(0.0383, (1, Y), 6, eom=True)),
        ("0D then 3 months, Following, one fixing day", rh.fra_period_length(0.0384, (0, Dd), 3, 1, convention=F)),
        ("6M then 3 months, not indexed", rh.fra_period_length(0.0385, (6, M), 3, indexed=False)),
        ("3M then 3 months, pillar at maturity",
         rh.fra_period_length(0.0386, (3, M), 3, pillar=ql.Pillar.MaturityDate)),
        ("2W then 3 months, refused", rh.fra_period_length(0.0387, (2, W), 3)),
        ("1M then 9 months on the US SOFR calendar", rh.fra_period_length(0.0388, (1, M), 9, 2, rh.US_SOFR, MF)),
    ]
    return out


def mixed_futures():
    """(name, spec) for the 6 futures helpers in QuantLib's mixed quote and value forms."""
    out = [
        ("IMM March 2024, 3 months, the price a quote",
         rh.futures_length(96.21, ql.Date(20, 3, 2024), 3, convexity=0.0002)),
        ("IMM June 2024, 3 months, the adjustment a quote",
         rh.futures_length(96.31, ql.Date(19, 6, 2024), 3, convexity=0.0004)),
        ("IMM March 2024 with no end date, the price a quote",
         rh.futures_dates(96.21, ql.Date(20, 3, 2024), None, convexity=0.0001)),
        ("custom from 20 February 2024 to 6 April, the adjustment a quote",
         rh.futures_dates(96.5, ql.Date(20, 2, 2024), ql.Date(6, 4, 2024), convexity=0.0003,
                          futures_type=ql.Futures.Custom)),
        ("ASX March 2024 on Euribor 3M, the price a quote",
         rh.futures_index(96.26, ql.Date(8, 3, 2024), rh.E3M, 0.0002, ql.Futures.ASX)),
        ("IMM June 2024 on Euribor 3M, the adjustment a quote",
         rh.futures_index(96.31, ql.Date(19, 6, 2024), rh.E3M, 0.0004)),
    ]
    for k, (_, spec) in enumerate(out):
        spec["quotes"] = [k % 2 == 0, k % 2 == 1]
    return out


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


def moved(H, spec, expected):
    """The outputs #2899 moves: a built deposit's or indexed FRA's implied quotes and quote errors that QuantLib 1.44
    refuses for its fixing before the evaluation date, not stored ("Missing ... fixing for ..."), where master's
    forecast reads the curve from the helper's earliest date, on or after the evaluation date."""
    indexed = spec["kind"] == "DepositRateHelper" or (spec["kind"] == "FraRateHelper"
                                                      and spec.get("useIndexedCoupon", True))
    if not indexed or expected.get("built") is not True:
        return []
    out = []
    for name, curve in zip(("Flat", "Curve"), CURVES):
        try:
            H.call("RateHelper::impliedQuote", spec, curve)
        except RuntimeError as e:
            if str(e).startswith("Missing ") and " fixing for " in str(e):
                out += ["impliedQuote" + name, "quoteError" + name]
    # master forecasts from the curve, which reads no date before the evaluation date (the wheel's helper on it)
    if out:
        v.settings(TODAY)
        if rh.make_helper(spec).earliestDate() < TODAY:
            return []
    return out


def cases():
    out = []
    rows = [(n, s, a) for n, s, a in rh.all_helpers()] + [(f"swap {n}", s, None) for n, s in pricer_helpers()]
    rows += [(n, s, None) for n, s in rh.more_helpers()]
    rows += [(f"fra {n}", s, None) for n, s in fra_period_lengths()]
    rows += [(f"futures {n}", s, None) for n, s in mixed_futures()]
    skips = []
    with cxx.connect() as H:
        for name, spec, at_par in rows:
            with H.case_settings(v.enc(TODAY), use_at_par_coupons=at_par) as st:
                expected = outputs(H, spec)
                skips.append(moved(H, spec, expected))
            out.append({"name": f"{TODAY.ISO()} {name}", "inputs": {"settings": st, "helper": spec,
                                                                    "curves": CURVES},
                        "expected": expected})
    check_rows(out)
    skipped = []
    for c, skip in zip(out, skips):
        if skip:
            for k in skip:
                c["expected"][k] = None
            c["inputs"]["skip"] = skip
            skipped.append(c["name"][len(TODAY.ISO()) + 1:])
    # the deposits whose fixing precedes the evaluation date, a US holiday (Martin Luther King Day) or a given Friday
    assert skipped == ["deposit 1Y on the US SOFR calendar", "deposit on an index of the US SOFR calendar",
                       "deposit fixing on Friday 12 January 2024"], skipped
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
    # an FX swap helper with no collateral curve can't be bound
    assert by_name[f"{first} fx 1M with no collateral curve"]["impliedQuoteFlat"] == v.THROWS
    assert by_name[f"{first} fra 2W then 3 months, refused"] == {"built": v.THROWS}
    # the mixed forms give the plain forms' values
    assert (by_name[f"{first} futures IMM June 2024 on Euribor 3M, the adjustment a quote"]["impliedQuoteFlat"]
            == by_name[f"{first} futures IMM June 2024 on Euribor 3M, convexity"]["impliedQuoteFlat"])


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/ratehelpers.hpp", cases)
