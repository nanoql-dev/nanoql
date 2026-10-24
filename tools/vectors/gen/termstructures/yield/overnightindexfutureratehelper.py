# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's overnight-index future rate helpers (ql/termstructures/yield/overnightindexfutureratehelper.hpp):
OvernightIndexFutureRateHelper and SofrFutureRateHelper, through the wheel's, on the evaluation date 15 January 2025,
on a SOFR-style index with no curve of its own (the helper forwards on the curve it bootstraps) and an ESTR-style one.

- "helper" cases: a helper on its value and maturity dates (the generic one: compounded or averaged, a convexity
  adjustment, the last-relevant, maturity or a custom pillar) or on its reference month, year and frequency (the SOFR
  one: monthly and quarterly, seasoned and forward, a convexity adjustment, a custom pillar): its quote and convexity
  adjustment, its earliest, latest, maturity, pillar and latest relevant dates, and a log-linear discount curve
  (Actual/365 (Fixed)) bootstrapped on it alone: the discount at its latest date, its implied quote and quote error on
  that curve. A seasoned helper's past fixings are stored, 4.3% plus a basis point per business day from the reference
  period's start. The seasoned monthly SOFR future, maturing on a Saturday, 1 February 2025, is refused by the bootstrap
  on it alone, as QuantLib refuses it: the curve ends on its pillar, before a time its rate reads.
- "curve" cases: a log-linear discount curve bootstrapped on a SOFR strip of six monthly and four quarterly futures, and
  one on four ESTR futures: the discount at each helper's latest date, and each helper's implied quote.
- "refused" cases: a custom pillar not given, before the reference period's start or after its end, and a SOFR future
  that is neither monthly nor quarterly ("only monthly and quarterly SOFR futures accepted").

