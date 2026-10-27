# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticDiscreteGeometricAveragePriceAsianEngine
(ql/pricingengines/asian/analytic_discr_geom_av_price.hpp, .cpp), on the evaluation date 15 October 2026: discrete
geometric average-price Asian options in closed form.

Geometric calls and puts at 90, 100 and 110, over six months, a year and two years, with 4 and 12 fixings, unseasoned
and with two past fixings, on the flat, constant-volatility processes of tools/vectors/gen/instruments/asianoption.py:
the NPV and the Greeks the engine sets. Then an arithmetic option priced as the arithmetic engine's control, a payoff
that isn't plain vanilla, an exercise that isn't European, and an expired option. Off flat markets the outputs are
left out (the S-bar plan's §2.10: BR4 (a)'s exact form).

A case holds one process and option. The wheel's calls, by QuantLib-SWIG 1.44's options.i:
AnalyticDiscreteGeometricAveragePriceAsianEngine(process) and the option's NPV, delta, gamma, theta, vega, rho and
dividendRho. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Asian/AnalyticDiscreteGeometricAveragePriceAsianEngineVectorTests.cs."""
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


ao = _load("asianoption", "instruments/asianoption.py")
abe, bsp, bc = ao.abe, ao.bsp, ao.bc
R, CALL, PUT, ARITHMETIC, GEOMETRIC = ao.R, ao.CALL, ao.PUT, ao.ARITHMETIC, ao.GEOMETRIC
GREEKS = ["NPV", "delta", "gamma", "theta", "vega", "rho", "dividendRho"]


def outputs(opt, engine, names=GREEKS):
    opt.setPricingEngine(engine)
    return {n: v.attempt(getattr(opt, n)) for n in names}


def cases():
    st = v.settings(R)
    out = []
    for pname, pspec in ao.FLAT:
        engine = ql.AnalyticDiscreteGeometricAveragePriceAsianEngine(bsp.process(pspec))
        for days in abe.EXPIRIES:
            ex = abe.exercise_spec("European", days)
            for n in (4, 12):
                for sname, running, past in (("unseasoned", None, 0), ("two past fixings at 95 and 105", 95.0 * 105.0, 2)):
                    for tname, t in (("call", CALL), ("put", PUT)):
                        for k in (90.0, 100.0, 110.0):
                            spec = ao.option_spec(GEOMETRIC, ao.dates(days, n), bc.payoff_spec("PlainVanilla", t, k), ex,
                                                  running=running, past=past)
                            out.append({"name": f"{pname}, {days} days, {n} fixings, {sname}, a {tname} at {k:g}",
                                        "inputs": {"settings": st, "process": pspec, "option": spec},
                                        "expected": outputs(ao.option(spec), engine)})
    pspec = ao.MERTON
    engine = ql.AnalyticDiscreteGeometricAveragePriceAsianEngine(bsp.process(pspec))
    for name, spec in (
            ("an arithmetic option, priced as the control",
             ao.option_spec(ARITHMETIC, ao.dates(365, 12), bc.payoff_spec("PlainVanilla", CALL, 100.0),
                            abe.exercise_spec("European", 365), running=200.0, past=2)),
            ("a cash-or-nothing payoff",
             ao.option_spec(GEOMETRIC, ao.dates(365, 12), bc.payoff_spec("CashOrNothing", CALL, 100.0),
                            abe.exercise_spec("European", 365))),
            ("an American exercise",
             ao.option_spec(GEOMETRIC, ao.dates(365, 12), bc.payoff_spec("PlainVanilla", CALL, 100.0),
                            abe.exercise_spec("American", 365))),
            ("expired",
             ao.option_spec(GEOMETRIC, ao.dates(-1, 1), bc.payoff_spec("PlainVanilla", CALL, 100.0),
                            abe.exercise_spec("European", -1)))):
        out.append({"name": name, "inputs": {"settings": st, "process": pspec, "option": spec},
                    "expected": outputs(ao.option(spec), engine, ["NPV"])})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/asian/analytic_discr_geom_av_price.hpp", cases)
