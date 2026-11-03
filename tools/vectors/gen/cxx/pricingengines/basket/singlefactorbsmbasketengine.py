# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SingleFactorBsmBasketEngine and its root solver (ql/pricingengines/basket/
singlefactorbsmbasketengine.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness
(docs/porting-playbook.md §9.4), on the evaluation date 15 October 2026: the engine's NPV and its root's d for European
average basket calls and puts on Black-Scholes-Merton processes with positive and negative weights, spots and
volatilities (QuantLib's testSingleFactorBsmBasketEngine's market and others), and with every volatility 0 (no d); the
root solver's value and derivatives at points, its roots by its four strategies (Ridder, Newton, Brent, Halley) with
their function, derivative and second-derivative call counts, and its two refusals (a·σ negative, a non-positive strike
where every a is positive).

Each case solves, the engine's root by Brent's solver and the root solver's by its strategy, so the stability filter
applies: every case is refitted with each input moved one ulp up and down, and an output that moves by more than 1e-12
relative (the counts: at all) in any refit is dropped, QuantLib's own cases covering the solve.

The harness's calls are tools/cxx/src/pricingengines/basket/singlefactorbsmbasketengine.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/Basket/SingleFactorBsmBasketEngineCxxVectorTests.cs."""
import math
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

CALL, PUT = 1, -1
QL_EPSILON = 2.220446049250313e-16


def stable(a, b):
    if isinstance(a, int):
        return a == b
    if a is None or b is None:
        return a is b
    return abs(a - b) <= 1e-12 * max(abs(a), abs(b), 1.0)


def moved(values):
    """The lists with each element moved one ulp up and one ulp down, one element at a time; a zero moves up only, a
    zero volatility moved below zero leaving the engine's domain (its a·σ refused)."""
    out = []
    for i in range(len(values)):
        for d in ((math.inf,) if values[i] == 0.0 else (math.inf, -math.inf)):
            m = list(values)
            m[i] = math.nextafter(m[i], d)
            out.append(m)
    return out


def filtered(base, refits):
    """The outputs of base stable across every refit, a refit QuantLib refuses (None) moving them all."""
    return {k: x for k, x in base.items() if all(r is not None and stable(x, r[k]) for r in refits)}


def refit(f, *args):
    """f's outputs, or None where QuantLib refuses the moved inputs (a zero volatility moved below zero)."""
    try:
        return f(*args)
    except RuntimeError:
        return None

# (name, spots, dividends, r, vols, weights, payoff)
ENGINE = [
    ("three assets, negative spot and volatility, call", [200.0, 50.0, -125.0], [0.03, 0.075, 0.04], 0.05,
     [0.4, 0.3, -0.5], [0.5, 0.25, 1.0], CALL),
    ("three assets, negative spot and volatility, put", [200.0, 50.0, -125.0], [0.03, 0.075, 0.04], 0.05,
     [0.4, 0.3, -0.5], [0.5, 0.25, 1.0], PUT),
    ("spread, put", [100.0, 50.0], [0.03, 0.075], 0.025, [0.4, -0.3], [1.0, -2.0], PUT),
    ("spread, call", [100.0, 50.0], [0.03, 0.075], 0.025, [0.4, -0.3], [1.0, -2.0], CALL),
    ("one asset", [100.0], [0.03], 0.045, [0.4], [1.0], CALL),
    ("four assets, one volatility 0", [100.0, 50.0, 100.0, 150.0], [0.03, 0.05, 0.02, 0.0], 0.045,
     [0.4, 0.0, 0.2, 0.1], [1.0, 2.0, 1.0, 1.0], CALL),
    ("every volatility 0", [100.0, 50.0], [0.03, 0.05], 0.055, [0.0, 0.0], [1.0, 1.95], CALL),
]


def cases():
    rng = random.Random(20261025)
    out = []
    with cxx.connect() as H:
        today = H.call("Date(Day,Month,Year)", 15, 10, 2026)
        st = H.settings(today)
        maturity = today + 548
        for name, spots, divs, r, vols, weights, kind in ENGINE:
            strike = sum(w * s for w, s in zip(weights, spots))
            for k, kname in ((strike, "at the money"), (strike * 1.1 if strike > 0 else strike * 0.9, "away")):
                payoff = [kind, k]

                def call(spots, divs, r, vols, weights, k):
                    return H.call("SingleFactorBsmBasketEngine::results", today, maturity, spots, divs, r, vols,
                                  weights, [kind, k], 1e4 * QL_EPSILON)

                base = call(spots, divs, r, vols, weights, k)
                refits = ([refit(call, m, divs, r, vols, weights, k) for m in moved(spots)]
                          + [refit(call, spots, m, r, vols, weights, k) for m in moved(divs)]
                          + [refit(call, spots, divs, m[0], vols, weights, k) for m in moved([r])]
                          + [refit(call, spots, divs, r, m, weights, k) for m in moved(vols)]
                          + [refit(call, spots, divs, r, vols, m, k) for m in moved(weights)]
                          + [refit(call, spots, divs, r, vols, weights, m[0]) for m in moved([k])])
                res = filtered(base, refits)
                out.append({"name": f"engine, {name}, {kname}",
                            "inputs": {"settings": st, "today": today, "maturity": maturity, "spots": spots,
                                       "dividends": divs, "r": r, "vols": vols, "weights": weights,
                                       "payoff": payoff},
                            "expected": res})
        for i in range(12):
            n = rng.randint(1, 6)
            offset = -1.0 if rng.random() < 0.3 else 0.0
            a = [rng.random() + offset for _ in range(n)]
            sig = [math.copysign(1.0, ai) * rng.random() for ai in a]
            k_min = H.call("SumExponentialsRootSolver::values", a, sig, 0.0, -10.0)["value"]
            k_max = H.call("SumExponentialsRootSolver::values", a, sig, 0.0, 10.0)["value"]
            k = (k_max - k_min) * rng.random() + k_min
            expected = dict(H.call("SumExponentialsRootSolver::values", a, sig, k, 0.3))
            for strategy, sname in ((0, "Ridder"), (1, "Newton"), (2, "Brent"), (3, "Halley")):

                def root(a, sig, k):
                    return H.call("SumExponentialsRootSolver::getRoot", a, sig, k, 1e8 * QL_EPSILON, strategy)

                base = root(a, sig, k)
                refits = ([refit(root, m, sig, k) for m in moved(a)] + [refit(root, a, m, k) for m in moved(sig)]
                          + [refit(root, a, sig, m[0]) for m in moved([k])])
                for key, x in filtered(base, refits).items():
                    expected[sname + key[0].upper() + key[1:]] = x
            out.append({"name": f"root solver, case {i}",
                        "inputs": {"settings": st, "a": a, "sig": sig, "K": k, "x": 0.3, "xTol": 1e8 * QL_EPSILON},
                        "expected": expected})
        for name, a, sig, k in (("a*sig negative (refused)", [2.0, 3.0, 4.0], [0.2, 0.4, -0.1], 0.0),
                                ("non-positive strike (refused)", [2.0, 3.0], [0.2, 0.4], 0.0)):
            res = v.attempt(lambda: H.call("SumExponentialsRootSolver::getRoot", a, sig, k, 1e6 * QL_EPSILON, 2))
            out.append({"name": f"root solver, {name}",
                        "inputs": {"settings": st, "a": a, "sig": sig, "K": k, "xTol": 1e6 * QL_EPSILON},
                        "expected": {"BrentRoot": res}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/basket/singlefactorbsmbasketengine.hpp", cases)
