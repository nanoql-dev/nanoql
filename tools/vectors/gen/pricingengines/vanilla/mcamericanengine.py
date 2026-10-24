# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MCAmericanEngine (ql/pricingengines/vanilla/mcamericanengine.hpp, .cpp), Longstaff and
Schwartz's estimator, on the evaluation date 15 October 2026, on fixed seeds.

American puts at two strikes and a call on a dividend-paying underlying, exercisable from the evaluation date for a
year, and quarterly Bermudan puts on one step per date, on three Merton processes (flat curves and a constant
volatility, zero curves and a constant volatility, zero curves and a variance curve), under six engine settings
(pseudo-random numbers on the monomials, Laguerre, Hermite, hyperbolic and second-kind Chebyshev bases, of orders 2
and 3, with and without antithetic variates in the pricing and the calibration, the European control variate, to a
number of samples or a tolerance; low-discrepancy numbers on the monomials): the NPV and, for pseudo-random numbers,
the error estimate. The second-kind Chebyshev basis prices puts alone: a call's regression meets the basis's NaN above a
scaled spot of 1, where QuantLib's decomposition never ends and nanoql's refuses (tests/NanoQL.Tests/Divergences/
SvdNonFiniteRefusalTests.cs). Then the refusals of a European exercise, of a payoff at expiry and of the Legendre basis.

The outputs nanoql fixes are left out (the S-van plan's VR4): every case passes its calibration seed, since QuantLib's
default is its narrowed null where nanoql's is the documented seed plus 1768237423 (LongstaffSchwartzCalibrationSeed
Tests.cs); a Bermudan option prices on one step per date and every American exercise opens on the evaluation date,
where nanoql's pricer, exercising only at the exercise times, is QuantLib's (LongstaffSchwartzExerciseTimesTests.cs);
and every structure is on the risk-free curve's clock (McMismatchedTimesTests.cs). The exercise probability, an
additional result the wheel doesn't expose, is the harness's (tools/vectors/gen/cxx/methods/montecarlo/
longstaffschwartzpathpricer.py).

An engine spec is {"traits", "timeSteps", "timeStepsPerYear", "antitheticVariate", "controlVariate",
"requiredSamples", "requiredTolerance", "maxSamples", "seed", "polynomialOrder", "polynomialType" (LsmBasisSystem's
integer), "nCalibrationSamples", "antitheticVariateCalibration" (null for the pricing's), "seedCalibration"}, nulls
for QuantLib's Null; an exercise {"type": "American", "date"} (from the evaluation date) or {"type": "Bermudan",
"date", "dates"}, the date the last. The wheel's calls, by QuantLib-SWIG 1.44's options.i: MCAmericanEngine(process, traits, ...) picks
MCPRAmericanEngine or MCLDAmericanEngine, the C++ MCAmericanEngine on PseudoRandom or LowDiscrepancy (its calibration
traits the same) with QuantLib's Statistics, its arguments passed through in order. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/MCAmericanEngineVectorTests.cs."""
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


bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
bc = _load("blackcalculator", "pricingengines/blackcalculator.py")
R = bsp.R
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)
L = ql.LsmBasisSystem

PROCESSES = [
    ("Merton, flat, constant", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, bsp.CONSTANT,
                                                bsp.FLAT_Q)),
    ("Merton, zero curves, constant", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, bsp.CONSTANT,
                                                       bsp.ZERO_Q)),
    ("Merton, zero curves, variance curve", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R,
                                                             bsp.CURVE, bsp.ZERO_Q)),
]


def engine_spec(traits, seed, seed_calibration, order, kind, steps=None, per_year=None, anti=False, cv=False,
                samples=None, tolerance=None, max_samples=None, n_calibration=512, anti_calibration=None):
    return {"traits": traits, "timeSteps": steps, "timeStepsPerYear": per_year, "antitheticVariate": anti,
            "controlVariate": cv, "requiredSamples": samples, "requiredTolerance": tolerance,
            "maxSamples": max_samples, "seed": seed, "polynomialOrder": order, "polynomialType": int(kind),
            "nCalibrationSamples": n_calibration, "antitheticVariateCalibration": anti_calibration,
            "seedCalibration": seed_calibration}


SETTINGS = [
    ("PR, monomials", engine_spec("PseudoRandom", 42, 42 + 1768237423, 2, L.Monomial, steps=20, samples=3000)),
    ("PR, Laguerre, antithetic", engine_spec("PseudoRandom", 7, 99, 3, L.Laguerre, steps=15, anti=True, samples=2000)),
    ("PR, Hermite, control variate", engine_spec("PseudoRandom", 11, 1234, 2, L.Hermite, steps=20, cv=True,
                                                 samples=2000, anti_calibration=True)),
    ("PR, hyperbolic, tolerance", engine_spec("PseudoRandom", 13, 5, 2, L.Hyperbolic, steps=10, anti=True,
                                              tolerance=0.06, max_samples=20000, anti_calibration=False)),
    ("PR, second-kind Chebyshev", engine_spec("PseudoRandom", 17, 18, 2, L.Chebyshev2nd, per_year=12,
                                              samples=2000)),
    ("LD, monomials", engine_spec("LowDiscrepancy", 1, 2, 2, L.Monomial, steps=16, samples=1023)),
]


