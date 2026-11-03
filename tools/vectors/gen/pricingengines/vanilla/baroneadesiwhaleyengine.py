# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BaroneAdesiWhaleyApproximationEngine (ql/pricingengines/vanilla/baroneadesiwhaleyengine.hpp,
.cpp), on the evaluation date 15 October 2026: American calls and puts on plain vanilla payoffs, exercisable from the
evaluation date.

The grid, GRID below, which the Bjerksund-Stensland and Ju generators share: calls and puts at strikes 80 to 120 on a
spot of 100, exercisable for 30 days to three years, on Merton processes on flat curves and constant volatilities, from
negative to high rates and dividend yields and volatilities of 1% to 60%, on a Merton process on zero curves and a
variance curve, and on a Black-Scholes process with no dividends. Each case records the NPV and the eleven Greeks of
OneAssetOption, null where QuantLib doesn't provide one (here the value alone where early exercise can be optimal, the
European value and every Greek where it never is). Then an option whose window opened before the evaluation date, an
expired one, and the refusals of a European exercise, a payoff at expiry, a spot at zero and a negative rate.

The outputs nanoql fixes are left out: where QuantLib's Newton iteration for the critical price leaves (0, K) for a put
or (K, ∞) for a call, judged by newton_leaves() below, QuantLib's own iteration with a margin
(tests/NanoQL.Tests/Divergences/BaroneAdesiWhaleyCriticalPriceTests.cs); and the puts' in-the-money probability, which
QuantLib's BlackCalculator gives as the call's (BlackCalculatorItmProbabilityTests.cs). Every structure is dated on the
evaluation date and counts on Actual/365 (Fixed), and no window opens after it, so the mismatched-dates class and the
window (ApproximationTheta..., ApproximationWindowTests.cs) don't reach the file.

The process specs are tools/vectors/gen/processes/blackscholesprocess.py's and the payoff specs
tools/vectors/gen/pricingengines/blackcalculator.py's; an exercise {"type": "American", "date", "earliestDate"?,
"payoffAtExpiry"?}. The wheel's calls, by QuantLib-SWIG 1.44's options.i: BaroneAdesiWhaleyApproximationEngine(process),
the C++ constructor, VanillaOption(payoff, exercise), and the option's NPV and Greeks, OneAssetOption's accessors. Read
back by tests/NanoQL.Tests/Vectors/PricingEngines/BaroneAdesiWhaleyApproximationEngineVectorTests.cs."""
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


bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
bc = _load("blackcalculator", "pricingengines/blackcalculator.py")
R = bsp.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
EPS = 2.220446049250313e-16

GREEKS = ["delta", "deltaForward", "elasticity", "gamma", "theta", "thetaPerDay", "vega", "rho", "dividendRho",
          "strikeSensitivity", "itmCashProbability"]
MARKETS = [(0.05, 0.02, 0.25), (0.08, 0.12, 0.2), (0.03, 0.0, 0.4), (0.10, 0.10, 0.15), (0.0, 0.03, 0.3),
           (0.06, 0.0, 0.05), (0.02, 0.04, 0.6), (0.04, -0.01, 0.1), (0.07, 0.03, 0.01), (-0.01, -0.02, 0.2)]
DAYS = (30, 182, 365, 1095)
STRIKES = (80.0, 90.0, 100.0, 110.0, 120.0)


def flat_process(r, q, vol, x0=100.0):
    return bsp.process_spec("BlackScholesMertonProcess", x0, bsp.flat_spec(R, r), bsp.bvts.vol_spec_constant(R, vol),
                            bsp.flat_spec(R, q))


def processes():
    out = [(f"flat r {r} q {q} vol {vol}", flat_process(r, q, vol)) for r, q, vol in MARKETS]
    out.append(("zero curves, variance curve",
                bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, bsp.CURVE, bsp.ZERO_Q)))
    out.append(("Black-Scholes, flat r 0.05, vol 0.3",
                bsp.process_spec("BlackScholesProcess", 100.0, bsp.flat_spec(R, 0.05),
                                 bsp.bvts.vol_spec_constant(R, 0.3))))
    return out


def market(p, date, strike):
    """The discounts and the variance the engines read at the exercise date."""
    return (p.riskFreeRate().discount(date), p.dividendYield().discount(date),
            p.blackVolatility().blackVariance(date, strike), p.x0())


def american(spec):
    return ql.AmericanExercise(ql.Date(spec["earliestDate"]) if "earliestDate" in spec else R, ql.Date(spec["date"]),
                               spec.get("payoffAtExpiry", False))


def put_greeks(greeks):
    """The Greeks recorded for a put: all but the in-the-money probability."""
    return [g for g in greeks if g != "itmCashProbability"]


def outputs(option, greeks):
    """The NPV and the Greeks; each Greek a throw where the NPV throws, null where QuantLib doesn't provide it."""
    npv = v.attempt(lambda: option.NPV())
    out = {"NPV": npv}
    for g in greeks:
        out[g] = dict(v.THROWS) if isinstance(npv, dict) else v.result(lambda: getattr(option, g)())
    return out


