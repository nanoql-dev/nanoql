# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GlobalBootstrap (ql/termstructures/globalbootstrap.hpp) on piecewise curves, with the four
yield traits' global guesses and transforms (ql/termstructures/yield/bootstraptraits.hpp): 92 curves in 13 cases, on
the deposit and swap helpers of termstructures/yield/ratehelpers.py, under the evaluation date 15 January 2024, each
curve with a fixed reference date two TARGET days later but where it is QuantLib's moving curve.

- 72 curves in 12 cases: 6 traits and interpolator pairs (discounts: log-linear, log-cubic with the spline; zero rates:
  linear, cubic with Kruger's derivatives; forward rates: backward-flat; simple zero rates: linear) x 3 helper sets
  (3 deposits and 9 swaps; the same swaps discounted on a separate flat curve, with the context's indexed coupons; the
  same with pillars at maturity and at custom dates 20 days before it) x 4 setups: QuantLib's default bootstrap;
  additional helpers (5 deposits of 2, 4, 5, 7 and 8 months) with the wheel's penalty, the three inner helpers' implied
  rates on the line between the outer two's, and as additional dates the pillars of the 4-, 5- and 7-month deposits;
  an explicit Levenberg-Marquardt method (1e-8 each) and end criteria (1000 iterations, 100 stationary, 1e-10 each);
  and the wheel's constant initial guess, a value for every pillar (0.95 for discounts, 0.03 for rates), before the
  traits' inverse transform. QuantLib refuses the simple-zero curve on the custom pillars in every setup: its maximum
  date is its last node's, before the last swap's maturity.
- 20 curves in one case: the ten global named classes this file builds (each through its class, in its two forms,
  a reference date and settlement days), on the first helper set with QuantLib's default bootstrap; the log-cubic one
  on an explicit LogCubic and the log-mixed-linear-cubic one on termstructures/yield/piecewiseyieldcurve.py's
  two-point, shared-ranges LogMixedLinearCubic, since QuantLib has neither by default and the wheel's defaults aren't
  QuantLib's.

A curve's outputs are those of termstructures/yield/piecewiseyieldcurve.py's curves: whether it builds, QuantLib's
refusals recorded as its throw; its nodes' dates and data; its maximum date; its discounts at 40 dates (35 from its
reference date to its maximum date, 5 past it, extrapolated); and every helper's quote error, read from the helper
QuantLib has bound to the curve. Each output is an array of one element per curve of the case.

Each case's inputs hold its settings, "helpers" (helper specs), "curves" (curve specs) with "discountDates" and
"extrapolate", as termstructures/yield/piecewiseyieldcurve.py writes them; a curve spec's "bootstrap" is null for
QuantLib's default GlobalBootstrap, or {"accuracy", "optimizer", "endCriteria", "additionalHelpers",
"additionalDates", "penalty", "initialGuess"}: an optimizer {"type": "LevenbergMarquardt", "epsfcn", "xtol",
"gtol"} or null for the default, end criteria {"maxIterations", "maxStationaryStateIterations", "rootEpsilon",
"functionEpsilon", "gradientNormEpsilon"} or null, the additional helpers' specs and dates, the penalty "wheel" (the
wheel's AdditionalErrors, SWIG/piecewiseyieldcurve.i) or null, and the initial guess as its values or null. The
named classes' case's curve specs add "class", the class built, and "interpolator" null for the class's default.

The wheel's GlobalBootstrap is write-only and takes no weights, penalty or guess function of one's own (its
caveats, s5-swig SURVEY.md section 4): its penalty is its AdditionalErrors and its guess a constant vector, which the
specs record. Every curve is built on fresh helpers under the case's settings and kept alive while its helpers are
read. Read back by tests/NanoQL.Tests/Vectors/TermStructures/GlobalBootstrapVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_piecewiseyieldcurve", os.path.join(v.GEN_ROOT, "termstructures", "yield", "piecewiseyieldcurve.py"))
pw = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(pw)
rh = pw.rh
CI = ql.CubicInterpolation

TODAY = pw.EVALUATION_DATES[0]
A360, A365 = pw.A360, pw.A365
LOG_CUBIC = pw.cubic(CI.Spline, log=True)

# The 6 pairs, each with its day counter.
PAIRS = [
    ("Discount", pw.LOG_LINEAR, A360),
    ("Discount", LOG_CUBIC, A365),
    ("ZeroYield", pw.LINEAR, A365),
    ("ZeroYield", pw.KRUGER_CUBIC, A360),
    ("ForwardRate", pw.BACKWARD_FLAT, A360),
    ("SimpleZeroYield", pw.LINEAR, A365),
]

