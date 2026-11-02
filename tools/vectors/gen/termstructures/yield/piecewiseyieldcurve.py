# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's PiecewiseYieldCurve (ql/termstructures/yield/piecewiseyieldcurve.hpp) on its iterative
bootstrap, with the four yield traits (ql/termstructures/yield/bootstraptraits.hpp) and the interpolated curves'
formulas they carry: 209 curves in 43 cases, on deposit and swap helpers of termstructures/yield/ratehelpers.py;
then, appended after them, 30 curves in 14 cases on its FRA, futures, FX swap and OIS helpers; then 1 curve in 1
case on its BMA swap helpers, re-dated.

- 132 curves in 12 cases: 11 traits and interpolator pairs (discounts: log-linear, spline cubic, log-cubic with the
  spline and with Kruger's derivatives, monotonic log-cubic; zero rates: linear, cubic with Kruger's derivatives and
  with the spline; forward rates: backward-flat and linear; simple zero rates: linear) x 4 helper sets (7 deposits;
  QuantLib's test fixture, 6 deposits on Euribor indexes and 15 swaps on Euribor 6M; 3 deposits and 9 swaps
  discounted on a separate flat curve, with the context's indexed coupons; 3 deposits and 9 swaps with pillars at
  maturity and at custom dates 20 days before it) x 3 evaluation dates (Monday 15 January 2024; Thursday 29 February
  2024; Thursday 28 March 2024, before TARGET's Easter holidays: a deposit's fixing date on a weekend evaluation date
  is before it, and QuantLib reads its fixing), each curve with a fixed reference date two TARGET days after the
  evaluation date, but on the third date, where it is QuantLib's moving curve on 2 TARGET settlement days; the day
  counter Actual/360 or Actual/365 (Fixed) by the pair. QuantLib refuses two pairs on one set each, on every date: the
  spline cubic discount curve on the discounted swaps, whose third pass can't bracket a node, and the simple-zero
  curve on the custom pillars, whose maximum date is its last node's, before the last swap's maturity.
- 10 curves in one case, on the fixture's helpers with jumps, given with their dates (one before the reference date
  among them) or on QuantLib's default dates, 31 December of successive years.
- 12 cubic zero curves on the fixture's helpers in 3 cases, one per evaluation date: Akima's derivatives and a
  Lagrange left end, which need four points, so that QuantLib bootstraps them on linear interpolation until it can
  construct them on the first pass's first nodes; and Periodic ends and the FourthOrder derivatives, which QuantLib
  refuses, its interpolation refusing their nodes when updated.
- 21 cases of a curve rebuilt after a bumped quote, which replace QuantLib's testObservability
  (test-suite/piecewiseyieldcurve.cpp) with the fixture's moving log-linear discount curve: each rebuilt with its
  i-th quote times 1.01, the discount at the time of the unbumped i-th helper's pillar (extrapolated, as the test reads
  it), which differs from the unbumped curve's, as the test asserts.
- 4 curves in 2 cases, built 15 TARGET days after the evaluation date their helpers were built on (the fixture's, and
  the custom pillars'): QuantLib's helpers re-date themselves when the evaluation date moves, so the curves' values are
  those of fresh helpers on the later date, which this generator builds (nanoql's curve re-dates the helpers it was
  given to its own context).
- 18 curves in 2 cases: the wheel's nine named iterative curves (PiecewiseLinearSimpleZero, PiecewiseFlatForward,
  PiecewiseLogLinearDiscount, PiecewiseLinearForward, PiecewiseLinearZero, PiecewiseCubicZero,
  PiecewiseLogCubicDiscount, PiecewiseKrugerLogDiscount, PiecewiseLogMixedLinearCubicDiscount) on the fixture's helpers
  on the first evaluation date, with a fixed reference date and on 2 TARGET settlement days, each built through its
  class by nanoql.
- 12 log-mixed-linear-cubic discount curves in 2 cases, on the fixture's helpers (22 nodes) and on 4 deposits (5
  nodes): the switch point n = 3 and 5 under both behaviours, whose construction refuses the first pass's first
  prefixes, so that QuantLib bootstraps them on linear interpolation until it can construct them (on 5 nodes, n = 5
  never can, and the curve stays linear); and the derivatives matched at the switch point n = 2 (a first-derivative
  left end with no value), under SplitRanges, and under ShareRanges, whose construction refuses them on every prefix,
  so that the curve is linear.

- The appended cases: 3 curves (log-linear discounts, linear zero rates, backward-flat forwards) on 2 deposits, 7
  FRAs on Euribor 3M and 5 quarterly swaps on Euribor 3M, on each evaluation date; 2 curves (log-linear discounts,
  backward-flat forwards) on 2 deposits, 4 IMM futures on Euribor 3M from the first whose fixing isn't before the
  evaluation date, a custom futures helper with no end date, whose null pillar the bootstraps drop as expired (DS13),
  and 4 swaps, on each evaluation date; 2 curves (Kruger cubic zero rates, monotonic log-cubic discounts) on a 1-day
  deposit and 12 OIS helpers on ESTR with a payment lag, from the evaluation date, on each evaluation date; 2 curves (log-linear discounts,
  linear zero rates) on 6 EUR/USD FX swap helpers with a flat collateral curve, on the first evaluation date; 2
  log-linear discount curves on QuantLib's ARS/USD FX swap helpers with a flat USD collateral curve on 25 September
  2019, which QuantLib's default bootstrap refuses and 5 attempts build; and the DS12 pair: 2 deposits and an IMM FRA
  from the spot date to the first IMM date with its pillar at maturity, before its latest relevant date, under the
  linear simple-zero curve, which QuantLib refuses, its maximum date its last pillar, and the log-linear discount
  curve, which builds (DS12); and 3 curves built 15 TARGET days after the evaluation date their FRA and FX swap
  helpers were built on, which re-date them.
