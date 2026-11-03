# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SimpleQuoteVariables (ql/termstructures/globalbootstrapvars.hpp, .cpp) and the global
bootstrap's additional variables (ql/termstructures/globalbootstrap.hpp), which the wheel doesn't expose, each output
computed by the C++ harness.

- The variables alone (8 cases): one to three variables, with fewer guesses than variables (the rest 0), with and
  without lower bounds (a null bound none), the guesses initialize(false) returns and the values after update(x).
- QuantLib's testGlobalBootstrapVariables (test-suite/piecewiseyieldcurve.cpp:1491-1548) and variations (6 cases), on
  25 September 2019, the curve a log-linear discount curve on Actual/365 (Fixed) two TARGET days after it: the
  fixture's deposits and swaps (tools/vectors/gen/termstructures/yield/piecewiseyieldcurve.py's fixture) less the first
  swap, which is the additional helper whose quote error, times the penalty weight, is the penalty; and three IMM
  futures on Euribor 3M (QuantLib's prices, 95.419, 95.427 and 95.443), each with a Hull-White convexity adjustment
  at a mean reversion of 3% on the volatility the bootstrap optimizes, guessed at 1 with a lower bound of 0. The
  variations: the guess at 0.5, no lower bound with a guess of 0.01, a mean reversion of 0, a penalty weight of 100,
  and an accuracy of 1e-10. The curve's nodes' dates and data, the volatility at the optimum, and each helper's
  quote error (the curve's helpers', then the futures') and the additional helper's.

Instrument weights are in no case: they act only where the instruments keep errors at the optimum, a set more penalised
than its unknowns, and on every such set tried a one-ulp move of an input moves the fit by 1e-12 to 6e-11 (QuantLib's
and nanoql's alike), past the stability filter's margin, so the filter would drop every output.
tests/NanoQL.Tests/Conventions/BootstrapVariantConventionTests.cs reads the weights on such a set by two routes.

A case's inputs are its settings and "kind": "variables" with "count", "initialGuesses", "lowerBounds" and "x"; or
"bootstrap" with "curve", "helpers", "additionalHelpers", "futures" ({"index", "dates", "prices", "meanReversion"}),
"variable" ({"guess", "lowerBound"}), "penaltyWeight" and "accuracy", as
tools/cxx/src/termstructures/globalbootstrapvars.cpp documents them. Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/GlobalBootstrapVarsCxxVectorTests.cs."""
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
irx = pw.irx

TODAY = ql.Date(25, ql.September, 2019)
EURIBOR3M = irx.ibor_spec("Euribor", (3, ql.Months), 2, irx.TARGET, ql.ModifiedFollowing, True, irx.A360)
IMM_PRICES = [100.0 - 4.581, 100.0 - 4.573, 100.0 - 4.557]

VARIABLES = [(1, [1.0], [0.0], [0.3]), (1, [], [], [0.02]), (1, [0.5], [], [-0.7]),
             (2, [1.0], [None, 0.0], [0.1, -2.0]), (2, [0.2, 0.3], [0.01, 0.02], [-1.5, 0.4]),
             (3, [], [None, None, -1.0], [0.1, 0.2, 0.3]),
             (3, [1.0, 2.0, 3.0], [0.0, 0.5, 1.0], [0.0, -1.0, 1.0]), (2, [0.04, 0.05], [None], [0.06, 0.07])]


def imm_dates():
    """QuantLib's test's IMM dates: from the evaluation date, each the next IMM date, jumping one forward where its
    Euribor 3M fixing is before the evaluation date."""
    index = irx.make_index(EURIBOR3M)
    out = []
    d = TODAY
    for _ in IMM_PRICES:
        d = ql.IMM.nextDate(d)
        if index.fixingDate(d) < TODAY:
            d = ql.IMM.nextDate(d)
        out.append(d.serialNumber())
    return out


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(TODAY.serialNumber())
        for count, guesses, bounds, x in VARIABLES:
            args = [count, guesses, bounds, x]
            out.append({"name": f"{count} variables, guesses {guesses}, bounds {bounds}",
                        "inputs": {"settings": st, "kind": "variables", "count": count, "initialGuesses": guesses,
                                   "lowerBounds": bounds, "x": x},
                        "expected": v.enc(H.call("SimpleQuoteVariables", *args))})
        spot = ql.TARGET().advance(TODAY, 2, ql.Days)
        curve = {"traits": "Discount", "interpolator": "LogLinear", "referenceDate": v.enc(spot),
                 "dayCounter": pw.A365}
        fixture = pw.fixture()
        deposits = len(pw.DEPOSIT_DATA)
        helpers = fixture[:deposits] + fixture[deposits + 1:]
        additional = [fixture[deposits]]
        rows = [("QuantLib's case", 1.0, 0.0, 0.03, 1e4, 1e-12), ("a guess of 0.5", 0.5, 0.0, 0.03, 1e4, 1e-12),
                ("no lower bound", 0.01, None, 0.03, 1e4, 1e-12), ("no mean reversion", 1.0, 0.0, 0.0, 1e4, 1e-12),
                ("a penalty weight of 100", 1.0, 0.0, 0.03, 100.0, 1e-12), ("an accuracy of 1e-10", 1.0, 0.0, 0.03,
                                                                             1e4, 1e-10)]
        for name, guess, bound, mr, weight, accuracy in rows:
            futures = {"index": EURIBOR3M, "dates": imm_dates(), "prices": IMM_PRICES, "meanReversion": mr}
            variable = {"guess": guess, "lowerBound": bound}
            out.append({"name": name,
                        "inputs": {"settings": st, "kind": "bootstrap", "curve": curve, "helpers": helpers,
                                   "additionalHelpers": additional, "futures": futures, "variable": variable,
                                   "penaltyWeight": weight, "accuracy": accuracy},
                        "expected": v.enc(H.call("GlobalBootstrap::variables", curve, helpers, additional, futures,
                                                 variable, weight, accuracy))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/globalbootstrapvars.hpp", cases)
