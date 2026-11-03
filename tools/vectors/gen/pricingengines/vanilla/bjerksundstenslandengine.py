# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BjerksundStenslandApproximationEngine (ql/pricingengines/vanilla/
bjerksundstenslandengine.hpp, .cpp), on the evaluation date 15 October 2026: American calls and puts on plain vanilla
payoffs, exercisable from the evaluation date, over the Barone-Adesi-Whaley generator's grid
(tools/vectors/gen/pricingengines/vanilla/baroneadesiwhaleyengine.py): the NPV and the eight Greeks QuantLib sets,
GREEKS below. Its deltaForward, elasticity and itmCashProbability are left out: QuantLib assigns the engine's results
from a local OneAssetOption::results it never resets, so those three are uninitialized memory, where nanoql provides
none (tests/NanoQL.Tests/Divergences/BjerksundStenslandUnsetGreeksTests.cs). Then an option whose window opened
before the evaluation date, an expired one, and the refusals of a European exercise, a payoff at expiry, a spot at
zero, a cash-or-nothing payoff and a call in the double-boundary case r < q < 0.

The outputs nanoql fixes are left out, judged in the call frame QuantLib's put-call symmetry gives, by kept() below:
- where the 1993 trigger I falls below B0 or isn't finite, which is where bT + 2√v < 0
  (tests/NanoQL.Tests/Divergences/BjerksundStenslandTriggerTests.cs), with a margin;
- where a power (I/S)^κ of the phi family overflows or its normal tail, or erfc's, underflows, or a density's exponent
  overflows (BjerksundStenslandOverflowTests.cs), with a margin on each exponent;
- below a variance of ε, where QuantLib's master floors the European value at intrinsic (#2854); the grid holds none.
  Every structure is dated on the evaluation date and counts on Actual/365 (Fixed), and no window opens after it, so
  the mismatched-dates class (BjerksundStenslandMismatchedTimesTests.cs, ApproximationTheta...) and the window
  (ApproximationWindowTests.cs) don't reach the file.

The wheel's calls, by QuantLib-SWIG 1.44's options.i: BjerksundStenslandApproximationEngine(process), the C++
constructor, VanillaOption(payoff, exercise), and the option's NPV and Greeks, OneAssetOption's accessors. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/BjerksundStenslandApproximationEngineVectorTests.cs."""
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


GRID = _load("baroneadesiwhaleyengine", "pricingengines/vanilla/baroneadesiwhaleyengine.py")
bc = GRID.bc
R, CALL, PUT, EPS = GRID.R, GRID.CALL, GRID.PUT, GRID.EPS

GREEKS = ["delta", "gamma", "theta", "thetaPerDay", "vega", "rho", "dividendRho", "strikeSensitivity"]

# The margins: the trigger's edge, and the exponents short of a double's range (709.78) and the tails short of their
# underflow (N to 0 below -38.4, erfc above 27.2).
EDGE = 1e-9
EXPONENT = 705.0
NORMAL_TAIL = -38.0
ERFC_TAIL = 26.8


def guard_fires(s, x, i, beta, rt, bt, v_):
    """Whether a guard of the phi family can fire, with the margins: the power (I/S)^κ or (I/S)^k overflowing, the
    normal tail or erfc's underflowing, or the density's exponent y²/8v overflowing, over QuantLib's gammas and
    boundaries."""
    sv = math.sqrt(v_)
    lis = math.log(i / s)
    for gamma in (beta, 1.0, 0.0):
        for h in (i, x):
            lsh = math.log(s / h)
            d = -(lsh + (bt + (gamma - 0.5) * v_)) / sv
            kappa = 2.0 * bt / v_ + (2.0 * gamma - 1.0)
            k = 2 * (gamma + bt / v_)
            y = 2 * bt - v_ + 2 * gamma * v_ + 4 * lis + 2 * lsh
            if (max(kappa * lis, k * lis) > EXPONENT or d - 2.0 * lis / sv < NORMAL_TAIL
                    or y / (2.0 * math.sqrt(2.0) * sv) > ERFC_TAIL or y * y / (8.0 * v_) > EXPONENT):
                return True
    return False


def kept(p, date, option_type, strike):
    rfd, dd, variance, spot = GRID.market(p, date, strike)
    s, x = spot, strike
    if option_type == PUT:
        s, x, rfd, dd = x, s, dd, rfd
    if dd > 1.0 and rfd > dd:
        return True
    if variance < EPS * 10:
        return False
    if dd >= 1.0 and dd >= rfd:
        return True
    bt = math.log(dd / rfd)
    rt = math.log(1.0 / rfd)
    if bt + 2.0 * math.sqrt(variance) < EDGE:
        return False
    beta = (0.5 - bt / variance) + math.sqrt(max(0.0, (bt / variance - 0.5) ** 2 + 2.0 * rt / variance))
    if abs(beta - 1.0) < 1e-6:
        return False
    b_inf = beta / (beta - 1.0) * x
    b0 = x if bt == rt else max(x, rt / (rt - bt) * x)
    ht = -(bt + 2.0 * math.sqrt(variance)) * b0 / (b_inf - b0)
    i = b0 + (b_inf - b0) * (1 - math.exp(ht))
    if s >= i * (1 - EDGE):
        return s >= i * (1 + EDGE)
    return not guard_fires(s, x, i, beta, rt, bt, variance)


def cases():
    engine = ql.BjerksundStenslandApproximationEngine
    one_year = (R + 365).serialNumber()
    refusals = [("cash-or-nothing payoff", GRID.flat_process(0.05, 0.02, 0.25),
                 bc.payoff_spec("CashOrNothing", PUT, 105.0),
                 {"type": "American", "date": one_year}),
                ("double-boundary call", GRID.flat_process(-0.02, -0.01, 0.2),
                 bc.payoff_spec("PlainVanilla", CALL, 105.0), {"type": "American", "date": one_year})]
    return GRID.grid_cases(engine, kept, GREEKS) + GRID.edge_cases(engine, refusals, GREEKS)


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/bjerksundstenslandengine.hpp", cases)