- Then, appended last, 5 cases of convex-monotone curves (ql/math/interpolations/convexmonotoneinterpolation.hpp) on
  the fixture's helpers, one per evaluation date and one more: forward rates and zero rates on QuantLib's default
  ConvexMonotone, and forward rates at quadraticities of 1 and 0.5 (monotonicity 0.9), and of 0 without forcing
  positivity, the fixture's rates positive, so that no section reaches the S-core plan's CR7 (i)-(iii) fixes; and the
  wheel's four named convex-monotone curves (PiecewiseConvexMonotoneForward, PiecewiseConvexMonotoneZero,
  GlobalPiecewiseConvexMonotoneForward, GlobalPiecewiseConvexMonotoneZero) on the fixture's helpers on the first
  evaluation date, with a fixed reference date and on 2 TARGET settlement days, each built through its class by nanoql,
  the global ones on QuantLib's default GlobalBootstrap.

A curve's outputs are whether it builds, QuantLib's refusals recorded as its throw; its nodes' dates and data, its
maximum date, its discounts at 40 dates (35 from its reference date to its maximum date, the earlier ones closer
together, and 5 past it, extrapolated) and every helper's quote error, read from the helper QuantLib has bound to the
curve; a curve built after its helpers' evaluation date gives no quote errors (its helpers are re-dated copies a user
can't read). A case of several curves on the same helpers ("kind": "curves" or "moved") gives each output as an array
of one element per curve, a refused curve's outputs empty and its maximum date null; a rebuilt curve's case ("kind":
"bumped") gives its dates, data and the one discount.

Each case's inputs hold its settings, its "kind", "helpers" (the helper specs of termstructures/yield/ratehelpers.py),
and "curves" (curve specs) with "discountDates" and "extrapolate" (one list per curve, a flag per date), "moved" adding
"helpersEvaluationDate", or for "bumped" "curve" (one spec) and "testTime". A curve is {"traits", "interpolator",
"form", "dayCounter", "jumps", "jumpDates", "bootstrap"}, "form" being "referenceDate" with "referenceDate" or
"settlementDays" with "settlementDays" and "calendar"; null jumps or jump dates are none, so the jumps take QuantLib's
default dates; the bootstrap is null for QuantLib's default IterativeBootstrap, or {"accuracy", "minValue", "maxValue",
"maxAttempts", "maxFactor", "minFactor", "dontThrow", "dontThrowSteps", "maxEvaluations"}, null reals for
Null<Real>(); a named curve adds "class", the named class nanoql builds it through. An interpolator is {"type":
"Linear" | "LogLinear" | "BackwardFlat" | "KrugerLog" | "MonotonicLogCubic"}, or {"type": "Cubic" | "LogCubic",
"derivativeApprox", "monotonic", "leftCondition", "leftConditionValue", "rightCondition", "rightConditionValue"}, or
{"type": "LogMixedLinearCubic", "n", "behavior", and those six}, every parameter named, since the wheel's defaults for
LogCubic and LogMixedLinearCubic aren't QuantLib's (a null "leftConditionValue" is Null<Real>()), or {"type":
"ConvexMonotone", "quadraticity", "monotonicity", "forcePositive"}; a "global" bootstrap is QuantLib's default
GlobalBootstrap, the named global classes' own.

The wheel names each pair's class (PiecewiseLogLinearDiscount, PiecewiseLogCubicDiscount, PiecewiseKrugerLogDiscount,
PiecewiseLinearZero, PiecewiseCubicZero, PiecewiseFlatForward, PiecewiseLinearForward, PiecewiseLinearSimpleZero,
PiecewiseLogMixedLinearCubicDiscount), the
spline cubic discount curve by its shim's target _PiecewiseSplineCubicDiscount (on the wheel's SplineCubic, QuantLib's
Cubic with the spline and no monotonicity), and builds each from (reference date or settlement days and calendar,
helpers, day counter, jumps, jump dates, interpolator, bootstrap). Every curve is built on fresh helpers under the
case's settings, and kept alive while its helpers are read, since a helper holds a raw pointer to it. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/Yield/PiecewiseYieldCurveVectorTests.cs, with
tests/NanoQL.Tests/Vectors/BootstrapInputs.cs, which documents the specs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_ratehelpers",
                                               os.path.join(v.GEN_ROOT, "termstructures", "yield", "ratehelpers.py"))
rh = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(rh)
fvf, irx = rh.fvf, rh.irx

D = rh.D
TARGET, A360, A365 = rh.TARGET, rh.A360, rh.A365
# Monday 15 January 2024; Thursday 29 February 2024, the end of a month; Thursday 28 March 2024, before TARGET's
# Easter holidays. A deposit's fixing date on a weekend evaluation date is before it, and QuantLib reads its fixing.
EVALUATION_DATES = [D(15, 1, 2024), D(29, 2, 2024), D(28, 3, 2024)]
CI = ql.CubicInterpolation


def cubic(da, monotonic=False, left=CI.SecondDerivative, left_value=0.0, right=CI.SecondDerivative, right_value=0.0,
          log=False):
    return {"type": "LogCubic" if log else "Cubic", "derivativeApprox": int(da), "monotonic": monotonic,
            "leftCondition": int(left), "leftConditionValue": left_value, "rightCondition": int(right),
            "rightConditionValue": right_value}


def log_mixed(n, behavior, da=CI.Spline, monotonic=True, left=CI.SecondDerivative, left_value=0.0,
              right=CI.SecondDerivative, right_value=0.0):
    """A LogMixedLinearCubic spec, every parameter named; a None left value is Null<Real>()."""
    return {"type": "LogMixedLinearCubic", "n": n, "behavior": int(behavior), "derivativeApprox": int(da),
            "monotonic": monotonic, "leftCondition": int(left), "leftConditionValue": left_value,
            "rightCondition": int(right), "rightConditionValue": right_value}