def grid_cases(engine, kept, greeks=GREEKS):
    """The grid's cases, one per process, exercise length and option type over the strikes kept(process, date,
    option type, strike) keeps: the NPV and the Greeks named."""
    out = []
    st = v.settings(R)
    for pname, pspec in processes():
        p = bsp.process(pspec)
        for days in DAYS:
            date = R + days
            for option_type in (CALL, PUT):
                strikes = [k for k in STRIKES if kept(p, date, option_type, k)]
                if not strikes:
                    continue
                payoffs = [bc.payoff_spec("PlainVanilla", option_type, k) for k in strikes]
                ex = {"type": "American", "date": date.serialNumber()}
                named = greeks if option_type == CALL else put_greeks(greeks)
                results = {g: [] for g in ["NPV"] + named}
                for ps in payoffs:
                    option = ql.VanillaOption(bc.payoff(ps), american(ex))
                    option.setPricingEngine(engine(p))
                    for g, value in outputs(option, named).items():
                        results[g].append(value)
                out.append({"name": f"{pname}, {days} days, {'call' if option_type == CALL else 'put'}",
                            "inputs": {"settings": st, "process": pspec, "payoffs": payoffs, "exercise": ex},
                            "expected": results})
    return out


def edge_cases(engine, refusals, greeks=GREEKS):
    """A window opened before the evaluation date, an expired option, and the refusals: (name, process spec, payoff
    spec, exercise spec) each, priced alone."""
    st = v.settings(R)
    pspec = flat_process(0.05, 0.02, 0.25)
    put = bc.payoff_spec("PlainVanilla", PUT, 105.0)
    one_year = (R + 365).serialNumber()
    specs = [("window opened before the evaluation date", pspec, put,
              {"type": "American", "date": one_year, "earliestDate": (R - 30).serialNumber()}),
             ("expired", pspec, put, {"type": "American", "date": (R - 1).serialNumber(),
                                      "earliestDate": (R - 60).serialNumber()}),
             ("European exercise", pspec, put, {"type": "European", "date": one_year}),
             ("payoff at expiry", pspec, put, {"type": "American", "date": one_year, "payoffAtExpiry": True}),
             ("spot at zero", dict(pspec, x0=0.0), put, {"type": "American", "date": one_year})] + refusals
    out = []
    for name, spec, payoff, ex in specs:
        qex = ql.EuropeanExercise(ql.Date(ex["date"])) if ex["type"] == "European" else american(ex)
        option = ql.VanillaOption(bc.payoff(payoff), qex)
        option.setPricingEngine(engine(bsp.process(spec)))
        out.append({"name": name, "inputs": {"settings": st, "process": spec, "payoffs": [payoff], "exercise": ex},
                    "expected": {g: [x] for g, x in outputs(
                        option, greeks if payoff["optionType"] == CALL else put_greeks(greeks)).items()}})
    return out


