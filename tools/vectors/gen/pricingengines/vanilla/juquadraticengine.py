# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's JuQuadraticApproximationEngine (ql/pricingengines/vanilla/juquadraticengine.hpp, .cpp), on
the evaluation date 15 October 2026: American calls and puts on plain vanilla payoffs, exercisable from the evaluation
date, over the Barone-Adesi-Whaley generator's grid (tools/vectors/gen/pricingengines/vanilla/
baroneadesiwhaleyengine.py): the NPV and the eleven Greeks, null where QuantLib doesn't provide one (here the value,
delta and gamma where early exercise can be optimal, the European value and every Greek where it never is). Then an
option whose window opened before the evaluation date, an expired one, and the refusals of a European exercise, a
payoff at expiry and a spot at zero.

The outputs QuantLib's master changes since the pin are left out (#2906): where QuantLib's value falls below the
European value, which the master floors at it (tests/NanoQL.Tests/TestSuite/AmericanOptionTests.cs'
testJuNotBelowEuropeanValue), judged against the analytic European engine with a margin. So are the outputs nanoql
fixes in the critical price Ju shares with Barone-Adesi-Whaley, where its Newton iteration leaves its range
(Divergences/BaroneAdesiWhaleyCriticalPriceTests.cs), and the puts' in-the-money probability, both as the
Barone-Adesi-Whaley generator judges them. Every structure is dated on the evaluation date and counts on Actual/365
(Fixed), and no window opens after it, so the mismatched-dates class (ApproximationTheta...) and the window
(ApproximationWindowTests.cs) don't reach the file.

The wheel's calls, by QuantLib-SWIG 1.44's options.i: JuQuadraticApproximationEngine(process), the C++ constructor,
VanillaOption(payoff, exercise), and the option's NPV and Greeks, OneAssetOption's accessors; the European value from
AnalyticEuropeanEngine(process). Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/JuQuadraticApproximationEngineVectorTests.cs."""
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


GRID = _load("baroneadesiwhaleyengine", "pricingengines/vanilla/baroneadesiwhaleyengine.py")
R = GRID.R

MARGIN = 1e-9


def kept(p, date, option_type, strike):
    """Whether early exercise is never optimal (the European results, which the floor doesn't reach); otherwise whether
    the critical price's Newton iteration keeps its range and QuantLib's value is above the European one by a relative
    margin, or QuantLib throws (before the floor)."""
    rfd, dd, _, _ = GRID.market(p, date, strike)
    if GRID.never_optimal(option_type, rfd, dd):
        return True
    if not GRID.kept(p, date, option_type, strike):
        return False
    payoff = ql.PlainVanillaPayoff(option_type, strike)
    american = ql.VanillaOption(payoff, ql.AmericanExercise(R, date))
    american.setPricingEngine(ql.JuQuadraticApproximationEngine(p))
    european = ql.VanillaOption(payoff, ql.EuropeanExercise(date))
    european.setPricingEngine(ql.AnalyticEuropeanEngine(p))
    try:
        value = american.NPV()
    except RuntimeError:
        return True
    return value >= european.NPV() * (1 + MARGIN)


def cases():
    engine = ql.JuQuadraticApproximationEngine
    return GRID.grid_cases(engine, kept) + GRID.edge_cases(engine, [])


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/juquadraticengine.hpp", cases)