LINEAR, LOG_LINEAR, BACKWARD_FLAT = {"type": "Linear"}, {"type": "LogLinear"}, {"type": "BackwardFlat"}
KRUGER_LOG, MONOTONIC_LOG_CUBIC = {"type": "KrugerLog"}, {"type": "MonotonicLogCubic"}
SPLINE_CUBIC = cubic(CI.Spline)
KRUGER_CUBIC = cubic(CI.Kruger)
SHARE_RANGES, SPLIT_RANGES = ql.MixedInterpolation.ShareRanges, ql.MixedInterpolation.SplitRanges

# The 11 pairs, each with its day counter.
PAIRS = [
    ("Discount", LOG_LINEAR, A360),
    ("Discount", SPLINE_CUBIC, A365),
    ("Discount", cubic(CI.Spline, log=True), A360),
    ("Discount", KRUGER_LOG, A365),
    ("Discount", MONOTONIC_LOG_CUBIC, A360),
    ("ZeroYield", LINEAR, A365),
    ("ZeroYield", KRUGER_CUBIC, A360),
    ("ZeroYield", SPLINE_CUBIC, A365),
    ("ForwardRate", BACKWARD_FLAT, A360),
    ("ForwardRate", LINEAR, A365),
    ("SimpleZeroYield", LINEAR, A360),
]

# The wheel's class for each traits and interpolator type.
WHEEL = {
    ("Discount", "LogLinear"): ql.PiecewiseLogLinearDiscount,
    ("Discount", "Cubic"): ql.QuantLib._PiecewiseSplineCubicDiscount,
    ("Discount", "LogCubic"): ql.PiecewiseLogCubicDiscount,
    ("Discount", "KrugerLog"): ql.PiecewiseKrugerLogDiscount,
    ("Discount", "MonotonicLogCubic"): ql.PiecewiseLogCubicDiscount,
    ("ZeroYield", "Linear"): ql.PiecewiseLinearZero,
    ("ZeroYield", "Cubic"): ql.PiecewiseCubicZero,
    ("ForwardRate", "BackwardFlat"): ql.PiecewiseFlatForward,
    ("ForwardRate", "Linear"): ql.PiecewiseLinearForward,
    ("SimpleZeroYield", "Linear"): ql.PiecewiseLinearSimpleZero,
    ("Discount", "LogMixedLinearCubic"): ql.PiecewiseLogMixedLinearCubicDiscount,
    ("ForwardRate", "ConvexMonotone"): ql.PiecewiseConvexMonotoneForward,
    ("ZeroYield", "ConvexMonotone"): ql.PiecewiseConvexMonotoneZero,
}

# The wheel's class for each pair on the global bootstrap, by the named class.
GLOBAL_WHEEL = {
    "GlobalPiecewiseConvexMonotoneForward": ql.GlobalPiecewiseConvexMonotoneForward,
    "GlobalPiecewiseConvexMonotoneZero": ql.GlobalPiecewiseConvexMonotoneZero,
}


def make_interpolator(traits, spec):
    t = spec["type"]
    if t == "LogMixedLinearCubic":
        return ql.LogMixedLinearCubic(spec["n"], spec["behavior"], spec["derivativeApprox"], spec["monotonic"],
                                      spec["leftCondition"], spec["leftConditionValue"], spec["rightCondition"],
                                      spec["rightConditionValue"])
    if t in ("Cubic", "LogCubic"):
        args = (spec["derivativeApprox"], spec["monotonic"], spec["leftCondition"], spec["leftConditionValue"],
                spec["rightCondition"], spec["rightConditionValue"])
        if traits == "Discount" and t == "Cubic":
            # _PiecewiseSplineCubicDiscount takes the wheel's SplineCubic only.
            assert spec == SPLINE_CUBIC, spec
            return ql.SplineCubic()
        return ql.Cubic(*args) if t == "Cubic" else ql.LogCubic(*args)
    if t == "ConvexMonotone":
        return ql.ConvexMonotone(spec["quadraticity"], spec["monotonicity"], spec["forcePositive"])
    return {"Linear": ql.Linear, "LogLinear": ql.LogLinear, "BackwardFlat": ql.BackwardFlat,
            "KrugerLog": ql.KrugerLog, "MonotonicLogCubic": ql.MonotonicLogCubic}[t]()


def bootstrap_spec(accuracy=None, min_value=None, max_value=None, max_attempts=1, max_factor=2.0, min_factor=2.0,
                   dont_throw=False, dont_throw_steps=10, max_evaluations=100):
    return {"accuracy": accuracy, "minValue": min_value, "maxValue": max_value, "maxAttempts": max_attempts,
            "maxFactor": max_factor, "minFactor": min_factor, "dontThrow": dont_throw,
            "dontThrowSteps": dont_throw_steps, "maxEvaluations": max_evaluations}


def make_bootstrap(spec):
    """The wheel's IterativeBootstrap, which is write-only: the spec records what was passed."""
    if spec is None:
        return ql.IterativeBootstrap()
    return ql.IterativeBootstrap(spec["accuracy"], spec["minValue"], spec["maxValue"], spec["maxAttempts"],
                                 spec["maxFactor"], spec["minFactor"], spec["dontThrow"], spec["dontThrowSteps"],
                                 spec["maxEvaluations"])


def curve_spec(traits, interpolator, dc, today, moving=False, jumps=None, jump_dates=None, bootstrap=None,
               named=None):
    spec = {"traits": traits, "interpolator": interpolator}
    if moving:
        spec.update({"form": "settlementDays", "settlementDays": 2, "calendar": TARGET})
    else:
        spec.update({"form": "referenceDate", "referenceDate": v.enc(ql.TARGET().advance(today, 2, ql.Days))})
    spec.update({"dayCounter": dc, "jumps": jumps, "jumpDates": v.enc(jump_dates), "bootstrap": bootstrap})
    if named is not None:
        # the named class nanoql builds the curve through; the wheel's class of that name is WHEEL's for the pair
        spec["class"] = named
    return spec


