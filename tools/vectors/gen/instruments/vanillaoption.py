# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's VanillaOption, OneAssetOption and EuropeanOption (ql/instruments/vanillaoption.hpp, .cpp,
oneassetoption.hpp, .cpp, europeanoption.hpp, .cpp) and its implied volatility (ql/instruments/impliedvolatility.hpp,
.cpp, through VanillaOption), on the evaluation date 15 October 2026.

- Expiry: European options expiring before, on and after the evaluation date, with the reference date's events
  excluded and included: isExpired, and the NPV and Greeks of an expired one on the analytic engine (all zero).
- Implied volatilities of European options: the analytic engine's value at a volatility (a plain vanilla call and
  put at three strikes, a cash-or-nothing call, a gap put; with dividends through the analytic dividend engine, and a
  BlackScholesProcess) solved back with QuantLib's C++ defaults (accuracy 1e-4, 100 evaluations, the bracket [1e-7,
  4]) and tighter accuracies, by Brent's method from the middle of the bracket. Then the refusals: an expired option
  ("option expired"), a target no volatility reaches, too few evaluations, and an empty bracket.
- Implied volatilities of American and Bermudan options: the finite-difference engine's value on its default grid
  (100 x 100, the Douglas scheme), which QuantLib's impliedVolatility builds for these exercises, solved back at
  accuracy 1e-8: puts and calls at two strikes, with and without dividends, and a Bermudan put of quarterly dates.

The stability filter of the S-eq plan's S7R6 runs on each solve: the solve is redone with the target price, the spot
and the two curves' rates moved one ulp up, then down, together and each alone, and the implied volatility is written
only where every move leaves it within 1e-12 x max(|a|, |b|, 1), or throwing where it throws; the generator prints
"dropped n of m outputs" per solve and for the file.