# The wheel's global class for each traits and interpolator type.
WHEEL = {
    ("Discount", "LogLinear"): ql.GlobalPiecewiseLogLinearDiscount,
    ("Discount", "LogCubic"): ql.GlobalPiecewiseLogCubicDiscount,
    ("Discount", "Cubic"): ql.GlobalPiecewiseSplineCubicDiscount,
    ("Discount", "KrugerLog"): ql.GlobalPiecewiseKrugerLogDiscount,
    ("ZeroYield", "Linear"): ql.GlobalPiecewiseLinearZero,
    ("ZeroYield", "Cubic"): ql.GlobalPiecewiseCubicZero,
    ("ForwardRate", "BackwardFlat"): ql.GlobalPiecewiseFlatForward,
    ("ForwardRate", "Linear"): ql.GlobalPiecewiseLinearForward,
    ("SimpleZeroYield", "Linear"): ql.GlobalLinearSimpleZeroCurve,
    ("Discount", "LogMixedLinearCubic"): ql.GlobalPiecewiseLogMixedLinearCubicDiscount,
}

# The named classes, each with its traits, interpolator spec (None for the class's default) and day counter.
NAMED = [
    ("GlobalLinearSimpleZeroCurve", "SimpleZeroYield", None, "Linear", A365),
    ("GlobalPiecewiseFlatForward", "ForwardRate", None, "BackwardFlat", A360),
    ("GlobalPiecewiseLogLinearDiscount", "Discount", None, "LogLinear", A360),
    ("GlobalPiecewiseLinearForward", "ForwardRate", None, "Linear", A365),
    ("GlobalPiecewiseLinearZero", "ZeroYield", None, "Linear", A360),
    ("GlobalPiecewiseCubicZero", "ZeroYield", None, "Cubic", A365),
    ("GlobalPiecewiseLogCubicDiscount", "Discount", pw.cubic(CI.Kruger, monotonic=True, log=True), "LogCubic", A360),
    ("GlobalPiecewiseSplineCubicDiscount", "Discount", None, "Cubic", A365),
    ("GlobalPiecewiseKrugerLogDiscount", "Discount", None, "KrugerLog", A360),
    ("GlobalPiecewiseLogMixedLinearCubicDiscount", "Discount", pw.log_mixed(2, pw.SHARE_RANGES), "LogMixedLinearCubic",
     A365),
]


def deposits_and_swaps(today):
    return pw.SHORT_DEPOSITS + [rh.swap_tenor(rate, (n, ql.Years)) for n, rate in pw.SWAP_YEARS]


# The helper sets: their names, specs on an evaluation date, and the at-par switch they set.
HELPER_SETS = [("deposits and swaps", deposits_and_swaps, None),
               ("discounted swaps", pw.discounted_swaps, False),
               ("pillared swaps", pw.pillared_swaps, None)]

ADDITIONAL = [rh.deposit_tenor(rate, (n, ql.Months), 2, rh.TARGET, ql.ModifiedFollowing, False, A360)
              for n, rate in [(2, 0.0452), (4, 0.0457), (5, 0.0455), (7, 0.0449), (8, 0.0447)]]


def bootstrap_spec(accuracy=None, optimizer=None, end_criteria=None, additional_helpers=(), additional_dates=(),
                   penalty=None, initial_guess=None):
    return {"accuracy": accuracy, "optimizer": optimizer, "endCriteria": end_criteria,
            "additionalHelpers": list(additional_helpers), "additionalDates": list(additional_dates),
            "penalty": penalty, "initialGuess": initial_guess}


def additional_setup():
    """The additional helpers with the wheel's penalty, and as additional dates the pillars of the 4-, 5- and 7-month
    deposits."""
    pillars = [rh.make_helper(h).pillarDate() for h in ADDITIONAL]
    return bootstrap_spec(additional_helpers=ADDITIONAL, additional_dates=v.enc([pillars[1], pillars[2], pillars[3]]),
                          penalty="wheel")


LEVENBERG_MARQUARDT = {"type": "LevenbergMarquardt", "epsfcn": 1e-8, "xtol": 1e-8, "gtol": 1e-8}
END_CRITERIA = {"maxIterations": 1000, "maxStationaryStateIterations": 100, "rootEpsilon": 1e-10,
                "functionEpsilon": 1e-10, "gradientNormEpsilon": 1e-10}


def setups(traits, pillars):
    """The 4 setups for a curve on the traits whose curve has the given number of pillars."""
    guess = [0.95 if traits == "Discount" else 0.03] * pillars
    return [("defaults", None),
            ("additional helpers", additional_setup()),
            ("explicit optimizer", bootstrap_spec(optimizer=LEVENBERG_MARQUARDT, end_criteria=END_CRITERIA)),
            ("constant guess", bootstrap_spec(initial_guess=guess))]


