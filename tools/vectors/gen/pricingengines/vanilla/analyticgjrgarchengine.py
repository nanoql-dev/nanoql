# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticGJRGARCHEngine (ql/pricingengines/vanilla/analyticgjrgarchengine.hpp, .cpp), on the
evaluation date 15 October 2026: Duan's approximation of the GJR-GARCH(1,1) price by its first four moments.

QuantLib's gjrgarchmodel test's model (omega 2e-6, alpha 0.024, beta 0.93, gamma 0.059 at 365 days a year, the spot 50)
at its three risk premia lambda 0, 0.1 and 0.2, each from its stationary variance, and
tools/vectors/gen/processes/gjrgarchprocess.py's second set, on a flat and a linear zero risk-free curve with no
dividend yield: the NPVs of calls and puts at six strikes (0.7 to 1.2 of the spot, the test's 35 to 60) and five
maturities (a day, the test's 90 and 180 days, a year and two), one engine per model.

Left out: every structure is on the risk-free curve's day counter and reference date; a dividend yield, where nanoql
prices on r − q (tests/NanoQL.Tests/Divergences/AnalyticGjrGarchDividendTests.cs); an option at expiry, where
QuantLib's 0/0 is NaN (tests/NanoQL.Tests/Divergences/GjrGarchExpiryTests.cs); and non-plain payoffs, which nanoql
refuses (tests/NanoQL.Tests/Divergences/AnalyticGjrGarchPayoffRefusalTests.cs).

A case holds one process, gjrgarchprocess.py's spec, its maturities in days and its strikes. The wheel's calls, by
QuantLib-SWIG 1.44's vanilla.i: AnalyticGJRGARCHEngine(GJRGARCHModel(GJRGARCHProcess(...))) on a VanillaOption, NPV
the option's accessor. Read back by tests/NanoQL.Tests/Vectors/PricingEngines/AnalyticGJRGARCHEngineVectorTests.cs."""
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
gp = _load("gjrgarchprocess", "processes/gjrgarchprocess.py")
R = bsp.R
FULL_TRUNCATION = int(ql.GJRGARCHProcess.FullTruncation)
NO_DIVIDEND = bsp.flat_spec(R, 0.0)
MATURITIES = [1, 90, 180, 365, 730]
STRIKES = [0.7, 0.8, 0.9, 1.0, 1.1, 1.2]


def parameter_sets():
    """The test's model at its three risk premia, each from its stationary variance, and the process file's second."""
    omega, alpha, beta, gamma = 2.0e-6, 0.024, 0.93, 0.059
    sets = [(f"gjrgarchmodel's model, lambda {lam}", 50.0, gp.stationary_variance(omega, alpha, beta, gamma, lam),
             omega, alpha, beta, gamma, lam, 365.0) for lam in (0.0, 0.1, 0.2)]
    return sets + [gp.PARAMETERS[1]]


def cases():
    st = v.settings(R)
    out = []
    for params in parameter_sets():
        for cname, rf in (("flat", bsp.FLAT_R), ("zero", bsp.ZERO_R)):
            spec = gp.process_spec(params, rf, NO_DIVIDEND, FULL_TRUNCATION)
            engine = ql.AnalyticGJRGARCHEngine(ql.GJRGARCHModel(gp.process(spec)))
            strikes = [k * spec["s0"] for k in STRIKES]
            npv = []
            for days in MATURITIES:
                for kind in (ql.Option.Call, ql.Option.Put):
                    for k in strikes:
                        o = ql.VanillaOption(ql.PlainVanillaPayoff(kind, k), ql.EuropeanExercise(R + days))
                        o.setPricingEngine(engine)
                        npv.append(v.attempt(lambda: o.NPV()))
            out.append({"name": f"{params[0]}, {cname} risk-free curve",
                        "inputs": {"settings": st, "process": spec, "maturities": MATURITIES, "strikes": strikes},
                        "expected": {"npv": npv}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/analyticgjrgarchengine.hpp", cases)