The process specs are tools/vectors/gen/processes/blackscholesprocess.py's, a payoff spec
tools/vectors/gen/pricingengines/blackcalculator.py's, a dividend tools/vectors/gen/pricingengines/vanilla/
analyticdividendeuropeanengine.py's. The wheel's calls, by QuantLib-SWIG 1.44's options.i: VanillaOption(payoff,
exercise) and EuropeanOption(payoff, exercise), the C++ constructors; isExpired; impliedVolatility(price, process,
accuracy, maxEvaluations, minVol, maxVol) and its dividend form, whose minVol default is 1e-4 in the shim where C++'s
is 1e-7, so the generator passes every argument. Read back by
tests/NanoQL.Tests/Vectors/Instruments/VanillaOptionVectorTests.cs."""
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
ade = _load("analyticdividendeuropeanengine", "pricingengines/vanilla/analyticdividendeuropeanengine.py")
R = bsp.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
GREEKS = ["delta", "deltaForward", "elasticity", "gamma", "theta", "thetaPerDay", "vega", "rho", "dividendRho",
          "strikeSensitivity", "itmCashProbability"]
STABLE = 1e-12


def stable(base, moved):
    if isinstance(base, dict) or isinstance(moved, dict):
        return base == moved
    return abs(base - moved) <= STABLE * max(abs(base), abs(moved), 1.0)


def market(x0, r, q, vol, kind):
    """A process spec on flat curves; kind is BlackScholesMertonProcess or BlackScholesProcess."""
    if kind == "BlackScholesProcess":
        return bsp.process_spec(kind, x0, bsp.flat_spec(R, r), bsp.bvts.vol_spec_constant(R, vol))
    return bsp.process_spec(kind, x0, bsp.flat_spec(R, r), bsp.bvts.vol_spec_constant(R, vol), bsp.flat_spec(R, q))


def exercise_spec(kind, days):
    if kind == "Bermudan":
        return {"type": kind, "date": (R + days).serialNumber(),
                "dates": [(R + d).serialNumber() for d in range(days % 91 or 91, days + 1, 91)]}
    return {"type": kind, "date": (R + days).serialNumber()}


def exercise(spec):
    if spec["type"] == "European":
        return ql.EuropeanExercise(ql.Date(spec["date"]))
    if spec["type"] == "American":
        return ql.AmericanExercise(R, ql.Date(spec["date"]))
    return ql.BermudanExercise([ql.Date(d) for d in spec["dates"]])


def option(payoff_spec, days, european=True, kind="European"):
    ex = exercise(exercise_spec(kind, days))
    return (ql.EuropeanOption if european else ql.VanillaOption)(bc.payoff(payoff_spec), ex)


def npv(payoff_spec, days, pspec, dividends, kind="European"):
    o = option(payoff_spec, days, kind == "European", kind)
    p = bsp.process(pspec)
    if kind != "European":
        # the engine QuantLib's impliedVolatility builds for an early exercise, on its default grid
        o.setPricingEngine(ql.FdBlackScholesVanillaEngine(p, [ade.dividend(d) for d in dividends], 100, 100)
                           if dividends else ql.FdBlackScholesVanillaEngine(p, 100, 100))
    elif dividends:
        o.setPricingEngine(ql.AnalyticDividendEuropeanEngine(p, [ade.dividend(d) for d in dividends]))
    else:
        o.setPricingEngine(ql.AnalyticEuropeanEngine(p))
    return o.NPV()


def solve(payoff_spec, days, pspec, dividends, target, args, kind="European"):
    o = option(payoff_spec, days, european=False, kind=kind)
    p = bsp.process(pspec)
    a = (args["accuracy"], args["maxEvaluations"], args["minVol"], args["maxVol"])
    if dividends:
        return v.attempt(lambda: o.impliedVolatility(target, p, [ade.dividend(d) for d in dividends], *a))
    return v.attempt(lambda: o.impliedVolatility(target, p, *a))


def moved(x, direction):
    return math.nextafter(x, direction)


def cases():
    out = []
    # expiry
    pspec = market(100.0, 0.04, 0.02, 0.2, "BlackScholesMertonProcess")
    for include in (False, True):
        st = v.settings(R, include_reference_date_events=include)
        for days in (-1, 0, 1):
            payoff_spec = bc.payoff_spec("PlainVanilla", CALL, 100.0)
            o = option(payoff_spec, days)
            o.setPricingEngine(ql.AnalyticEuropeanEngine(bsp.process(pspec)))
            e = {"isExpired": o.isExpired()}
            if o.isExpired():
                e.update({"NPV": o.NPV(), **{g: getattr(o, g)() for g in GREEKS}})
            out.append({"name": f"expiry at {days}, reference date's events {'in' if include else 'out'}",
                        "inputs": {"settings": st, "payoff": payoff_spec,
                                   "exercise": {"type": "European", "date": (R + days).serialNumber()},
                                   "process": pspec},
                        "expected": e})
    # implied volatilities
    st = v.settings(R)
    defaults = {"accuracy": 1.0e-4, "maxEvaluations": 100, "minVol": 1.0e-7, "maxVol": 4.0}
    solves = []
    for strike in (80.0, 100.0, 125.0):
        for option_type in (CALL, PUT):
            for vol in (0.08, 0.3, 1.1):
                solves.append((f"plain {option_type} K {strike} vol {vol}", bc.payoff_spec("PlainVanilla", option_type,
                                                                                           strike),
                               365, 100.0, 0.04, 0.02, vol, "BlackScholesMertonProcess", [], dict(defaults), None))
    for accuracy in (1e-8, 1e-12):
        solves.append((f"plain call accuracy {accuracy}", bc.payoff_spec("PlainVanilla", CALL, 105.0), 730, 100.0,
                       0.03, 0.01, 0.25, "BlackScholesMertonProcess", [], dict(defaults, accuracy=accuracy), None))
    solves.append(("cash-or-nothing call", bc.payoff_spec("CashOrNothing", CALL, 110.0), 182, 100.0, 0.04, 0.0, 0.3,
                   "BlackScholesMertonProcess", [], dict(defaults, accuracy=1e-8), None))
    solves.append(("gap put", bc.payoff_spec("Gap", PUT, 95.0), 365, 100.0, 0.04, 0.02, 0.25,
                   "BlackScholesMertonProcess", [], dict(defaults, accuracy=1e-8), None))
    solves.append(("Black-Scholes process", bc.payoff_spec("PlainVanilla", PUT, 100.0), 365, 100.0, 0.05, 0.0, 0.2,
                   "BlackScholesProcess", [], dict(defaults, accuracy=1e-8), None))
    divs = [ade.fixed(1.5, 91), ade.fixed(1.5, 273)]
    for strike in (90.0, 110.0):
        solves.append((f"with dividends K {strike}", bc.payoff_spec("PlainVanilla", CALL, strike), 365, 100.0, 0.04,
                       0.01, 0.22, "BlackScholesMertonProcess", divs, dict(defaults, accuracy=1e-8), None))
    solves.append(("a target no volatility reaches", bc.payoff_spec("PlainVanilla", CALL, 100.0), 365, 100.0, 0.04,
                   0.02, 0.2, "BlackScholesMertonProcess", [], dict(defaults), 150.0))
    solves.append(("too few evaluations", bc.payoff_spec("PlainVanilla", CALL, 100.0), 365, 100.0, 0.04, 0.02, 0.2,
                   "BlackScholesMertonProcess", [], dict(defaults, accuracy=1e-12, maxEvaluations=3), None))
    solves.append(("an empty bracket", bc.payoff_spec("PlainVanilla", CALL, 100.0), 365, 100.0, 0.04, 0.02, 0.2,
                   "BlackScholesMertonProcess", [], dict(defaults, minVol=2.0, maxVol=1.0), None))
    solves.append(("an expired option", bc.payoff_spec("PlainVanilla", CALL, 100.0), -1, 100.0, 0.04, 0.02, 0.2,
                   "BlackScholesMertonProcess", [], dict(defaults), 5.0))
    solves = [s + ("European",) for s in solves]
    for ex_kind, days in (("American", 365), ("Bermudan", 364)):
        for option_type in (CALL, PUT):
            for strike in (90.0, 110.0):
                for dname, dividends in (("", []), (" with dividends", divs)):
                    solves.append((f"{ex_kind} {option_type} K {strike}{dname}",
                                   bc.payoff_spec("PlainVanilla", option_type, strike), days, 100.0, 0.04, 0.03, 0.25,
                                   "BlackScholesMertonProcess", dividends, dict(defaults, accuracy=1e-8), None,
                                   ex_kind))
    dropped_total, outputs_total = 0, 0
    for name, payoff_spec, days, x0, r, q, vol, kind, dividends, args, fixed_target, ex_kind in solves:
        pspec = market(x0, r, q, vol, kind)
        target = fixed_target if fixed_target is not None else npv(payoff_spec, days, pspec, dividends, ex_kind)
        base = solve(payoff_spec, days, pspec, dividends, target, args, ex_kind)
        keep = True
        inputs = [target, x0, r, q]
        moves = [[moved(x, d) for x in inputs] for d in (math.inf, -math.inf)]
        for i in range(len(inputs)):
            for d in (math.inf, -math.inf):
                m = list(inputs)
                m[i] = moved(inputs[i], d)
                moves.append(m)
        for m_target, m_x0, m_r, m_q in moves:
            m = solve(payoff_spec, days, market(m_x0, m_r, m_q, vol, kind), dividends, m_target, args, ex_kind)
            if not stable(base, m):
                keep = False
                break
        dropped = 0 if keep else 1
        dropped_total += dropped
        outputs_total += 1
        print(f"{name}: dropped {dropped} of 1 outputs")
        e = {"targetValue": target}
        if keep:
            e["impliedVolatility"] = base
        out.append({"name": f"implied volatility, {name}",
                    "inputs": {"settings": st, "payoff": payoff_spec, "exercise": exercise_spec(ex_kind, days),
                               "process": pspec, "dividends": dividends, "targetFrom": fixed_target is None,
                               **args},
                    "expected": e})
    print(f"vanillaoption: dropped {dropped_total} of {outputs_total} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/vanillaoption.hpp", cases)