def make_curve(spec, helpers):
    """The wheel's curve for a spec on the given helpers, built under the settings already set."""
    traits, t = spec["traits"], spec["interpolator"]["type"]
    if spec["form"] == "referenceDate":
        head = (ql.Date(spec["referenceDate"]),)
    else:
        head = (spec["settlementDays"], irx.calendar(spec["calendar"]))
    jumps = [ql.QuoteHandle(ql.SimpleQuote(x)) for x in spec["jumps"] or []]
    jump_dates = [ql.Date(x) for x in spec["jumpDates"] or []]
    if spec["bootstrap"] == "global":
        # the wheel's global classes take the bootstrap before the jumps
        cls = GLOBAL_WHEEL[spec["class"]]
        return cls(*head, helpers, irx.day_counter(spec["dayCounter"]), ql.GlobalBootstrap(), jumps, jump_dates,
                   make_interpolator(traits, spec["interpolator"]))
    cls = WHEEL[(traits, t)]
    # a deprecated name's class is its shim's target, the name with a leading underscore
    assert spec.get("class", cls.__name__) == cls.__name__.lstrip("_") if "class" in spec else True, spec
    return cls(*head, helpers, irx.day_counter(spec["dayCounter"]), jumps, jump_dates,
               make_interpolator(traits, spec["interpolator"]), make_bootstrap(spec["bootstrap"]))


def discount_dates(curve):
    """35 dates from the reference date to the maximum date, closer together early on, then 5 past it."""
    reference, last = curve.referenceDate(), curve.maxDate()
    span = last - reference
    inside = [reference + int(round(span * (k / 34.0) ** 2)) for k in range(35)]
    past = [last + n for n in (1, 30, 365, 1826, 3653)]
    return inside + past, [False] * 35 + [True] * 5


def curve_outputs(curve, helpers, quote_errors=True):
    """A built curve's outputs, and its discount dates and their flags."""
    dates, flags = discount_dates(curve)
    out = {"built": True, "dates": v.enc(list(curve.dates())), "data": list(curve.data()),
           "maxDate": v.enc(curve.maxDate()),
           # a zero-rate convex-monotone curve's extrapolation reads its interpolation's derivative, which QuantLib
           # refuses
           "discounts": [v.attempt(lambda d=d, x=x: curve.discount(d, x)) for d, x in zip(dates, flags)]}
    if quote_errors:
        # a helper the bootstraps drop as expired is never bound, and QuantLib refuses its quote error
        out["quoteErrors"] = [v.attempt(h.quoteError) for h in helpers]
    return out, v.enc(dates), flags


def group_case(name, st, kind, specs, helper_specs, quote_errors=True, extra=None):
    """One case of several curves on the same helper specs, each curve built on fresh helpers and kept alive while
    they are read: each output an array of one element per curve, a refused curve's outputs empty (its maximum date
    null) after its throw in "built"."""
    keys = ["built", "dates", "data", "maxDate", "discounts"] + (["quoteErrors"] if quote_errors else [])
    expected = {k: [] for k in keys}
    discount_inputs, flag_inputs = [], []
    for spec in specs:
        helpers = [rh.make_value_helper(h) for h in helper_specs]
        try:
            curve = make_curve(spec, helpers)
            curve.nodes()
        except RuntimeError:
            one = {"built": dict(v.THROWS), "dates": [], "data": [], "maxDate": None, "discounts": [],
                   "quoteErrors": []}
            dates, flags = [], []
        else:
            one, dates, flags = curve_outputs(curve, helpers, quote_errors)
        for k in keys:
            expected[k].append(one[k])
        discount_inputs.append(dates)
        flag_inputs.append(flags)
    inputs = {"settings": st, "kind": kind}
    inputs.update(extra or {})
    inputs.update({"curves": specs, "helpers": helper_specs, "discountDates": discount_inputs,
                   "extrapolate": flag_inputs})
    return {"name": name, "inputs": inputs, "expected": expected}


# QuantLib's test fixture (test-suite/piecewiseyieldcurve.cpp): deposits on Euribor of their tenors and swaps on
# Euribor 6M against an annual, unadjusted 30/360 fixed leg on TARGET.
DEPOSIT_DATA = [(1, ql.Weeks, 4.559), (1, ql.Months, 4.581), (2, ql.Months, 4.573), (3, ql.Months, 4.557),
                (6, ql.Months, 4.496), (9, ql.Months, 4.490)]
SWAP_DATA = [(1, 4.54), (2, 4.63), (3, 4.75), (4, 4.86), (5, 4.99), (6, 5.11), (7, 5.23), (8, 5.33), (9, 5.41),
             (10, 5.47), (12, 5.60), (15, 5.75), (20, 5.89), (25, 5.95), (30, 5.96)]


def fixture(deposit_rates=None, swap_rates=None):
    """The fixture's 21 helpers, with the given rates (by default the fixture's, each its datum over 100)."""
    deposit_rates = deposit_rates or [r / 100 for _, _, r in DEPOSIT_DATA]
    swap_rates = swap_rates or [r / 100 for _, r in SWAP_DATA]
    out = [rh.deposit_index(rate, rh.euribor(n, units)) for (n, units, _), rate in zip(DEPOSIT_DATA, deposit_rates)]
    out += [rh.swap_tenor(rate, (n, ql.Years)) for (n, _), rate in zip(SWAP_DATA, swap_rates)]
    return out


