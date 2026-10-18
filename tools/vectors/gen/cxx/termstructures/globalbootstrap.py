# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GlobalBootstrap (ql/termstructures/globalbootstrap.hpp), each output computed by the C++
harness: what the wheel's GlobalBootstrap can't take (its caveats, s5-swig SURVEY.md section 4), on piecewise curves
under the evaluation date 15 January 2024, 22 curves in 22 cases, each curve with a fixed reference date.

- Instrument weights (6 curves): two 6-month deposits quoted at 1% and 2%, QuantLib's
  testGlobalBootstrapInstrumentWeights' over-determined pair, with weights 0.1 and 0.9 on the log-linear discount and
  linear zero curves, 0.5 and 0.5, and 1 and 0 on the backward-flat forward curve; an overnight deposit, expired at
  the curve's reference date two TARGET days after the evaluation date, before 3 deposits and 9 swaps, with a weight
  for each helper, the expired one's included; and 3 weights for the two deposits, which QuantLib refuses.
- A gradient penalty of the harness's own, QuantLib's testGlobalBootstrapInitialGuessFn's (6 curves): a curve with no
  helpers of its own, the 3 deposits and 9 swaps as additional helpers, and as dates the evaluation date plus 9
  offsets 45 days apart and the helpers' pillars past them; the penalty the additional helpers' quote errors and 0.01
  (or 0.1) times each change of the simple forward rates between nodes over its time step. With the harness's
  initial-guess function returning 1 for every node, as the QuantLib case's does, and with 2 values more than the
  nodes after the first, extra variables which no error term reads; with the traits' global guess; and on the
  harness's traits, QuantLib's Discount without globalGuess, whose curve takes each node's guess in turn.
