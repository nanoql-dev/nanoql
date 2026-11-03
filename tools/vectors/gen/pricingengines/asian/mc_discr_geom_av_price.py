# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCDiscreteGeometricAPEngine (ql/pricingengines/asian/mc_discr_geom_av_price.hpp,
.cpp), on the evaluation date 15 October 2026: discrete geometric average-price Asian options by Monte Carlo.

Geometric calls and puts at 90%, 100% and 110% of the spot, over a year, with 4 and 12 fixings, unseasoned and with two
past fixings, on the ten processes of tools/vectors/gen/processes/blackscholesprocess.py, 511 pseudo-random samples at
seeds 42 and 1234, with and without the Brownian bridge and antithetic variates: the NPV and the error estimate. Then
the refusals of a payoff that isn't plain vanilla, an exercise that isn't European, every fixing in the past and one
fixing left today, and an expired option. The outputs nanoql fixes are left out: fixing dates given twice and a
geometric product out of range.

The engine's settings go in as "engine". The wheel's calls, by QuantLib-SWIG 1.44's options.i:
MCPRDiscreteGeometricAPEngine(process, brownianBridge, antitheticVariate, requiredSamples, requiredTolerance,
maxSamples, seed) and the option's NPV and errorEstimate; then
MCLDDiscreteGeometricAPEngine, the same arguments, on low-discrepancy draws ("traits": "ld"), its NPV
alone. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Asian/MCDiscreteGeometricAPEngineVectorTests.cs."""
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


def geometric_settings(pname):
    return [{"brownianBridge": True, "antitheticVariate": False, "seed": 42},
            {"brownianBridge": False, "antitheticVariate": True, "seed": 1234}]


def make(p, brownianBridge, antitheticVariate, seed):
    return ql.MCPRDiscreteGeometricAPEngine(p, brownianBridge, antitheticVariate, SAMPLES, None, None, seed)


def make_ld(p, brownianBridge, antitheticVariate, seed):
    return ql.MCLDDiscreteGeometricAPEngine(p, brownianBridge, antitheticVariate, SAMPLES, None, None, seed)


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
        for days in (365,):
            ex = abe.exercise_spec("European", days)
            for n in (4, 12):
                for sname, running, past in (("unseasoned", None, 0), ("two past fixings at 95 and 105", x0 * x0, 2)):
                    for tname, t in (("call", CALL), ("put", PUT)):
                        for k in (0.9, 1.0, 1.1):
                            for settings in geometric_settings(pname):
                                spec = ao.option_spec(GEOMETRIC, ao.dates(days, n), bc.payoff_spec("PlainVanilla", t, k * x0), ex,
                                                      running=running, past=past)
                                engine = make(p, **settings)
                                out.append({"name": f"{pname}, {days} days, {n} fixings, {sname}, a {tname} at {k:g} x spot, {settings}",
                                            "inputs": {"settings": st, "process": pspec, "option": spec, "engine": settings},
                                            "expected": mc_outputs(ao.option(spec), engine)})
    pspec = ao.MERTON
    p = bsp.process(pspec)
    pay = bc.payoff_spec("PlainVanilla", CALL, 100.0)
    ex = abe.exercise_spec("European", 365)
    settings = geometric_settings("Merton, flat, constant")[0]
    for name, spec in (("a cash-or-nothing payoff",
                        ao.option_spec(GEOMETRIC, ao.dates(365, 12), bc.payoff_spec("CashOrNothing", CALL, 100.0), ex)),
                       ("an American exercise",
                        ao.option_spec(GEOMETRIC, ao.dates(365, 12), pay, abe.exercise_spec("American", 365))),
                       ("every fixing in the past",
                        ao.option_spec(GEOMETRIC, [(R - 20).serialNumber(), (R - 10).serialNumber()], pay, ex,
                                       running=100.0 * 100.0, past=2)),
                       ("one fixing left, today",
                        ao.option_spec(GEOMETRIC, [R.serialNumber()], pay, ex, running=100.0 * 100.0, past=2)),
                       ("expired", ao.option_spec(GEOMETRIC, ao.dates(-1, 1), pay, abe.exercise_spec("European", -1)))):
        out.append({"name": name, "inputs": {"settings": st, "process": pspec, "option": spec, "engine": settings},
                    "expected": mc_outputs(ao.option(spec), make(p, **settings))})
    # MCLDDiscreteGeometricAPEngine: low-discrepancy draws on the first two processes, the NPV alone
    for pname, pspec in bsp.PROCESSES[:2]:
        p = bsp.process(pspec)
        x0 = pspec["x0"]
        ex = abe.exercise_spec("European", 365)
        for sname, running, past in (("unseasoned", None, 0), ("two past fixings at 95 and 105", x0 * x0, 2)):
            for tname, t in (("call", CALL), ("put", PUT)):
                settings = {"traits": "ld", "brownianBridge": True, "antitheticVariate": False, "seed": 42}
                spec = ao.option_spec(GEOMETRIC, ao.dates(365, 12), bc.payoff_spec("PlainVanilla", t, x0), ex,
                                      running=running, past=past)
                engine = make_ld(p, **{k: s for k, s in settings.items() if k != "traits"})
                out.append({"name": f"low-discrepancy, {pname}, 365 days, 12 fixings, {sname}, a {tname} at the spot",
                            "inputs": {"settings": st, "process": pspec, "option": spec, "engine": settings},
                            "expected": ld_outputs(ao.option(spec), engine)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/asian/mc_discr_geom_av_price.hpp", cases)