def deposits_only():
    MF = ql.ModifiedFollowing
    data = [((1, ql.Weeks), 0.04559, ql.Following), ((1, ql.Months), 0.04581, MF), ((2, ql.Months), 0.04573, MF),
            ((3, ql.Months), 0.04557, MF), ((6, ql.Months), 0.04496, MF), ((9, ql.Months), 0.04490, MF),
            ((12, ql.Months), 0.04471, MF)]
    return [rh.deposit_tenor(rate, tenor, 2, TARGET, convention, False, A360) for tenor, rate, convention in data]


SHORT_DEPOSITS = [rh.deposit_index(0.0458, rh.E1M), rh.deposit_index(0.0456, rh.E3M),
                  rh.deposit_index(0.0450, rh.E6M)]
SWAP_YEARS = [(1, 0.0454), (2, 0.0463), (3, 0.0475), (5, 0.0499), (7, 0.0523), (10, 0.0547), (15, 0.0575),
              (20, 0.0589), (30, 0.0596)]


def discounted_swaps(today):
    discount = fvf.curve(0.027, reference=today)
    return SHORT_DEPOSITS + [rh.swap_tenor(rate, (n, ql.Years), discount=discount) for n, rate in SWAP_YEARS]


def pillared_swaps(today):
    """Swaps with pillars at maturity, at the last relevant date and at custom dates 20 days before maturity."""
    out = list(SHORT_DEPOSITS)
    choices = [ql.Pillar.MaturityDate, ql.Pillar.CustomDate, ql.Pillar.MaturityDate, ql.Pillar.CustomDate,
               ql.Pillar.LastRelevantDate, ql.Pillar.MaturityDate, ql.Pillar.CustomDate, ql.Pillar.MaturityDate,
               ql.Pillar.CustomDate]
    for (n, rate), choice in zip(SWAP_YEARS, choices):
        indexed = True if n == 10 else None
        custom = None
        if choice == ql.Pillar.CustomDate:
            with v.case_settings(today):
                maturity = rh.make_helper(rh.swap_tenor(rate, (n, ql.Years))).maturityDate()
            custom = maturity - 20
        out.append(rh.swap_tenor(rate, (n, ql.Years), pillar=choice, custom=custom, indexed=indexed))
    return out


# The helper sets: their names, specs on an evaluation date, and the at-par switch they set.
HELPER_SETS = [("deposits", lambda today: deposits_only(), None),
               ("fixture", lambda today: fixture(), None),
               ("discounted swaps", discounted_swaps, False),
               ("pillared swaps", pillared_swaps, None)]


def grid_cases():
    """The 11 pairs on each helper set and evaluation date, a case each."""
    out = []
    for k, today in enumerate(EVALUATION_DATES):
        for set_name, helpers_on, at_par in HELPER_SETS:
            with v.case_settings(today, use_at_par_coupons=at_par) as st:
                specs = [curve_spec(traits, interpolator, dc, today, k == 2) for traits, interpolator, dc in PAIRS]
                out.append(group_case(f"{today.ISO()} {set_name}", st, "curves", specs, helpers_on(today)))
    return out


def jump_cases():
    """10 curves on the fixture's helpers with jumps."""
    today = EVALUATION_DATES[0]
    given = ([0.999, 0.9995], [D(29, 12, 2025), D(30, 12, 2026)])
    before = ([0.998, 0.999, 1.001], [D(10, 1, 2024), D(28, 6, 2024), D(31, 12, 2030)])
    default = ([0.9997, 0.9993], None)
    rows = [(PAIRS[0], given), (PAIRS[0], default), (PAIRS[5], given), (PAIRS[6], default), (PAIRS[8], before),
            (PAIRS[9], default), (PAIRS[10], given), (PAIRS[4], default), (PAIRS[3], before), (PAIRS[7], given)]
    with v.case_settings(today) as st:
        specs = [curve_spec(traits, interpolator, dc, today, jumps=jumps, jump_dates=dates)
                 for (traits, interpolator, dc), (jumps, dates) in rows]
        return [group_case(f"{today.ISO()} jumps on fixture", st, "curves", specs, fixture())]


FALLBACK_CURVES = [cubic(CI.Akima), cubic(CI.Spline, left=CI.Lagrange),
                   cubic(CI.Spline, left=CI.Periodic, right=CI.Periodic), cubic(CI.FourthOrder)]


def fallback_cases():
    """The cubic zero curves QuantLib bootstraps on linear interpolation until it can, and those it refuses."""
    out = []
    for today in EVALUATION_DATES:
        with v.case_settings(today) as st:
            specs = [curve_spec("ZeroYield", interpolator, A360, today) for interpolator in FALLBACK_CURVES]
            out.append(group_case(f"{today.ISO()} cubic zero curves on fixture", st, "curves", specs, fixture()))
    return out


def bumped_cases():
    """testObservability's quote changes: the fixture's moving log-linear discount curve rebuilt with each quote
    bumped, read at the time of the unbumped helper's pillar."""
    today = EVALUATION_DATES[0]
    deposit_rates = [r / 100 for _, _, r in DEPOSIT_DATA]
    swap_rates = [r / 100 for _, r in SWAP_DATA]
    out = []
    with v.case_settings(today):
        spec = curve_spec("Discount", LOG_LINEAR, A360, today, moving=True)
        helpers = [rh.make_helper(h) for h in fixture()]
        curve = make_curve(spec, helpers)
        settlement = curve.referenceDate()
        times = [ql.Actual360().yearFraction(settlement, h.pillarDate()) for h in helpers]
        unbumped = [curve.discount(t) for t in times]
    rates = deposit_rates + swap_rates
    for i in range(len(rates)):
        bumped = list(rates)
        bumped[i] = rates[i] * 1.01
        helper_specs = fixture(bumped[:len(deposit_rates)], bumped[len(deposit_rates):])
        with v.case_settings(today) as st:
            helpers = [rh.make_helper(h) for h in helper_specs]
            curve = make_curve(spec, helpers)
            curve.nodes()
            expected = {"built": True, "dates": v.enc(list(curve.dates())), "data": list(curve.data()),
                        "discountAtTestTime": curve.discount(times[i], True)}
            # the rebuilt curve reads another discount there, as testObservability asserts
            assert expected["discountAtTestTime"] != unbumped[i], i
            out.append({"name": f"{today.ISO()} quote {i + 1} bumped", "inputs": {
                "settings": st, "kind": "bumped", "curve": spec, "helpers": helper_specs, "testTime": times[i]},
                "expected": expected})
    return out