A case's inputs hold its settings with the stored fixings, its kind and its helpers ({"type": "generic", "price",
"valueDate", "maturityDate", "index", "convexityAdjustment", "averaging", "pillar", "customPillarDate"} or {"type":
"sofr", "price", "month", "year", "frequency", "convexityAdjustment", "pillar", "customPillarDate"}, an index an
overnight spec of indexes/interestrateindex.py with no curve), and a curve case its interpolation. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/Yield/OvernightIndexFutureRateHelperVectorTests.cs."""
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


irx = _load("wheel_interestrateindex", "indexes/interestrateindex.py")

D = ql.Date
TODAY = irx.TODAY
COMPOUND, SIMPLE = int(ql.RateAveraging.Compound), int(ql.RateAveraging.Simple)
LAST, MATURITY, CUSTOM = int(ql.Pillar.LastRelevantDate), int(ql.Pillar.MaturityDate), int(ql.Pillar.CustomDate)
MONTHLY, QUARTERLY, ANNUAL = int(ql.Monthly), int(ql.Quarterly), int(ql.Annual)
SOFR = irx.overnight_spec("SOFR", 0, irx.US_SOFR, irx.A360)
ESTR = irx.overnight_spec("ESTR", 0, irx.TARGET, irx.A360)


def generic(price, value, maturity, index=SOFR, convexity=0.0, averaging=COMPOUND, pillar=LAST, custom=None):
    return {"type": "generic", "price": price, "valueDate": v.enc(value), "maturityDate": v.enc(maturity),
            "index": index, "convexityAdjustment": convexity, "averaging": averaging, "pillar": pillar,
            "customPillarDate": v.enc(custom)}


def sofr(price, month, year, frequency, convexity=0.0, pillar=LAST, custom=None):
    return {"type": "sofr", "price": price, "month": int(month), "year": year, "frequency": frequency,
            "convexityAdjustment": convexity, "pillar": pillar, "customPillarDate": v.enc(custom)}


def make_helper(spec):
    custom = ql.Date() if spec["customPillarDate"] is None else ql.Date(spec["customPillarDate"])
    if spec["type"] == "generic":
        return ql.OvernightIndexFutureRateHelper(
            ql.QuoteHandle(ql.SimpleQuote(spec["price"])), ql.Date(spec["valueDate"]), ql.Date(spec["maturityDate"]),
            irx.make_index(spec["index"]), ql.QuoteHandle(ql.SimpleQuote(spec["convexityAdjustment"])),
            spec["averaging"], spec["pillar"], custom)
    return ql.SofrFutureRateHelper(spec["price"], spec["month"], spec["year"], spec["frequency"],
                                   spec["convexityAdjustment"], spec["pillar"], custom)


def fixings(start, index_spec=SOFR):
    """The past fixings from start to the day before the evaluation date: 4.3% plus a basis point a business day."""
    index = irx.make_index(index_spec)
    cal = index.fixingCalendar()
    pairs, d, k = [], cal.adjust(start, ql.Preceding), 0
    while d < TODAY:
        pairs.append((d, 0.043 + 0.0001 * k))
        d, k = cal.advance(d, 1, ql.Days), k + 1
    return [(index, pairs)]


HELPERS = [
    ("generic, quarterly, compounded", generic(95.75, D(19, 3, 2025), D(18, 6, 2025)), None),
    ("generic, quarterly, averaged", generic(95.75, D(19, 3, 2025), D(18, 6, 2025), averaging=SIMPLE), None),
    ("generic, a convexity adjustment", generic(95.70, D(19, 3, 2025), D(18, 6, 2025), convexity=0.0012), None),
    ("generic, the maturity pillar", generic(95.75, D(19, 3, 2025), D(18, 6, 2025), pillar=MATURITY), None),
    ("generic, a custom pillar", generic(95.75, D(19, 3, 2025), D(18, 6, 2025), pillar=CUSTOM, custom=D(1, 5, 2025)),
     None),
    ("generic, seasoned", generic(95.71, D(2, 1, 2025), D(3, 2, 2025)), fixings(D(2, 1, 2025))),
    ("generic, ESTR", generic(97.05, D(19, 3, 2025), D(18, 6, 2025), ESTR), None),
    ("SOFR monthly, forward", sofr(95.69, ql.March, 2025, MONTHLY), None),
    ("SOFR monthly, seasoned", sofr(95.71, ql.January, 2025, MONTHLY), fixings(D(31, 12, 2024))),
    ("SOFR quarterly, forward", sofr(95.80, ql.June, 2025, QUARTERLY), None),
    ("SOFR quarterly, seasoned", sofr(95.66, ql.December, 2024, QUARTERLY), fixings(D(18, 12, 2024))),
    ("SOFR quarterly, a convexity adjustment and a custom pillar",
     sofr(95.80, ql.June, 2025, QUARTERLY, 0.0008, CUSTOM, D(15, 8, 2025)), None),
]

CURVES = [
    ("a SOFR strip", [sofr(p, m, y, MONTHLY) for p, m, y in ((95.70, ql.February, 2025), (95.69, ql.March, 2025),
                                                            (95.72, ql.April, 2025), (95.76, ql.May, 2025),
                                                            (95.80, ql.June, 2025), (95.85, ql.July, 2025))]
     + [sofr(p, m, y, QUARTERLY) for p, m, y in ((95.90, ql.September, 2025), (96.00, ql.December, 2025),
                                                 (96.10, ql.March, 2026), (96.15, ql.June, 2026))], None),
    ("ESTR futures", [generic(p, a, b, ESTR) for p, a, b in ((97.05, D(19, 3, 2025), D(18, 6, 2025)),
                                                           (97.15, D(18, 6, 2025), D(17, 9, 2025)),
                                                           (97.25, D(17, 9, 2025), D(17, 12, 2025)),
                                                           (97.30, D(17, 12, 2025), D(18, 3, 2026)))], None),
]

REFUSALS = [
    ("a custom pillar not given", generic(95.75, D(19, 3, 2025), D(18, 6, 2025), pillar=CUSTOM)),
    ("a custom pillar before the start", generic(95.75, D(19, 3, 2025), D(18, 6, 2025), pillar=CUSTOM,
                                                 custom=D(1, 3, 2025))),
    ("a custom pillar after the end", generic(95.75, D(19, 3, 2025), D(18, 6, 2025), pillar=CUSTOM,
                                              custom=D(20, 7, 2025))),
    ("an annual SOFR future", sofr(95.75, ql.June, 2025, ANNUAL)),
]


def helper_cases():
    out = []
    for k, (label, spec, stored) in enumerate(HELPERS):
        with v.case_settings(TODAY, fixings=stored) as st:
            h = make_helper(spec)
            expected = {"quote": h.quote().value(), "convexityAdjustment": h.convexityAdjustment(),
                        "earliestDate": v.enc(h.earliestDate()), "latestDate": v.enc(h.latestDate()),
                        "maturityDate": v.enc(h.maturityDate()), "pillarDate": v.enc(h.pillarDate()),
                        "latestRelevantDate": v.enc(h.latestRelevantDate())}
            # a curve bootstrapped on the helper alone, kept alive while the helper, which points to it, is read
            curve = ql.PiecewiseLogLinearDiscount(TODAY, [h], ql.Actual365Fixed())
            expected.update({"bootstrapDiscount": v.attempt(lambda: curve.discount(h.latestDate())),
                             "impliedQuote": v.attempt(h.impliedQuote), "quoteError": v.attempt(h.quoteError)})
            out.append({"name": f"helper {label} / {k}",
                        "inputs": {"settings": st, "kind": "helper", "helpers": [spec]}, "expected": expected})
    return out


def curve_cases():
    out = []
    for k, (label, specs, stored) in enumerate(CURVES):
        with v.case_settings(TODAY, fixings=stored) as st:
            helpers = [make_helper(s) for s in specs]
            curve = ql.PiecewiseLogLinearDiscount(TODAY, helpers, ql.Actual365Fixed())
            out.append({"name": f"curve {label} / {k}", "inputs": {"settings": st, "kind": "curve", "helpers": specs},
                        "expected": {
                            "discounts": [v.attempt(lambda h=h: curve.discount(h.latestDate())) for h in helpers],
                            "impliedQuotes": [v.attempt(h.impliedQuote) for h in helpers]}})
    return out


def refusal_cases():
    out = []
    for label, spec in REFUSALS:
        with v.case_settings(TODAY) as st:
            out.append({"name": f"refused / {label}", "inputs": {"settings": st, "kind": "refused", "helpers": [spec]},
                        "expected": {"construct": v.attempt(lambda: make_helper(spec))}})
    return out


def make_cases():
    out = helper_cases() + curve_cases() + refusal_cases()
    for c in out[-len(REFUSALS):]:
        assert c["expected"]["construct"] == v.THROWS, c["name"]
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/overnightindexfutureratehelper.hpp", make_cases)