def close(x, y, n):
    """QuantLib's close(x, y, n)."""
    if x == y:
        return True
    diff = abs(x - y)
    tolerance = n * EPS
    if x == 0.0 or y == 0.0:
        return diff < tolerance * tolerance
    return diff <= tolerance * abs(x) and diff <= tolerance * abs(y)


def never_optimal(option_type, rfd, dd):
    return ((dd >= 1.0 and dd >= rfd and option_type == CALL)
            or (rfd >= 1.0 and rfd >= dd and option_type == PUT))


def newton_leaves(option_type, strike, rfd, dd, variance, tolerance=1e-6, margin=1e-9):
    """Whether QuantLib's Newton iteration for the critical price leaves its range, or comes within a margin of the
    range's end, or doesn't converge in 1,000 steps: baroneadesiwhaleyengine.cpp:52-140 step for step."""
    n = 2.0 * math.log(dd / rfd) / variance
    m = -2.0 * math.log(rfd) / variance
    bt = math.log(dd / rfd)
    sv = math.sqrt(variance)
    if option_type == CALL:
        qu = (-(n - 1.0) + math.sqrt((n - 1.0) * (n - 1.0) + 4.0 * m)) / 2.0
        su = strike / (1.0 - 1.0 / qu)
        h = -(bt + 2.0 * sv) * strike / (su - strike)
        si = strike + (su - strike) * (1.0 - math.exp(h))
        if not si > 0.0:
            return True
    else:
        qu = (-(n - 1.0) - math.sqrt((n - 1.0) * (n - 1.0) + 4.0 * m)) / 2.0
        su = strike / (1.0 - 1.0 / qu)
        h = (bt - 2.0 * sv) * strike / (strike - su)
        try:
            si = su + (strike - su) * math.exp(h)
        except OverflowError:
            si = math.inf
        if not (si > 0.0) or not math.isfinite(si):
            si = su
    cnd = ql.CumulativeNormalDistribution()
    k = -2.0 * math.log(rfd) / (variance * (1.0 - rfd)) if not close(rfd, 1.0, 1000) else 2.0 / variance
    ql_type = ql.Option.Call if option_type == CALL else ql.Option.Put
    phi = 1.0 if option_type == CALL else -1.0
    q = (-(n - 1.0) + phi * math.sqrt((n - 1.0) * (n - 1.0) + 4 * k)) / 2
    for _ in range(1000):
        forward = si * dd / rfd
        d1 = (math.log(forward / strike) + 0.5 * variance) / sv
        temp = ql.blackFormula(ql_type, strike, forward, sv) * rfd
        if option_type == CALL:
            lhs = si - strike
            rhs = temp + (1 - dd * cnd(d1)) * si / q
            bi = dd * cnd(d1) * (1 - 1 / q) + (1 - dd * cnd.derivative(d1) / sv) / q
        else:
            lhs = strike - si
            rhs = temp - (1 - dd * cnd(-d1)) * si / q
            bi = -dd * cnd(-d1) * (1 - 1 / q) - (1 + dd * cnd.derivative(-d1) / sv) / q
        if not (abs(lhs - rhs) / strike > tolerance):
            return False
        si = (strike + rhs - bi * si) / (1 - bi) if option_type == CALL else (strike - rhs + bi * si) / (1 + bi)
        if option_type == CALL and not (si > strike * (1 + margin)):
            return True
        if option_type == PUT and not (strike * margin < si < strike * (1 - margin)):
            return True
    return True


def kept(p, date, option_type, strike):
    rfd, dd, variance, _ = market(p, date, strike)
    if never_optimal(option_type, rfd, dd) or rfd > 1.0:
        return True
    return not newton_leaves(option_type, strike, rfd, dd, variance)


def cases():
    engine = ql.BaroneAdesiWhaleyApproximationEngine
    refusals = [("negative rate", flat_process(-0.01, -0.02, 0.2), bc.payoff_spec("PlainVanilla", PUT, 105.0),
                 {"type": "American", "date": (R + 365).serialNumber()})]
    return grid_cases(engine, kept) + edge_cases(engine, refusals)


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/baroneadesiwhaleyengine.hpp", cases)