- An initial-guess function of the harness's own (4 curves), a + b t at each node's time t, called once per curve:
  on the log-linear discount curve (with the traits' inverse transform); with 2 extra values, which leave fewer error
  terms than variables and QuantLib refuses; on the linear zero curve (no transform) and on the linear simple-zero
  curve.
- A dates functor (3 curves): offsets of 1 day (expired at the reference date), 45 and 100 days twice, and a helper's
  pillar, merged with the pillars; without a penalty, which leaves fewer error terms than nodes and QuantLib refuses,
  and with a gradient penalty that reads no helper, on the log-linear discount and backward-flat forward curves.
- The harness's traits on the 3 deposits and 9 swaps and on the same with pillars at maturity and custom dates; the
  linear discount curve; and an explicit accuracy of 1e-8 (3 more curves).

A case's outputs are whether its curve builds, QuantLib's refusal recorded as its throw, then its nodes' dates and data,
its maximum date, each helper's and each additional helper's quote error (null for one whose pillar isn't after the
reference date, which QuantLib doesn't bind) and the guess function's calls.

Each case's inputs hold its settings, "curve" ({"traits", "interpolator", "referenceDate", "dayCounter"}), "helpers"
(helper specs of termstructures/yield/ratehelpers.py) and "bootstrap" ({"accuracy", "weights", "additionalHelpers",
"dates", "penalty", "guess"}), as tools/cxx/src/termstructures/globalbootstrap.cpp documents them. Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/GlobalBootstrapCxxVectorTests.cs."""
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
    "wheel_piecewiseyieldcurve", os.path.join(v.GEN_ROOT, "termstructures", "yield", "piecewiseyieldcurve.py"))
pw = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(pw)
rh = pw.rh

TODAY = pw.EVALUATION_DATES[0]
SPOT = ql.TARGET().advance(TODAY, 2, ql.Days)
A360, A365 = pw.A360, pw.A365


def short():
    """3 deposits and 9 swaps."""
    return pw.SHORT_DEPOSITS + [rh.swap_tenor(rate, (n, ql.Years)) for n, rate in pw.SWAP_YEARS]


# QuantLib's testGlobalBootstrapInstrumentWeights' helpers: two 6-month deposits on the same pillar.
PAIR = [rh.deposit_tenor(rate, (6, ql.Months), 2, rh.TARGET, ql.ModifiedFollowing, True, A360) for rate in (0.01, 0.02)]
EXPIRED = [rh.deposit_tenor(0.0390, (1, ql.Days), 0, rh.TARGET, ql.Following, False, A360)]


def curve(traits, interpolator, reference=SPOT, dc=A365):
    return {"traits": traits, "interpolator": interpolator, "referenceDate": v.enc(reference), "dayCounter": dc}


def bootstrap(accuracy=None, weights=(), additional=(), offsets=None, penalty=None, guess=None):
    return {"accuracy": accuracy, "weights": list(weights), "additionalHelpers": list(additional),
            "dates": None if offsets is None else {"offsets": list(offsets)}, "penalty": penalty, "guess": guess}


def pillar_offsets(helper_specs, after):
    """The helpers' pillars past the given offset, as offsets from the evaluation date."""
    with v.case_settings(TODAY):
        pillars = [rh.make_helper(h).pillarDate() - TODAY for h in helper_specs]
    return [p for p in pillars if p > after]


def rows():
    gradient = list(range(16, 16 + 9 * 45, 45))
    gradient += pillar_offsets(short(), gradient[-1])
    one = {"a": 1.0, "b": 0.0, "extra": 0}
    LL, L, BF = "LogLinear", "Linear", "BackwardFlat"
    with v.case_settings(TODAY):
        a_pillar = rh.make_helper(short()[4]).pillarDate() - TODAY
    return [
        ("weights 0.1 and 0.9", curve("Discount", LL, TODAY, A360), PAIR, bootstrap(1e-10, weights=[0.1, 0.9])),
        ("weights 0.5 and 0.5", curve("Discount", LL, TODAY, A360), PAIR, bootstrap(1e-10, weights=[0.5, 0.5])),
        ("weights 0.1 and 0.9, zero rates", curve("ZeroYield", L, TODAY, A360), PAIR,
         bootstrap(1e-10, weights=[0.1, 0.9])),
        ("weights 1 and 0, forward rates", curve("ForwardRate", BF, TODAY, A360), PAIR,
         bootstrap(1e-10, weights=[1.0, 0.0])),
        ("weights with an expired helper", curve("Discount", LL), EXPIRED + short(),
         bootstrap(weights=[3.0] + [1.0, 2.0, 0.5] * 4)),
        ("3 weights for 2 helpers", curve("Discount", LL, TODAY, A360), PAIR, bootstrap(weights=[0.1, 0.2, 0.7])),
        ("gradient penalty 0.01, guess 1", curve("Discount", LL), [],
         bootstrap(1e-12, additional=short(), offsets=gradient, penalty={"weight": 1e-2, "helpers": True}, guess=one)),
        ("gradient penalty 0.01", curve("Discount", LL), [],
         bootstrap(1e-12, additional=short(), offsets=gradient, penalty={"weight": 1e-2, "helpers": True})),
        ("gradient penalty 0.1", curve("Discount", LL), [],
         bootstrap(1e-12, additional=short(), offsets=gradient, penalty={"weight": 1e-1, "helpers": True})),
        ("gradient penalty 0.01, traits without global guess", curve("DiscountWithoutGlobalGuess", LL), [],
         bootstrap(1e-12, additional=short(), offsets=gradient, penalty={"weight": 1e-2, "helpers": True})),
        ("gradient penalty 0.01, traits without global guess, guess 1", curve("DiscountWithoutGlobalGuess", LL), [],
         bootstrap(1e-12, additional=short(), offsets=gradient, penalty={"weight": 1e-2, "helpers": True},
                   guess=one)),
        ("guess function", curve("Discount", LL), short(), bootstrap(guess={"a": 1.0, "b": -0.03, "extra": 0})),
        ("guess function with 2 extra values", curve("Discount", LL), short(),
         bootstrap(guess={"a": 1.0, "b": -0.03, "extra": 2})),
        ("gradient penalty 0.01, guess 1 with 2 extra values", curve("Discount", LL), [],
         bootstrap(1e-12, additional=short(), offsets=gradient, penalty={"weight": 1e-2, "helpers": True},
                   guess={"a": 1.0, "b": 0.0, "extra": 2})),
        ("guess function, zero rates", curve("ZeroYield", L), short(),
         bootstrap(guess={"a": 0.03, "b": 0.001, "extra": 0})),
        ("guess function, simple zero rates", curve("SimpleZeroYield", L, dc=A360), short(),
         bootstrap(guess={"a": 0.04, "b": 0.0, "extra": 0})),
        ("dates functor without a penalty", curve("Discount", LL), short(),
         bootstrap(offsets=[1, 45, 100, 100, a_pillar])),
        ("dates functor with a penalty", curve("Discount", LL), short(),
         bootstrap(offsets=[1, 45, 100, 100, a_pillar], penalty={"weight": 1e-3, "helpers": False})),
        ("dates functor with a penalty, forward rates", curve("ForwardRate", BF, dc=A360), short(),
         bootstrap(offsets=[1, 45, 100, 100, a_pillar], penalty={"weight": 1e-3, "helpers": False})),
        ("traits without global guess", curve("DiscountWithoutGlobalGuess", LL), short(), bootstrap()),
        ("traits without global guess, pillared swaps", curve("DiscountWithoutGlobalGuess", LL, dc=A360),
         pw.pillared_swaps(TODAY), bootstrap()),
        ("linear discounts, accuracy 1e-8", curve("Discount", L), short(), bootstrap(1e-8)),
    ]


KEYS = ["dates", "data", "maxDate", "quoteErrors", "additionalQuoteErrors", "guessCalls"]


def cases():
    out = []
    with cxx.connect() as H:
        for name, c, helpers, b in rows():
            with H.case_settings(v.enc(TODAY)) as st:
                try:
                    r = H.call("GlobalBootstrap::curve", c, helpers, b)
                except RuntimeError:
                    expected = {"built": dict(v.THROWS)}
                else:
                    expected = {"built": True}
                    expected.update({k: r[k] for k in KEYS})
            out.append({"name": f"{TODAY.ISO()} {name}", "inputs": {"settings": st, "curve": c, "helpers": helpers,
                                                                    "bootstrap": b},
                        "expected": expected})
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"][11:]: c["expected"] for c in out}
    assert len(out) == 22
    # QuantLib refuses a weight count that isn't the helpers', and an optimization with fewer error terms than
    # variables: nodes, or a longer guess's values
    assert [n for n, e in by_name.items() if e["built"] is not True] == [
        "3 weights for 2 helpers", "guess function with 2 extra values", "dates functor without a penalty"], by_name
    # the guess function is called once per curve; none is given elsewhere
    assert all(e["guessCalls"] == (1 if "guess 1" in n or "guess function" in n else 0)
               for n, e in by_name.items() if e["built"] is True)
    # the weights decide the over-determined curve
    assert by_name["weights 0.1 and 0.9"]["data"] != by_name["weights 0.5 and 0.5"]["data"]
    # the expired helper is weighted but not bound
    assert by_name["weights with an expired helper"]["quoteErrors"][0] is None
    # a longer guess's extra variables, which no error term reads, leave the curve within the optimizer's accuracy
    longer, plain = by_name["gradient penalty 0.01, guess 1 with 2 extra values"], by_name["gradient penalty 0.01, guess 1"]
    assert all(abs(x - y) < 1e-9 for x, y in zip(longer["data"], plain["data"])), (longer, plain)


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/globalbootstrap.hpp", cases)
