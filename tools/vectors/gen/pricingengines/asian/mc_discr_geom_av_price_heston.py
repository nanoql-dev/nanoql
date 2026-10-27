# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCDiscreteGeometricAPHestonEngine
(ql/pricingengines/asian/mc_discr_geom_av_price_heston.hpp, .cpp), on the evaluation date 15 October 2026: discrete
geometric average-price Asian options by Monte Carlo on a Heston process.

Geometric calls and puts at 90% and 110% of the spot, over six months and a year, with 4 and 12 fixings, unseasoned and
with two past fixings, on the three parameter sets of tools/vectors/gen/processes/hestonprocess.py on flat curves (the
quadratic-exponential martingale scheme), 511 pseudo-random samples at seeds 42 and 1234, on the fixing times alone, 24
steps and 20 steps a year: the NPV and the error estimate. Then the refusals of a payoff that isn't plain vanilla, an
exercise that isn't European and every fixing in the past, and an expired option. The outputs nanoql fixes are left
out (the S-bar plan's §2.10): fixing dates given twice (BR5 (xxviii)) and a geometric product out of range (BR5 (xli)).

The engine's settings go in as "engine". The wheel's calls, by QuantLib-SWIG 1.44's options.i:
MCPRDiscreteGeometricAPHestonEngine(process, antitheticVariate, requiredSamples, requiredTolerance, maxSamples, seed,
timeSteps, timeStepsPerYear) and the option's NPV and errorEstimate. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/Asian/MCDiscreteGeometricAPHestonEngineVectorTests.cs."""
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


def make(p, antitheticVariate, seed, timeSteps, timeStepsPerYear):
    return ql.MCPRDiscreteGeometricAPHestonEngine(p, antitheticVariate, SAMPLES, None, None, seed, timeSteps,
                                                  timeStepsPerYear)

hp = _load("hestonprocess", "processes/hestonprocess.py")
QE = int(ql.HestonProcess.QuadraticExponentialMartingale)


def cases():
    st = v.settings(R)
    out = []
    for pname, s0, v0, kappa, theta, sigma, rho in hp.PARAMETERS:
        pspec = {"riskFree": bsp.FLAT_R, "dividend": bsp.FLAT_Q, "s0": s0, "v0": v0, "kappa": kappa, "theta": theta,
                 "sigma": sigma, "rho": rho, "discretization": QE}
        p = hp.process(pspec)
        for days in (182, 365):
            ex = abe.exercise_spec("European", days)
            for n in (4, 12):
                for sname, running, past in (("unseasoned", None, 0), ("two past fixings", 100.0 * 100.0, 2)):
                    for tname, t in (("call", CALL), ("put", PUT)):
                        for k in (0.9, 1.1):
                            for settings in ({"antitheticVariate": False, "seed": 42, "timeSteps": None, "timeStepsPerYear": None},
                                             {"antitheticVariate": True, "seed": 1234, "timeSteps": 24, "timeStepsPerYear": None},
                                             {"antitheticVariate": False, "seed": 42, "timeSteps": None, "timeStepsPerYear": 20}):
                                spec = ao.option_spec(GEOMETRIC, ao.dates(days, n), bc.payoff_spec("PlainVanilla", t, k * s0), ex,
                                                      running=running if running is None else running * (s0 / 100.0) ** 2,
                                                      past=past)
                                engine = make(p, **settings)
                                out.append({"name": f"{pname}, {days} days, {n} fixings, {sname}, a {tname} at {k:g} x spot, {settings}",
                                            "inputs": {"settings": st, "process": pspec, "option": spec, "engine": settings},
                                            "expected": mc_outputs(ao.option(spec), engine)})
    pname, s0, v0, kappa, theta, sigma, rho = hp.PARAMETERS[0]
    pspec = {"riskFree": bsp.FLAT_R, "dividend": bsp.FLAT_Q, "s0": s0, "v0": v0, "kappa": kappa, "theta": theta,
             "sigma": sigma, "rho": rho, "discretization": QE}
    p = hp.process(pspec)
    pay = bc.payoff_spec("PlainVanilla", CALL, 100.0)
    ex = abe.exercise_spec("European", 365)
    settings = {"antitheticVariate": False, "seed": 42, "timeSteps": None, "timeStepsPerYear": None}
    for name, spec in (("a cash-or-nothing payoff",
                        ao.option_spec(GEOMETRIC, ao.dates(365, 12), bc.payoff_spec("CashOrNothing", CALL, 100.0), ex)),
                       ("an American exercise",
                        ao.option_spec(GEOMETRIC, ao.dates(365, 12), pay, abe.exercise_spec("American", 365))),
                       ("every fixing in the past",
                        ao.option_spec(GEOMETRIC, [(R - 20).serialNumber(), (R - 10).serialNumber()], pay, ex,
                                       running=100.0 * 100.0, past=2)),
                       ("expired", ao.option_spec(GEOMETRIC, ao.dates(-1, 1), pay, abe.exercise_spec("European", -1)))):
        out.append({"name": name, "inputs": {"settings": st, "process": pspec, "option": spec, "engine": settings},
                    "expected": mc_outputs(ao.option(spec), make(p, **settings))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/asian/mc_discr_geom_av_price_heston.hpp", cases)