def moved_cases():
    """Curves built 15 TARGET days after their helpers' evaluation date: the values of fresh helpers on that date."""
    first = EVALUATION_DATES[0]
    later = ql.TARGET().advance(first, 15, ql.Days)
    out = []
    for name, rows, helper_specs in (
            ("fixture", [("Discount", LOG_LINEAR, A360, True), ("ZeroYield", KRUGER_CUBIC, A360, False),
                         ("ForwardRate", BACKWARD_FLAT, A365, True)], fixture()),
            ("pillared swaps", [("Discount", MONOTONIC_LOG_CUBIC, A360, False)], pillared_swaps(first))):
        with v.case_settings(later) as st:
            specs = [curve_spec(traits, interpolator, dc, later, moving) for traits, interpolator, dc, moving in rows]
            out.append(group_case(f"{later.ISO()} {name} built on {first.ISO()}", st, "moved", specs, helper_specs,
                                  quote_errors=False, extra={"helpersEvaluationDate": first.serialNumber()}))
    return out


# The wheel's named curves, each with the pair it closes and a day counter.
NAMED = [("PiecewiseLinearSimpleZero", "SimpleZeroYield", LINEAR, A360),
         ("PiecewiseFlatForward", "ForwardRate", BACKWARD_FLAT, A365),
         ("PiecewiseLogLinearDiscount", "Discount", LOG_LINEAR, A360),
         ("PiecewiseLinearForward", "ForwardRate", LINEAR, A360),
         ("PiecewiseLinearZero", "ZeroYield", LINEAR, A365),
         ("PiecewiseCubicZero", "ZeroYield", KRUGER_CUBIC, A360),
         ("PiecewiseLogCubicDiscount", "Discount", cubic(CI.Spline, True, log=True), A365),
         ("PiecewiseKrugerLogDiscount", "Discount", KRUGER_LOG, A360),
         ("PiecewiseLogMixedLinearCubicDiscount", "Discount", log_mixed(2, SHARE_RANGES), A365)]


def named_cases():
    """The nine named curves on the fixture's helpers, each built through its class, with a fixed reference date and
    on settlement days."""
    today = EVALUATION_DATES[0]
    out = []
    for moving in (False, True):
        with v.case_settings(today) as st:
            specs = [curve_spec(traits, interpolator, dc, today, moving, named=named)
                     for named, traits, interpolator, dc in NAMED]
            form = "on settlement days" if moving else "with a reference date"
            out.append(group_case(f"{today.ISO()} named classes on fixture {form}", st, "curves", specs, fixture()))
    return out


def deprecated_named_cases():
    """The wheel's deprecated PiecewiseSplineCubicDiscount, its shim's target _PiecewiseSplineCubicDiscount on the
    wheel's SplineCubic, through nanoql's class of the name, on the fixture's helpers, with a fixed reference date and
    on settlement days."""
    today = EVALUATION_DATES[0]
    out = []
    for moving in (False, True):
        with v.case_settings(today) as st:
            specs = [curve_spec("Discount", SPLINE_CUBIC, A365, today, moving, named="PiecewiseSplineCubicDiscount")]
            form = "on settlement days" if moving else "with a reference date"
            out.append(group_case(f"{today.ISO()} the deprecated named class on fixture {form}", st, "curves", specs,
                                  fixture()))
    return out


def four_deposits():
    """4 deposits, so 5 nodes."""
    return [deposits_only()[k] for k in (1, 3, 4, 6)]


def log_mixed_cases():
    """Log-mixed-linear-cubic discount curves whose target refuses the first pass's prefix, so that QuantLib bootstraps
    them on linear interpolation until it can construct them: n = 3 and 5 under both behaviours, on the fixture's 22
    nodes and on 4 deposits' 5, where n = 5 is never constructed, so the curve stays linear; and the derivatives matched
    at the switch point (a first-derivative left end with no value) under SplitRanges, and under ShareRanges, whose
    construction refuses them on every prefix, so the curve is linear."""
    today = EVALUATION_DATES[0]
    rows = [log_mixed(n, behavior) for n in (3, 5) for behavior in (SHARE_RANGES, SPLIT_RANGES)]
    rows += [log_mixed(2, behavior, CI.Spline, False, CI.FirstDerivative, None)
             for behavior in (SHARE_RANGES, SPLIT_RANGES)]
    out = []
    for set_name, helpers in (("fixture", fixture()), ("4 deposits", four_deposits())):
        with v.case_settings(today) as st:
            specs = [curve_spec("Discount", interpolator, A365, today) for interpolator in rows]
            out.append(group_case(f"{today.ISO()} log-mixed-linear-cubic curves on {set_name}", st, "curves", specs,
                                  helpers))
    return out


def ref_spec(traits, interpolator, dc, today, bootstrap=None):
    return curve_spec(traits, interpolator, dc, today, bootstrap=bootstrap)


