# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCDiscreteArithmeticASEngine (ql/pricingengines/asian/mc_discr_arith_av_strike.hpp,
.cpp), on the evaluation date 15 October 2026: discrete arithmetic average-strike Asian options by Monte Carlo, the
exercise a time of the grid where it follows the last fixing.

Arithmetic average-strike calls and puts over six months and a year, with 4 fixings (the exercise 30 days after the
last) and 12 (on the last), unseasoned and with two past fixings, on the ten processes of
tools/vectors/gen/processes/blackscholesprocess.py, 511 pseudo-random samples at seeds 42 and 1234, with and without the
Brownian bridge and antithetic variates: the NPV and the error estimate. Then the refusals of a payoff that isn't plain
vanilla, an exercise that isn't European, every fixing in the past and one fixing left today, and an expired option.
Fixing dates given twice are left out (the S-bar plan's §2.10: BR5 (xxviii)).

The engine's settings go in as "engine". The wheel's calls, by QuantLib-SWIG 1.44's options.i:
MCPRDiscreteArithmeticASEngine(process, brownianBridge, antitheticVariate, requiredSamples, requiredTolerance,
maxSamples, seed) and the option's NPV and errorEstimate; then
MCLDDiscreteArithmeticASEngine, the same arguments, on low-discrepancy draws ("traits": "ld"), its NPV
alone. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Asian/MCDiscreteArithmeticASEngineVectorTests.cs."""
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
SAMPLES = 511
SEEDS = [42, 1234]


def mc_outputs(opt, engine):
    opt.setPricingEngine(engine)
    return {"NPV": v.attempt(opt.NPV), "errorEstimate": v.attempt(opt.errorEstimate)}


def strike_settings(pname):
    return [{"brownianBridge": True, "antitheticVariate": False, "seed": 42},
            {"brownianBridge": False, "antitheticVariate": True, "seed": 1234}]


def make(p, brownianBridge, antitheticVariate, seed):
    return ql.MCPRDiscreteArithmeticASEngine(p, brownianBridge, antitheticVariate, SAMPLES, None, None, seed)


def make_ld(p, brownianBridge, antitheticVariate, seed):
    return ql.MCLDDiscreteArithmeticASEngine(p, brownianBridge, antitheticVariate, SAMPLES, None, None, seed)


def ld_outputs(opt, engine):
    """The NPV alone: the low-discrepancy traits give no error estimate."""
    opt.setPricingEngine(engine)
    return {"NPV": v.attempt(opt.NPV)}


def cases():
    st = v.settings(R)
    out = []
    for pname, pspec in bsp.PROCESSES:
        p = bsp.process(pspec)
        x0 = pspec["x0"]
        for days in (182, 365):
            ex = abe.exercise_spec("European", days)
            for n in (4, 12):
                for sname, running, past in (("unseasoned", None, 0), ("two past fixings at 95 and 105", 2.0 * x0, 2)):
                    for tname, t in (("call", CALL), ("put", PUT)):
                        for k in (1.0,):
                            for settings in strike_settings(pname):
                                spec = ao.option_spec(ARITHMETIC, ao.dates(days, n), bc.payoff_spec("PlainVanilla", t, k * x0), abe.exercise_spec("European", days + 30) if n == 4 else ex,
                                                      running=running, past=past)
                                engine = make(p, **settings)
                                out.append({"name": f"{pname}, {days} days, {n} fixings, {sname}, a {tname} at {k:g} x spot, {settings}",
                                            "inputs": {"settings": st, "process": pspec, "option": spec, "engine": settings},
                                            "expected": mc_outputs(ao.option(spec), engine)})
    pspec = ao.MERTON
    p = bsp.process(pspec)
    pay = bc.payoff_spec("PlainVanilla", CALL, 100.0)
    ex = abe.exercise_spec("European", 365)
    settings = strike_settings("Merton, flat, constant")[0]
    for name, spec in (("a cash-or-nothing payoff",
                        ao.option_spec(ARITHMETIC, ao.dates(365, 12), bc.payoff_spec("CashOrNothing", CALL, 100.0), ex)),
                       ("an American exercise",
                        ao.option_spec(ARITHMETIC, ao.dates(365, 12), pay, abe.exercise_spec("American", 365))),
                       ("every fixing in the past",
                        ao.option_spec(ARITHMETIC, [(R - 20).serialNumber(), (R - 10).serialNumber()], pay, ex,
                                       running=200.0, past=2)),
                       ("one fixing left, today",
                        ao.option_spec(ARITHMETIC, [R.serialNumber()], pay, ex, running=200.0, past=2)),
                       ("expired", ao.option_spec(ARITHMETIC, ao.dates(-1, 1), pay, abe.exercise_spec("European", -1)))):
        out.append({"name": name, "inputs": {"settings": st, "process": pspec, "option": spec, "engine": settings},
                    "expected": mc_outputs(ao.option(spec), make(p, **settings))})
    # MCLDDiscreteArithmeticASEngine: low-discrepancy draws on the first two processes, the NPV alone
    for pname, pspec in bsp.PROCESSES[:2]:
        p = bsp.process(pspec)
        x0 = pspec["x0"]
        ex = abe.exercise_spec("European", 365)
        for sname, running, past in (("unseasoned", None, 0), ("two past fixings at 95 and 105", 2.0 * x0, 2)):
            for tname, t in (("call", CALL), ("put", PUT)):
                settings = {"traits": "ld", "brownianBridge": True, "antitheticVariate": False, "seed": 42}
                spec = ao.option_spec(ARITHMETIC, ao.dates(365, 12), bc.payoff_spec("PlainVanilla", t, x0), ex,
                                      running=running, past=past)
                engine = make_ld(p, **{k: s for k, s in settings.items() if k != "traits"})
                out.append({"name": f"low-discrepancy, {pname}, 365 days, 12 fixings, {sname}, a {tname} at the spot",
                            "inputs": {"settings": st, "process": pspec, "option": spec, "engine": settings},
                            "expected": ld_outputs(ao.option(spec), engine)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/asian/mc_discr_arith_av_strike.hpp", cases)
