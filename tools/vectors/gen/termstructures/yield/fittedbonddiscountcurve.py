# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FittedBondDiscountCurve (ql/termstructures/yield/fittedbonddiscountcurve.hpp, .cpp): curves
that evaluate a fitting method on given parameters, and curves fitted to bond prices.

Under the evaluation date Monday 16 June 2025, on Actual/365 (Fixed):
- **Evaluators:** Nelson-Siegel and Svensson parameters, dated at the evaluation date (QuantLib's fixed reference date)
  and from 0 and 2 TARGET settlement days (its moving one), to 20 years; the curve's reference date, maximum date and
  time, number of bonds, the method's solution (the parameters), iterations (0) and cost (null), and the discount at 12
  times, past the maximum refused.
- **Fits:** ten fixed-rate bonds, semiannual on Actual/Actual (ISDA), two settlement days, maturing from 1 to 20 years,
  their clean prices those of a Nelson-Siegel curve rounded to a thousandth, fitted by Nelson-Siegel, Svensson,
  exponential splines with nine coefficients, cubic B-splines constrained at zero, a simple polynomial of degree 3 and
  the spread method over Nelson-Siegel on a flat 2% curve of the same date and day counter, each from its own guess, at
  QuantLib's default accuracy (1e-10), 10,000 evaluations and Simplex lambda 1. A fit's outputs are the curve's
  maximum date and number of bonds, the method's weights (the bonds' normalized inverse durations), solution, number of
  iterations and cost, and the curve's discount at 12 dates.