def make_bootstrap(spec):
    """The wheel's GlobalBootstrap, which is write-only: the spec records what was passed."""
    if spec is None:
        return ql.GlobalBootstrap()
    optimizer = None
    if spec["optimizer"] is not None:
        o = spec["optimizer"]
        optimizer = ql.LevenbergMarquardt(o["epsfcn"], o["xtol"], o["gtol"])
    end_criteria = None
    if spec["endCriteria"] is not None:
        e = spec["endCriteria"]
        end_criteria = ql.EndCriteria(e["maxIterations"], e["maxStationaryStateIterations"], e["rootEpsilon"],
                                      e["functionEpsilon"], e["gradientNormEpsilon"])
    guess = spec["initialGuess"] or []
    if spec["additionalHelpers"]:
        assert spec["penalty"] == "wheel"
        helpers = [rh.make_helper(h) for h in spec["additionalHelpers"]]
        dates = [ql.Date(d) for d in spec["additionalDates"]]
        return ql.GlobalBootstrap(helpers, dates, spec["accuracy"], optimizer, end_criteria, guess)
    assert spec["penalty"] is None and not spec["additionalDates"]
    return ql.GlobalBootstrap(spec["accuracy"], optimizer, end_criteria, guess)


def curve_spec(traits, interpolator, dc, today, moving=False, bootstrap=None, cls=None):
    spec = pw.curve_spec(traits, interpolator, dc, today, moving, bootstrap=bootstrap)
    if cls is not None:
        spec = dict({"class": cls}, **spec)
    return spec


def make_curve(spec, helpers):
    """The wheel's global curve for a spec on the given helpers, built under the settings already set."""
    traits, i = spec["traits"], spec["interpolator"]
    if "class" in spec:
        cls = getattr(ql, spec["class"])
    else:
        cls = WHEEL[(traits, i["type"])]
    if spec["form"] == "referenceDate":
        head = (ql.Date(spec["referenceDate"]),)
    else:
        head = (spec["settlementDays"], pw.irx.calendar(spec["calendar"]))
    args = (*head, helpers, pw.irx.day_counter(spec["dayCounter"]), make_bootstrap(spec["bootstrap"]))
    if i is None:
        return cls(*args)
    return cls(*args, [], [], pw.make_interpolator(traits, i))


def group_case(name, st, specs, helper_specs):
    """One case of several curves on the same helper specs, each on fresh helpers kept alive while they are read."""
    keys = ["built", "dates", "data", "maxDate", "discounts", "quoteErrors"]
    expected = {k: [] for k in keys}
    discount_inputs, flag_inputs = [], []
    for spec in specs:
        helpers = [rh.make_helper(h) for h in helper_specs]
        try:
            curve = make_curve(spec, helpers)
            curve.nodes()
        except RuntimeError:
            one = {"built": dict(v.THROWS), "dates": [], "data": [], "maxDate": None, "discounts": [],
                   "quoteErrors": []}
            dates, flags = [], []
        else:
            one, dates, flags = pw.curve_outputs(curve, helpers)
        for k in keys:
            expected[k].append(one[k])
        discount_inputs.append(dates)
        flag_inputs.append(flags)
    return {"name": name, "inputs": {"settings": st, "curves": specs, "helpers": helper_specs,
                                     "discountDates": discount_inputs, "extrapolate": flag_inputs},
            "expected": expected}


def setup_cases():
    out = []
    for set_name, helpers_on, at_par in HELPER_SETS:
        for k in range(4):
            with v.case_settings(TODAY, use_at_par_coupons=at_par) as st:
                helper_specs = helpers_on(TODAY)
                specs = []
                for traits, interpolator, dc in PAIRS:
                    setup_name, b = setups(traits, len(helper_specs))[k]
                    specs.append(curve_spec(traits, interpolator, dc, TODAY, bootstrap=b))
                out.append(group_case(f"{TODAY.ISO()} {set_name}, {setup_name}", st, specs, helper_specs))
    return out


def named_cases():
    with v.case_settings(TODAY) as st:
        specs = [curve_spec(traits, interpolator, dc, TODAY, moving, cls=cls)
                 for cls, traits, interpolator, _, dc in NAMED for moving in (False, True)]
        return [group_case(f"{TODAY.ISO()} named classes", st, specs, deposits_and_swaps(TODAY))]


def cases():
    out = setup_cases() + named_cases()
    check_rows(out)
    return out


def check_rows(out):
    assert sum(len(c["expected"]["built"]) for c in out) == 92
    # QuantLib refuses the simple-zero curve on custom pillars in every setup: its maximum date is its last node's,
    # before the last swap's maturity, so its helper's pricing reads past it
    refused = [(c["name"], k) for c in out for k, b in enumerate(c["expected"]["built"]) if b is not True]
    assert refused == [(f"{TODAY.ISO()} pillared swaps, {s}", 5)
                       for s in ("defaults", "additional helpers", "explicit optimizer", "constant guess")], refused
    # the named classes close the generics of the pairs' wheel classes
    for cls, traits, _, itype, _ in NAMED:
        assert WHEEL[(traits, itype)].__name__ == cls, cls


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/globalbootstrap.hpp", cases)
