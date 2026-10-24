# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticHestonEngine (ql/pricingengines/vanilla/analytichestonengine.hpp, .cpp), on the
evaluation date 15 October 2026: European options under the Heston model by Fourier transform.

Four models (the S-eq plan's SP4 sets, and one with a volatility of variance of 1e-9, the characteristic function's
expansion branch) on flat curves: chF and lnChF at three maturities on SP4's contours, the real line and the shifted
lines z = u - 0.5i and u - 1.75i and -u + 0.25i, u from 0.0025 to 4 quadratically; the NPV and numberOfEvaluations of
puts and calls at five strikes and three maturities under each complex-log formula and control variate (Gauss-Laguerre
at QuantLib's default order 144 and others, Gauss-Legendre and both Gauss-Chebyshev rules for Gatheral's form, and the
adaptive integrations C2's review sent here: Gauss-Lobatto at a relative tolerance, which QuantLib's engine runs with
the optimal control variate over Andersen and Piterbarg's integration limit, and Gauss-Lobatto, Simpson and the
trapezoid rule for Gatheral's form); OptimalAlpha's searches above 0 and below -1, its choice and the count it reports
after it, its bounds, M and k, each on a fresh object, with the count it reports after that one call (C2's review:
Boost's brent_find_minima), on the first three models and on a fifth, a volatility of variance of 5 at 10 years, where
the search above 0 finds no minimum and the choice is QuantLib's fallback, -0.5. Then the engine's refusal of the branch
correction with an adaptive integration ("Branch correction does not work in conjunction with adaptive integration
methods").

**The stability filter** (the S7 plan's S7R6) runs on every NPV, count and OptimalAlpha output: each is computed again
with the model's v0, kappa, theta, sigma and rho moved one ulp, together up and down and each alone, and written only
where every move leaves it within 1e-12 x max(|a|, |b|, 1) (a count, where every move gives the same); the generator
prints "dropped n of m outputs". QuantLib's own values move there: OptimalAlpha's searches and choice by up to 9e-7
(brent_find_minima at 26 bits), where their counts don't move, the asymptotic control variate on the fast-reversion model, outside its domain, by up to 1e-6, and an
adaptive integration's count on the small-sigma model over 7,490 to 9,110. The characteristic function and M and k
compute no search and aren't filtered. A price case's NPVs are its "strikes"' and its counts its "countStrikes"'.

A case holds one model {"riskFree", "dividend", "s0", "v0", "kappa", "theta", "sigma", "rho"}, the curves
tools/vectors/gen/processes/blackscholesprocess.py's specs, and an engine {"cpxLog" (QuantLib's integer, or null for the
order or tolerance constructor), "integration", "andersenPiterbargEpsilon"} with an integration {"type": "gaussLaguerre"
| "gaussLegendre" | "gaussChebyshev" | "gaussChebyshev2nd", "order"} | {"type": "gaussLobatto", "relTolerance",
"absTolerance", "maxEvaluations"} | {"type": "simpson" | "trapezoid" | "gaussKronrod", "absTolerance", "maxEvaluations"}
| {"type": "relTolerance", "relTolerance", "maxEvaluations"} (the engine's Gauss-Lobatto constructor). The wheel's
calls, by QuantLib-SWIG 1.44's options.i and fourier.i: HestonModel(HestonProcess(...)), the engine's order, tolerance
and formula constructors (andersenPiterbargEpsilon passed as C++'s default, 1e-25, where given), chF, lnChF,
numberOfEvaluations, and AnalyticHestonEngine_OptimalAlpha(t, engine). Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/AnalyticHestonEngineVectorTests.cs."""
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
R = bsp.R
RF, Q = bsp.flat_spec(R, 0.03), bsp.flat_spec(R, 0.01)
MODELS = [  # v0, kappa, theta, sigma, rho
    ("SP4 equity", 0.04, 1.5, 0.04, 0.3, -0.7),
    ("SP4 high correlation", 0.09, 0.5, 0.06, 0.8, -0.9),
    ("SP4 fast reversion", 0.0225, 3.0, 0.03, 0.15, 0.2),
    ("small sigma", 0.03, 1.25, 0.01, 1e-9, -0.9),
]
ALPHA_MODELS = [("high volatility of variance", 0.04, 0.1, 0.04, 5.0, 0.9)]
CHF_TIMES = [0.1, 1.0, 5.0]
CONTOUR = [0.05 * k * k / 20.0 for k in range(1, 41)]
MATURITIES = [91, 365, 1825]
STRIKES = [60.0, 90.0, 100.0, 110.0, 150.0]
G = int(ql.AnalyticHestonEngine.Gatheral)
FORMULAS = [("Gatheral", ql.AnalyticHestonEngine.Gatheral), ("BranchCorrection", ql.AnalyticHestonEngine.BranchCorrection),
            ("AndersenPiterbarg", ql.AnalyticHestonEngine.AndersenPiterbarg),
            ("AndersenPiterbargOptCV", ql.AnalyticHestonEngine.AndersenPiterbargOptCV),
            ("AsymptoticChF", ql.AnalyticHestonEngine.AsymptoticChF),
            ("AngledContour", ql.AnalyticHestonEngine.AngledContour),
            ("AngledContourNoCV", ql.AnalyticHestonEngine.AngledContourNoCV),
            ("OptimalCV", ql.AnalyticHestonEngine.OptimalCV)]


def quad(kind, order):
    return {"type": kind, "order": order}


ENGINES = [("default (Laguerre 144)", {"cpxLog": None, "integration": quad("gaussLaguerre", 144),
                                       "andersenPiterbargEpsilon": None}),
           ("Laguerre 64", {"cpxLog": None, "integration": quad("gaussLaguerre", 64), "andersenPiterbargEpsilon": None}),
           ("Lobatto at 1e-8", {"cpxLog": None, "integration": {"type": "relTolerance", "relTolerance": 1e-8,
                                                                 "maxEvaluations": 10000},
                                "andersenPiterbargEpsilon": None})]
ENGINES += [(f"{name}, Laguerre 128", {"cpxLog": int(f), "integration": quad("gaussLaguerre", 128),
                                       "andersenPiterbargEpsilon": 1e-25}) for name, f in FORMULAS]
ENGINES += [(f"Gatheral, {k} 128", {"cpxLog": G, "integration": quad(k, 128), "andersenPiterbargEpsilon": 1e-25})
            for k in ("gaussLegendre", "gaussChebyshev", "gaussChebyshev2nd")]
ENGINES += [("Gatheral, Lobatto", {"cpxLog": G, "integration": {"type": "gaussLobatto", "relTolerance": None,
                                                                 "absTolerance": 1e-9, "maxEvaluations": 10000},
                                   "andersenPiterbargEpsilon": 1e-25}),
            ("Gatheral, Simpson", {"cpxLog": G, "integration": {"type": "simpson", "absTolerance": 1e-8,
                                                                 "maxEvaluations": 1000},
                                   "andersenPiterbargEpsilon": 1e-25}),
            ("Gatheral, trapezoid", {"cpxLog": G, "integration": {"type": "trapezoid", "absTolerance": 1e-8,
                                                                   "maxEvaluations": 1000},
                                     "andersenPiterbargEpsilon": 1e-25}),
            ("Gatheral, Gauss-Kronrod", {"cpxLog": G, "integration": {"type": "gaussKronrod", "absTolerance": 1e-8,
                                                                       "maxEvaluations": 1000},
                                         "andersenPiterbargEpsilon": 1e-25})]


def integration(spec):
    t = spec["type"]
    if t in ("gaussLaguerre", "gaussLegendre", "gaussChebyshev", "gaussChebyshev2nd"):
        return getattr(ql.FourierIntegration, t)(spec["order"])
    if t == "gaussLobatto":
        rel = ql.nullDouble() if spec["relTolerance"] is None else spec["relTolerance"]
        return ql.FourierIntegration.gaussLobatto(rel, spec["absTolerance"], spec["maxEvaluations"])
    return getattr(ql.FourierIntegration, t)(spec["absTolerance"], spec["maxEvaluations"])


def model(spec):
    process = ql.HestonProcess(ql.YieldTermStructureHandle(bsp.curve(spec["riskFree"])),
                               ql.YieldTermStructureHandle(bsp.curve(spec["dividend"])),
                               ql.QuoteHandle(ql.SimpleQuote(spec["s0"])), spec["v0"], spec["kappa"], spec["theta"],
                               spec["sigma"], spec["rho"])
    return ql.HestonModel(process)


def engine(m, spec):
    i = spec["integration"]
    if spec["cpxLog"] is None:
        if i["type"] == "relTolerance":
            return ql.AnalyticHestonEngine(m, i["relTolerance"], i["maxEvaluations"])
        return ql.AnalyticHestonEngine(m, i["order"])
    return ql.AnalyticHestonEngine(m, spec["cpxLog"], integration(i), spec["andersenPiterbargEpsilon"])


PARAMETERS = ["v0", "kappa", "theta", "sigma", "rho"]


def moved_specs(mspec):
    """The model's parameters moved one ulp, together up and down and each alone (the S7 plan's S7R6)."""
    out = []
    for d in (math.inf, -math.inf):
        out.append({**mspec, **{p: math.nextafter(mspec[p], d) for p in PARAMETERS}})
        for p in PARAMETERS:
            out.append({**mspec, p: math.nextafter(mspec[p], d)})
    return out


def within(a, b):
    if isinstance(a, dict) or isinstance(b, dict):
        return a == b
    if isinstance(a, list):
        return isinstance(b, list) and all(within(x, y) for x, y in zip(a, b))
    if isinstance(a, int) and not isinstance(a, bool):
        return a == b
    return abs(a - b) <= 1e-12 * max(abs(a), abs(b), 1.0)


def stable(f, mspec, base):
    """Whether every move leaves f's value within the filter's measure."""
    return all(within(base, f(m)) for m in moved_specs(mspec))


class Tally:
    dropped = total = 0


def priced(mspec, espec, option_type, days, strike):
    eng = engine(model(mspec), espec)
    option = ql.VanillaOption(ql.PlainVanillaPayoff(option_type, strike), ql.EuropeanExercise(R + days))
    option.setPricingEngine(eng)
    npv = v.attempt(option.NPV)
    return npv, (dict(v.THROWS) if isinstance(npv, dict) else eng.numberOfEvaluations())


def alpha_cases(st, mname, mspec, times, tally):
    """OptimalAlpha's cases for a model at each time: its searches, its choice and its count, its bounds at three
    strikes, and M and k."""
    out = []
    for t in times:
        engines = []

        def alpha_of(s):
            # OptimalAlpha keeps a raw pointer to its engine, which must outlive it
            engines.append(ql.AnalyticHestonEngine(model(s)))
            return ql.AnalyticHestonEngine_OptimalAlpha(t, engines[-1])

        def searched(s, member, strike):
            a = alpha_of(s)
            try:
                r = getattr(a, member)(strike)
            except RuntimeError:
                return dict(v.THROWS)
            return [r[0], r[1], a.numberOfEvaluations()]

        def chosen(s, strike):
            return alpha_of(s)(strike)

        def chosen_count(s, strike):
            a = alpha_of(s)
            a(strike)
            return a.numberOfEvaluations()

        for strike in (80.0, 100.0, 150.0):
            outputs = {
                "alphaGreaterZero": lambda s: searched(s, "alphaGreaterZero", strike),
                "alphaSmallerMinusOne": lambda s: searched(s, "alphaSmallerMinusOne", strike),
                "alpha": lambda s: chosen(s, strike),
                "alphaEvaluations": lambda s: chosen_count(s, strike),
                "alphaMax": lambda s: v.attempt(lambda: alpha_of(s).alphaMax(strike)),
                "alphaMin": lambda s: v.attempt(lambda: alpha_of(s).alphaMin(strike)),
            }
            expected = {}
            for key, f in outputs.items():
                base = f(mspec)
                tally.total += 1
                if stable(f, mspec, base):
                    expected[key] = base
                else:
                    tally.dropped += 1
            if expected:
                out.append({"name": f"{mname}, OptimalAlpha at {t}, strike {strike}",
                            "inputs": {"settings": st, "model": mspec, "t": t, "strike": strike},
                            "expected": expected})
        ks = [-3.0, -1.5, 0.5, 2.0, 4.0]
        a = alpha_of(mspec)
        out.append({"name": f"{mname}, OptimalAlpha's M and k at {t}",
                    "inputs": {"settings": st, "model": mspec, "t": t, "k": ks},
                    "expected": {
                        "M": [v.attempt(lambda: a.M(k)) for k in ks],
                        "kPlus": [a.k(x, 1) for x in (0.0, 3.0, 6.283185307179586)],
                        "kMinus": [a.k(x, -1) for x in (0.0, 3.0, 6.283185307179586)]}})
    return out


def cases():
    st = v.settings(R)
    out = []
    tally = Tally()
    for mname, v0, kappa, theta, sigma, rho in MODELS:
        mspec = {"riskFree": RF, "dividend": Q, "s0": 100.0, "v0": v0, "kappa": kappa, "theta": theta, "sigma": sigma,
                 "rho": rho}
        m = model(mspec)
        e = ql.AnalyticHestonEngine(m)
        zs = [[u, 0.0] for u in CONTOUR] + [[u, -0.5] for u in CONTOUR] + [[u, -1.75] for u in CONTOUR] \
            + [[-u, 0.25] for u in CONTOUR]
        for t in CHF_TIMES:
            def pair(c):
                return [c.real, c.imag]
            out.append({"name": f"{mname}, chF at {t}",
                        "inputs": {"settings": st, "model": mspec, "t": t, "z": zs},
                        "expected": {"chF": [v.attempt(lambda: pair(e.chF(complex(*z), t))) for z in zs],
                                     "lnChF": [v.attempt(lambda: pair(e.lnChF(complex(*z), t))) for z in zs]}})
        for ename, espec in ENGINES:
            if mname == "small sigma" and espec["cpxLog"] not in (None, G):
                continue
            try:
                eng = engine(m, espec)
            except RuntimeError:
                continue
            for days in MATURITIES:
                for option_type in (int(ql.Option.Call), int(ql.Option.Put)):
                    kept = {"NPV": [], "numberOfEvaluations": []}
                    for strike in STRIKES:
                        npv, count = priced(mspec, espec, option_type, days, strike)
                        for key, base, i in (("NPV", npv, 0), ("numberOfEvaluations", count, 1)):
                            tally.total += 1
                            if stable(lambda s: priced(s, espec, option_type, days, strike)[i], mspec, base):
                                kept[key].append((strike, base))
                            else:
                                tally.dropped += 1
                    if not kept["NPV"] and not kept["numberOfEvaluations"]:
                        continue
                    out.append({"name": f"{mname}, {ename}, {days} days, {'call' if option_type == 1 else 'put'}",
                                "inputs": {"settings": st, "model": mspec, "engine": espec, "type": option_type,
                                           "strikes": [k for k, _ in kept["NPV"]],
                                           "countStrikes": [k for k, _ in kept["numberOfEvaluations"]],
                                           "maturity": (R + days).serialNumber()},
                                "expected": {"NPV": [x for _, x in kept["NPV"]],
                                             "numberOfEvaluations": [x for _, x in kept["numberOfEvaluations"]]}})
        if mname == "small sigma":
            continue
        out += alpha_cases(st, mname, mspec, (0.5, 2.0), tally)
    # a volatility of variance of 5 at 10 years: the search above 0 finds no minimum, so the choice is QuantLib's -0.5
    for mname, v0, kappa, theta, sigma, rho in ALPHA_MODELS:
        mspec = {"riskFree": RF, "dividend": Q, "s0": 100.0, "v0": v0, "kappa": kappa, "theta": theta, "sigma": sigma,
                 "rho": rho}
        out += alpha_cases(st, mname, mspec, (10.0,), tally)
    print(f"dropped {tally.dropped} of {tally.total} outputs", file=sys.stderr)
    mspec = {"riskFree": RF, "dividend": Q, "s0": 100.0, "v0": 0.04, "kappa": 1.5, "theta": 0.04, "sigma": 0.3,
             "rho": -0.7}
    espec = {"cpxLog": int(ql.AnalyticHestonEngine.BranchCorrection),
             "integration": {"type": "gaussLobatto", "relTolerance": None, "absTolerance": 1e-9,
                             "maxEvaluations": 1000}, "andersenPiterbargEpsilon": 1e-25}
    out.append({"name": "branch correction with an adaptive integration",
                "inputs": {"settings": st, "model": mspec, "engine": espec, "type": 1, "strikes": [100.0],
                           "countStrikes": [100.0], "maturity": (R + 365).serialNumber()},
                "expected": {"NPV": [v.attempt(lambda: engine(model(mspec), espec))],
                             "numberOfEvaluations": [dict(v.THROWS)]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/analytichestonengine.hpp", cases)