Filtered (the S7R6 rule of the S7 plan, which the S-sr plan's SSD13 applies to the fitted parameters): each fit is
refitted on the wheel with every clean price one ulp up, then down, by math.nextafter, together, then each alone, up
and down; an output is written only if every refit leaves it within 1e-12 × max(|a|, |b|, 1), and the dropped outputs
are counted in the case's inputs. The polynomial's powers are std::pow's, which agree with .NET's within R7, not bit for
bit.

Left out: the spread method over a discounting curve on another clock (the S-sr plan's SSR6 (xiv),
tests/NanoQL.Tests/Divergences/SpreadFittingClockTests.cs), and exponential splines with a fixed κ (SSR6 (v),
tests/NanoQL.Tests/Divergences/ExponentialSplinesFixedKappaTests.cs).

Each case's inputs hold its settings, its "kind" ("evaluator" or "fit"), the "method"
(tools/vectors/gen/termstructures/yield/nonlinearfittingmethods.py's spec) and, for an evaluator, the "parameters",
the "form" ("referenceDate", or "settlementDays" with "settlementDays") and "maxDate", or for a fit the "bonds" ({"issueDate",
"maturityDate", "coupon", "price"}), the "guess" and "droppedOutputs"; and the "times" or "dates" read. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/Yield/FittedBondDiscountCurveVectorTests.cs."""
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


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


zs = _load("zerospreadedtermstructure", "termstructures/yield/zerospreadedtermstructure.py")
nfm = _load("nonlinearfittingmethods", "termstructures/yield/nonlinearfittingmethods.py")

TODAY = zs.TODAY
A365 = ql.Actual365Fixed()
RELATIVE = 1e-12
TIMES = [0.0, 0.1, 0.5, 1.0, 2.0, 5.0, 8.0, 10.0, 15.0, 19.0, 20.0, 25.0]
FIT_DATES = [TODAY + ql.Period(n, u) for n, u in [(3, ql.Months), (6, ql.Months), (1, ql.Years), (2, ql.Years),
                                                    (3, ql.Years), (5, ql.Years), (7, ql.Years), (10, ql.Years),
                                                    (12, ql.Years), (15, ql.Years), (18, ql.Years), (20, ql.Years)]]

# (years to maturity, coupon)
BONDS = [(1, 0.02), (2, 0.025), (3, 0.0275), (4, 0.03), (5, 0.03), (7, 0.0325), (9, 0.035), (12, 0.0375), (15, 0.04),
         (20, 0.0425)]
TRUE_CURVE = [0.042, -0.018, -0.01, 0.45]
FLAT_2 = zs.flat(0.02, zs.A365, ql.Continuous, ql.Annual, False, reference=TODAY)

EVALUATORS = [
    ({"type": "NelsonSiegel"}, [0.035, -0.02, 0.01, 0.6]),
    ({"type": "Svensson"}, [0.035, -0.02, 0.01, -0.015, 0.6, 0.15]),
]
FITS = [
    ({"type": "NelsonSiegel"}, [0.03, -0.01, 0.0, 0.5]),
    ({"type": "Svensson"}, [0.03, -0.01, 0.0, 0.0, 0.5, 0.2]),
    ({"type": "ExponentialSplines", "constrainAtZero": True, "numCoeffs": 9},
     [0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.3]),
    ({"type": "CubicBSplines", "knots": [-30.0, -20.0, 0.0, 5.0, 10.0, 15.0, 20.0, 25.0, 30.0, 40.0],
      "constrainAtZero": True}, [0.9, 0.8, 0.65, 0.5, 0.4]),
    ({"type": "SimplePolynomial", "degree": 3, "constrainAtZero": True}, [-0.03, 0.0, 0.0]),
    ({"type": "Spread", "method": {"type": "NelsonSiegel"}, "discountCurve": FLAT_2}, [0.01, 0.0, 0.0, 0.5]),
]


def bond(issue, maturity, coupon):
    schedule = ql.Schedule(issue, maturity, ql.Period(6, ql.Months), ql.TARGET(), ql.Unadjusted, ql.Unadjusted,
                           ql.DateGeneration.Backward, False)
    return ql.FixedRateBond(2, 100.0, schedule, [coupon], ql.ActualActual(ql.ActualActual.ISDA))


def bond_specs():
    """The ten bonds, issued six months before the evaluation date, priced on the true Nelson-Siegel curve."""
    true = ql.FittedBondDiscountCurve(TODAY, ql.NelsonSiegelFitting(), ql.Array(TRUE_CURVE),
                                      TODAY + ql.Period(30, ql.Years), A365)
    engine = ql.DiscountingBondEngine(ql.YieldTermStructureHandle(true))
    out = []
    for years, coupon in BONDS:
        issue = TODAY - ql.Period(6, ql.Months)
        maturity = TODAY + ql.Period(years, ql.Years)
        b = bond(issue, maturity, coupon)
        b.setPricingEngine(engine)
        out.append({"issueDate": v.enc(issue), "maturityDate": v.enc(maturity), "coupon": coupon,
                    "price": round(b.cleanPrice(), 3)})
    return out


def fit_outputs(method_spec, guess, bonds, prices):
    handles = []
    method = nfm.make_method(method_spec, handles)
    helpers = [ql.BondHelper(ql.QuoteHandle(ql.SimpleQuote(p)),
                             bond(ql.Date(b["issueDate"]), ql.Date(b["maturityDate"]), b["coupon"]))
               for b, p in zip(bonds, prices)]
    curve = ql.FittedBondDiscountCurve(TODAY, helpers, A365, method, 1e-10, 10000, ql.Array(guess), 1.0)
    results = curve.fitResults()
    out = {"maxDate": curve.maxDate().serialNumber(), "numberOfBonds": len(bonds),
           "numberOfIterations": results.numberOfIterations(), "minimumCostValue": results.minimumCostValue()}
    for i, x in enumerate(results.weights()):
        out[("weights", i)] = x
    for i, x in enumerate(results.solution()):
        out[("solution", i)] = x
    for i, d in enumerate(FIT_DATES):
        out[("discount", i)] = curve.discount(d)
    return out


def within(a, b):
    if a == b:
        return True
    return abs(a - b) <= RELATIVE * max(abs(a), abs(b), 1.0)


def moves(prices):
    out = [[math.nextafter(p, math.inf) for p in prices], [math.nextafter(p, -math.inf) for p in prices]]
    for i in range(len(prices)):
        for direction in (math.inf, -math.inf):
            moved = list(prices)
            moved[i] = math.nextafter(prices[i], direction)
            out.append(moved)
    return out


def fit_case(method_spec, guess, bonds):
    with v.case_settings(TODAY) as st:
        prices = [b["price"] for b in bonds]
        base = fit_outputs(method_spec, guess, bonds, prices)
        keep = set(base)
        for moved in moves(prices):
            other = fit_outputs(method_spec, guess, bonds, moved)
            keep = {k for k in keep if within(base[k], other[k])}
        dropped = len(base) - len(keep)
        print(f"{method_spec['type']}: dropped {dropped} of {len(base)} outputs")
        expected = {k: base[k] for k in ("maxDate", "numberOfBonds", "numberOfIterations", "minimumCostValue")
                    if k in keep}
        indices = {}
        for name in ("weights", "solution", "discount"):
            indices[name] = [i for (n, i) in sorted(k for k in base if isinstance(k, tuple) and k[0] == name)
                             if (name, i) in keep]
            expected[name] = [base[(name, i)] for i in indices[name]]
        return {"name": f"fit {method_spec['type']}",
                "inputs": {"settings": st, "kind": "fit", "method": method_spec, "bonds": bonds, "guess": guess,
                           "dates": v.enc([FIT_DATES[i] for i in indices["discount"]]),
                           "weightIndices": indices["weights"], "solutionIndices": indices["solution"],
                           "droppedOutputs": dropped},
                "expected": expected}


def evaluator_case(method_spec, params, form):
    with v.case_settings(TODAY) as st:
        handles = []
        method = nfm.make_method(method_spec, handles)
        max_date = TODAY + ql.Period(20, ql.Years)
        if form == "referenceDate":
            curve = ql.FittedBondDiscountCurve(TODAY, method, ql.Array(params), max_date, A365)
        else:
            curve = ql.FittedBondDiscountCurve(form, ql.TARGET(), method, ql.Array(params), max_date, A365)
        results = curve.fitResults()
        expected = {"referenceDate": curve.referenceDate().serialNumber(), "maxDate": curve.maxDate().serialNumber(),
                    "maxTime": curve.maxTime(), "numberOfBonds": 0,
                    "solution": list(results.solution()), "numberOfIterations": results.numberOfIterations(),
                    # QuantLib's Null<Real>(), nanoql's null
                    "minimumCostValue": None if results.minimumCostValue() == v.NULL_REAL else results.minimumCostValue(),
                    "discount": [v.attempt(lambda t=t: curve.discount(t)) for t in TIMES]}
        inputs = {"settings": st, "kind": "evaluator", "method": method_spec, "parameters": params,
                  "maxDate": v.enc(max_date), "times": TIMES}
        if form == "referenceDate":
            inputs["form"] = "referenceDate"
        else:
            inputs.update({"form": "settlementDays", "settlementDays": form})
        return {"name": f"evaluator {method_spec['type']} / {form}", "inputs": inputs, "expected": expected}


def cases():
    out = []
    for spec, params in EVALUATORS:
        for form in ("referenceDate", 0, 2):
            out.append(evaluator_case(spec, params, form))
    with v.case_settings(TODAY):
        bonds = bond_specs()
    for spec, guess in FITS:
        out.append(fit_case(spec, guess, bonds))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/fittedbonddiscountcurve.hpp", cases)