def fra_set(today):
    fras = [(0, 0.0393), (1, 0.0395), (2, 0.0397), (3, 0.0399), (6, 0.0402), (9, 0.0405), (12, 0.0409)]
    swaps = [(2, 0.0417), (3, 0.0428), (5, 0.0446), (7, 0.0461), (10, 0.0475)]
    return ([rh.deposit_index(0.0386, rh.E1W), rh.deposit_index(0.0389, rh.E1M)]
            + [rh.fra_months_index(rate, n, rh.E3M) for n, rate in fras]
            + [rh.swap_tenor(rate, (n, ql.Years), freq=ql.Quarterly, index=rh.E3M) for n, rate in swaps])


def futures_set(today):
    """2 deposits, 4 IMM futures on Euribor 3M, as QuantLib's test fixture picks them, a custom one with no end date
    and 4 swaps."""
    euribor3m = ql.Euribor3M()
    out = [rh.deposit_index(0.0386, rh.E1W), rh.deposit_index(0.0389, rh.E1M)]
    imm = today
    for price in (96.07, 95.95, 95.82, 95.70):
        imm = ql.IMM.nextDate(imm, False)
        # if the fixing is before the evaluation date, we
        # just jump forward by one future maturity
        if euribor3m.fixingDate(imm) < today:
            imm = ql.IMM.nextDate(imm, False)
        out.append(rh.futures_index(price, imm, rh.E3M, 0.0001))
    out.append(rh.futures_dates(96.5, ql.TARGET().advance(today, 10, ql.Days), None, A360, 0.0, ql.Futures.Custom))
    swaps = [(2, 0.0417), (3, 0.0428), (5, 0.0446), (10, 0.0475)]
    return out + [rh.swap_tenor(rate, (n, ql.Years)) for n, rate in swaps]


def ois_set(today):
    data = [((1, ql.Weeks), 0.0391), ((1, ql.Months), 0.0392), ((3, ql.Months), 0.0390), ((6, ql.Months), 0.0386),
            ((1, ql.Years), 0.0371), ((2, ql.Years), 0.0342), ((3, ql.Years), 0.0328), ((5, ql.Years), 0.0317),
            ((7, ql.Years), 0.0315), ((10, ql.Years), 0.0318), ((20, ql.Years), 0.0322), ((30, ql.Years), 0.0310)]
    deposit = rh.deposit_tenor(0.0390, (1, ql.Days), 0, TARGET, ql.Following, False, A360)
    return [deposit] + [rh.ois_tenor(rate, tenor, payment_lag=2) for tenor, rate in data]


def fx_set(today):
    data = [((1, ql.Weeks), 0.0006), ((1, ql.Months), 0.0026), ((3, ql.Months), 0.0079), ((6, ql.Months), 0.0157),
            ((1, ql.Years), 0.0306), ((2, ql.Years), 0.0570)]
    collateral = fvf.curve(0.052, A360, ql.Simple, reference=today)
    return [rh.fx_tenor(points, 1.0950, tenor, collateral=collateral) for tenor, points in data]


ARS_DATE = D(25, 9, 2019)


def ars_set():
    """QuantLib's testIterativeBootstrapRetries' FX swap helpers, on a flat USD collateral curve."""
    data = [((1, ql.Months), 8.5157), ((2, ql.Months), 12.7180), ((3, ql.Months), 17.8310), ((6, ql.Months), 30.3680),
            ((9, ql.Months), 45.5520), ((1, ql.Years), 60.7370)]
    collateral = fvf.curve(0.0185, A365, reference=ARS_DATE)
    calendar = {"type": "UnitedStates", "market": int(ql.UnitedStates.GovernmentBond)}
    return [rh.fx_tenor(points, 56.881, tenor, 2, calendar, ql.Following, False, True, collateral)
            for tenor, points in data]


def ds12_set(today):
    return [rh.deposit_index(0.0386, rh.E1W), rh.deposit_index(0.0389, rh.E1M),
            rh.fra_imm(0.0391, 0, 1, rh.E3M, pillar=ql.Pillar.MaturityDate)]


def more_cases():
    """The curves on FRA, futures, FX swap and OIS helpers, and the DS12 pair."""
    out = []
    rows = [("FRAs", fra_set, [("Discount", LOG_LINEAR, A360), ("ZeroYield", LINEAR, A365),
                               ("ForwardRate", BACKWARD_FLAT, A360)]),
            ("futures", futures_set, [("Discount", LOG_LINEAR, A360), ("ForwardRate", BACKWARD_FLAT, A365)]),
            ("OIS", ois_set, [("ZeroYield", KRUGER_CUBIC, A365), ("Discount", MONOTONIC_LOG_CUBIC, A365)])]
    for today in EVALUATION_DATES:
        for name, helpers_on, pairs in rows:
            with v.case_settings(today) as st:
                specs = [ref_spec(traits, interpolator, dc, today) for traits, interpolator, dc in pairs]
                if name == "OIS":
                    # the curve starts on the evaluation date, where the 1-day deposit does
                    for spec in specs:
                        spec["referenceDate"] = v.enc(today)
                out.append(group_case(f"{today.ISO()} {name}", st, "curves", specs, helpers_on(today)))
    today = EVALUATION_DATES[0]
    with v.case_settings(today) as st:
        specs = [ref_spec("Discount", LOG_LINEAR, A365, today), ref_spec("ZeroYield", LINEAR, A365, today)]
        out.append(group_case(f"{today.ISO()} FX swaps", st, "curves", specs, fx_set(today)))
    with v.case_settings(ARS_DATE) as st:
        specs = [curve_spec("Discount", LOG_LINEAR, A365, ARS_DATE),
                 curve_spec("Discount", LOG_LINEAR, A365, ARS_DATE, bootstrap=bootstrap_spec(max_attempts=5))]
        for spec in specs:
            spec["referenceDate"] = v.enc(ARS_DATE)
        out.append(group_case(f"{ARS_DATE.ISO()} ARS FX swaps", st, "curves", specs, ars_set()))
    with v.case_settings(today) as st:
        specs = [ref_spec("SimpleZeroYield", LINEAR, A360, today), ref_spec("Discount", LOG_LINEAR, A360, today)]
        out.append(group_case(f"{today.ISO()} IMM FRA with its pillar at maturity", st, "curves", specs,
                              ds12_set(today)))
    # curves built 15 TARGET days after the evaluation date their FRA and FX swap helpers were built on, which re-date
    # them as moved_cases' curves re-date theirs
    later = ql.TARGET().advance(today, 15, ql.Days)
    for name, helper_specs, rows in (
            ("FRAs", fra_set(today), [("Discount", LOG_LINEAR, A360), ("ForwardRate", BACKWARD_FLAT, A360)]),
            ("FX swaps", fx_set(today), [("Discount", LOG_LINEAR, A365)])):
        with v.case_settings(later) as st:
            specs = [ref_spec(traits, interpolator, dc, later) for traits, interpolator, dc in rows]
            out.append(group_case(f"{later.ISO()} {name} built on {today.ISO()}", st, "moved", specs, helper_specs,
                                  quote_errors=False, extra={"helpersEvaluationDate": today.serialNumber()}))
    return out