def engine(process, spec):
    kw = dict(timeSteps=spec["timeSteps"], timeStepsPerYear=spec["timeStepsPerYear"],
              antitheticVariate=spec["antitheticVariate"], controlVariate=spec["controlVariate"],
              requiredSamples=spec["requiredSamples"], requiredTolerance=spec["requiredTolerance"],
              maxSamples=spec["maxSamples"], seed=spec["seed"], polynomOrder=spec["polynomialOrder"],
              polynomType=spec["polynomialType"], nCalibrationSamples=spec["nCalibrationSamples"],
              seedCalibration=spec["seedCalibration"])
    if spec["antitheticVariateCalibration"] is not None:
        kw["antitheticVariateCalibration"] = spec["antitheticVariateCalibration"]
    return ql.MCAmericanEngine(process, spec["traits"], **kw)


def exercise(spec):
    if spec["type"] == "American":
        return ql.AmericanExercise(R, ql.Date(spec["date"]))
    if spec["type"] == "Bermudan":
        return ql.BermudanExercise([ql.Date(d) for d in spec["dates"]])
    return ql.EuropeanExercise(ql.Date(spec["date"]))


def priced(p, spec, payoff_spec, ex):
    option = ql.VanillaOption(bc.payoff(payoff_spec), exercise(ex))
    option.setPricingEngine(engine(p, spec))
    return v.enc(option.NPV()), v.result(lambda: option.errorEstimate())


def cases():
    out = []
    st = v.settings(R)
    american = {"type": "American", "date": (R + 365).serialNumber()}
    bermudan = {"type": "Bermudan", "date": (R + 364).serialNumber(),
                "dates": [(R + 91 * k).serialNumber() for k in range(1, 5)]}
    for pname, pspec in PROCESSES:
        p = bsp.process(pspec)
        for sname, spec in SETTINGS:
            options = [("put", PUT, [95.0, 110.0], american)]
            if spec["polynomialType"] != int(L.Chebyshev2nd):
                options.append(("call", CALL, [90.0], american))
            for name, option_type, strikes, ex in options:
                payoffs = [bc.payoff_spec("PlainVanilla", option_type, k) for k in strikes]
                results = [priced(p, spec, ps, ex) for ps in payoffs]
                out.append({"name": f"{pname}, {sname}, American {name}",
                            "inputs": {"settings": st, "process": pspec, "engine": spec, "payoffs": payoffs,
                                       "exercise": ex},
                            "expected": {"NPV": [r[0] for r in results], "errorEstimate": [r[1] for r in results]}})
            if spec["timeSteps"] is not None:
                one_per_date = dict(spec, timeSteps=4)
                payoffs = [bc.payoff_spec("PlainVanilla", PUT, 105.0)]
                results = [priced(p, one_per_date, ps, bermudan) for ps in payoffs]
                out.append({"name": f"{pname}, {sname}, Bermudan put",
                            "inputs": {"settings": st, "process": pspec, "engine": one_per_date, "payoffs": payoffs,
                                       "exercise": bermudan},
                            "expected": {"NPV": [r[0] for r in results], "errorEstimate": [r[1] for r in results]}})
    pspec = PROCESSES[0][1]
    p = bsp.process(pspec)
    put = bc.payoff_spec("PlainVanilla", PUT, 100.0)
    for name, spec, ex in [("European exercise", SETTINGS[0][1], {"type": "European", "date": (R + 365).serialNumber()}),
                           ("Legendre basis", dict(SETTINGS[0][1], polynomialType=int(L.Legendre)), american)]:
        option = ql.VanillaOption(bc.payoff(put), exercise(ex))
        option.setPricingEngine(engine(p, spec))
        out.append({"name": name,
                    "inputs": {"settings": st, "process": pspec, "engine": spec, "payoffs": [put], "exercise": ex},
                    "expected": {"NPV": [v.attempt(lambda: option.NPV())]}})
    at_expiry = {"type": "American", "date": (R + 365).serialNumber(), "payoffAtExpiry": True}
    option = ql.VanillaOption(bc.payoff(put), ql.AmericanExercise(R, R + 365, True))
    option.setPricingEngine(engine(p, SETTINGS[0][1]))
    out.append({"name": "payoff at expiry",
                "inputs": {"settings": st, "process": pspec, "engine": SETTINGS[0][1], "payoffs": [put],
                           "exercise": at_expiry},
                "expected": {"NPV": [v.attempt(lambda: option.NPV())]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/mcamericanengine.hpp", cases)