def bma_moved_cases():
    """A log-linear discount curve on four of termstructures/yield/ratehelpers.py's BMA swap helpers, built 15 TARGET
    days after the evaluation date they were built on, which re-dates them as moved_cases' curves re-date theirs; the
    case stores the Libor and BMA fixings their swaps read before the curve's evaluation date."""
    today = EVALUATION_DATES[0]
    later = ql.TARGET().advance(today, 15, ql.Days)
    helper_specs = [rh.bma_helper(rate, tenor, bma_period=period) for rate, tenor, period in (
        (0.700, (1, ql.Years), (1, ql.Weeks)), (0.705, (2, ql.Years), (1, ql.Weeks)),
        (0.710, (3, ql.Years), (1, ql.Weeks)), (0.715, (5, ql.Years), (1, ql.Months)))]
    with v.case_settings(later, fixings=rh.bma_fixings(helper_specs[0], later)) as st:
        specs = [ref_spec("Discount", LOG_LINEAR, A365, later)]
        return [group_case(f"{later.ISO()} BMA swaps built on {today.ISO()}", st, "moved", specs, helper_specs,
                           quote_errors=False, extra={"helpersEvaluationDate": today.serialNumber()})]


def convex_monotone(quadraticity=0.3, monotonicity=0.7, force_positive=True):
    return {"type": "ConvexMonotone", "quadraticity": quadraticity, "monotonicity": monotonicity,
            "forcePositive": force_positive}


CM_ROWS = [("ForwardRate", convex_monotone(), A360), ("ZeroYield", convex_monotone(), A365),
           ("ForwardRate", convex_monotone(1.0), A365), ("ForwardRate", convex_monotone(0.5, 0.9), A360),
           ("ForwardRate", convex_monotone(0.0, 0.7, False), A365)]
CM_NAMED = [("PiecewiseConvexMonotoneForward", "ForwardRate"), ("PiecewiseConvexMonotoneZero", "ZeroYield"),
            ("GlobalPiecewiseConvexMonotoneForward", "ForwardRate"), ("GlobalPiecewiseConvexMonotoneZero", "ZeroYield")]


def convex_monotone_cases():
    """Convex-monotone curves on the fixture's helpers, one case per evaluation date, and the four named classes."""
    out = []
    for k, today in enumerate(EVALUATION_DATES):
        with v.case_settings(today) as st:
            specs = [curve_spec(traits, interpolator, dc, today, k == 2) for traits, interpolator, dc in CM_ROWS]
            out.append(group_case(f"{today.ISO()} convex-monotone curves on fixture helpers", st, "curves", specs,
                                  fixture()))
    today = EVALUATION_DATES[0]
    for moving in (False, True):
        with v.case_settings(today) as st:
            specs = [curve_spec(traits, convex_monotone(), A360, today, moving, named=named,
                                bootstrap="global" if named.startswith("Global") else None)
                     for named, traits in CM_NAMED]
            form = " on settlement days" if moving else ""
            out.append(group_case(f"{today.ISO()} named convex-monotone classes{form} on fixture helpers", st,
                                  "curves", specs, fixture()))
    return out


def cases():
    out = grid_cases() + jump_cases() + fallback_cases() + bumped_cases() + moved_cases()
    out += named_cases() + log_mixed_cases() + more_cases() + bma_moved_cases()
    out += convex_monotone_cases()
    out += deprecated_named_cases()
    check_rows(out)
    return out


def check_rows(out):
    # QuantLib refuses the Periodic and FourthOrder cubic zero curves when their interpolation is updated; the spline
    # cubic discount curve on swaps discounted apart, whose third pass can't bracket a node; and the simple-zero curve
    # on custom pillars, whose maximum date is its last node's, before the last swap's maturity.
    refused = []
    for today in EVALUATION_DATES:
        cubics = f"{today.ISO()} cubic zero curves on fixture"
        refused += [(cubics, 2), (cubics, 3), (f"{today.ISO()} discounted swaps", 1),
                    (f"{today.ISO()} pillared swaps", 10)]
    # QuantLib's default bootstrap refuses the ARS curve, which 5 attempts build; and DS12: the simple-zero curve, whose
    # maximum date is its last pillar, refuses the IMM FRA whose latest relevant date is after it
    refused += [(f"{ARS_DATE.ISO()} ARS FX swaps", 0),
                (f"{EVALUATION_DATES[0].ISO()} IMM FRA with its pillar at maturity", 0)]
    got = []
    for c in out:
        built = c["expected"]["built"]
        got += [(c["name"], k) for k, b in enumerate(built if isinstance(built, list) else [built]) if b is not True]
    assert sorted(got) == sorted(refused), got


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/piecewiseyieldcurve.hpp", cases)
